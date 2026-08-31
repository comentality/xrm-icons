using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Icons.Logic
{
    /// <summary>How the icon should be coloured once it is a table icon.</summary>
    public enum IconColourMode
    {
        /// <summary>
        /// Let the app decide, by painting with currentColor. This is what Microsoft asks for:
        /// a table icon with no colour of its own picks up the theme, stays legible in dark
        /// mode, and does not have to be redone when the theme changes.
        /// </summary>
        Theme,

        /// <summary>Paint one fixed colour, whatever the theme. For brand icons.</summary>
        Fixed
    }

    /// <summary>
    /// Turns a library icon into the SVG that gets stored as a web resource.
    ///
    /// The changes are deliberately tiny - a fill on the root element, and the declared size if
    /// asked - because the less this rewrites, the less there is to go wrong between what the
    /// preview drew and what the app renders.
    /// </summary>
    public static class SvgOutput
    {
        private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";

        /// <summary>
        /// Rewrites an icon for storage.
        /// </summary>
        /// <param name="svg">The icon as it ships.</param>
        /// <param name="mode">Theme-coloured or a fixed colour.</param>
        /// <param name="colour">The colour, when the mode is Fixed.</param>
        /// <param name="sizePixels">
        /// Declared width and height, or null to leave them alone. The viewBox is never touched:
        /// changing it would crop the drawing rather than resize it.
        /// </param>
        public static string Build(string svg, IconColourMode mode, Color colour, int? sizePixels = null)
        {
            if (svg == null) throw new ArgumentNullException(nameof(svg));
            if (sizePixels.HasValue && sizePixels.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizePixels), "A declared size must be positive.");

            XDocument document;
            try
            {
                document = XDocument.Parse(svg);
            }
            catch (XmlException ex)
            {
                throw new FormatException("Not well-formed XML: " + ex.Message, ex);
            }

            var root = document.Root;
            if (root == null || root.Name.LocalName != "svg")
                throw new FormatException("The root element is not <svg>.");

            // Some SVGs arrive without the namespace. A web resource served to a browser needs
            // it, or the markup renders as nothing at all.
            if (root.Name.Namespace == XNamespace.None)
            {
                foreach (var element in root.DescendantsAndSelf().ToList())
                    element.Name = SvgNs + element.Name.LocalName;
            }

            root.SetAttributeValue("fill", mode == IconColourMode.Fixed ? ToHex(colour) : "currentColor");

            if (sizePixels.HasValue)
            {
                var size = sizePixels.Value.ToString(CultureInfo.InvariantCulture);
                root.SetAttributeValue("width", size);
                root.SetAttributeValue("height", size);
            }

            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                Encoding = new UTF8Encoding(false),
            };

            using (var buffer = new StringWriter(CultureInfo.InvariantCulture))
            {
                using (var writer = XmlWriter.Create(buffer, settings))
                    document.Save(writer);
                return buffer.ToString();
            }
        }

        /// <summary>
        /// What is wrong with this icon as a table icon, in the terms Microsoft's own guidance
        /// uses. Empty means nothing is. These are warnings, not refusals: an icon that paints
        /// its own colours is a legitimate choice, it just will not follow the theme.
        /// </summary>
        public static IReadOnlyList<string> Inspect(string svg)
        {
            var problems = new List<string>();

            XDocument document;
            try
            {
                document = XDocument.Parse(svg);
            }
            catch (XmlException ex)
            {
                problems.Add("The file is not well-formed XML: " + ex.Message);
                return problems;
            }

            var root = document.Root;
            if (root == null || root.Name.LocalName != "svg")
            {
                problems.Add("The root element is not <svg>, so the app will not render it.");
                return problems;
            }

            if (root.Attribute("viewBox") == null)
                problems.Add("There is no viewBox, so the icon cannot scale. Microsoft asks for width, height and viewBox.");

            var paintsOwn = root.Descendants()
                .Any(e =>
                {
                    var fill = (string)e.Attribute("fill");
                    return !string.IsNullOrWhiteSpace(fill)
                           && !fill.Equals("none", StringComparison.OrdinalIgnoreCase)
                           && !fill.Equals("currentColor", StringComparison.OrdinalIgnoreCase);
                });

            if (paintsOwn)
            {
                problems.Add(
                    "Some shapes set their own fill, which overrides the colour chosen here. " +
                    "The icon will keep those colours in every theme.");
            }

            if (root.Descendants().Any(e => e.Name.LocalName == "style") ||
                root.DescendantsAndSelf().Any(e => e.Attribute("class") != null))
            {
                problems.Add("The file uses a stylesheet or CSS classes. Microsoft advises against both in a table icon.");
            }

            return problems;
        }

        /// <summary>Bytes as Dataverse wants a web resource: base64 of the UTF-8 text.</summary>
        public static string ToBase64(string svg)
        {
            if (svg == null) throw new ArgumentNullException(nameof(svg));
            return Convert.ToBase64String(new UTF8Encoding(false).GetBytes(svg));
        }

        /// <summary>Bytes as Dataverse wants a web resource, for a raster icon.</summary>
        public static string ToBase64(byte[] content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            return Convert.ToBase64String(content);
        }

        private static string ToHex(Color colour)
        {
            return "#" + colour.R.ToString("X2", CultureInfo.InvariantCulture)
                       + colour.G.ToString("X2", CultureInfo.InvariantCulture)
                       + colour.B.ToString("X2", CultureInfo.InvariantCulture);
        }
    }
}
