using System;
using System.Collections.Generic;
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
    /// Asks the org what an app's navigation is made of, and writes it out so the mock can draw
    /// the real thing instead of an invented one.
    ///
    /// Reads only. Three things it established that were not guessable:
    ///
    /// A modern app's site map does not come back from a plain query on <c>sitemap</c> - that
    /// returns the legacy Settings map and nothing else. It has to be reached through the app's
    /// own component list and retrieved by id.
    ///
    /// A SubArea can name its own icon, and in the Settings app every one of them does. So a
    /// navigation entry's icon is not always the table's icon, and reading only IconVectorName
    /// would draw the wrong thing.
    ///
    /// And the tables a real app puts in its navigation mostly do have a drawable icon. The
    /// first pass said none of them did, but it was counting the legacy Settings map, which is
    /// not a navigation anybody sees. Counted against a real app it is nearly all of them: the
    /// exceptions are the core standard tables like contact.
    /// </summary>
    internal static class SiteMapProbe
    {
        /// <summary>appmodulecomponent.componenttype for a site map.</summary>
        private const int SiteMapComponent = 62;

        public static int Run(CrmServiceClient client, string root)
        {
            var report = new StringBuilder();
            void Say(string line)
            {
                Console.WriteLine("        " + line);
                report.AppendLine(line);
            }

            // Metadata first: the labels and icons of the tables a site map will name.
            var response = (RetrieveAllEntitiesResponse)client.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });

            var byLogical = new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in response.EntityMetadata)
            {
                if (e.LogicalName != null) byLogical[e.LogicalName] = e;
            }

            var named = response.EntityMetadata.Where(e => e.DisplayName?.UserLocalizedLabel?.Label != null).ToList();
            Say("== the environment's icons ==");
            Say("  tables with a display name: " + named.Count);
            Say("  with IconVectorName: " + named.Count(e => !string.IsNullOrEmpty(e.IconVectorName))
                + "  (custom " + named.Count(e => e.IsCustomEntity == true && !string.IsNullOrEmpty(e.IconVectorName))
                + ", standard " + named.Count(e => e.IsCustomEntity != true && !string.IsNullOrEmpty(e.IconVectorName)) + ")");

            // ----- the apps, by the unique id their components refer to them by -----

            var apps = client.RetrieveMultiple(new QueryExpression("appmodule")
            {
                ColumnSet = new ColumnSet("appmoduleid", "appmoduleidunique", "name", "uniquename"),
                TopCount = 100
            });

            var nameOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var app in apps.Entities)
            {
                var unique = Raw(app, "appmoduleidunique");
                if (unique.Length > 0) nameOf[unique] = app.GetAttributeValue<string>("name") ?? "(unnamed)";
            }

            var links = client.RetrieveMultiple(new QueryExpression("appmodulecomponent")
            {
                ColumnSet = new ColumnSet("appmoduleidunique", "objectid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("componenttype", ConditionOperator.Equal, SiteMapComponent) }
                },
                TopCount = 200
            });

            Say("");
            Say("== apps and their navigations ==");

            var navRoot = Path.Combine(root, "nav");
            Directory.CreateDirectory(navRoot);

            foreach (var link in links.Entities)
            {
                var appUnique = Raw(link, "appmoduleidunique");
                var appName = nameOf.ContainsKey(appUnique) ? nameOf[appUnique] : "app-" + appUnique;

                Guid mapId;
                if (!Guid.TryParse(Raw(link, "objectid"), out mapId)) continue;

                string xml;
                try
                {
                    xml = client.Retrieve("sitemap", mapId, new ColumnSet("sitemapxml"))
                        .GetAttributeValue<string>("sitemapxml");
                }
                catch (Exception e)
                {
                    Say("  " + appName + ": could not retrieve its site map - " + e.Message);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(xml))
                {
                    Say("  " + appName + ": no sitemapxml");
                    continue;
                }

                var rows = Read(xml, byLogical);
                var tables = rows.Count(r => r.Kind == "table");
                var drawable = rows.Count(r => r.Kind == "table" && r.Icon.Length > 0);
                Say("  " + appName + ": " + rows.Count + " row(s), " + tables + " table(s), "
                    + drawable + " with an icon this tool could fetch");

                Dump(client, navRoot, appName, rows, Say);
            }

            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "sitemap-probe.txt");
            File.WriteAllText(path, report.ToString());
            Console.WriteLine("        wrote " + path);
            return 0;
        }

        /// <summary>One navigation row as the mock needs it.</summary>
        private sealed class Row
        {
            public string Kind = "table";   // area, group or table
            public string Label = "";
            public string Icon = "";        // the web resource that draws it, if any
        }

        /// <summary>
        /// The site map as a flat list of headings and tables, in the order it puts them.
        ///
        /// A SubArea's own Icon wins over the table's, because that is the one the app shows.
        /// A SubArea with no Entity is a page rather than a table and keeps whatever label it
        /// has; a SubArea with no title of its own borrows the table's display name, which is
        /// what the app does too.
        /// </summary>
        private static List<Row> Read(string xml, Dictionary<string, EntityMetadata> byLogical)
        {
            var rows = new List<Row>();

            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch { return rows; }

            foreach (var area in doc.Descendants("Area"))
            {
                foreach (var group in area.Elements("Group"))
                {
                    var heading = Title(group);
                    if (heading.Length > 0) rows.Add(new Row { Kind = "group", Label = heading });

                    foreach (var sub in group.Elements("SubArea"))
                    {
                        var entity = Attr(sub, "Entity");
                        var label = Title(sub);
                        var icon = Attr(sub, "Icon");

                        EntityMetadata meta = null;
                        if (entity.Length > 0) byLogical.TryGetValue(entity, out meta);

                        if (label.Length == 0)
                            label = meta?.DisplayName?.UserLocalizedLabel?.Label ?? entity;

                        if (icon.Length == 0) icon = meta?.IconVectorName ?? "";

                        // Only an SVG web resource can be fetched and drawn. A /_imgs/ path is
                        // a bitmap the platform serves and not something to pretend about.
                        if (!icon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) icon = "";
                        icon = icon.TrimStart('/');
                        if (icon.StartsWith("WebResources/", StringComparison.OrdinalIgnoreCase))
                            icon = icon.Substring("WebResources/".Length);

                        rows.Add(new Row
                        {
                            Kind = entity.Length > 0 ? "table" : "page",
                            Label = label,
                            Icon = icon
                        });
                    }
                }
            }

            return rows;
        }

        /// <summary>
        /// Writes the navigation and every icon in it, so the mock can draw the real thing with
        /// no connection of its own.
        /// </summary>
        private static void Dump(CrmServiceClient client, string navRoot, string appName,
            List<Row> rows, Action<string> say)
        {
            var folder = Path.Combine(navRoot, Safe(appName));
            Directory.CreateDirectory(folder);

            var wanted = rows.Select(r => r.Icon).Where(i => i.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var got = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in wanted)
            {
                var found = client.RetrieveMultiple(new QueryExpression("webresource")
                {
                    ColumnSet = new ColumnSet("name", "content"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.Equal, name) }
                    },
                    TopCount = 1
                });

                var record = found.Entities.FirstOrDefault();
                var content = record?.GetAttributeValue<string>("content");
                if (string.IsNullOrEmpty(content)) continue;

                try
                {
                    var file = Safe(name) + ".svg";
                    File.WriteAllBytes(Path.Combine(folder, file), Convert.FromBase64String(content));
                    got[name] = file;
                }
                catch (FormatException)
                {
                    // Not base64: nothing to draw, and nothing to say about it beyond that.
                }
            }

            using (var writer = new StreamWriter(Path.Combine(folder, "nav.tsv"), false, Encoding.UTF8))
            {
                writer.WriteLine("kind\tlabel\tfile");
                foreach (var row in rows)
                {
                    string file;
                    got.TryGetValue(row.Icon ?? "", out file);
                    writer.WriteLine(row.Kind + "\t" + row.Label + "\t" + (file ?? ""));
                }
            }

            say("    -> " + folder + " (" + got.Count + " of " + wanted.Count + " icons fetched)");
        }

        private static string Safe(string name)
        {
            var builder = new StringBuilder();
            foreach (var c in name) builder.Append(char.IsLetterOrDigit(c) ? c : '-');
            return builder.ToString();
        }

        /// <summary>
        /// One attribute as text, whatever the platform typed it as. These columns are not all
        /// what they look like - appmoduleidunique is not returned as a Guid - and the probe is
        /// here to find that out rather than to assert it.
        /// </summary>
        private static string Raw(Entity entity, string name)
        {
            object value;
            if (!entity.Attributes.TryGetValue(name, out value) || value == null) return "";

            var reference = value as EntityReference;
            if (reference != null) return reference.Id.ToString();

            var option = value as OptionSetValue;
            if (option != null) return option.Value.ToString();

            return Convert.ToString(value);
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
            var text = (string)title?.Attribute("Title") ?? "";

            // A ResourceId is the platform's own string table, which this cannot resolve. Better
            // an empty label the caller fills from the table than a Homepage_SyncError on screen.
            return text;
        }
    }
}
