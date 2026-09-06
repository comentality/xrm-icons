using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Icons.Logic
{
    /// <summary>What a navigation row is.</summary>
    public enum SiteMapEntryKind
    {
        /// <summary>A heading over the rows under it. No icon, and nothing to select.</summary>
        Group,

        /// <summary>A table. The rows this tool is about.</summary>
        Table,

        /// <summary>A page or a link. It has an icon and a label, but no table behind it.</summary>
        Page
    }

    /// <summary>One row of an app's navigation.</summary>
    public sealed class SiteMapEntry
    {
        public SiteMapEntry(SiteMapEntryKind kind, string label, string iconWebResource, string tableLogicalName)
        {
            Kind = kind;
            Label = label ?? "";
            IconWebResource = iconWebResource ?? "";
            TableLogicalName = tableLogicalName ?? "";
        }

        public SiteMapEntryKind Kind { get; }

        /// <summary>What the row is called, already fallen back to the table's own name.</summary>
        public string Label { get; }

        /// <summary>
        /// The web resource that draws it, or empty when nothing can. Empty is the honest answer
        /// for a standard table: those keep no icon in Dataverse at all.
        /// </summary>
        public string IconWebResource { get; }

        /// <summary>The table behind the row, or empty when it is a page.</summary>
        public string TableLogicalName { get; }

        public override string ToString()
        {
            return Kind + " " + Label;
        }
    }

    /// <summary>What this reader needs to know about a table the navigation names.</summary>
    public sealed class SiteMapTable
    {
        public string DisplayName;
        public string VectorIcon;
    }

    /// <summary>
    /// An app's navigation, out of the site map XML the app keeps it in.
    ///
    /// Two things about this were not guessable and are worth stating where they are relied on.
    ///
    /// A row's icon is not always the table's icon. A SubArea can name one of its own, and where
    /// it does that is the one the app shows - the Settings app names one on every row. So the
    /// SubArea is asked first and the table only after.
    ///
    /// And a row's label is often not in the site map at all. A SubArea with no Titles borrows
    /// the table's display name, which is what the app does. What it must never do is show the
    /// ResourceId - Homepage_SyncError is the platform's own string table key, and this cannot
    /// resolve it.
    /// </summary>
    public static class SiteMap
    {
        /// <summary>appmodulecomponent.componenttype for a site map.</summary>
        public const int ComponentType = 62;

        /// <summary>
        /// The navigation as a flat list of headings and rows, in the order the app puts them.
        /// A malformed site map is an empty navigation rather than an exception: a preview that
        /// cannot be drawn is a preview that is not shown, not a tool that stops.
        /// </summary>
        public static IReadOnlyList<SiteMapEntry> Parse(string xml, IDictionary<string, SiteMapTable> tables)
        {
            var entries = new List<SiteMapEntry>();
            if (string.IsNullOrWhiteSpace(xml)) return entries;

            XDocument document;
            try
            {
                document = XDocument.Parse(xml);
            }
            catch (System.Xml.XmlException)
            {
                return entries;
            }

            foreach (var area in document.Descendants("Area"))
            {
                foreach (var group in area.Elements("Group"))
                {
                    var heading = Title(group);

                    // Held back until a row under it turns up. A group whose rows are all
                    // hidden is not a heading over nothing, it is nothing.
                    var pending = heading.Length > 0 ? heading : null;

                    foreach (var sub in group.Elements("SubArea"))
                    {
                        var entry = ReadSubArea(sub, tables);
                        if (entry == null) continue;

                        if (pending != null)
                        {
                            entries.Add(new SiteMapEntry(SiteMapEntryKind.Group, pending, null, null));
                            pending = null;
                        }

                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private static SiteMapEntry ReadSubArea(XElement sub, IDictionary<string, SiteMapTable> tables)
        {
            var logical = Attribute(sub, "Entity");

            SiteMapTable table = null;
            if (logical.Length > 0 && tables != null) tables.TryGetValue(logical, out table);

            var label = Title(sub);
            if (label.Length == 0) label = table?.DisplayName ?? logical;

            // A row nothing can be said about is not drawn. A blank line in a preview of a
            // navigation is a claim that the app has a blank line in its navigation.
            if (label.Length == 0) return null;

            var icon = WebResource(Attribute(sub, "Icon"));
            if (icon.Length == 0) icon = WebResource(table?.VectorIcon);

            return new SiteMapEntry(
                logical.Length > 0 ? SiteMapEntryKind.Table : SiteMapEntryKind.Page,
                label, icon, logical);
        }

        /// <summary>
        /// A web resource name out of whatever the site map wrote, or empty.
        ///
        /// Only an SVG is kept. The rest of what turns up here - /_imgs/ico_18_audit.gif, a
        /// 32-pixel PNG - is a bitmap the platform serves from its own files, and this tool
        /// cannot fetch it or draw it. Empty says so; a name that cannot be fetched would not.
        /// </summary>
        public static string WebResource(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            var name = raw.Trim();
            if (!name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return "";

            name = name.TrimStart('/');
            if (name.StartsWith("WebResources/", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("WebResources/".Length);

            // A cache-busting version folder sits between the two on some paths:
            // /%7B637...%7D/WebResources/name.svg
            var marker = name.IndexOf("WebResources/", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0) name = name.Substring(marker + "WebResources/".Length);

            return name;
        }

        private static string Attribute(XElement element, string name)
        {
            return ((string)element.Attribute(name) ?? "").Trim();
        }

        /// <summary>
        /// The label a node shows. It lives in a Titles child rather than an attribute, and a
        /// node that has none has none: the ResourceId beside it is a key into the platform's
        /// string table, and showing that reads as a bug rather than as a name.
        /// </summary>
        private static string Title(XElement element)
        {
            var titles = element.Element("Titles");
            if (titles == null) return "";

            // The list is per language. Nothing here knows which one the user reads, so the
            // first is taken - which for a single-language site map is the only one.
            var title = titles.Elements("Title").FirstOrDefault();
            return ((string)title?.Attribute("Title") ?? "").Trim();
        }
    }
}
