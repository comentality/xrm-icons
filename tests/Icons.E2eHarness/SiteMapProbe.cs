using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Icons.Logic;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Tooling.Connector;

namespace Icons.E2eHarness
{
    /// <summary>
    /// Runs the tool's own site map reading against a real environment, and writes what it found.
    ///
    /// It calls <see cref="SiteMapSource"/> rather than queries of its own, so this is a test of
    /// the code that ships and not of a copy of it. That matters more here than usual: the path
    /// to an app's navigation was not guessable and had to be measured. A plain query on
    /// <c>sitemap</c> returns the legacy Settings map and no modern app's, so a map is reached
    /// through the app's component list and retrieved by id.
    ///
    /// Reads only. The navigation it writes out is what Icons.MockHarness draws from.
    /// </summary>
    internal static class SiteMapProbe
    {
        public static int Run(CrmServiceClient client, string root)
        {
            var report = new StringBuilder();
            var failures = 0;

            void Say(string line)
            {
                Console.WriteLine("        " + line);
                report.AppendLine(line);
            }

            // What the site map needs to know about the tables it names - the same thing the
            // tool already has in hand from its own load.
            var response = (RetrieveAllEntitiesResponse)client.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });

            var tables = new Dictionary<string, SiteMapTable>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in response.EntityMetadata)
            {
                if (e.LogicalName == null) continue;

                tables[e.LogicalName] = new SiteMapTable
                {
                    DisplayName = e.DisplayName?.UserLocalizedLabel?.Label,
                    VectorIcon = e.IconVectorName
                };
            }

            var named = response.EntityMetadata.Where(e => e.DisplayName?.UserLocalizedLabel?.Label != null).ToList();
            Say("== the environment's icons ==");
            Say("  tables with a display name: " + named.Count);
            Say("  with IconVectorName: " + named.Count(e => !string.IsNullOrEmpty(e.IconVectorName))
                + "  (custom " + named.Count(e => e.IsCustomEntity == true && !string.IsNullOrEmpty(e.IconVectorName))
                + ", standard " + named.Count(e => e.IsCustomEntity != true && !string.IsNullOrEmpty(e.IconVectorName)) + ")");

            var apps = SiteMapSource.Apps(client);

            Say("");
            Say("== apps with a navigation ==");
            Say("  " + apps.Count + " found");

            if (apps.Count == 0)
            {
                Say("  ! nothing to read. Either this environment has no app, or the path to one changed.");
                failures++;
            }

            var navRoot = Path.Combine(root, "nav");
            Directory.CreateDirectory(navRoot);

            foreach (var app in apps)
            {
                IReadOnlyList<SiteMapEntry> entries;
                try
                {
                    entries = SiteMapSource.Navigation(client, app.SiteMapId, tables);
                }
                catch (Exception e)
                {
                    Say("  " + app.Name + ": could not read its navigation - " + e.Message);
                    failures++;
                    continue;
                }

                var rows = entries.Count(e => e.Kind != SiteMapEntryKind.Group);
                var wanted = entries.Select(e => e.IconWebResource).Where(n => n.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // The one query the exploring never exercised: every icon asked for at once.
                var svg = SiteMapSource.Icons(client, wanted);

                Say("");
                Say("  " + app.Name + ": " + entries.Count + " row(s), " + rows + " with an icon slot, "
                    + wanted.Count + " naming a web resource, " + svg.Count + " fetched");

                if (wanted.Count > 0 && svg.Count == 0)
                {
                    Say("  ! named " + wanted.Count + " web resources and fetched none. The bulk fetch is wrong.");
                    failures++;
                }

                var unreadable = 0;
                foreach (var text in svg.Values)
                {
                    try { SvgImage.Parse(text); }
                    catch { unreadable++; }
                }

                if (unreadable > 0)
                    Say("  " + unreadable + " of " + svg.Count + " fetched icons will not draw");

                foreach (var entry in entries.Where(e => e.Kind == SiteMapEntryKind.Table))
                {
                    Say("    " + entry.Label.PadRight(34)
                        + (entry.IconWebResource.Length == 0
                            ? "(no icon this tool can fetch)"
                            : svg.ContainsKey(entry.IconWebResource) ? entry.IconWebResource : entry.IconWebResource + "  (NOT FETCHED)"));
                }

                Dump(navRoot, app.Name, entries, svg);
            }

            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "sitemap-probe.txt");
            File.WriteAllText(path, report.ToString());
            Console.WriteLine("        wrote " + path);

            Console.WriteLine(failures == 0
                ? "        the tool read this environment's navigations."
                : "        " + failures + " problem(s) reading this environment's navigations.");

            return failures == 0 ? 0 : 1;
        }

        /// <summary>
        /// Writes one navigation and its icons out, so the mock harness can draw the real thing
        /// with no connection of its own.
        /// </summary>
        private static void Dump(string navRoot, string appName,
            IReadOnlyList<SiteMapEntry> entries, Dictionary<string, string> svg)
        {
            var folder = Path.Combine(navRoot, Safe(appName));
            Directory.CreateDirectory(folder);

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in svg)
            {
                var file = Safe(pair.Key) + ".svg";
                File.WriteAllText(Path.Combine(folder, file), pair.Value);
                files[pair.Key] = file;
            }

            using (var writer = new StreamWriter(Path.Combine(folder, "nav.tsv"), false, Encoding.UTF8))
            {
                writer.WriteLine("kind\tlabel\tfile");
                foreach (var entry in entries)
                {
                    string file;
                    files.TryGetValue(entry.IconWebResource, out file);
                    writer.WriteLine(
                        (entry.Kind == SiteMapEntryKind.Group ? "group" : "table")
                        + "\t" + entry.Label + "\t" + (file ?? ""));
                }
            }
        }

        private static string Safe(string name)
        {
            var builder = new StringBuilder();
            foreach (var c in name) builder.Append(char.IsLetterOrDigit(c) ? c : '-');
            return builder.ToString();
        }
    }
}
