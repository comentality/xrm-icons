using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Icons.Logic
{
    /// <summary>An app, and the site map it draws its navigation from.</summary>
    public sealed class AppNavigation
    {
        public string Name;
        public Guid SiteMapId;

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Where an app's navigation is kept, and how to get it out.
    ///
    /// The path is not the obvious one. A plain query on <c>sitemap</c> returns the legacy
    /// Settings map and nothing else - not one modern app's - so an app's own map is reached
    /// through its component list and retrieved by id. That was measured against a real
    /// environment rather than reasoned out, and it is the whole reason this class exists
    /// instead of a single RetrieveMultiple at the call site.
    /// </summary>
    public static class SiteMapSource
    {
        /// <summary>
        /// Every app that has a navigation, with the id of the map holding it. Two queries, no
        /// matter how many apps: the site maps themselves are not read here, because a preview
        /// only ever shows one.
        /// </summary>
        public static IReadOnlyList<AppNavigation> Apps(IOrganizationService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var apps = service.RetrieveMultiple(new QueryExpression("appmodule")
            {
                ColumnSet = new ColumnSet("appmoduleidunique", "name"),
                TopCount = 200
            });

            var nameOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var app in apps.Entities)
            {
                var unique = Text(app, "appmoduleidunique");
                if (unique.Length == 0) continue;

                nameOf[unique] = app.GetAttributeValue<string>("name") ?? "(unnamed app)";
            }

            var links = service.RetrieveMultiple(new QueryExpression("appmodulecomponent")
            {
                ColumnSet = new ColumnSet("appmoduleidunique", "objectid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("componenttype", ConditionOperator.Equal, SiteMap.ComponentType)
                    }
                },
                TopCount = 500
            });

            var found = new List<AppNavigation>();
            foreach (var link in links.Entities)
            {
                Guid mapId;
                if (!Guid.TryParse(Text(link, "objectid"), out mapId)) continue;

                string name;
                if (!nameOf.TryGetValue(Text(link, "appmoduleidunique"), out name)) continue;

                found.Add(new AppNavigation { Name = name, SiteMapId = mapId });
            }

            return found
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>One app's navigation, as rows in the order it puts them.</summary>
        public static IReadOnlyList<SiteMapEntry> Navigation(
            IOrganizationService service, Guid siteMapId, IDictionary<string, SiteMapTable> tables)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var map = service.Retrieve("sitemap", siteMapId, new ColumnSet("sitemapxml"));
            return SiteMap.Parse(map.GetAttributeValue<string>("sitemapxml"), tables);
        }

        /// <summary>
        /// The SVG behind each of those web resource names, by name. Asked for in one query
        /// rather than one each, and a name that comes back with nothing usable is simply
        /// absent from the answer - the caller draws what it has.
        /// </summary>
        public static Dictionary<string, string> Icons(IOrganizationService service, IEnumerable<string> names)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var wanted = (names ?? Enumerable.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (wanted.Length == 0) return found;

            var records = service.RetrieveMultiple(new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("name", "content"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.In, wanted.Cast<object>().ToArray()) }
                },
                TopCount = 500
            });

            foreach (var record in records.Entities)
            {
                var name = record.GetAttributeValue<string>("name");
                var content = record.GetAttributeValue<string>("content");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(content)) continue;

                try
                {
                    found[name] = Encoding.UTF8.GetString(Convert.FromBase64String(content));
                }
                catch (FormatException)
                {
                    // Not base64, so not an SVG this can draw. Nothing to say about it that the
                    // caller could act on, and an icon missing from a preview is not an error.
                }
            }

            return found;
        }

        /// <summary>
        /// One column as text, whatever the platform typed it as. appmoduleidunique comes back
        /// as a string rather than the Guid its name promises, which is worth not asserting.
        /// </summary>
        private static string Text(Entity entity, string column)
        {
            object value;
            if (!entity.Attributes.TryGetValue(column, out value) || value == null) return "";

            var reference = value as EntityReference;
            if (reference != null) return reference.Id.ToString();

            return Convert.ToString(value);
        }
    }
}
