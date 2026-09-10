using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

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

            try
            {
                using (var control = new IconsControl())
                using (var form = new Form { Width = 1180, Height = 760, Text = "Icons" })
                {
                    form.Controls.Add(control);
                    form.Show();
                    Pump();

                    Shoot(form, outputDir, "01-opened");

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
                        .OfType<ToolStrip>()
                        .SelectMany(t => t.Items.OfType<ToolStripButton>())
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

                    failures += CheckContextPreviewOpens(control, outputDir);
                    failures += CheckNameSurvivesRebuild(control, form, outputDir);
                    failures += CheckUnsetOnlyFilter(control, form, outputDir);
                    failures += CheckLockedTablesAreHiddenAndExplained(control, form, outputDir);
                    failures += CheckRasterHelpOffered(control);
                    failures += CheckFamiliesAreShownAndFiltered(control, form, outputDir);

                    form.Close();
                    Pump();
                }
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
            var familyBox = Descendants(control).OfType<ComboBox>()
                .FirstOrDefault(c => c.Items.Count > 0 && c.Items[0] as string == "All families");

            if (search == null || grid == null || toggle == null || familyBox == null)
            {
                Console.WriteLine("FAIL: the family toggle or the family filter is not in the window");
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
            for (var i = 1; i < familyBox.Items.Count; i++)
            {
                familyBox.SelectedIndex = i;
                Pump();
                narrowed.Add(familyBox.Items[i] + ": " + grid.VirtualListSize);

                if (grid.VirtualListSize == 0)
                {
                    Console.WriteLine("FAIL: " + familyBox.Items[i] + " has no folder, so that family is not in the bundle");
                    failures++;
                }
                if (grid.VirtualListSize >= everyFamily)
                {
                    Console.WriteLine("FAIL: filtering to " + familyBox.Items[i] + " did not narrow anything");
                    failures++;
                }
            }

            Console.WriteLine("Folders per family - " + string.Join(", ", narrowed) + " (all families: " + everyFamily + ")");
            Shoot(form, outputDir, "08-one-family");

            familyBox.SelectedIndex = 0;
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
        /// Opens the in-context preview and photographs it.
        ///
        /// It is a window of its own, so nothing else in this harness would ever draw it, and a
        /// form that throws on its first paint throws in front of a user. There is no connection
        /// here, so what it must show is the stand-in navigation - and it must say that it is
        /// one, because a preview of an invented navigation and a preview of the environment's
        /// own are worth very different amounts.
        /// </summary>
        private static int CheckContextPreviewOpens(Control control, string outputDir)
        {
            var failures = 0;

            var button = Descendants(control)
                .OfType<ToolStrip>()
                .SelectMany(t => t.Items.OfType<ToolStripButton>())
                .FirstOrDefault(b => b.Text.StartsWith("See it in context"));

            if (button == null)
            {
                Console.WriteLine("FAIL: no in-context preview button found");
                return 1;
            }

            if (!button.Enabled)
            {
                Console.WriteLine("FAIL: the preview button is dead with an icon chosen");
                return 1;
            }

            button.PerformClick();
            Pump();

            var window = Application.OpenForms.OfType<Form>()
                .FirstOrDefault(f => f.Text == "The icon in context");

            if (window == null)
            {
                Console.WriteLine("FAIL: clicking the preview button opened no window");
                return 1;
            }

            Pump();
            Shoot(window, outputDir, "06-in-context");

            var note = Descendants(window)
                .OfType<ToolStrip>()
                .SelectMany(t => t.Items.OfType<ToolStripLabel>())
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

            window.Close();
            Pump();

            return failures;
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

            var tableBox = RowControl(control, "Table") as ComboBox;
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

        private static int IndexOfTable(ComboBox tableBox, string logicalName)
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

            var tableBox = RowControl(control, "Table") as ComboBox;
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

            filterBox.Text = "";
            showTables.Invoke(control, null);
            Pump();

            if (tableBox.Items.Count != 3)
            {
                Console.WriteLine("FAIL: unfiltered, all three seeded tables should be offered, not " +
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

            var tableBox = RowControl(control, "Table") as ComboBox;
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
        private static string Listed(ComboBox box)
        {
            return box.Items.Count == 0
                ? "nothing"
                : string.Join(", ", box.Items.Cast<object>().Select(i => i.ToString()));
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
                var label = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Text == caption);
                if (label == null) continue;
                return panel.GetControlFromPosition(1, panel.GetPositionFromControl(label).Row);
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
