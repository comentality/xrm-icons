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
    /// Draws the navigation mock to PNG and stops.
    ///
    /// No window and no connection: the icon library is inside the assembly and the chrome is
    /// drawn from constants, so the only thing this needs is a font. It exists to be looked at
    /// and argued with before any of it goes near the tool.
    ///
    ///     Icons.MockHarness.exe [output dir] [icon name] [scale]
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

            int scale;
            if (args.Length < 3 || !int.TryParse(args[2], out scale) || scale < 1) scale = 2;

            using (var catalog = IconCatalog.Load())
            {
                var chosen = Read(catalog, wanted);
                if (chosen == null)
                {
                    Console.WriteLine("FAIL: no icon in the library matched " + wanted);
                    return 1;
                }

                var cast = new Cast { Chosen = chosen, ChosenLabel = "Accounts" };

                // A stand-in navigation. Connected, these are the environment's own tables in
                // the order an app's site map puts them; nothing here reads that yet, so the
                // shape is invented and only the chosen icon is real.
                cast.Entries = new List<NavEntry>
                {
                    new NavEntry("Home", Read(catalog, "home"), false),
                    new NavEntry("Recent", Read(catalog, "history"), false),
                    new NavEntry("Pinned", Read(catalog, "pin"), false),
                    new NavEntry("Customers", null, true),
                    new NavEntry(cast.ChosenLabel, chosen, false),
                    new NavEntry("Contacts", Read(catalog, "person"), false),
                    new NavEntry("Sales", null, true),
                    new NavEntry("Leads", Read(catalog, "target"), false),
                    new NavEntry("Opportunities", Read(catalog, "trophy"), false),
                    new NavEntry("Orders", Read(catalog, "receipt"), false),
                    new NavEntry("Cases", Read(catalog, "headset"), false)
                };
                cast.ChosenAt = 4;

                Save(Sheets.AppScreen(cast, Theme.Light, scale, false), outputDir, "nav-light");
                Save(Sheets.AppScreen(cast, Theme.Dark, scale, false), outputDir, "nav-dark");
                Save(Sheets.AppScreen(cast, Theme.Light, scale, true), outputDir, "nav-light-collapsed");
                Save(Sheets.AppScreen(cast, Theme.Dark, scale, true), outputDir, "nav-dark-collapsed");
            }

            Console.WriteLine("Wrote sheets to " + Path.GetFullPath(outputDir));
            return 0;
        }

        /// <summary>
        /// The library's icon for a word. Prefers an exact name, then the shortest name that
        /// starts with it, so "person" finds person rather than person_wrench.
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
