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
    ///     Icons.MockHarness.exe [output dir] [icon name] [scale] [navigation dump]
    ///
    /// The navigation is invented unless a dump is given. A dump is what Icons.E2eHarness
    /// --sitemap writes: a real app's navigation, in its order, with its own icons beside it.
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

                var dump = args.Length > 3 ? args[3] : null;
                if (dump != null)
                {
                    if (!LoadNavigation(cast, dump, catalog))
                    {
                        Console.WriteLine("FAIL: no nav.tsv under " + dump);
                        return 1;
                    }

                    Save(Sheets.AppScreen(cast, Theme.Light, scale, false), outputDir, "nav-real-light");
                    Save(Sheets.AppScreen(cast, Theme.Dark, scale, false), outputDir, "nav-real-dark");
                    Console.WriteLine("Wrote sheets to " + Path.GetFullPath(outputDir));
                    return 0;
                }

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
        /// A real app's navigation, as Icons.E2eHarness --sitemap wrote it out.
        ///
        /// The chosen icon takes the place of one of the tables rather than being added beside
        /// them: the question the preview answers is how this icon sits among that app's own,
        /// and an extra row nobody has is not that question.
        /// </summary>
        private static bool LoadNavigation(Cast cast, string folder, IconCatalog catalog)
        {
            var file = Path.Combine(folder, "nav.tsv");
            if (!File.Exists(file)) return false;

            var entries = new List<NavEntry>();
            foreach (var line in File.ReadAllLines(file).Skip(1))
            {
                var parts = line.Split('	');
                if (parts.Length < 3) continue;

                var kind = parts[0].Trim();
                var label = parts[1].Trim();
                if (label.Length == 0) continue;

                if (kind == "group")
                {
                    entries.Add(new NavEntry(label, null, true));
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

                // A table whose icon the environment cannot supply - a standard one, mostly,
                // since none of those keep theirs as a web resource - is drawn from the library
                // by its own name. The app draws those from Fluent too, so this is closer to the
                // truth than an empty box, though it is still a stand-in and not the org's.
                //
                // Mock only. This takes the first library name that contains a word of the
                // label, which is the substring match the tool's own TODO calls out for
                // producing confident nonsense. The real preview should ask IconSearch.
                if (icon == null)
                {
                    foreach (var word in label.ToLowerInvariant()
                                 .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        icon = Read(catalog, word);
                        if (icon != null) break;
                    }
                }

                entries.Add(new NavEntry(label, icon));
            }

            if (entries.Count == 0) return false;

            // Stand in the middle of the list, where there are neighbours above and below.
            var tables = entries.Where(e => !e.IsGroup).ToList();
            var target = tables.Count > 0 ? tables[tables.Count / 2] : entries[0];
            target.Icon = cast.Chosen;
            cast.ChosenLabel = target.Label;
            cast.Entries = entries;
            cast.ChosenAt = entries.IndexOf(target);

            Console.WriteLine("  navigation from " + folder + ": " + entries.Count + " row(s), "
                + entries.Count(e => !e.IsGroup && e.Icon != null) + " drawn");
            return true;
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
