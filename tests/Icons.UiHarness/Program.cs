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

                    failures += CheckNameSurvivesRebuild(control, form, outputDir);
                    failures += CheckUnsetOnlyFilter(control, form, outputDir);
                    failures += CheckRasterHelpOffered(control);

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
        /// A hand-edited web resource name must survive everything that rebuilds the table list
        /// and re-selects the same table - a reload, or a single keystroke in the table filter.
        /// Both fire SelectedIndexChanged, and SuggestName used to overwrite the box on every
        /// one of them.
        ///
        /// There is no connection here, so the tables are seeded straight into the private
        /// list. Reflection, because the product needs no seam for this and should not grow one.
        /// </summary>
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
            return failures;
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
                                          string currentIcon = null)
        {
            var item = Activator.CreateInstance(itemType, true);
            itemType.GetField("LogicalName").SetValue(item, logicalName);
            itemType.GetField("DisplayName").SetValue(item, displayName);
            itemType.GetField("Customizable").SetValue(item, true);
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
