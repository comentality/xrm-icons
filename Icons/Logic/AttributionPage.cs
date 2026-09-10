using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Icons.Logic
{
    /// <summary>One icon this tool has stored in an environment, and where it came from.</summary>
    public sealed class StoredIcon
    {
        public StoredIcon(string webResourceName, IconProvenance provenance)
        {
            WebResourceName = webResourceName ?? throw new ArgumentNullException(nameof(webResourceName));
            Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        }

        /// <summary>What it is stored under, which is what the table points at.</summary>
        public string WebResourceName { get; }

        public IconProvenance Provenance { get; }
    }

    /// <summary>
    /// Builds the attribution page that lives beside the icons.
    ///
    /// Every icon this tool writes carries its own provenance, and this turns a folder of them
    /// back into one page: who drew each icon, under what licence, and what was done to it.
    /// For the four families bundled today it is a courtesy - their licences ask only that the
    /// notice travel with the software, which <c>ABOUT.txt</c> in the bundle is. For a set
    /// licensed CC BY it is the obligation itself, and the licence says attribution may be
    /// given "in any reasonable manner based on the medium", naming a link to a resource
    /// holding the information as its own example. A web resource has a URI, so this is that
    /// link rather than a file nobody can reach.
    ///
    /// It is rebuilt from the environment on every apply rather than added to, so an icon
    /// somebody replaced by hand stops being credited the moment it stops being there.
    /// </summary>
    public static class AttributionPage
    {
        /// <summary>The file name the page is always stored under, in the icons' own folder.</summary>
        public const string FileName = "ATTRIBUTION.html";

        /// <summary>webresourcetype 1, "Webpage (HTML)".</summary>
        public const int WebResourceTypeHtml = 1;

        /// <summary>
        /// Where the page belongs for an icon stored under this name: beside it, in the same
        /// folder. A second folder of icons gets its own page rather than one of them getting a
        /// page that talks about the other.
        /// </summary>
        public static string NameFor(string iconWebResourceName)
        {
            if (string.IsNullOrWhiteSpace(iconWebResourceName))
                throw new ArgumentException("An icon name is needed to say where the page goes.", nameof(iconWebResourceName));

            return FolderOf(iconWebResourceName) + FileName;
        }

        /// <summary>
        /// What the page beside this icon is the page for: its folder, with the trailing slash.
        ///
        /// A name with no folder in it - <c>new_account_icon.svg</c>, which is a name somebody
        /// typed rather than one this tool would build - falls back to the publisher prefix.
        /// The page has to be stored under a prefix itself, so a bare <c>ATTRIBUTION.html</c>
        /// is not a name Dataverse would take; and scoping to the prefix keeps the page about
        /// one publisher's icons rather than every web resource in the environment.
        /// </summary>
        public static string FolderOf(string webResourceName)
        {
            if (string.IsNullOrEmpty(webResourceName)) return "";

            var slash = webResourceName.LastIndexOf('/');
            if (slash >= 0) return webResourceName.Substring(0, slash + 1);

            var underscore = webResourceName.IndexOf('_');
            return underscore >= 0 ? webResourceName.Substring(0, underscore + 1) : "";
        }

        /// <summary>
        /// The page, as HTML. Sets in alphabetical order, and each set's icons in the order
        /// they are stored, so that two runs over the same environment produce the same page
        /// and a reader can find a name.
        /// </summary>
        public static string Build(IEnumerable<StoredIcon> icons, DateTime written)
        {
            if (icons == null) throw new ArgumentNullException(nameof(icons));

            var listed = icons
                .Where(i => i != null)
                .OrderBy(i => i.WebResourceName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var page = new StringBuilder();
            page.AppendLine("<!DOCTYPE html>");
            page.AppendLine("<html lang=\"en\">");
            page.AppendLine("<head>");
            page.AppendLine("<meta charset=\"utf-8\" />");
            page.AppendLine("<title>Icon attribution</title>");
            page.AppendLine("<style>");
            page.AppendLine("body{font-family:Segoe UI,system-ui,sans-serif;color:#242424;margin:2rem auto;max-width:52rem;padding:0 1rem;line-height:1.5}");
            page.AppendLine("h1{font-size:1.5rem}h2{font-size:1.15rem;margin-top:2rem}");
            page.AppendLine("table{border-collapse:collapse;width:100%;margin-top:.5rem}");
            page.AppendLine("th,td{border-bottom:1px solid #e0e0e0;padding:.4rem .6rem;text-align:left;vertical-align:top;font-size:.9rem}");
            page.AppendLine("th{font-weight:600;white-space:nowrap}");
            page.AppendLine("code{font-family:Consolas,monospace;font-size:.85rem}");
            page.AppendLine("p.note{color:#616161;font-size:.9rem}");
            page.AppendLine("</style>");
            page.AppendLine("</head>");
            page.AppendLine("<body>");
            page.AppendLine("<h1>Icon attribution</h1>");

            if (listed.Count == 0)
            {
                page.AppendLine("<p>No icons written by this tool are stored here.</p>");
            }
            else
            {
                page.AppendLine("<p>The table icons stored in this environment, who drew them, and under what "
                                + "licence. Each icon carries this information inside its own file; this page is "
                                + "rebuilt from those files, so it says what is actually stored here rather than "
                                + "what was stored here once.</p>");

                foreach (var set in Group(listed))
                {
                    page.Append("<h2>").Append(Escape(set.Key.Set)).AppendLine("</h2>");
                    page.Append("<p>").Append(Credit(set.Key)).AppendLine("</p>");

                    page.AppendLine("<table>");
                    page.AppendLine("<tr><th>Stored as</th><th>Icon</th><th>Changes made</th></tr>");
                    foreach (var icon in set)
                    {
                        page.Append("<tr><td><code>").Append(Escape(icon.WebResourceName)).Append("</code></td>");
                        page.Append("<td>").Append(Escape(icon.Provenance.Describe())).Append("</td>");
                        page.Append("<td>").Append(Escape(Changes(icon.Provenance))).AppendLine("</td></tr>");
                    }
                    page.AppendLine("</table>");
                }
            }

            page.Append("<p class=\"note\">Written by <a href=\"https://github.com/comentality/xrm-icons\">Icons</a> ")
                .Append("on ").Append(written.ToString("d MMMM yyyy", CultureInfo.InvariantCulture))
                .AppendLine(". It is rewritten every time an icon is applied.</p>");

            page.AppendLine("</body>");
            page.AppendLine("</html>");

            return page.ToString();
        }

        /// <summary>
        /// Every icon in this environment that says where it came from, read out of the
        /// web resources themselves. One that says nothing is left out rather than guessed at.
        /// </summary>
        public static IReadOnlyList<StoredIcon> Read(IEnumerable<KeyValuePair<string, string>> namedSvgs)
        {
            if (namedSvgs == null) throw new ArgumentNullException(nameof(namedSvgs));

            var found = new List<StoredIcon>();
            foreach (var pair in namedSvgs)
            {
                var provenance = Provenance.Find(pair.Value);
                if (provenance != null) found.Add(new StoredIcon(pair.Key, provenance));
            }

            return found;
        }

        /// <summary>
        /// Icons grouped by the set that drew them. Grouped on the credit rather than the
        /// family name, so two icons whose files disagree about the licence - one written by an
        /// older version of the tool - are credited separately instead of one of them silently
        /// speaking for the other.
        /// </summary>
        private static IEnumerable<IGrouping<Credited, StoredIcon>> Group(IEnumerable<StoredIcon> icons)
        {
            return icons
                .GroupBy(i => new Credited(i.Provenance))
                .OrderBy(g => g.Key.Set, StringComparer.OrdinalIgnoreCase);
        }

        private static string Credit(Credited set)
        {
            var line = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(set.Holder))
                line.Append("Copyright &copy; ").Append(Escape(set.Holder)).Append(". ");

            if (!string.IsNullOrWhiteSpace(set.Licence))
            {
                line.Append("Licensed under ");
                line.Append(string.IsNullOrWhiteSpace(set.LicenceUrl)
                    ? Escape(set.Licence)
                    : Link(set.LicenceUrl, set.Licence));
                line.Append(". ");
            }

            if (!string.IsNullOrWhiteSpace(set.Source))
                line.Append("The original icons are at ").Append(Link(set.Source, set.Source)).Append('.');

            return line.ToString().TrimEnd();
        }

        private static string Changes(IconProvenance provenance)
        {
            return provenance.Changes.Count == 0
                ? "None."
                : char.ToUpperInvariant(provenance.Changes[0][0]) + provenance.Changes[0].Substring(1) +
                  string.Concat(provenance.Changes.Skip(1).Select(c => "; " + c)) + ".";
        }

        private static string Link(string url, string text)
        {
            return "<a href=\"" + Escape(url) + "\">" + Escape(text) + "</a>";
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        /// <summary>The part of a provenance that is about the set rather than the icon.</summary>
        private sealed class Credited : IEquatable<Credited>
        {
            public Credited(IconProvenance provenance)
            {
                Set = string.IsNullOrWhiteSpace(provenance.Set) ? provenance.Family : provenance.Set;
                Holder = provenance.Holder;
                Licence = provenance.Licence;
                LicenceUrl = provenance.LicenceUrl;
                Source = provenance.Source;
            }

            public string Set { get; }
            public string Holder { get; }
            public string Licence { get; }
            public string LicenceUrl { get; }
            public string Source { get; }

            public bool Equals(Credited other)
            {
                return other != null
                       && Set == other.Set && Holder == other.Holder && Licence == other.Licence
                       && LicenceUrl == other.LicenceUrl && Source == other.Source;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Credited);
            }

            public override int GetHashCode()
            {
                return (Set ?? "").GetHashCode() ^ (Holder ?? "").GetHashCode() ^ (Licence ?? "").GetHashCode();
            }
        }
    }
}
