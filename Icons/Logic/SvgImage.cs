using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Icons.Logic
{
    /// <summary>
    /// As much of an SVG as this tool needs to draw one: a viewBox and some filled paths.
    ///
    /// This is not a general SVG reader and does not pretend to be. It covers the dialect the
    /// bundled library is written in. Anything outside that - a gradient, a stroke, a nested
    /// group - is reported rather than silently dropped, so a preview never quietly disagrees
    /// with what Dataverse will show.
    /// </summary>
    public sealed class SvgImage
    {
        private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

        private SvgImage(RectangleF viewBox, List<Shape> shapes, List<string> unsupported)
        {
            ViewBox = viewBox;
            Shapes = shapes;
            Unsupported = unsupported;
        }

        public RectangleF ViewBox { get; }

        private List<Shape> Shapes { get; }

        /// <summary>
        /// Features found in the file that this reader does not draw. Empty for every icon in
        /// the bundled library; non-empty is a reason to warn rather than to guess.
        /// </summary>
        public IReadOnlyList<string> Unsupported { get; }

        /// <summary>Whether the file paints its own colours, so recolouring it would not take.</summary>
        public bool HasOwnColours
        {
            get { return Shapes.Any(s => s.Fill.HasValue); }
        }

        /// <exception cref="FormatException">The text is not an SVG this reader can draw.</exception>
        public static SvgImage Parse(string svg)
        {
            if (svg == null) throw new ArgumentNullException(nameof(svg));

            XDocument document;
            try
            {
                document = XDocument.Parse(svg);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new FormatException("Not well-formed XML: " + ex.Message, ex);
            }

            var root = document.Root;
            if (root == null || root.Name.LocalName != "svg")
                throw new FormatException("The root element is not <svg>.");

            var viewBox = ReadViewBox(root);
            var shapes = new List<Shape>();
            var unsupported = new List<string>();

            foreach (var element in root.Descendants())
            {
                var name = element.Name.LocalName;
                if (name == "path")
                {
                    var d = (string)element.Attribute("d");
                    if (string.IsNullOrWhiteSpace(d)) continue;

                    shapes.Add(new Shape(
                        d,
                        ParseColour((string)element.Attribute("fill")),
                        ParseOpacity((string)element.Attribute("opacity"))
                            ?? ParseOpacity((string)element.Attribute("fill-opacity"))));
                }
                else if (name != "title" && name != "desc" && name != "metadata")
                {
                    if (!unsupported.Contains(name)) unsupported.Add(name);
                }
            }

            if (shapes.Count == 0 && unsupported.Count == 0)
                throw new FormatException("The file contains no drawable paths.");

            return new SvgImage(viewBox, shapes, unsupported);
        }

        /// <summary>
        /// Draws the icon square at the given pixel size, in one colour unless the file insists
        /// on its own. The bitmap has an alpha channel and no background.
        /// </summary>
        public Bitmap Render(int pixels, Color colour)
        {
            return Render(pixels, pixels, colour);
        }

        /// <summary>
        /// Draws the icon into a bitmap of the given shape, fitted and centred. Used for the
        /// classic raster icon properties, one of which is 66x48 rather than square.
        /// </summary>
        public Bitmap Render(int width, int height, Color colour)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            var bitmap = new Bitmap(width, height);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    Draw(graphics, new RectangleF(0, 0, width, height), colour);
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Draws the icon into a rectangle, fitted and centred so a non-square viewBox keeps
        /// its proportions rather than being stretched to fill.
        /// </summary>
        public void Draw(Graphics graphics, RectangleF bounds, Color colour)
        {
            if (graphics == null) throw new ArgumentNullException(nameof(graphics));
            if (ViewBox.Width <= 0 || ViewBox.Height <= 0) return;

            var scale = Math.Min(bounds.Width / ViewBox.Width, bounds.Height / ViewBox.Height);
            var offsetX = bounds.X + (bounds.Width - ViewBox.Width * scale) / 2f;
            var offsetY = bounds.Y + (bounds.Height - ViewBox.Height * scale) / 2f;

            var saved = graphics.Save();
            try
            {
                graphics.TranslateTransform(offsetX, offsetY);
                graphics.ScaleTransform(scale, scale);
                graphics.TranslateTransform(-ViewBox.X, -ViewBox.Y);

                foreach (var shape in Shapes)
                {
                    var fill = shape.Fill ?? colour;
                    if (shape.Opacity.HasValue)
                        fill = Color.FromArgb((int)Math.Round(fill.A * shape.Opacity.Value), fill);

                    using (var path = SvgPath.Parse(shape.Data))
                    using (var brush = new SolidBrush(fill))
                    {
                        graphics.FillPath(brush, path);
                    }
                }
            }
            finally
            {
                graphics.Restore(saved);
            }
        }

        private static RectangleF ReadViewBox(XElement root)
        {
            var raw = (string)root.Attribute("viewBox");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var parts = raw.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4)
                {
                    float x, y, w, h;
                    if (TryFloat(parts[0], out x) && TryFloat(parts[1], out y) &&
                        TryFloat(parts[2], out w) && TryFloat(parts[3], out h) && w > 0 && h > 0)
                    {
                        return new RectangleF(x, y, w, h);
                    }
                }
            }

            // No usable viewBox: fall back to the declared size, and failing that to Fluent's
            // canonical box, which is right for everything the tool bundles.
            var width = ReadLength(root, "width") ?? 24f;
            var height = ReadLength(root, "height") ?? 24f;
            return new RectangleF(0, 0, width, height);
        }

        private static float? ReadLength(XElement element, string attribute)
        {
            var raw = (string)element.Attribute(attribute);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var trimmed = raw.Trim().TrimEnd('p', 'x', 'P', 'X');
            float value;
            return TryFloat(trimmed, out value) && value > 0 ? value : (float?)null;
        }

        private static bool TryFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static float? ParseOpacity(string raw)
        {
            float value;
            if (!string.IsNullOrWhiteSpace(raw) && TryFloat(raw.Trim(), out value))
                return Math.Min(1f, Math.Max(0f, value));
            return null;
        }

        /// <summary>
        /// A fill the file states for itself. "none" and "currentColor" both come back null:
        /// one paints nothing and one defers to the caller, and for a preview the caller's
        /// colour is the honest answer to both.
        /// </summary>
        private static Color? ParseColour(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var text = raw.Trim();
            if (text.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("currentColor", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("inherit", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                var hex = text.Substring(1);
                if (hex.Length == 3)
                {
                    hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
                }
                if (hex.Length == 6)
                {
                    int rgb;
                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                        return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                }
                return null;
            }

            try
            {
                var known = Color.FromName(text);
                return known.IsKnownColor ? known : (Color?)null;
            }
            catch
            {
                return null;
            }
        }

        private sealed class Shape
        {
            public Shape(string data, Color? fill, float? opacity)
            {
                Data = data;
                Fill = fill;
                Opacity = opacity;
            }

            public string Data { get; }
            public Color? Fill { get; }
            public float? Opacity { get; }
        }
    }
}
