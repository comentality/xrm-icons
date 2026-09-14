using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Icons.Catalog;

namespace Icons.Logic
{
    /// <summary>
    /// Where one stored icon came from: which set drew it, who holds the copyright, under what
    /// licence, and what was done to the drawing on the way in.
    ///
    /// This travels inside the SVG, as a comment, rather than in this tool's memory. The
    /// attribution page has to be rebuildable from what the environment holds - by a later
    /// version, by a different machine, by somebody who never ran the session that wrote the
    /// icon - and the only thing all three can read is the file itself.
    /// </summary>
    public sealed class IconProvenance
    {
        /// <summary>The family's own word for itself: Fluent, Phosphor, Lucide, Tabler, FontAwesome, Solar.</summary>
        public string Family { get; set; }

        /// <summary>The family's own word for the weight: regular, filled, bold, fill, outline.</summary>
        public string Weight { get; set; }

        /// <summary>The icon's name in its family: <c>folder</c>, <c>arrow_trending_lines</c>.</summary>
        public string Icon { get; set; }

        /// <summary>The set's full name, as its authors write it.</summary>
        public string Set { get; set; }

        /// <summary>Whose copyright the notice names.</summary>
        public string Holder { get; set; }

        /// <summary>The licence, by its SPDX identifier.</summary>
        public string Licence { get; set; }

        /// <summary>Where the licence text is.</summary>
        public string LicenceUrl { get; set; }

        /// <summary>Where the original drawing is.</summary>
        public string Source { get; set; }

        /// <summary>
        /// What was done to the drawing, in the order it happened: what the bundle changed
        /// first, then what storing it changed. CC BY asks for exactly this list, and the other
        /// licences do not mind it being there.
        /// </summary>
        public IList<string> Changes { get; } = new List<string>();

        /// <summary>The icon as the window names it: "Folder (Lucide regular)".</summary>
        public string Describe()
        {
            var name = string.IsNullOrEmpty(Icon) ? "" : Icon.Replace('_', ' ').Replace('-', ' ');
            if (name.Length > 0) name = char.ToUpperInvariant(name[0]) + name.Substring(1);

            var origin = string.Join(" ", new[] { Family, Weight }.Where(s => !string.IsNullOrEmpty(s)));
            return origin.Length > 0 ? name + " (" + origin + ")" : name;
        }

        public IconProvenance Copy()
        {
            var copy = new IconProvenance
            {
                Family = Family,
                Weight = Weight,
                Icon = Icon,
                Set = Set,
                Holder = Holder,
                Licence = Licence,
                LicenceUrl = LicenceUrl,
                Source = Source
            };
            foreach (var change in Changes) copy.Changes.Add(change);
            return copy;
        }

        /// <summary>What is known about an icon before anything has been done to it.</summary>
        public static IconProvenance For(IconEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var licence = IconLicence.For(entry.Family);
            var provenance = new IconProvenance
            {
                Family = entry.Family.ToString(),
                Weight = entry.Weight,
                Icon = entry.Name,
                Set = licence.SetName,
                Holder = licence.Holder,
                Licence = licence.Licence,
                LicenceUrl = licence.LicenceUrl,
                Source = licence.SourceUrl
            };

            if (!string.IsNullOrEmpty(licence.BundleChange)) provenance.Changes.Add(licence.BundleChange);
            return provenance;
        }
    }

    /// <summary>
    /// Reads and writes the provenance comment inside a stored SVG.
    ///
    /// One line, one comment, keys and quoted values, so that a person opening the web resource
    /// can read it and a later version of this tool can parse it. The marker carries a version
    /// because the day a field is added, the pages already in customers' environments still
    /// have to be readable by whatever writes the next one.
    /// </summary>
    public static class Provenance
    {
        /// <summary>What every provenance comment starts with. Version 1 is the fields below.</summary>
        public const string Marker = "xrm-icons/1";

        private static readonly Regex Pair = new Regex("([a-z-]+)=\"([^\"]*)\"", RegexOptions.Compiled);

        /// <summary>The text of the comment, without the angle brackets.</summary>
        public static string Comment(IconProvenance provenance)
        {
            if (provenance == null) throw new ArgumentNullException(nameof(provenance));

            var builder = new StringBuilder(Marker);
            Append(builder, "family", provenance.Family);
            Append(builder, "weight", provenance.Weight);
            Append(builder, "icon", provenance.Icon);
            Append(builder, "set", provenance.Set);
            Append(builder, "holder", provenance.Holder);
            Append(builder, "licence", provenance.Licence);
            Append(builder, "licence-url", provenance.LicenceUrl);
            Append(builder, "source", provenance.Source);
            Append(builder, "changes", string.Join("; ", provenance.Changes.Where(c => !string.IsNullOrWhiteSpace(c))));

            return builder.Append(' ').ToString();
        }

        /// <summary>
        /// The provenance in this SVG, or null if it carries none - which is what an icon
        /// somebody put there by hand, or a version of this tool older than the page, looks
        /// like. Neither is an error: they simply do not appear on the page.
        /// </summary>
        public static IconProvenance Find(string svg)
        {
            if (string.IsNullOrWhiteSpace(svg)) return null;

            XDocument document;
            try
            {
                document = XDocument.Parse(svg);
            }
            catch (XmlException)
            {
                return null;
            }

            foreach (var comment in document.DescendantNodes().OfType<XComment>())
            {
                var found = Parse(comment.Value);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>The comment text as a record, or null if it is somebody else's comment.</summary>
        public static IconProvenance Parse(string comment)
        {
            if (comment == null) return null;

            var text = comment.Trim();
            if (!text.StartsWith(Marker, StringComparison.Ordinal)) return null;

            var provenance = new IconProvenance();
            foreach (Match match in Pair.Matches(text))
            {
                var value = match.Groups[2].Value;
                switch (match.Groups[1].Value)
                {
                    case "family": provenance.Family = value; break;
                    case "weight": provenance.Weight = value; break;
                    case "icon": provenance.Icon = value; break;
                    case "set": provenance.Set = value; break;
                    case "holder": provenance.Holder = value; break;
                    case "licence": provenance.Licence = value; break;
                    case "licence-url": provenance.LicenceUrl = value; break;
                    case "source": provenance.Source = value; break;
                    case "changes":
                        foreach (var change in value.Split(';'))
                            if (change.Trim().Length > 0) provenance.Changes.Add(change.Trim());
                        break;
                }
            }

            return provenance;
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            builder.Append(' ').Append(key).Append("=\"").Append(Escape(value)).Append('"');
        }

        /// <summary>
        /// A value an XML comment can actually hold. A comment may not contain a double hyphen
        /// or end in one, and the quote is what separates one value from the next here, so all
        /// three are spent characters. Nothing in a licence notice needs them, and turning an
        /// SVG into one that will not parse to keep a run of hyphens is a bad trade.
        /// </summary>
        private static string Escape(string value)
        {
            var flattened = Regex.Replace(value, @"\s+", " ").Trim().Replace('"', '\'');
            return Regex.Replace(flattened, "-{2,}", "-");
        }

        /// <summary>What storing it did to the drawing, in the words the page says it in.</summary>
        public static string ColourChange(bool stroked, string ink, int followers = 0, int otherFollowers = 0)
        {
            var attribute = stroked ? "stroke" : "fill";
            var other = stroked ? "fill" : "stroke";
            var change = "the " + attribute + " was set to " + ink;

            // Said separately because it happened separately, and because a page that claims
            // one colour was set on an icon drawn in two would be worth less than no page.
            //
            // "and so was" covers only what had the same attribute written. An icon drawn in
            // both - Solar strokes a group and fills a detail inside it - gets the other named
            // for what it is rather than counted in with the first, and "element" rather than
            // "shape" because the thing carrying a Solar stroke is a group, not a drawing.
            if (followers == 1) change += ", and so was the one element inside it that followed the theme";
            else if (followers > 1)
                change += ", and so were the "
                          + followers.ToString(CultureInfo.InvariantCulture)
                          + " elements inside it that followed the theme";

            if (otherFollowers == 1) change += ", along with the " + other + " of one element that followed the theme";
            else if (otherFollowers > 1)
                change += ", along with the " + other + " of "
                          + otherFollowers.ToString(CultureInfo.InvariantCulture)
                          + " elements that followed the theme";

            return change;
        }

        /// <summary>What declaring a size did, in the same words.</summary>
        public static string SizeChange(int pixels)
        {
            var size = pixels.ToString(CultureInfo.InvariantCulture);
            return "the declared size was set to " + size + " by " + size;
        }
    }
}
