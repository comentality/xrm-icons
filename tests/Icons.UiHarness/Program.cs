using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
