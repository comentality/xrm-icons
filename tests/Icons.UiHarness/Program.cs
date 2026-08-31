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
