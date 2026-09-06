using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Icons.Catalog;
using Icons.Logic;

namespace Icons.MockHarness
{
    /// <summary>
    /// Draws the candidate in-context previews to PNG and stops.
    ///
    /// No window and no connection: the icon library is inside the assembly and the chrome is
    /// drawn from constants, so the only thing this needs is a font. It exists to be looked at
    /// and argued with before any of it goes near the tool.
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

            using (var catalog = IconCatalog.Load())
            {
                var cast = new Cast
                {
                    Chosen = Read(catalog, wanted),
                    ChosenLabel = "Account",
                    Neighbours = new Dictionary<string, SvgImage>
                    {
                        // Stand-ins for the tables either side of the one being iconed. Fixed
                        // names for now; connected, these would be the environment's own.
                        { "home", Read(catalog, "home") },
                        { "recent", Read(catalog, "history") },
                        { "pinned", Read(catalog, "pin") },
                        { "contact", Read(catalog, "person") },
                        { "case", Read(catalog, "headset") },
                        { "order", Read(catalog, "receipt") }
                    }
                };

                if (cast.Chosen == null)
                {
                    Console.WriteLine("FAIL: no icon in the library matched " + wanted);
                    return 1;
                }

                Save(Sheets.ContactSheet(cast), outputDir, "a-contact-sheet");
                Save(Sheets.AppScreen(cast, Theme.Light), outputDir, "b-app-screen-light");
                Save(Sheets.AppScreen(cast, Theme.Dark), outputDir, "b-app-screen-dark");
                Save(Sheets.Magnified(cast, Theme.Light), outputDir, "c-magnified");
            }

            Console.WriteLine("Wrote sheets to " + Path.GetFullPath(outputDir));
            return 0;
        }

        /// <summary>
        /// The library's icon for a word. Prefers an exact name, then the shortest name that
        /// starts with it, so "person" finds person rather than person_wrench_20.
        /// </summary>
        private static SvgImage Read(IconCatalog catalog, string word)
        {
            var candidates = catalog.Icons
                .Where(i => i.Family == IconFamily.Fluent && i.Style == IconStyle.Regular)
                .ToList();

            var entry = candidates.FirstOrDefault(i => i.Name == word)
                        ?? candidates.Where(i => i.Name.StartsWith(word + "_", StringComparison.Ordinal))
                            .OrderBy(i => i.Name.Length).FirstOrDefault()
                        ?? candidates.Where(i => i.Name.IndexOf(word, StringComparison.Ordinal) >= 0)
                            .OrderBy(i => i.Name.Length).FirstOrDefault();

            if (entry == null)
            {
                Console.WriteLine("  (no icon for " + word + "; its slot will be an empty box)");
                return null;
            }

            Console.WriteLine("  " + word + " -> " + entry.Name);
            try { return SvgImage.Parse(catalog.ReadSvg(entry)); }
            catch (Exception e) { Console.WriteLine("  ! " + entry.Name + ": " + e.Message); return null; }
        }

        /// <summary>
        /// Saved twice: once a pixel to a pixel, and once at double, which is what a 200%
        /// display shows and what a screenshot needs to be legible when it is looked at.
        /// </summary>
        private static void Save(Bitmap sheet, string dir, string name)
        {
            using (sheet)
            {
                sheet.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);

                using (var big = new Bitmap(sheet.Width * 2, sheet.Height * 2))
                {
                    using (var g = Graphics.FromImage(big))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                        g.DrawImage(sheet, 0, 0, big.Width, big.Height);
                    }

                    big.Save(Path.Combine(dir, name + "@2x.png"), ImageFormat.Png);
                }

                Console.WriteLine("  wrote " + name + ".png (" + sheet.Width + "x" + sheet.Height + ")");
            }
        }
    }
}
