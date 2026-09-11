using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Icons.Catalog
{
    /// <summary>
    /// One drawing, reduced to the thing that makes it that drawing: the shapes, in the order
    /// they are drawn, in the coordinate space they are drawn in.
    ///
    /// This is how an icon already sitting in an environment is recognised as one of the
    /// bundled ones. Everything that storing an icon changes is deliberately left out - the
    /// colour, the declared size, the provenance comment, the CSS class - so the file this tool
    /// wrote last year, the same icon pasted in from the family's own web page, and the entry
    /// in today's bundle all reduce to the same string.
    ///
    /// It is an equality, not a similarity. Two icons either are the same drawing or they are
    /// not; nothing here ranks near misses, because a confidently wrong "this is your icon" is
    /// worse than saying nothing.
    /// </summary>
    public static class IconFingerprint
    {
        /// <summary>
        /// Which canonical form these fingerprints are in. It is in the name of the index
        /// bundled beside the icons, so a build of the tool only ever reads an index its own
        /// canonicaliser wrote: change anything below that changes the string being hashed, and
        /// change this with it, and every older index simply stops being found.
        /// </summary>
        public const string Version = "1";

        /// <summary>
        /// Tabler opens every icon with an invisible full-canvas rectangle, there to give the
        /// icon a hit area in a browser. The bundle strips it, so a file pasted in straight from
        /// Tabler carries one where the bundled copy does not. Dropping it on both sides is what
        /// lets those two be recognised as the same drawing.
        /// </summary>
        private const string Backdrop = "M0 0h24v24H0z";

        /// <summary>The elements that put ink on the canvas, and what of each one matters.</summary>
        private static readonly Dictionary<string, string[]> Shapes =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["path"] = new[] { "d" },
                ["circle"] = new[] { "cx", "cy", "r" },
                ["ellipse"] = new[] { "cx", "cy", "rx", "ry" },
                ["rect"] = new[] { "x", "y", "width", "height", "rx", "ry" },
                ["line"] = new[] { "x1", "y1", "x2", "y2" },
                ["polygon"] = new[] { "points" },
                ["polyline"] = new[] { "points" }
            };

        /// <summary>How much of the digest is kept. See where it is taken, below.</summary>
        private const int Bytes = 12;

        private static readonly Regex Whitespace = new Regex(@"[\s,]+", RegexOptions.Compiled);

        /// <summary>
        /// The fingerprint of this SVG, or null when there is nothing to fingerprint: text that
        /// will not parse as XML, or a document that draws nothing. Null never matches anything,
        /// including another null.
        /// </summary>
        public static string Of(string svg)
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

            return Of(document);
        }

        /// <summary>The same, for a document already parsed.</summary>
        public static string Of(XDocument document)
        {
            if (document?.Root == null) return null;

            var canonical = new StringBuilder();
            canonical.Append("viewBox:").Append(Flatten(document.Root.Attribute("viewBox")?.Value));

            var shapes = 0;
            foreach (var element in document.Root.DescendantsAndSelf())
            {
                string[] wanted;
                if (!Shapes.TryGetValue(element.Name.LocalName, out wanted)) continue;

                if (element.Name.LocalName == "path" && IsBackdrop(element)) continue;

                var geometry = string.Join(" ",
                    wanted.Select(a => a + "=" + Flatten(element.Attribute(a)?.Value)));

                shapes++;
                canonical.Append('\n').Append(element.Name.LocalName).Append(' ').Append(geometry);
            }

            // A document with no shapes in it is not an icon, and two of them are not the same
            // icon. Say nothing rather than hand back a fingerprint every blank file shares.
            if (shapes == 0) return null;

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));

                // Ninety-six bits of it. The whole digest is 44 characters of base64 against
                // 16, and one of these is written per icon into a file that ships inside the
                // assembly - half a megabyte to say nothing more. Eighteen thousand drawings
                // in a space of 2^96 collide with a probability of about one in 10^21, which
                // is not a risk, and two that did would be read as one drawing under two
                // names: answered with neither, which is what the tool does with those anyway.
                Array.Resize(ref hash, Bytes);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>Whether this path is the invisible full-canvas rectangle, and not a drawing.</summary>
        private static bool IsBackdrop(XElement path)
        {
            return string.Equals(Flatten(path.Attribute("d")?.Value), Backdrop, StringComparison.Ordinal);
        }

        /// <summary>
        /// An attribute value with the formatting taken out of it. A path is the same path
        /// whether its author separated the numbers with spaces, commas or a line break, and
        /// XML puts the line break back differently from how it found it.
        /// </summary>
        private static string Flatten(string value)
        {
            return value == null ? "" : Whitespace.Replace(value, " ").Trim();
        }
    }
}
