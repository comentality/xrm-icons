using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Icons.Catalog;

namespace Icons.Logic
{
    /// <summary>How an icon already in the environment was recognised.</summary>
    public enum IconEvidence
    {
        /// <summary>It says so itself: this tool wrote it, and left a provenance comment saying what it is.</summary>
        Provenance,

        /// <summary>It is, shape for shape, one of the bundled drawings.</summary>
        Drawing,

        /// <summary>It still carries the CSS class its family ships it with, and that class names it.</summary>
        Marker
    }

    /// <summary>
    /// What the icon a table already carries turns out to be.
    ///
    /// <see cref="Entry"/> is the bundled icon when there is one, which is what lets the window
    /// go to it in the grid. It can be null on a known icon: a provenance comment names a
    /// drawing that this bundle no longer carries, or a class names an icon in a weight that
    /// cannot be told apart. The name is still worth saying in that case, so the two are
    /// separate.
    /// </summary>
    public sealed class IconIdentity
    {
        public IconIdentity(string described, IconEntry entry, IconEvidence evidence)
        {
            Described = described ?? "";
            Entry = entry;
            Evidence = evidence;
        }

        /// <summary>The icon as the window says it: "Folder (Lucide regular)".</summary>
        public string Described { get; }

        /// <summary>The bundled icon this is, or null when it is known by name but not in hand.</summary>
        public IconEntry Entry { get; }

        /// <summary>What the recognition rested on.</summary>
        public IconEvidence Evidence { get; }
    }

    /// <summary>
    /// Works out which library icon a stored SVG is.
    ///
    /// Three things can say so, and they are asked in order of how much they can be trusted.
    /// The provenance comment is the icon telling you outright, and only this tool writes one.
    /// The drawing itself is next: an exact shape-for-shape match against the bundle recognises
    /// an icon whoever put it there, including one this tool wrote before it carried comments.
    /// The CSS class the family ships is last, because it survives a drawing being edited and
    /// so can outlive the truth - but it is the one thing left when a family has moved an icon
    /// on since this bundle was built.
    ///
    /// Nothing here guesses. An icon that is none of the bundled ones comes back as null, and
    /// the window says so, because "this looks a bit like Folder" is worse than silence.
    /// </summary>
    public static class IconIdentifier
    {
        /// <summary>
        /// The class attributes the families ship: <c>lucide lucide-folder</c> and
        /// <c>icon icon-tabler icon-tabler-folder</c>. The bundle strips them, so they are only
        /// ever seen on a file somebody pasted in from the family's own site.
        /// </summary>
        private static readonly Dictionary<string, IconFamily> Markers =
            new Dictionary<string, IconFamily>(StringComparer.OrdinalIgnoreCase)
            {
                ["lucide-"] = IconFamily.Lucide,
                ["icon-tabler-"] = IconFamily.Tabler
            };

        /// <summary>Class tokens that name the family or the weight rather than the icon.</summary>
        private static readonly HashSet<string> NotNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "lucide", "icon", "tabler", "outline", "filled", "fill", "regular", "bold" };

        private static readonly Regex Space = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// What this stored SVG is, or null when it is not one of the bundled icons - which is
        /// every hand-drawn icon, every icon from a set this tool does not bundle, and anything
        /// that will not parse.
        /// </summary>
        public static IconIdentity Identify(string svg, IconCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(svg) || catalog == null) return null;

            XDocument document;
            try
            {
                document = XDocument.Parse(svg);
            }
            catch (XmlException)
            {
                return null;
            }

            if (document.Root == null) return null;

            return FromProvenance(document, catalog)
                   ?? FromDrawing(document, catalog)
                   ?? FromMarker(document, catalog);
        }

        /// <summary>The comment this tool wrote into it, if it wrote it.</summary>
        private static IconIdentity FromProvenance(XDocument document, IconCatalog catalog)
        {
            var provenance = document.DescendantNodes().OfType<XComment>()
                .Select(c => Provenance.Parse(c.Value))
                .FirstOrDefault(p => p != null);

            if (provenance == null || string.IsNullOrEmpty(provenance.Icon)) return null;

            return new IconIdentity(
                provenance.Describe(),
                catalog.Find(provenance.Family, provenance.Weight, provenance.Icon),
                IconEvidence.Provenance);
        }

        /// <summary>The same shapes, in the same order, in the same coordinate space.</summary>
        private static IconIdentity FromDrawing(XDocument document, IconCatalog catalog)
        {
            var entry = catalog.Drawn(IconFingerprint.Of(document));
            return entry == null ? null : new IconIdentity(entry.ToString(), entry, IconEvidence.Drawing);
        }

        /// <summary>
        /// The class its family ships it under. The token is matched against the family's own
        /// names rather than parsed, because a family is free to change the shape of its class
        /// and a name that is really in the bundle is evidence either way.
        /// </summary>
        private static IconIdentity FromMarker(XDocument document, IconCatalog catalog)
        {
            foreach (var element in document.Root.DescendantsAndSelf())
            {
                var classes = element.Attribute("class")?.Value;
                if (string.IsNullOrWhiteSpace(classes)) continue;

                var tokens = Space.Split(classes.Trim()).Where(t => t.Length > 0).ToList();

                foreach (var marker in Markers)
                {
                    var family = marker.Value;

                    var names = tokens
                        .Where(t => t.StartsWith(marker.Key, StringComparison.OrdinalIgnoreCase))
                        .Select(t => t.Substring(marker.Key.Length))
                        .Where(n => n.Length > 0 && !NotNames.Contains(n));

                    foreach (var name in names)
                    {
                        var found = catalog.FindByName(family, name);
                        if (found.Count == 0) continue;

                        // One weight, so the class has named an icon outright. Several, and it
                        // has named a drawing without saying which of them - worth the name,
                        // not worth a guess at the weight.
                        if (found.Count == 1)
                            return new IconIdentity(found[0].ToString(), found[0], IconEvidence.Marker);

                        var described = found[0].DisplayName + " (" + family + ")";
                        return new IconIdentity(described, null, IconEvidence.Marker);
                    }
                }
            }

            return null;
        }
    }
}
