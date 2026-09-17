using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Comentality.Xtb.About;
using Icons.Catalog;
using Icons.Ui;

namespace Icons.UiHarness
{
    /// <summary>
    /// Hosts the real control in a bare form, drives a few gestures, and photographs the result.
    ///
    /// There is no connection here, which is the point: the icon library lives inside the
    /// assembly, so everything except reading tables and writing them has to work before an
    /// environment is ever chosen. If the grid is empty or the preview is blank in these shots,
    /// it is empty in XrmToolBox too.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var outputDir = args.Length > 0
                ? args[0]
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".ui");
            Directory.CreateDirectory(outputDir);

            var failures = 0;

            // The About box is its own window, so it is photographed on its own rather than
            // through the control. Both states: folded, which is what opens, and unfolded, which
            // is the whole reason the licences did not go into a second window.
            failures += ShootAboutBox(outputDir);

            try
            {
                using (var control = new IconsControl())
                using (var form = new Form { Width = 1180, Height = 760, Text = "Icons" })
                {
                    form.Controls.Add(control);
                    form.Show();
                    Pump();

                    Shoot(form, outputDir, "01-opened");

                    failures += NoScrollBars(control, "on opening");
                    failures += NothingIsClipped(control, "on opening");

                    var search = Find<TextBox>(control, "search");
                    if (search == null)
                    {
                        Console.WriteLine("FAIL: no search box found");
                        failures++;
                    }
                    else
                    {
                        search.Text = "account";
                        Pump();
                        Shoot(form, outputDir, "02-searched");
                    }

                    var grid = Descendants(control).OfType<ListView>().FirstOrDefault();
                    if (grid == null)
                    {
                        Console.WriteLine("FAIL: no icon grid found");
                        failures++;
                    }
                    else
                    {
                        Console.WriteLine("Grid holds " + grid.VirtualListSize + " icons after searching for 'account'");
                        if (grid.VirtualListSize == 0)
                        {
                            Console.WriteLine("FAIL: searching for 'account' matched nothing");
                            failures++;
                        }

                        if (grid.VirtualListSize > 0)
                        {
                            grid.SelectedIndices.Add(0);
                            grid.Focus();
                            Pump();
                            Shoot(form, outputDir, "03-selected");

                            // Choose an icon and then keep typing. The grid keeps an index, and
                            // the index means a different icon once the ranking changes, so the
                            // highlighted cell and the preview drift apart unless the selection
                            // is carried across. Only reachable in this order.
                            var chosen = Chosen(control);
                            if (search != null)
                            {
                                search.Text = "person";
                                Pump();

                                var stillChosen = Chosen(control);
                                var highlighted = grid.SelectedIndices.Count > 0 ? grid.SelectedIndices[0] : -1;

                                // The thing that actually breaks is not the preview - that holds
                                // the icon itself and survives. It is that the grid holds an
                                // index, and the same index is a different icon once the ranking
                                // changes. So compare the two: whatever cell is lit has to be
                                // the icon the preview is showing.
                                var litIcon = highlighted >= 0 ? grid.Items[highlighted].Text : null;
                                var previewIcon = stillChosen?.Split(new[] { "  ·  " }, StringSplitOptions.None)[0];

                                if (stillChosen == null && highlighted < 0)
                                {
                                    Console.WriteLine("OK: the chosen icon no longer matches, and nothing is lit");
                                }
                                else if (stillChosen == null || highlighted < 0)
                                {
                                    Console.WriteLine("FAIL: preview says " + (previewIcon ?? "nothing") +
                                                      " but the lit cell says " + (litIcon ?? "nothing"));
                                    failures++;
                                }
                                else if (litIcon != previewIcon)
                                {
                                    Console.WriteLine("FAIL: the lit cell is " + litIcon +
                                                      " but the preview shows " + previewIcon +
                                                      " - the selection did not follow the re-ranking");
                                    failures++;
                                }
                                else
                                {
                                    Console.WriteLine("OK: after re-searching, the lit cell and the preview " +
                                                      "both show " + litIcon);
                                }

                                Shoot(form, outputDir, "04-searched-again");
                            }
                        }
                    }

                    // Nothing may be writable without a connection and a table.
                    var apply = Descendants(control)
                        .OfType<Button>()
                        .FirstOrDefault(b => b.Text.StartsWith("Set as"));

                    if (apply == null)
                    {
                        Console.WriteLine("FAIL: no apply button found");
                        failures++;
                    }
                    else if (apply.Enabled)
                    {
                        Console.WriteLine("FAIL: the apply button is live with no connection and no table chosen");
                        failures++;
                    }
                    else
                    {
                        Console.WriteLine("OK: apply stays dead until there is a table to apply to");
                    }

                    failures += CheckContextPreviewIsInTheWindow(control, form, outputDir);
                    failures += CheckNameSurvivesRebuild(control, form, outputDir);
                    failures += CheckUnsetOnlyFilter(control, form, outputDir);
                    failures += CheckLockedTablesAreHiddenAndExplained(control, form, outputDir);
                    failures += CheckASetTableKeepsItsPlaceAndIsMarked(control, form, outputDir);
                    failures += CheckRasterHelpOffered(control);
                    failures += CheckCreditLineIsSaidWhileChoosing(control, form, outputDir);
                    failures += CheckTheSearchFollowsTheTable(control, form, outputDir);
                    failures += CheckFamiliesAreShownAndFiltered(control, form, outputDir);
                    failures += CheckFormSurvivesTheNarrowestPane(control, form, outputDir);
                    failures += CheckTheSolutionLineStandsBesideTheBox(control, form, outputDir);
                    failures += CheckApplyStaysInTheWindowAsItShrinks(control, form, outputDir);
                    failures += CheckChosenRowStaysInViewAsItShrinks(control, form, outputDir);

                    form.Close();
                    Pump();
                }

                failures += ShootAtALargerFont(outputDir);
                failures += CheckTheWindowOpensNarrow(outputDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex);
                return 2;
            }

            Console.WriteLine(failures == 0 ? "All harness checks passed." : failures + " harness check(s) failed.");
            Console.WriteLine("Screenshots in " + Path.GetFullPath(outputDir));
            return failures == 0 ? 0 : 1;
        }


        /// <summary>
        /// The one bundled set whose licence asks for a credit line, and what the window says
        /// about it while somebody is still choosing.
        ///
        /// Two things are checked and the second is the one that breaks: that the sentence is
        /// there at all, and that the longest line this window can show still leaves the form
        /// whole in the pane it has to share.
        /// </summary>
        private static int CheckCreditLineIsSaidWhileChoosing(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            var warningField = typeof(IconsControl).GetField("_warningLabel", Private);
            if (warningField == null)
            {
                Console.WriteLine("FAIL: the warning line has moved and the harness cannot reach it");
                return 1;
            }

            var search = Find<TextBox>(control, "search");
            var grid = Descendants(control).OfType<ListView>().FirstOrDefault();
            var warning = warningField.GetValue(control) as Label;
            if (search == null || grid == null || warning == null)
            {
                Console.WriteLine("FAIL: the search box, the grid and the warning line are not all there");
                return 1;
            }

            var failures = 0;
            var licence = IconLicence.For(IconFamily.FontAwesome);

            try
            {
                control.ShowFamilies(IconFamily.FontAwesome);
                search.Text = "folder";
                Pump();

                if (grid.VirtualListSize == 0)
                {
                    Console.WriteLine("FAIL: no Font Awesome icon in the grid, so there is none to choose");
                    return 1;
                }

                grid.SelectedIndices.Clear();
                grid.SelectedIndices.Add(0);
                grid.Focus();
                Pump();

                if (!warning.Visible || warning.Text.IndexOf(licence.Licence, StringComparison.Ordinal) < 0)
                {
                    Console.WriteLine("FAIL: choosing an icon that asks for a credit line says " +
                                      (warning.Visible ? warning.Text : "nothing") + " under the grid");
                    failures++;
                }
                else if (warning.Text.IndexOf("ATTRIBUTION.html", StringComparison.Ordinal) < 0
                         || warning.Text.IndexOf(licence.SetName, StringComparison.Ordinal) < 0)
                {
                    Console.WriteLine("FAIL: the line says a credit is owed but not by whom or on what: " + warning.Text);
                    failures++;
                }
                else
                {
                    Console.WriteLine("OK: choosing a Font Awesome icon says what its licence asks and " +
                                      "what gets written for it");
                }

                Console.WriteLine("The credit line is " + warning.Text.Length + " characters and " +
                                  warning.Height + " pixels tall in a " + warning.Width + " pixel pane");

                failures += NoScrollBars(control, "with the credit line showing");
                failures += NothingIsClipped(control, "with the credit line showing");
                Shoot(form, outputDir, "13-credit-line");

                var width = form.Width;

                // The line is at its longest where the pane is at its narrowest, and this is a
                // sentence about a licence: half of it is not half as good.
                form.Width = 900;
                Pump();

                failures += NoScrollBars(control, "with the credit line in a narrow pane");
                failures += NothingIsClipped(control, "with the credit line in a narrow pane");
                Shoot(form, outputDir, "14-credit-line-narrow");

                form.Width = width;
                Pump();
            }
            finally
            {
                grid.SelectedIndices.Clear();
                control.ShowFamilies();
                search.Text = "";
                Pump();
            }

            if (warning.Visible && warning.Text.IndexOf(licence.SetName, StringComparison.Ordinal) >= 0)
            {
                Console.WriteLine("FAIL: the credit line outlived the icon it was about: " + warning.Text);
                failures++;
            }

            return failures;
        }

        /// <summary>The family toggles, which are every button-drawn check box in the window.</summary>
        private static List<CheckBox> _familyToggles(IconsControl control)
        {
            return Descendants(control).OfType<CheckBox>()
                .Where(c => c.Appearance == Appearance.Button && c.Text != "Outline" && c.Text != "Solid")
                .ToList();
        }

        /// <summary>Presses a link the way a person would, rather than calling what it calls.</summary>
        private static void Follow(LinkLabel link)
        {
            typeof(LinkLabel)
                .GetMethod("OnLinkClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(link, new object[] { new LinkLabelLinkClickedEventArgs(link.Links[0]) });
        }

        /// <summary>
        /// Four families draw a folder and all four are called Folder, so the grid has to be
        /// able to say which is which, and the search has to be able to be asked for one family
        /// only. Both are checked here because both change the shape of the cells the grid
        /// draws, which is the part that goes wrong silently.
        /// </summary>
        private static int CheckFamiliesAreShownAndFiltered(IconsControl control, Form form, string outputDir)
        {
            var failures = 0;

            var search = Find<TextBox>(control, "search");
            var grid = Descendants(control).OfType<ListView>().FirstOrDefault();
            var toggle = Descendants(control).OfType<CheckBox>().FirstOrDefault(c => c.Text == "Show family");

            if (search == null || grid == null || toggle == null)
            {
                Console.WriteLine("FAIL: the family tick or the search box is not in the window");
                return 1;
            }

            search.Text = "folder";
            Pump();

            var everyFamily = grid.VirtualListSize;
            toggle.Checked = true;
            Pump();
            Shoot(form, outputDir, "07-families-shown");

            if (grid.VirtualListSize != everyFamily)
            {
                Console.WriteLine("FAIL: showing the family changed what is in the grid, and it should only " +
                                  "change how it is drawn");
                failures++;
            }

            // Every family should have a folder, so narrowing to one has to leave something
            // and take something away.
            var narrowed = new List<string>();
            var alone = new Dictionary<IconFamily, int>();
            foreach (IconFamily family in Enum.GetValues(typeof(IconFamily)))
            {
                control.ShowFamilies(family);
                Pump();
                narrowed.Add(family + ": " + grid.VirtualListSize);
                alone[family] = grid.VirtualListSize;

                if (grid.VirtualListSize == 0)
                {
                    Console.WriteLine("FAIL: " + family + " has no folder, so that family is not in the bundle");
                    failures++;
                }
                if (grid.VirtualListSize >= everyFamily)
                {
                    Console.WriteLine("FAIL: filtering to " + family + " did not narrow anything");
                    failures++;
                }
            }

            Console.WriteLine("Folders per family - " + string.Join(", ", narrowed) + " (all families: " + everyFamily + ")");
            Shoot(form, outputDir, "08-one-family");

            // Two families at once, which no drop-down could be asked for: the pair has to be
            // more than either of them and less than the whole library.
            control.ShowFamilies(IconFamily.Fluent, IconFamily.Lucide);
            Pump();
            Shoot(form, outputDir, "08-two-families");

            var pair = grid.VirtualListSize;
            var biggest = Math.Max(alone[IconFamily.Fluent], alone[IconFamily.Lucide]);
            if (pair <= biggest || pair >= everyFamily)
            {
                Console.WriteLine("FAIL: Fluent and Lucide together show " + pair + " folders, against " +
                                  biggest + " for the bigger of them alone and " + everyFamily + " for all six");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: Fluent and Lucide together show " + pair + " folders, more than " +
                                  "either alone and fewer than every family");
            }

            // Lighting every one of them is the same library as lighting none, and the filter
            // has to be able to arrive back there the way a person would: by pressing All.
            var all = Descendants(control).OfType<LinkLabel>().FirstOrDefault(l => l.Text == "All");
            var none = Descendants(control).OfType<LinkLabel>().FirstOrDefault(l => l.Text == "None");

            if (all == null || none == null)
            {
                Console.WriteLine("FAIL: the All and None links are not in the family row");
                return failures + 1;
            }

            Follow(all);
            Pump();
            Shoot(form, outputDir, "08-every-family");

            if (grid.VirtualListSize != everyFamily)
            {
                Console.WriteLine("FAIL: All lights every family and shows " + grid.VirtualListSize +
                                  " icons, where none lit shows " + everyFamily);
                failures++;
            }
            else if (_familyToggles(control).Any(b => !b.Checked))
            {
                Console.WriteLine("FAIL: All left a family unlit");
                failures++;
            }
            else if (all.Enabled)
            {
                Console.WriteLine("FAIL: All is still offered with every family already lit");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: All lights every family, which is the same library as none lit, " +
                                  "and stops offering itself");
            }

            Follow(none);
            Pump();

            if (_familyToggles(control).Any(b => b.Checked) || none.Enabled || !all.Enabled)
            {
                Console.WriteLine("FAIL: None left " + _familyToggles(control).Count(b => b.Checked) +
                                  " families lit, or the two links did not swap over");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: None puts every family out and hands the offer back to All");
            }

            control.ShowFamilies();
            toggle.Checked = false;
            Pump();

            return failures;
        }

        /// <summary>
        /// A hand-edited web resource name must survive everything that rebuilds the table list
        /// and re-selects the same table - a reload, or a single keystroke in the table filter.
        /// Both fire SelectedIndexChanged, and SuggestName used to overwrite the box on every
        /// one of them.
        ///
        /// There is no connection here, so the tables are seeded straight into the private
        /// list. Reflection, because the product needs no seam for this and should not grow one.
        /// </summary>
        /// <summary>
        /// The in-context preview, which is part of the window rather than a window of its own.
        /// It is on screen from the moment the tool opens, so nothing has to be clicked to see
        /// it - and with no connection what it must show is the stand-in navigation, and it
        /// must say that it is one, because a preview of an invented navigation and a preview
        /// of the environment's own are worth very different amounts.
        /// </summary>
        private static int CheckContextPreviewIsInTheWindow(Control control, Form form, string outputDir)
        {
            var failures = 0;

            var preview = Descendants(control).OfType<ContextPreview>().FirstOrDefault();
            if (preview == null)
            {
                Console.WriteLine("FAIL: the in-context preview is not in the window");
                return 1;
            }

            if (!preview.Visible || preview.Width <= 0 || preview.Height <= 0)
            {
                Console.WriteLine("FAIL: the preview is in the window but has no room to draw in");
                failures++;
            }

            var note = Descendants(preview).OfType<Label>()
                .Where(l => l.Name == "note")
                .Select(l => l.Text)
                .FirstOrDefault();

            if (note == null || note.IndexOf("stand-in", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Console.WriteLine("FAIL: the preview does not say its navigation is invented; it says " +
                                  (note ?? "nothing"));
                failures++;
            }
            else
            {
                Console.WriteLine("OK: with no connection the preview shows a stand-in and says so");
            }

            // The dark box is the only place the icon is now seen on a dark ground, so it has
            // to be there and it has to redraw rather than throw.
            var dark = Descendants(preview).OfType<CheckBox>().FirstOrDefault(c => c.Text == "Dark");
            if (dark == null)
            {
                Console.WriteLine("FAIL: the preview offers no way to see the icon on a dark theme");
                return failures + 1;
            }

            dark.Checked = true;
            Pump();
            Shoot(form, outputDir, "06-in-context-dark");

            dark.Checked = false;
            Pump();
            Shoot(form, outputDir, "06-in-context");

            Console.WriteLine("OK: the preview draws on both themes");

            failures += CheckChosenRowIsScrolledTo(preview, form, outputDir);
            failures += CheckTheWheelScrolls(preview);

            return failures;
        }

        /// <summary>
        /// The preview has no scroll bar - the torn edge is why, and a ragged foot says the rows
        /// carry on better than a rectangular control floating in the grey beside it did. So the
        /// wheel is the only way down, and it is worth a check of its own: a real WM_MOUSEWHEEL
        /// at the pane, rather than a call to the handler, because what has to be true is that
        /// the message arrives and not that a method exists.
        /// </summary>
        private static int CheckTheWheelScrolls(ContextPreview preview)
        {
            var surface = preview.Controls.OfType<Panel>().FirstOrDefault(c => c.GetType().Name == "Surface");
            var field = surface?.GetType().GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic);
            if (surface == null || field == null)
            {
                Console.WriteLine("FAIL: the preview has no pane to wheel over");
                return 1;
            }

            var many = Enumerable.Range(0, 40).Select(i => new NavRow("Table " + i, null)).ToList();
            preview.ShowNavigation(many, 0, "A stand-in navigation, forty rows of it.", true);
            Pump();

            var failures = 0;
            var start = (int)field.GetValue(surface);

            Wheel(surface, -3);
            Pump();

            var down = (int)field.GetValue(surface);
            if (down <= start)
            {
                Console.WriteLine("FAIL: the wheel does not reach the preview - it sat at " + down
                                  + " with no bar to drag instead");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: the wheel scrolls the preview, " + start + " to " + down);
            }

            // Past both ends. A pane that can be wheeled off the top shows blank page above the
            // navigation, and one that can be wheeled off the foot shows it below.
            Wheel(surface, 40);
            Pump();

            if ((int)field.GetValue(surface) != 0)
            {
                Console.WriteLine("FAIL: wheeling up past the top left the preview at "
                                  + field.GetValue(surface) + " rather than 0");
                failures++;
            }

            Wheel(surface, -200);
            Pump();

            var bottom = (int)field.GetValue(surface);
            Wheel(surface, -40);
            Pump();

            if ((int)field.GetValue(surface) != bottom)
            {
                Console.WriteLine("FAIL: the preview keeps scrolling past its last row, " + bottom
                                  + " to " + field.GetValue(surface));
                failures++;
            }

            if (failures == 0) Console.WriteLine("OK: the wheel stops at both ends of the navigation");

            preview.ShowNavigation(new List<NavRow>(), -1, "", false);
            Pump();

            return failures;
        }

        private const int WmMouseWheel = 0x020A;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        /// <summary>One turn of the wheel per notch, positive up, as Windows sends it.</summary>
        private static void Wheel(Control surface, int notches)
        {
            var delta = (short)(notches * 120);
            var wParam = (IntPtr)unchecked((int)(uint)((delta & 0xFFFF) << 16));
            SendMessage(surface.Handle, WmMouseWheel, wParam, IntPtr.Zero);
        }

        /// <summary>
        /// A real app's navigation is forty rows where the stand-in is ten, and the pane is a few
        /// hundred pixels tall. A preview whose whole point is the row being chosen for is worth
        /// nothing if that row is below the fold, so the chosen row is scrolled to - and a row
        /// already in view is left alone, because scrolling under somebody who can see what they
        /// asked for is its own kind of wrong.
        ///
        /// Handed a navigation directly: nothing without a connection produces one this long,
        /// and the length is the whole point.
        /// </summary>
        private static int CheckChosenRowIsScrolledTo(ContextPreview preview, Form form, string outputDir)
        {
            // The pane scrolls on the wheel with no bar, so there is no AutoScrollPosition to
            // read and the offset it keeps instead is private. Reached by reflection rather than
            // opened up: how far the drawing has been wheeled is nobody's business but the
            // pane's, and a harness is not a reason to publish it.
            var surface = preview.Controls.OfType<Panel>().FirstOrDefault(c => c.GetType().Name == "Surface");
            var offset = surface?.GetType().GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic);
            if (surface == null || offset == null)
            {
                Console.WriteLine("FAIL: the preview has nothing that scrolls, so a long navigation would be cut");
                return 1;
            }

            var many = Enumerable.Range(0, 40)
                .Select(i => new NavRow("Table " + i, null))
                .ToList();

            preview.ShowNavigation(many, 3, "A stand-in navigation, near the top.", true);
            Pump();

            if ((int)offset.GetValue(surface) != 0)
            {
                Console.WriteLine("FAIL: a row already in view should not move the navigation, and it scrolled to "
                                  + offset.GetValue(surface));
                return 1;
            }

            preview.ShowNavigation(many, 34, "A stand-in navigation, near the bottom.", true);
            Pump();

            if ((int)offset.GetValue(surface) == 0)
            {
                Console.WriteLine("FAIL: row 34 of 40 is below the fold and the navigation did not scroll to it");
                Shoot(form, outputDir, "10-chosen-row-lost");
                return 1;
            }

            Console.WriteLine("OK: a chosen row below the fold is scrolled into view, one already in view is not");
            Shoot(form, outputDir, "10-chosen-row-far-down");

            // Back to what the window had, so the shots after this are of the tool rather than
            // of this check's leavings.
            preview.ShowNavigation(new List<NavRow>(), -1, "", false);
            Pump();

            return 0;
        }

        private static int CheckNameSurvivesRebuild(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            var tablesField = typeof(IconsControl).GetField("_tables", Private);
            var showTables = typeof(IconsControl).GetMethod("ShowTables", Private);
            var itemType = typeof(IconsControl).GetNestedType("TableItem", BindingFlags.NonPublic);
            if (tablesField == null || showTables == null || itemType == null)
            {
                Console.WriteLine("FAIL: the control's table plumbing has moved and the harness cannot seed it");
                return 1;
            }

            var tables = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            tables.Add(SeededTable(itemType, "account", "Account"));
            tables.Add(SeededTable(itemType, "contact", "Contact"));
            tablesField.SetValue(control, tables);
            showTables.Invoke(control, null);

            var tableBox = RowControl(control, "Table") as ListBox;
            var filterBox = RowControl(control, "Find table") as TextBox;
            var nameBox = RowControl(control, "Web resource") as TextBox;
            if (tableBox == null || filterBox == null || nameBox == null)
            {
                Console.WriteLine("FAIL: could not find the table, filter and name rows");
                return 1;
            }

            var failures = 0;

            tableBox.SelectedIndex = 0;
            if (nameBox.Text != "new_/icons/account.svg")
            {
                Console.WriteLine("FAIL: choosing Account suggested '" + nameBox.Text +
                                  "', not new_/icons/account.svg");
                failures++;
            }

            // An untouched box follows the table: the suggestion is a convenience, not a lock,
            // and a fix that never overwrote anything would fail here.
            tableBox.SelectedIndex = 1;
            if (nameBox.Text != "new_/icons/contact.svg")
            {
                Console.WriteLine("FAIL: the suggestion did not follow the table change; the box holds '" +
                                  nameBox.Text + "'");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: an untouched name follows the chosen table");
            }

            // Now the user types their own name, and then one keystroke lands in the table
            // filter. The list rebuilds, Contact is re-selected, SelectedIndexChanged fires.
            const string handEdited = "custom_/icons/handmade.svg";
            nameBox.Text = handEdited;
            filterBox.Text = "con";
            Pump();

            if (tableBox.Items.Count != 1)
            {
                Console.WriteLine("FAIL: filtering for 'con' should leave one table, not " + tableBox.Items.Count);
                failures++;
            }

            if (nameBox.Text != handEdited)
            {
                Console.WriteLine("FAIL: a filter keystroke overwrote the hand-edited name with '" +
                                  nameBox.Text + "'");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: the hand-edited name survives a filter keystroke re-adding its table");
            }

            Shoot(form, outputDir, "05-name-kept");

            // The other half of a typed name: where it puts the file is a decision about this
            // org, not about this table. Picking the next table has to leave that decision
            // standing while the file name follows the table - carrying the whole name over
            // would quietly point two tables at one web resource.
            filterBox.Text = "";
            Pump();

            tableBox.SelectedIndex = IndexOfTable(tableBox, "contact");
            nameBox.Text = "custom_/brand/tables/handmade.svg";
            tableBox.SelectedIndex = IndexOfTable(tableBox, "account");

            if (nameBox.Text != "new_/brand/tables/account.svg")
            {
                Console.WriteLine("FAIL: the folder from a typed name should carry to the next table, " +
                                  "and the box holds '" + nameBox.Text + "'");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: a typed folder carries to the next table, the file name does not");
            }

            // And emptying the box is how the org's own convention is asked for back. It is the
            // only thing a user can do to the name that says nothing about where they want it.
            nameBox.Text = "";
            tableBox.SelectedIndex = IndexOfTable(tableBox, "contact");

            if (nameBox.Text != "new_/icons/contact.svg")
            {
                Console.WriteLine("FAIL: emptying the name should give the convention back, " +
                                  "and the box holds '" + nameBox.Text + "'");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: emptying the name gives the suggested folder back");
            }

            return failures;
        }

        private static int IndexOfTable(ListBox tableBox, string logicalName)
        {
            var index = tableBox.Items.Cast<object>().ToList()
                .FindIndex(i => i.ToString().Contains("(" + logicalName + ")"));
            if (index < 0)
                throw new InvalidOperationException("No table called " + logicalName + " in the list.");
            return index;
        }


        /// <summary>
        /// The unset-only filter, with no connection and seeded tables. It is a narrowing on top
        /// of the text filter rather than instead of it - the two have to stack, or "cont" plus
        /// "no icon yet" quietly means one or the other - and unticking gives the list back.
        /// </summary>
        private static int CheckUnsetOnlyFilter(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            var tablesField = typeof(IconsControl).GetField("_tables", Private);
            var showTables = typeof(IconsControl).GetMethod("ShowTables", Private);
            var itemType = typeof(IconsControl).GetNestedType("TableItem", BindingFlags.NonPublic);
            if (tablesField == null || showTables == null || itemType == null)
            {
                Console.WriteLine("FAIL: the control's table plumbing has moved and the harness cannot seed it");
                return 1;
            }

            var tables = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            tables.Add(SeededTable(itemType, "account", "Account", "new_/icons/account.svg"));
            tables.Add(SeededTable(itemType, "contact", "Contact"));
            tables.Add(SeededTable(itemType, "contract", "Contract", "new_/icons/contract.svg"));
            tablesField.SetValue(control, tables);

            var tableBox = RowControl(control, "Table") as ListBox;
            var filterBox = RowControl(control, "Find table") as TextBox;
            var unsetOnly = Descendants(control)
                .OfType<CheckBox>()
                .FirstOrDefault(c => c.Text.StartsWith("Only tables", StringComparison.Ordinal));

            if (tableBox == null || filterBox == null || unsetOnly == null)
            {
                Console.WriteLine("FAIL: could not find the table row, the filter row and the unset-only box");
                return 1;
            }

            var failures = 0;

            if (!unsetOnly.Checked)
            {
                Console.WriteLine("FAIL: the window should open on the tables with no icon yet, and the box is unticked");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: the window opens on the tables with no icon yet");
            }

            // Everything, to have something to narrow. The box is ticked when the window opens,
            // so seeing the whole list is the thing that takes a click now.
            unsetOnly.Checked = false;
            filterBox.Text = "";
            showTables.Invoke(control, null);
            Pump();

            if (tableBox.Items.Count != 3)
            {
                Console.WriteLine("FAIL: unticked, all three seeded tables should be offered, not " +
                                  tableBox.Items.Count);
                failures++;
            }

            unsetOnly.Checked = true;
            Pump();

            if (tableBox.Items.Count != 1 || !tableBox.Items[0].ToString().Contains("(contact)"))
            {
                Console.WriteLine("FAIL: only Contact has no icon, and the list offers " + Listed(tableBox));
                failures++;
            }
            else
            {
                Console.WriteLine("OK: the unset-only box leaves out the tables that already have an icon");
            }

            // Both at once. "cont" alone is Contact and Contract; with no icon yet it is Contact
            // alone, and a filter that replaced the other rather than narrowing would give two.
            filterBox.Text = "cont";
            Pump();

            if (tableBox.Items.Count != 1 || !tableBox.Items[0].ToString().Contains("(contact)"))
            {
                Console.WriteLine("FAIL: 'cont' and no-icon-yet should leave Contact alone, and leaves " +
                                  Listed(tableBox));
                failures++;
            }
            else
            {
                Console.WriteLine("OK: the text filter and the unset-only box narrow together");
            }

            Shoot(form, outputDir, "06-unset-only");

            unsetOnly.Checked = false;
            Pump();

            if (tableBox.Items.Count != 2)
            {
                Console.WriteLine("FAIL: unticking should give back both 'cont' tables, and gives " +
                                  Listed(tableBox));
                failures++;
            }
            else
            {
                Console.WriteLine("OK: unticking gives the tables that have icons back");
            }

            return failures;
        }


        /// <summary>
        /// Picking a table puts that table's own word in the search box, and goes on doing it
        /// after somebody has chosen an icon for the table before.
        ///
        /// The second half is what this is here for. The window used to read an icon in hand as
        /// a hand on the library and leave the search alone, which meant the seed worked once:
        /// the second table of a session, and every table after it, opened on the first table's
        /// search with the first table's icon still lit.
        ///
        /// A search somebody typed themselves is still theirs, and the same run checks that, because
        /// a fix that seeded over a typed question would trade one annoyance for a worse one.
        /// </summary>
        private static int CheckTheSearchFollowsTheTable(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            var tablesField = typeof(IconsControl).GetField("_tables", Private);
            var showTables = typeof(IconsControl).GetMethod("ShowTables", Private);
            var itemType = typeof(IconsControl).GetNestedType("TableItem", BindingFlags.NonPublic);
            if (tablesField == null || showTables == null || itemType == null)
            {
                Console.WriteLine("FAIL: the control's table plumbing has moved and the harness cannot seed it");
                return 1;
            }

            // Two tables no other check uses, so this one cannot be handed a window that has
            // already seeded the table it is about to pick and would sit there saying nothing.
            var tables = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            tables.Add(SeededTable(itemType, "employee", "Employee"));
            tables.Add(SeededTable(itemType, "invoice", "Invoice"));
            tablesField.SetValue(control, tables);

            var tableBox = RowControl(control, "Table") as ListBox;
            var filterBox = RowControl(control, "Find table") as TextBox;
            var search = Find<TextBox>(control, "search");
            var grid = Descendants(control).OfType<ListView>().FirstOrDefault();
            if (tableBox == null || filterBox == null || search == null || grid == null)
            {
                Console.WriteLine("FAIL: the table row, the search box and the grid are not all there");
                return 1;
            }

            // Whatever the checks before left on screen. An empty search box is the state this
            // is about, and a narrowed family would rank a different grid under it.
            control.ShowFamilies();
            filterBox.Text = "";
            search.Text = "";
            showTables.Invoke(control, null);
            Pump();

            var failures = 0;

            tableBox.SelectedIndex = IndexOfTable(tableBox, "employee");
            Pump();

            var first = search.Text;
            if (first.Trim().Length == 0)
            {
                Console.WriteLine("FAIL: picking Employee put nothing in the search box");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: picking Employee searched for '" + first + "'");
            }

            // Somebody takes the window up on it. This is the gesture that used to freeze the
            // search box for the rest of the session.
            if (grid.VirtualListSize == 0)
            {
                Console.WriteLine("FAIL: nothing in the grid for Employee, so there is no icon to choose");
                failures++;
            }
            else
            {
                grid.SelectedIndices.Clear();
                grid.SelectedIndices.Add(0);
                grid.Focus();
                Pump();

                if (Chosen(control) == null)
                {
                    Console.WriteLine("FAIL: clicking the first cell chose no icon");
                    failures++;
                }
            }

            tableBox.SelectedIndex = IndexOfTable(tableBox, "invoice");
            Pump();

            var second = search.Text;
            if (second.Trim().Length == 0)
            {
                Console.WriteLine("FAIL: with an icon in hand, moving to Invoice emptied the search box");
                failures++;
            }
            else if (second == first)
            {
                Console.WriteLine("FAIL: with an icon in hand, moving to Invoice left the box searching for '" +
                                  first + "' - the seed only works until somebody uses it");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: with an icon in hand, moving to Invoice searched for '" + second + "'");
            }

            Shoot(form, outputDir, "09-search-follows-the-table");

            // The other half. A question somebody typed is not the window's to answer again.
            search.Text = "folder";
            Pump();
            tableBox.SelectedIndex = IndexOfTable(tableBox, "employee");
            Pump();

            if (search.Text != "folder")
            {
                Console.WriteLine("FAIL: a typed search should survive a table change, and became '" +
                                  search.Text + "'");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: a search somebody typed survives a table change");
            }

            tableBox.SelectedIndex = -1;
            search.Text = "";
            Pump();

            return failures;
        }

        /// <summary>
        /// A table locked against customisation. It is not offered at all by default, because
        /// nobody can give it an icon; unticking the box brings it back, and choosing it has to
        /// say why it is a dead end - under the table box, where the choosing happened, rather
        /// than in the red line at the foot of the pane that is for round trips that failed.
        /// </summary>
        private static int CheckLockedTablesAreHiddenAndExplained(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            var tablesField = typeof(IconsControl).GetField("_tables", Private);
            var showTables = typeof(IconsControl).GetMethod("ShowTables", Private);
            var itemType = typeof(IconsControl).GetNestedType("TableItem", BindingFlags.NonPublic);
            var lockedLabel = typeof(IconsControl).GetField("_lockedLabel", Private);
            if (tablesField == null || showTables == null || itemType == null || lockedLabel == null)
            {
                Console.WriteLine("FAIL: the control's table plumbing has moved and the harness cannot seed it");
                return 1;
            }

            var tables = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            tables.Add(SeededTable(itemType, "contact", "Contact"));
            tables.Add(SeededTable(itemType, "solution", "Solution", null, false));
            tablesField.SetValue(control, tables);

            var tableBox = RowControl(control, "Table") as ListBox;
            var filterBox = RowControl(control, "Find table") as TextBox;
            var hideLocked = Descendants(control)
                .OfType<CheckBox>()
                .FirstOrDefault(c => c.Text.StartsWith("Hide the ones", StringComparison.Ordinal));
            var note = lockedLabel.GetValue(control) as Label;

            if (tableBox == null || filterBox == null || hideLocked == null || note == null)
            {
                Console.WriteLine("FAIL: could not find the table row, the hide-locked box and the note under it");
                return 1;
            }

            var failures = 0;

            filterBox.Text = "";
            showTables.Invoke(control, null);
            Pump();

            if (!hideLocked.Checked)
            {
                Console.WriteLine("FAIL: the hide-locked box should start ticked");
                failures++;
            }

            if (tableBox.Items.Count != 1 || !tableBox.Items[0].ToString().Contains("(contact)"))
            {
                Console.WriteLine("FAIL: a locked table should not be offered, and the list holds " + Listed(tableBox));
                failures++;
            }
            else
            {
                Console.WriteLine("OK: a table nobody can customise is left out of the list");
            }

            if (note.Visible)
            {
                Console.WriteLine("FAIL: nothing locked is chosen, so the note should be hidden; it says " + note.Text);
                failures++;
            }

            hideLocked.Checked = false;
            Pump();

            if (tableBox.Items.Count != 2)
            {
                Console.WriteLine("FAIL: unticking should give the locked table back, and gives " + Listed(tableBox));
                failures++;
            }

            tableBox.SelectedIndex = IndexOfTable(tableBox, "solution");
            Pump();

            if (!note.Visible || note.Text.IndexOf("locked against customisation", StringComparison.Ordinal) < 0)
            {
                Console.WriteLine("FAIL: choosing the locked table says " +
                                  (note.Visible ? note.Text : "nothing") + " under the box");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: choosing a locked table says so under the table box");
            }

            Shoot(form, outputDir, "09-locked-table");

            // The note is an extra line in a form that had none, which is where the pane first
            // ran out of room, so this is the moment to look.
            failures += NoScrollBars(control, "with the locked note showing");

            tableBox.SelectedIndex = IndexOfTable(tableBox, "contact");
            Pump();

            if (note.Visible)
            {
                Console.WriteLine("FAIL: the note outlived the table it was about; it still says " + note.Text);
                failures++;
            }
            else
            {
                Console.WriteLine("OK: moving off the locked table takes the note with it");
            }

            hideLocked.Checked = true;
            Pump();

            return failures;
        }

        /// <summary>
        /// The PNG checkbox has to carry its own explanation, because "should I tick this?" is
        /// not answerable from the caption. The link is only checked for being there: the
        /// harness has no dialog sweeper, so clicking it would hang the run on a modal nobody
        /// answers. What it says is a matter for the shot.
        /// </summary>
        private static int CheckRasterHelpOffered(IconsControl control)
        {
            var link = Descendants(control)
                .OfType<LinkLabel>()
                .FirstOrDefault(l => l.Text.IndexOf("PNG", StringComparison.Ordinal) >= 0);

            if (link == null)
            {
                Console.WriteLine("FAIL: the classic PNG checkbox is offered with nothing that says when to use it");
                return 1;
            }

            Console.WriteLine("OK: the PNG checkbox comes with a link to what the PNGs are for");
            return 0;
        }
        private static string Listed(ListBox box)
        {
            return box.Items.Count == 0
                ? "nothing"
                : string.Join(", ", box.Items.Cast<object>().Select(i => i.ToString()));
        }
        /// <summary>
        /// What "only tables with no icon yet" does with a table this session has just given one,
        /// and what the row looks like once it has one.
        ///
        /// There is no connection here, so the apply itself cannot be driven - what the apply
        /// leaves behind is seeded instead: the icon on the table, and its name in the set of
        /// tables the window is keeping. Both halves are checked, because either alone is the
        /// old behaviour: without the set the row vanishes, and without the icon there is
        /// nothing to mark.
        ///
        /// The mark is read off the pixels rather than off a property, because it is drawn
        /// rather than spelled - a gutter with ink in it on the row that carries an icon, and
        /// none on the row that does not.
        /// </summary>
        private static int CheckASetTableKeepsItsPlaceAndIsMarked(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            var tablesField = typeof(IconsControl).GetField("_tables", Private);
            var keptField = typeof(IconsControl).GetField("_setThisSession", Private);
            var showTables = typeof(IconsControl).GetMethod("ShowTables", Private);
            var itemType = typeof(IconsControl).GetNestedType("TableItem", BindingFlags.NonPublic);
            if (tablesField == null || keptField == null || showTables == null || itemType == null)
            {
                Console.WriteLine("FAIL: the control's table plumbing has moved and the harness cannot seed it");
                return 1;
            }

            var tables = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            tables.Add(SeededTable(itemType, "account", "Account"));
            tables.Add(SeededTable(itemType, "contact", "Contact", "new_/icons/contact.svg"));
            tablesField.SetValue(control, tables);

            var kept = (HashSet<string>)keptField.GetValue(control);
            kept.Clear();

            var tableBox = RowControl(control, "Table") as ListBox;
            var unsetOnly = Descendants(control)
                .OfType<CheckBox>()
                .FirstOrDefault(c => c.Text.StartsWith("Only tables", StringComparison.Ordinal));
            if (tableBox == null || unsetOnly == null)
            {
                Console.WriteLine("FAIL: could not find the table row and the unset-only box");
                return 1;
            }

            var failures = 0;

            unsetOnly.Checked = true;
            showTables.Invoke(control, null);
            Pump();

            if (tableBox.Items.Count != 1 || !tableBox.Items[0].ToString().Contains("(account)"))
            {
                Console.WriteLine("FAIL: a table already carrying an icon should be out of the unset-only " +
                                  "list, and the list holds " + Listed(tableBox));
                failures++;
            }

            kept.Add("contact");
            showTables.Invoke(control, null);
            Pump();

            if (tableBox.Items.Count != 2)
            {
                Console.WriteLine("FAIL: a table this session set should keep its place whatever the " +
                                  "filters say, and the list holds " + Listed(tableBox));
                failures++;
            }
            else
            {
                Console.WriteLine("OK: a table set this session keeps its place in the unset-only list");
            }

            if (failures == 0)
            {
                failures += CheckOnlyTheCarryingRowIsMarked(tableBox);
            }

            Shoot(form, outputDir, "24-set-table-kept-and-marked");

            kept.Clear();
            return failures;
        }

        /// <summary>
        /// Ink in the gutter of the row that carries an icon, and none in the gutter of the row
        /// that does not. The gutter is a row's own height wide and the dot is drawn in the
        /// middle of it, so a column one third of the way across it is where to look.
        /// </summary>
        private static int CheckOnlyTheCarryingRowIsMarked(ListBox tableBox)
        {
            var account = IndexOfTable(tableBox, "account");
            var contact = IndexOfTable(tableBox, "contact");

            // Nothing chosen, or the chosen row is painted the highlight colour end to end and
            // every pixel of its gutter differs from the background whether it is marked or not.
            tableBox.SelectedIndex = -1;
            Pump();

            using (var bitmap = new Bitmap(tableBox.Width, tableBox.Height))
            {
                tableBox.DrawToBitmap(bitmap, new Rectangle(0, 0, tableBox.Width, tableBox.Height));

                var background = tableBox.BackColor;
                var height = tableBox.ItemHeight;

                Func<int, bool> marked = index =>
                {
                    var box = tableBox.GetItemRectangle(index);
                    for (var x = 1; x < height - 1 && x < bitmap.Width; x++)
                        for (var y = box.Top + 1; y < box.Bottom - 1 && y < bitmap.Height; y++)
                            if (bitmap.GetPixel(x, y).ToArgb() != background.ToArgb()) return true;
                    return false;
                };

                if (!marked(contact))
                {
                    Console.WriteLine("FAIL: the row carrying an icon has nothing in its gutter");
                    return 1;
                }

                if (marked(account))
                {
                    Console.WriteLine("FAIL: a row carrying no icon has something in its gutter");
                    return 1;
                }
            }

            Console.WriteLine("OK: the dot is on the row carrying an icon and on no other");
            return 0;
        }

        private static object SeededTable(Type itemType, string logicalName, string displayName,
                                          string currentIcon = null, bool customizable = true)
        {
            var item = Activator.CreateInstance(itemType, true);
            itemType.GetField("LogicalName").SetValue(item, logicalName);
            itemType.GetField("DisplayName").SetValue(item, displayName);
            itemType.GetField("Customizable").SetValue(item, customizable);
            itemType.GetField("CurrentIcon").SetValue(item, currentIcon);
            return item;
        }

        /// <summary>
        /// The input beside the given caption. Find() cannot tell these rows apart - the whole
        /// target pane is one TableLayoutPanel, so every row sees every label - hence by cell.
        /// </summary>
        private static Control RowControl(Control root, string caption)
        {
            foreach (var panel in Descendants(root).OfType<TableLayoutPanel>())
            {
                var label = Descendants(panel).OfType<Label>().FirstOrDefault(l => l.Text == caption);
                if (label == null) continue;

                // A caption that shares its cell with a button is nested one deeper, so the cell
                // is asked about whatever child of the panel the label is inside.
                Control cell = label;
                while (cell.Parent != panel && cell.Parent != null) cell = cell.Parent;

                return panel.GetControlFromPosition(1, panel.GetPositionFromControl(cell).Row);
            }
            return null;
        }

        /// <summary>
        /// Which icon the window says is chosen, read off the label above the form - which is
        /// driven by the same field the preview draws from, so it cannot agree with the preview
        /// and disagree with reality.
        /// </summary>
        private static string Chosen(Control root)
        {
            var label = Descendants(root)
                .OfType<Label>()
                .FirstOrDefault(l => l.Font.Bold && l.Dock == DockStyle.Top);

            if (label == null || label.Text.StartsWith("No icon")) return null;
            return label.Text;
        }

        /// <summary>
        /// The form dragged as narrow as it is allowed to go. This is the width at which the
        /// widest row - a tickbox and the question that answers it - has to give way, and if it
        /// does not, the pane grows a scroll bar along its foot and the two bars start feeding
        /// each other.
        /// </summary>
        private static int CheckFormSurvivesTheNarrowestPane(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var body = typeof(IconsControl).GetField("_body", Private)?.GetValue(control) as SplitContainer;

            if (body == null)
            {
                Console.WriteLine("FAIL: no body splitter to narrow");
                return 1;
            }

            var was = body.SplitterDistance;
            body.SplitterDistance = body.Width - body.Panel2MinSize - body.SplitterWidth;
            Pump();

            var failures = NoScrollBars(control, "with the pane dragged to its narrowest");
            Shoot(form, outputDir, "11-narrow-pane");

            body.SplitterDistance = was;
            Pump();
            return failures;
        }

        /// <summary>
        /// The button that writes the icon stays in the window at every height the window can
        /// be, and so does the form pane it sits under.
        ///
        /// Written for a bug that only a sweep finds. A SplitContainer fixed on Panel2 answers
        /// a change of height by moving the splitter and leaving Panel2 the height it had; once
        /// the splitter is down on Panel1MinSize there is nothing left to move, so the container
        /// goes on shrinking and the panel does not. Its foot leaves the window, taking whatever
        /// is docked there with it, and growing the window back does not undo it. Every height
        /// above about 580 is fine - which is every height a maximised window has - so the one
        /// shot this harness took would have gone on passing while a restored-down XrmToolBox
        /// hid the button under its status bar.
        ///
        /// The solution line is showing throughout, because that is the tallest the form gets
        /// before a table is chosen, and it is what an environment with no preferred solution
        /// opens on - which is how this was found.
        /// </summary>
        /// <summary>
        /// The line that asks for a solution stands directly above the box it is asking about.
        ///
        /// That line is docked to the top of the target pane, over the form rather than in it,
        /// because it wraps to three or four lines and a row that did that would shove the form
        /// about. So the only way it can read as being about the solution is for the solution to
        /// be the first row of the form - which is also where it belongs, being the first choice
        /// somebody makes. Two things are checked, and they are the two halves of that: that the
        /// box is row nought, and that nothing has got in between the two on the screen.
        /// </summary>
        private static int CheckTheSolutionLineStandsBesideTheBox(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            var solutionsField = typeof(IconsControl).GetField("_solutions", Private);
            var solutionType = typeof(IconsControl).GetNestedType("SolutionItem", BindingFlags.NonPublic);
            var syncEnabled = typeof(IconsControl).GetMethod("SyncEnabled", Private);

            if (solutionsField == null || solutionType == null || syncEnabled == null)
            {
                Console.WriteLine("FAIL: the solution plumbing is not where the harness expects it");
                return 1;
            }

            var solutionBox = RowControl(control, "Solution") as ComboBox;
            if (solutionBox == null)
            {
                Console.WriteLine("FAIL: no solution box on the form");
                return 1;
            }

            var grid = solutionBox.Parent as TableLayoutPanel;
            var row = grid == null ? -1 : grid.GetPositionFromControl(solutionBox).Row;
            if (row != 0)
            {
                Console.WriteLine("FAIL: the solution box is row " + row + " of the form, not the first row");
                return 1;
            }

            // Solutions to offer and none of them chosen, which is the one state that puts the
            // sentence asking for one above the form.
            var solutions = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(solutionType));
            var solution = Activator.CreateInstance(solutionType, true);
            solutionType.GetField("Id").SetValue(solution, Guid.NewGuid());
            solutionType.GetField("UniqueName").SetValue(solution, "contoso");
            solutionType.GetField("FriendlyName").SetValue(solution, "Contoso");
            solutionType.GetField("Prefix").SetValue(solution, "new");
            solutions.Add(solution);
            solutionsField.SetValue(control, solutions);

            solutionBox.Items.Clear();
            solutionBox.Items.Add(solution);
            solutionBox.SelectedIndex = -1;
            syncEnabled.Invoke(control, null);
            Pump();

            var warning = Descendants(control)
                .OfType<Label>()
                .FirstOrDefault(l => l.Visible &&
                                     l.Text.IndexOf("Choose the solution", StringComparison.Ordinal) >= 0);

            if (warning == null)
            {
                Console.WriteLine("FAIL: nothing on screen asks for a solution when none is chosen");
                return 1;
            }

            // Both in the control's own coordinates, so the panes and the scroll between them
            // are already accounted for.
            var lineBottom = control.PointToClient(warning.Parent.PointToScreen(new Point(0, warning.Bottom))).Y;
            var boxTop = control.PointToClient(solutionBox.Parent.PointToScreen(new Point(0, solutionBox.Top))).Y;
            var gap = boxTop - lineBottom;

            var failures = 0;
            if (gap < 0 || gap > 24)
            {
                Console.WriteLine("FAIL: the line asking for a solution is " + gap +
                                  " pixels from the box it is about, which is not beside it");
                failures++;
            }
            else
            {
                Console.WriteLine("OK: the line asking for a solution stands " + gap +
                                  " pixels above the first row of the form, which is the solution");
            }

            Shoot(form, outputDir, "16-solution-first");

            // Back to an unconnected window, which is what every check after this one assumes.
            solutionBox.Items.Clear();
            solutionsField.SetValue(control,
                Activator.CreateInstance(typeof(List<>).MakeGenericType(solutionType)));
            syncEnabled.Invoke(control, null);
            Pump();

            return failures;
        }

        private static int CheckApplyStaysInTheWindowAsItShrinks(IconsControl control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var split = typeof(IconsControl).GetField("_targetSplit", Private)?.GetValue(control) as SplitContainer;
            var button = typeof(IconsControl).GetField("_applyButton", Private)?.GetValue(control) as Control;
            var solutionsField = typeof(IconsControl).GetField("_solutions", Private);
            var solutionType = typeof(IconsControl).GetNestedType("SolutionItem", BindingFlags.NonPublic);
            var syncEnabled = typeof(IconsControl).GetMethod("SyncEnabled", Private);

            if (split == null || button == null || solutionsField == null ||
                solutionType == null || syncEnabled == null)
            {
                Console.WriteLine("FAIL: the splitter, the apply button and the solution plumbing " +
                                  "are not all where the harness expects them");
                return 1;
            }

            // Solutions offered and none of them chosen, which is what puts the concern line
            // above the form and makes it as tall as it gets.
            var solutions = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(solutionType));
            var solution = Activator.CreateInstance(solutionType, true);
            solutionType.GetField("Id").SetValue(solution, Guid.NewGuid());
            solutionType.GetField("UniqueName").SetValue(solution, "contoso");
            solutionType.GetField("FriendlyName").SetValue(solution, "Contoso");
            solutionType.GetField("Prefix").SetValue(solution, "new");
            solutions.Add(solution);
            solutionsField.SetValue(control, solutions);

            var solutionBox = RowControl(control, "Solution") as ComboBox;
            if (solutionBox == null)
            {
                Console.WriteLine("FAIL: no solution box to leave unchosen");
                return 1;
            }
            solutionBox.Items.Add(solution);
            solutionBox.SelectedIndex = -1;
            syncEnabled.Invoke(control, null);
            Pump();

            var wasHeight = form.Height;
            var failures = 0;
            var worstButton = 0;
            var worstPane = 0;
            var shot = false;

            // Down and back up, because the panel keeps the height it was left with: a sweep
            // that only shrinks would miss a window that never recovers.
            var heights = new List<int>();
            for (var h = wasHeight; h >= 360; h -= 20) heights.Add(h);
            for (var h = 360; h <= wasHeight; h += 20) heights.Add(h);

            foreach (var height in heights)
            {
                form.Height = height;
                Pump();

                // The button, in the control's own coordinates. Docked where it is, its foot
                // should be the foot of the pane less the strip's own padding - never past it.
                var bottom = control.PointToClient(button.Parent.PointToScreen(new Point(0, button.Bottom))).Y;
                var over = bottom - control.Height;
                if (over > 0 && over > worstButton)
                {
                    worstButton = over;
                    if (!shot) { Shoot(form, outputDir, "12-apply-off-the-bottom"); shot = true; }
                }

                // And the lower pane inside the splitter that holds it.
                var fits = split.Height - split.SplitterDistance - split.SplitterWidth;
                var hangs = split.Panel2.Height - fits;
                if (hangs > worstPane) worstPane = hangs;
            }

            if (worstButton > 0)
            {
                Console.WriteLine("FAIL: 'Set as table icon' leaves the window as it shrinks - " +
                                  "by " + worstButton + " pixels at its worst");
                failures++;
            }

            if (worstPane > 0)
            {
                Console.WriteLine("FAIL: the form pane hangs out of its splitter as the window " +
                                  "shrinks - by " + worstPane + " pixels at its worst");
                failures++;
            }

            if (failures == 0)
                Console.WriteLine("OK: the apply button and the form pane stay in the window at every height");

            form.Height = wasHeight;
            Pump();
            return failures;
        }

        /// <summary>
        /// The row being chosen for stays above the tear at every height the window can be.
        ///
        /// The preview scrolls on the wheel and keeps its own offset, and until now that offset
        /// was only ever reconsidered when a navigation arrived. A pane that loses height loses
        /// it off the foot, so a row that was in view at one height is under the tear at the
        /// next - and shrinking XrmToolBox is exactly how the preview pane loses height, because
        /// the form below it is fitted first and what is over goes here. A single-size shot
        /// cannot see it; a sweep can.
        ///
        /// The second half of the check is that it holds still. A preview that answered every
        /// pixel of a resize by scrolling would keep the row in view and be unreadable doing it,
        /// so a height where the row fits without scrolling has to leave the drawing at the top.
        ///
        /// Forty rows handed in directly: nothing without a connection produces a navigation
        /// long enough to have a fold, and the fold is the whole point.
        /// </summary>
        private static int CheckChosenRowStaysInViewAsItShrinks(Control control, Form form, string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            var preview = Descendants(control).OfType<ContextPreview>().FirstOrDefault();
            var surface = preview?.Controls.OfType<Panel>().FirstOrDefault(c => c.GetType().Name == "Surface");
            var offsetField = surface?.GetType().GetField("_offset", Private);
            var foldProperty = surface?.GetType().GetProperty("Fold", Private);

            if (preview == null || surface == null || offsetField == null || foldProperty == null)
            {
                Console.WriteLine("FAIL: the preview's pane, the offset it scrolls by or the fold the tear " +
                                  "takes are not where the harness expects them");
                return 1;
            }

            var many = Enumerable.Range(0, 40).Select(i => new NavRow("Table " + i, null)).ToList();

            // The pane's own arithmetic, asked of the same painter it draws with, so a change of
            // row height or of screen scale moves the check with it.
            var painter = new NavPainter(NavTheme.Light, surface.DeviceDpi / 96f);
            var rowHeight = painter.RowHeight;

            var wasHeight = form.Height;
            var heights = new List<int>();
            for (var h = wasHeight; h >= 360; h -= 20) heights.Add(h);
            for (var h = 360; h <= wasHeight; h += 20) heights.Add(h);

            var failures = 0;
            var shot = false;

            // One row far below the fold, which is what a real site map's chosen row usually is,
            // and one near the top, which is what must not be scrolled around for no reason.
            foreach (var selected in new[] { 34, 3 })
            {
                var top = painter.Top(many, selected);
                preview.ShowNavigation(many, selected, "A stand-in navigation, row " + selected + " of forty.", true);
                Pump();

                var worst = 0;
                var worstAt = 0;
                var fidgeted = new List<string>();

                foreach (var height in heights)
                {
                    form.Height = height;
                    Pump();

                    var offset = (int)offsetField.GetValue(surface);
                    var seen = surface.ClientSize.Height - (int)foldProperty.GetValue(surface, null);
                    if (seen <= 0) continue;

                    // A pane with no room for a whole row can only be asked for the row's head,
                    // which is the half with the icon in it.
                    var wanted = Math.Min(rowHeight, seen);
                    var hidden = Math.Max(offset - top, top + wanted - (offset + seen));
                    if (hidden > worst)
                    {
                        worst = hidden;
                        worstAt = height;
                    }

                    // Everything down to the chosen row fits, so there is nothing to scroll for.
                    if (top + rowHeight <= seen && offset != 0)
                        fidgeted.Add(height + " tall, scrolled " + offset);
                }

                if (worst > 0)
                {
                    Console.WriteLine("FAIL: row " + selected + " is cut off as the window shrinks - by " +
                                      worst + " pixels at its worst, with the window " + worstAt + " tall");
                    failures++;

                    if (!shot)
                    {
                        form.Height = worstAt;
                        Pump();
                        Shoot(form, outputDir, "13-chosen-row-cut-off");
                        shot = true;
                    }
                }

                if (fidgeted.Count > 0)
                {
                    Console.WriteLine("FAIL: the preview scrolled where row " + selected +
                                      " was in view without it - " + string.Join("; ", fidgeted));
                    failures++;
                }
            }

            if (failures == 0)
                Console.WriteLine("OK: the chosen row stays above the tear at every height, and the preview " +
                                  "holds still at the heights where it already fits");

            // A short window with the chosen row still in it, which is the shot the change is about.
            preview.ShowNavigation(many, 34, "A stand-in navigation, row 34 of forty.", true);
            form.Height = 400;
            Pump();
            Shoot(form, outputDir, "13-chosen-row-kept-in-view");

            form.Height = wasHeight;
            preview.ShowNavigation(new List<NavRow>(), -1, "", false);
            Pump();
            return failures;
        }

        /// <summary>
        /// The form pane scrolls downwards when it truly has to and never sideways, and it does
        /// not have to while there is height above it to take: the splitter gives way first.
        ///
        /// Worth a check of its own because the two bars feed each other. A form that is one row
        /// too tall grows a bar down the side, the bar takes 17 pixels of width, the widest row
        /// no longer fits, a bar appears along the foot, and that takes back the height the first
        /// one was about.
        /// </summary>
        private static int NoScrollBars(IconsControl control, string where)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var host = typeof(IconsControl).GetField("_formHost", Private)?.GetValue(control) as Panel;
            var form = typeof(IconsControl).GetField("_form", Private)?.GetValue(control) as Control;

            if (host == null || form == null)
            {
                Console.WriteLine("FAIL: no form and host to measure " + where);
                return 1;
            }

            var failures = 0;

            if (host.HorizontalScroll.Visible)
            {
                Console.WriteLine("FAIL: the form pane has a scroll bar along the foot " + where +
                                  " - " + form.Width + " wide in " + host.ClientSize.Width);
                failures++;
            }

            if (host.VerticalScroll.Visible)
            {
                Console.WriteLine("FAIL: the form pane has a scroll bar down the side " + where +
                                  " - " + form.Height + " tall in " + host.ClientSize.Height);
                failures++;
            }

            if (failures == 0) Console.WriteLine("OK: the form stands whole " + where);
            return failures;
        }

        /// <summary>
        /// The whole window again at a font half again as big, which is the only shape high-DPI
        /// trouble can reach this tool in.
        ///
        /// XrmToolBox is DPI-unaware: its manifest asks for "Per-monitor", which is not one of
        /// the strings Windows parses, so the process is unaware and Windows bitmap-stretches
        /// the whole window at 150%. Nothing in here ever sees 144 dpi. What it can see is a
        /// bigger font - somebody who ticks "Override high DPI scaling behaviour" on
        /// XrmToolBox.exe, or the day MscrmTools/XrmToolBox#1260 is merged. Then the text grows
        /// and everything written down in pixels does not.
        ///
        /// The font goes on the form before the control is added, which is the order the host
        /// does it in: a tool is built with the default font and inherits the window's when it
        /// is parented. Set it afterwards and the one-shot splitter minimums have already been
        /// laid at the old size, which would be a bug this harness invented rather than found.
        /// </summary>
        private static int ShootAtALargerFont(string outputDir)
        {
            // Windows will not show a window bigger than the screen, so a size asked for here is
            // only a wish on a laptop - and a pass that quietly ran in a window half the size it
            // meant to would be a pass about something else. It is taken from the screen, and
            // said out loud below.
            var work = Screen.PrimaryScreen.WorkingArea;

            using (var control = new IconsControl())
            using (var form = new Form
            {
                Width = Math.Min(1770, work.Width),
                Height = Math.Min(1140, work.Height),
                Text = "Icons, at a larger font"
            })
            {
                // The window grows with the font, because on the machine where the font is that
                // big the window is that big too. A 1180-wide window at a 1.5x font is a
                // different question - one about a pane too narrow for its rows - and check 11
                // is the one that asks it.
                form.Font = new Font(form.Font.FontFamily, form.Font.Size * 1.5f);
                form.Controls.Add(control);
                form.Show();
                Pump();

                Shoot(form, outputDir, "14-larger-font");

                var where = "at a font half again as big, in a " + form.Width + "x" + form.Height + " window";

                var failures = NoScrollBars(control, where);
                failures += NothingIsClipped(control, where);

                form.Close();
                Pump();
                return failures;
            }
        }

        /// <summary>
        /// The window opened in a pane narrower than the two sides would like, which is what a
        /// restored-down XrmToolBox is.
        ///
        /// The proportions are laid once, on the first layout wide enough to hold both sides -
        /// and what each side would like is now measured off its own rows, so it grew. If that
        /// measurement is also what decides whether the layout happens, a window between the old
        /// threshold and the new one gets no layout at all: a SplitContainer opens at fifty
        /// pixels and stays there, and the whole library is a sliver.
        /// </summary>
        private static int CheckTheWindowOpensNarrow(string outputDir)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

            using (var control = new IconsControl())
            using (var form = new Form { Width = 800, Height = 700, Text = "Icons, in a narrow window" })
            {
                form.Controls.Add(control);
                form.Show();
                Pump();

                Shoot(form, outputDir, "15-narrow-window");

                var body = typeof(IconsControl).GetField("_body", Private)?.GetValue(control) as SplitContainer;
                if (body == null)
                {
                    Console.WriteLine("FAIL: no body splitter in a narrow window");
                    return 1;
                }

                var failures = 0;

                // 320 is the width the library pane has always been allowed to shrink to, and
                // an 800-wide window has the room for it. Under that, either the proportions
                // were never laid at all or the target side took what was not its to take.
                if (body.SplitterDistance < 320)
                {
                    Console.WriteLine("FAIL: in an 800-wide window the library pane is only " +
                                      body.SplitterDistance + " pixels wide");
                    failures++;
                }
                else
                {
                    Console.WriteLine("OK: an 800-wide window still opens on two panes, the library " +
                                      body.SplitterDistance + " of it");
                }

                failures += NoScrollBars(control, "in an 800-wide window");

                form.Close();
                Pump();
                return failures;
            }
        }

        /// <summary>
        /// Everything is given at least the room it says it needs, and stays inside the cell it
        /// was put in.
        ///
        /// Worth a check of its own because nothing else here can see it. The form is docked to
        /// the top of a pane that scrolls, so a row too short for what stands in it grows no
        /// scroll bar anywhere: it just draws a combo box with its last two pixels missing, or a
        /// caption running on under the box beside it. Every other check passes and the
        /// screenshot looks fine until somebody reads it closely.
        /// </summary>
        private static int NothingIsClipped(Control root, string where)
        {
            var failures = 0;

            foreach (var control in Descendants(root))
            {
                if (!control.Visible || control.Width <= 0 || control.Height <= 0) continue;

                // The ones that hold text and are sized by somebody else. A list is as tall as
                // it is given on purpose, and the preview draws itself.
                if (!(control is TextBox || control is ComboBox || control is ButtonBase || control is Label))
                    continue;

                // Measured at the width it has, so a line that wraps is measured wrapped rather
                // than as one long line - which would pass on height and fail on width.
                var wanted = control.GetPreferredSize(new Size(control.Width, 0));

                if (wanted.Height > control.Height + 1)
                {
                    Console.WriteLine("FAIL: " + Describe(control) + " needs " + wanted.Height +
                                      " pixels of height " + where + " and has " + control.Height);
                    failures++;
                }

                var cell = Cell(control);
                if (cell != null && (control.Right > cell.Value.Right + 1 || control.Bottom > cell.Value.Bottom + 1))
                {
                    var at = ((TableLayoutPanel)control.Parent).GetCellPosition(control);
                    Console.WriteLine("FAIL: " + Describe(control) + " runs out of its cell " + where +
                                      " - row " + at.Row + ", column " + at.Column + " of " +
                                      Describe(control.Parent) + ", ending at " + control.Right + "," +
                                      control.Bottom + " where the cell ends at " + cell.Value.Right +
                                      "," + cell.Value.Bottom);
                    failures++;
                }
            }

            if (failures == 0) Console.WriteLine("OK: nothing is drawn smaller than what it holds " + where);
            return failures;
        }

        /// <summary>Where a control's cell is, for the ones laid out in a table.</summary>
        private static Rectangle? Cell(Control control)
        {
            var table = control.Parent as TableLayoutPanel;
            if (table == null) return null;

            var position = table.GetCellPosition(control);
            var widths = table.GetColumnWidths();
            var heights = table.GetRowHeights();

            if (position.Column < 0 || position.Row < 0 ||
                position.Column >= widths.Length || position.Row >= heights.Length) return null;

            var x = table.DisplayRectangle.X;
            for (var i = 0; i < position.Column; i++) x += widths[i];

            var y = table.DisplayRectangle.Y;
            for (var i = 0; i < position.Row; i++) y += heights[i];

            var width = 0;
            var lastColumn = Math.Min(widths.Length, position.Column + table.GetColumnSpan(control));
            for (var i = position.Column; i < lastColumn; i++) width += widths[i];

            var height = 0;
            var lastRow = Math.Min(heights.Length, position.Row + table.GetRowSpan(control));
            for (var i = position.Row; i < lastRow; i++) height += heights[i];

            return new Rectangle(x, y, width, height);
        }

        /// <summary>A control named the way somebody reading the failure would name it.</summary>
        private static string Describe(Control control)
        {
            var said = control.Text;
            if (string.IsNullOrEmpty(said)) said = control.AccessibleName;

            if (string.IsNullOrEmpty(said)) return "an unlabelled " + control.GetType().Name;
            if (said.Length > 40) said = said.Substring(0, 40) + "...";

            return "the " + control.GetType().Name + " \"" + said + "\"";
        }

        private static T Find<T>(Control root, string hint) where T : Control
        {
            // The controls are built in code and mostly unnamed, so find the search box by where
            // it sits: the only textbox inside the toolbar strip's neighbouring header row.
            return Descendants(root)
                .OfType<T>()
                .FirstOrDefault(c => c.Parent is TableLayoutPanel
                                     && c.Parent.Controls.OfType<Label>().Any(l =>
                                            l.Text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var grandchild in Descendants(child)) yield return grandchild;
            }
        }

        /// <summary>
        /// Opens the About box without a modal loop and photographs it folded and unfolded. A
        /// window that grows when you open the licences is a claim that can be measured, so it
        /// is: the unfolded shot has to be taller than the folded one and no wider.
        /// </summary>
        private static int ShootAboutBox(string outputDir)
        {
            var about = ToolAbout.FromPlugin(
                typeof(IconsPlugin), "icons", "https://github.com/comentality/xrm-icons");

            about.Notices = IconCatalog.Load().About;
            about.NoticesCaption = "Icon licences and credits";

            var failures = 0;

            using (var box = new AboutBox(about))
            {
                box.StartPosition = FormStartPosition.Manual;
                box.Location = new Point(40, 40);
                box.Show();
                Pump();

                var folded = box.Size;
                Shoot(box, outputDir, "10-about-folded");

                var disclosure = Descendants(box).OfType<LinkLabel>()
                    .FirstOrDefault(l => l.Text.IndexOf("licences", StringComparison.OrdinalIgnoreCase) >= 0);

                if (disclosure == null)
                {
                    Console.WriteLine("FAIL: the About box offers no way to open the licences");
                    return failures + 1;
                }

                Click(disclosure);
                Pump();

                var unfolded = box.Size;
                Shoot(box, outputDir, "11-about-unfolded");

                if (unfolded.Height <= folded.Height)
                {
                    Console.WriteLine("FAIL: opening the licences did not grow the window - " +
                                      folded.Height + " then " + unfolded.Height);
                    failures++;
                }

                if (unfolded.Width != folded.Width)
                {
                    Console.WriteLine("FAIL: opening the licences changed the width - " +
                                      folded.Width + " then " + unfolded.Width);
                    failures++;
                }

                var notices = Descendants(box).OfType<TextBox>().FirstOrDefault();
                if (notices == null || !notices.Visible || notices.Text.Length == 0)
                {
                    Console.WriteLine("FAIL: the licences are not showing after the click");
                    failures++;
                }

                box.Close();
            }

            return failures;
        }

        /// <summary>
        /// Clicks a LinkLabel the way a mouse does, without a mouse. LinkLabel raises its click
        /// from a protected method and offers nothing public that does the same.
        /// </summary>
        private static void Click(LinkLabel link)
        {
            var raise = typeof(LinkLabel).GetMethod("OnLinkClicked",
                BindingFlags.Instance | BindingFlags.NonPublic);

            raise.Invoke(link, new object[] { new LinkLabelLinkClickedEventArgs(link.Links[0]) });
        }

        private static void Pump()
        {
            for (var i = 0; i < 12; i++)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(40);
            }
        }

        private static void Shoot(Form form, string directory, string name)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                var path = Path.Combine(directory, name + ".png");
                bitmap.Save(path, ImageFormat.Png);
                Console.WriteLine("Wrote " + path);
            }
        }
    }
}
