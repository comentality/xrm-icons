using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;

namespace Icons.E2eHarness
{
    /// <summary>
    /// Asks the org what an app's navigation is actually made of, and how many of the tables in
    /// it could have their icon drawn.
    ///
    /// Reads only. It exists because a preview of a site map is worth building only if the site
    /// map can be read and the neighbours can be drawn, and both of those are facts about a real
    /// environment rather than things to reason out. The second one is the one that decides the
    /// design: a table whose icon is not an SVG web resource cannot be drawn from the org at
    /// all, and a navigation full of empty boxes is worse than an invented one.
    /// </summary>
    internal static class SiteMapProbe
    {
        public static int Run(CrmServiceClient client, string root)
        {
            var report = new StringBuilder();
            void Say(string line)
            {
                Console.WriteLine("        " + line);
                report.AppendLine(line);
            }

            Say("== apps in this environment ==");
            var apps = client.RetrieveMultiple(new QueryExpression("appmodule")
            {
                ColumnSet = new ColumnSet("appmoduleid", "name", "uniquename"),
                TopCount = 50
            });

            foreach (var app in apps.Entities)
            {
                Say("  " + app.GetAttributeValue<string>("name")
                    + "  (" + app.GetAttributeValue<string>("uniquename") + ")");
            }

            Say("");
            Say("== site maps ==");
            var maps = client.RetrieveMultiple(new QueryExpression("sitemap")
            {
                ColumnSet = new ColumnSet("sitemapid", "sitemapname", "sitemapnameunique", "sitemapxml"),
                TopCount = 50
            });

            Say("  " + maps.Entities.Count + " site map record(s)");

            // Only the shape matters here, so each map is reported as its areas, groups and the
            // tables under them rather than as raw XML.
            foreach (var map in maps.Entities)
            {
                var name = map.GetAttributeValue<string>("sitemapnameunique")
                           ?? map.GetAttributeValue<string>("sitemapname")
                           ?? map.Id.ToString();

                var xml = map.GetAttributeValue<string>("sitemapxml");
                if (string.IsNullOrWhiteSpace(xml))
                {
                    Say("  " + name + ": no sitemapxml");
                    continue;
                }

                Say("");
                Say("  --- " + name + " (" + xml.Length + " chars) ---");
                try
                {
                    var doc = XDocument.Parse(xml);
                    foreach (var area in doc.Descendants("Area"))
                    {
                        Say("  Area " + Attr(area, "Id") + "  title=" + Title(area));
                        foreach (var group in area.Elements("Group"))
                        {
                            Say("    Group " + Attr(group, "Id") + "  title=" + Title(group));
                            foreach (var sub in group.Elements("SubArea"))
                            {
                                Say("      SubArea " + Attr(sub, "Id")
                                    + "  Entity=" + Attr(sub, "Entity")
                                    + "  Url=" + Attr(sub, "Url")
                                    + "  Icon=" + Attr(sub, "Icon")
                                    + "  title=" + Title(sub));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Say("  ! could not parse: " + e.Message);
                }
            }

            Say("");
            Say("== can the neighbours be drawn? ==");
            var response = (RetrieveAllEntitiesResponse)client.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });

            var all = response.EntityMetadata
                .Where(e => e.DisplayName?.UserLocalizedLabel?.Label != null)
                .ToList();

            var withVector = all.Where(e => !string.IsNullOrEmpty(e.IconVectorName)).ToList();
            Say("  tables with a display name: " + all.Count);
            Say("  of those, with IconVectorName: " + withVector.Count);
            Say("    custom:   " + withVector.Count(e => e.IsCustomEntity == true));
            Say("    standard: " + withVector.Count(e => e.IsCustomEntity != true));
            Say("  standard tables WITHOUT one: " + all.Count(e => e.IsCustomEntity != true && string.IsNullOrEmpty(e.IconVectorName)));

            // The site map's own tables are the ones that would actually be drawn, so they are
            // the count that decides it.
            var inMaps = maps.Entities
                .Select(m => m.GetAttributeValue<string>("sitemapxml"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(x =>
                {
                    try { return XDocument.Parse(x).Descendants("SubArea").Select(s => Attr(s, "Entity")); }
                    catch { return Enumerable.Empty<string>(); }
                })
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Say("");
            Say("  tables named by a site map: " + inMaps.Count);
            var drawable = 0;
            foreach (var logical in inMaps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var meta = all.FirstOrDefault(e =>
                    string.Equals(e.LogicalName, logical, StringComparison.OrdinalIgnoreCase));
                var vector = meta?.IconVectorName;
                if (!string.IsNullOrEmpty(vector)) drawable++;
                Say("    " + logical.PadRight(28) + (string.IsNullOrEmpty(vector) ? "(no vector icon)" : vector));
            }

            Say("");
            Say("  of the site map's tables, drawable from the org: " + drawable + " of " + inMaps.Count);

            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "sitemap-probe.txt");
            File.WriteAllText(path, report.ToString());
            Console.WriteLine("        wrote " + path);
            return 0;
        }

        private static string Attr(XElement element, string name)
        {
            return (string)element.Attribute(name) ?? "";
        }

        /// <summary>The label a site map node shows, which lives in a Titles child, not an attribute.</summary>
        private static string Title(XElement element)
        {
            var titles = element.Element("Titles");
            var title = titles?.Elements("Title").FirstOrDefault();
            return (string)title?.Attribute("Title") ?? Attr(element, "ResourceId");
        }
    }
}
