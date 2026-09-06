using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Icons.Catalog;
using Icons.Logic;
using Icons.Ui;

namespace Icons.MockHarness
{
    /// <summary>
    /// Photographs the navigation preview and stops.
    ///
    /// It draws with the tool's own <see cref="NavPainter"/> rather than a copy of it, so a
    /// sheet that looks right is evidence about the tool and not about the harness. No window
    /// and no connection: the library is inside the assembly, and a real navigation is read
    /// from what Icons.E2eHarness --sitemap wrote out.
    ///
    ///     Icons.MockHarness.exe [output dir] [icon name] [scale] [navigation dump]
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var outputDir = args.Length > 0
                ? args[0]
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".mock");
            Directory.CreateDirectory(outputDir);

            var wanted = args.Length > 1 ? args[1] : "building_multiple";

            float scale;
            if (args.Length < 3 || !float.TryParse(args[2], out scale) || scale <= 0) scale = 2f;

            using (var catalog = IconCatalog.Load())
            {
                var chosen = FromLibrary(catalog, wanted);
                if (chosen == null)
                {
                    Console.WriteLine("FAIL: no icon in the library matched " + wanted);
                    return 1;
                }

                var dump = args.Length > 3 ? args[3] : null;

                List<NavRow> rows;
                int selected;
                string name;

                if (dump != null)
                {
                    if (!Read(dump, chosen, catalog, out rows, out selected))
                    {
                        Console.WriteLine("FAIL: no nav.tsv under " + dump);
                        return 1;
                    }

                    name = "nav-real";
                }
                else
                {
                    rows = StandIn(catalog, chosen);
                    selected = 4;
                    name = "nav";
                }

                Save(Draw(rows, selected, NavTheme.Light, scale), outputDir, name + "-light");
                Save(Draw(rows, selected, NavTheme.Dark, scale), outputDir, name + "-dark");
            }

            Console.WriteLine("Wrote sheets to " + Path.GetFullPath(outputDir));
            return 0;
        }

        /// <summary>
        /// The navigation in a window, drawn the way the preview draws it. The scenery to the
        /// right is blank on purpose: the icon appears where the icon appears, and a mock that
        /// invents a second place for it is the thing this was built to avoid.
        /// </summary>
        private static Bitmap Draw(IList<NavRow> rows, int selected, NavTheme theme, float scale)
        {
            var painter = new NavPainter(theme, scale);
            var navWidth = painter.Width;
            var navHeight = painter.Height(rows);

            var width = navWidth + At(460, scale);
            var height = Math.Max(navHeight, At(380, scale));

            var sheet = new Bitmap(width, height);
            using (var g = Graphics.FromImage(sheet))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                using (var brush = new SolidBrush(theme.Page)) g.FillRectangle(brush, 0, 0, width, height);

                painter.Draw(g, 0, 0, rows, selected, Color.FromArgb(0x24, 0x24, 0x24));

                if (navHeight < height)
                {
                    using (var brush = new SolidBrush(theme.NavBack))
                        g.FillRectangle(brush, 0, navHeight, navWidth, height - navHeight);
                }

                using (var pen = new Pen(theme.Line)) g.DrawLine(pen, navWidth, 0, navWidth, height);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(theme.Line))
                {
                    var x = navWidth + At(24, scale);
                    var y = At(28, scale);
                    Pill(g, brush, x, y, At(150, scale), At(14, scale));

                    y += At(40, scale);
                    for (var i = 0; i < 40 && y < height - At(12, scale); i++)
                    {
                        Pill(g, brush, x, y, At(i % 3 == 0 ? 180 : 130, scale), At(8, scale));
                        Pill(g, brush, x + At(220, scale), y, At(84, scale), At(8, scale));
                        y += At(30, scale);
                    }
                }
            }

            return sheet;
        }

        private static int At(int cssPixels, float scale)
        {
            return (int)Math.Round(cssPixels * scale);
        }

        private static void Pill(Graphics g, Brush brush, int x, int y, int w, int h)
        {
            if (w <= h) { g.FillRectangle(brush, x, y, w, h); return; }

            using (var path = new GraphicsPath())
            {
                path.AddArc(x, y, h, h, 90, 180);
                path.AddArc(x + w - h, y, h, h, 270, 180);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }

        /// <summary>The invented navigation, the same one the tool shows before it has a connection.</summary>
        private static List<NavRow> StandIn(IconCatalog catalog, SvgImage chosen)
        {
            return new List<NavRow>
            {
                new NavRow("Home", FromLibrary(catalog, "home")),
                new NavRow("Recent", FromLibrary(catalog, "history")),
                new NavRow("Pinned", FromLibrary(catalog, "pin")),
                new NavRow("Customers", null, true),
                new NavRow("Accounts", chosen),
                new NavRow("Contacts", FromLibrary(catalog, "person")),
                new NavRow("Sales", null, true),
                new NavRow("Leads", FromLibrary(catalog, "target")),
                new NavRow("Opportunities", FromLibrary(catalog, "trophy")),
                new NavRow("Orders", FromLibrary(catalog, "receipt"))
            };
        }

        /// <summary>
        /// A real app's navigation, as Icons.E2eHarness --sitemap wrote it out. The chosen icon
        /// takes the place of one of the tables rather than being added beside them: the question
        /// is how it sits among that app's own, and an extra row nobody has is not that question.
        /// </summary>
        private static bool Read(string folder, SvgImage chosen, IconCatalog catalog,
            out List<NavRow> rows, out int selected)
        {
            rows = new List<NavRow>();
            selected = -1;

            var file = Path.Combine(folder, "nav.tsv");
            if (!File.Exists(file)) return false;

            foreach (var line in File.ReadAllLines(file).Skip(1))
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;

                var label = parts[1].Trim();
                if (label.Length == 0) continue;

                if (parts[0].Trim() == "group")
                {
                    rows.Add(new NavRow(label, null, true));
                    continue;
                }

                SvgImage icon = null;
                if (parts[2].Trim().Length > 0)
                {
                    var path = Path.Combine(folder, parts[2].Trim());
                    if (File.Exists(path))
                    {
                        try { icon = SvgImage.Parse(File.ReadAllText(path)); }
                        catch (Exception e) { Console.WriteLine("  ! " + parts[2] + ": " + e.Message); }
                    }
                }

                rows.Add(new NavRow(label, icon));
            }

            if (rows.Count == 0) return false;

            // Stand in the middle, where there are neighbours above and below.
            var tables = rows.Select((r, i) => new { r, i }).Where(x => !x.r.IsGroup).ToList();
            if (tables.Count > 0)
            {
                selected = tables[tables.Count / 2].i;
                rows[selected].Icon = chosen;
            }

            Console.WriteLine("  navigation from " + folder + ": " + rows.Count + " row(s), "
                + rows.Count(r => !r.IsGroup && r.Icon != null) + " drawn");
            return true;
        }

        /// <summary>
        /// The library's icon for a word. Prefers an exact name, then the shortest name that
        /// starts with it, so "person" finds person rather than person_wrench.
        /// </summary>
        private static SvgImage FromLibrary(IconCatalog catalog, string word)
        {
            var candidates = catalog.Icons
                .Where(i => i.Family == IconFamily.Fluent && i.Style == IconStyle.Regular)
                .ToList();

            var entry = candidates.FirstOrDefault(i => i.Name == word)
                        ?? candidates.Where(i => i.Name.StartsWith(word + "_", StringComparison.Ordinal))
                            .OrderBy(i => i.Name.Length).FirstOrDefault();

            if (entry == null)
            {
                Console.WriteLine("  (no icon for " + word + "; its slot will be an empty box)");
                return null;
            }

            try { return SvgImage.Parse(catalog.ReadSvg(entry)); }
            catch (Exception e) { Console.WriteLine("  ! " + entry.Name + ": " + e.Message); return null; }
        }

        private static void Save(Bitmap sheet, string dir, string name)
        {
            using (sheet)
            {
                sheet.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
                Console.WriteLine("  wrote " + name + ".png (" + sheet.Width + "x" + sheet.Height + ")");
            }
        }
    }
}
