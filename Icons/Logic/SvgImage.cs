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
    /// As much of an SVG as this tool needs to draw one: a viewBox, some shapes, and whether
    /// each is filled, stroked or both.
    ///
    /// This is not a general SVG reader and does not pretend to be. It covers the dialect the
    /// bundled families are written in. Anything outside that - a gradient, a transform, a
    /// stylesheet - is reported rather than silently dropped, so a preview never quietly
    /// disagrees with what Dataverse will show.
    ///
    /// Strokes are here because half the library is drawn with them. Fluent and Phosphor state
    /// their icons as filled outlines, where the shape and its hole are both regions to paint;
    /// Lucide and Tabler state the same drawing as a line of a given width along a path. Filling
    /// the second kind gives a solid blob, which is exactly what this tool used to make of them.
    /// </summary>
    public sealed class SvgImage
    {
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
            get { return Shapes.Any(s => s.Fill.Colour.HasValue || s.Stroke.Colour.HasValue); }
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

            // What an element does not say, it inherits. Every stroke set bundled here states
            // the stroke once on the root and nowhere else, so without this they draw nothing.
            var initial = Style.Initial.Inherit(root);
            Walk(root, initial, shapes, unsupported);

            if (shapes.Count == 0 && unsupported.Count == 0)
                throw new FormatException("The file contains no drawable shapes.");

            return new SvgImage(viewBox, shapes, unsupported);
        }

        private static void Walk(XElement parent, Style inherited, List<Shape> shapes, List<string> unsupported)
        {
            foreach (var element in parent.Elements())
            {
                var name = element.Name.LocalName;

                if (name == "title" || name == "desc" || name == "metadata") continue;

                if (name == "g")
                {
                    // A group paints nothing itself; it is here to pass its style down.
                    Note(unsupported, element, "transform");
                    Walk(element, inherited.Inherit(element), shapes, unsupported);
                    continue;
                }

                var data = Geometry(element);
                if (data == null)
                {
                    if (!unsupported.Contains(name)) unsupported.Add(name);
                    continue;
                }

                Note(unsupported, element, "transform");

                var style = inherited.Inherit(element);
                if (style.Fill.Nothing && style.Stroke.Nothing) continue;

                shapes.Add(new Shape(data, style));
            }
        }

        private static void Note(List<string> unsupported, XElement element, string attribute)
        {
            if (element.Attribute(attribute) == null) return;
            if (!unsupported.Contains(attribute)) unsupported.Add(attribute);
        }

        /// <summary>
        /// The element as path data, or null when it is not a shape this reader draws. The
        /// primitives are converted rather than drawn directly so that everything downstream -
        /// the parser, the stroking, the fill rule - has one kind of thing to deal with.
        /// </summary>
        private static string Geometry(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "path":
                {
                    var d = (string)element.Attribute("d");
                    return string.IsNullOrWhiteSpace(d) ? null : d;
                }

                case "circle":
                {
                    var r = Length(element, "r") ?? 0;
                    return r <= 0 ? null : Ellipse(Length(element, "cx") ?? 0, Length(element, "cy") ?? 0, r, r);
                }

                case "ellipse":
                {
                    var rx = Length(element, "rx") ?? 0;
                    var ry = Length(element, "ry") ?? 0;
                    return rx <= 0 || ry <= 0 ? null : Ellipse(Length(element, "cx") ?? 0, Length(element, "cy") ?? 0, rx, ry);
                }

                case "rect":
                    return Rectangle(element);

                case "line":
                {
                    return Move(Length(element, "x1") ?? 0, Length(element, "y1") ?? 0) +
                           "L" + Number(Length(element, "x2") ?? 0) + "," + Number(Length(element, "y2") ?? 0);
                }

                case "polyline":
                    return Points(element, close: false);

                case "polygon":
                    return Points(element, close: true);

                default:
                    return null;
            }
        }

        /// <summary>Two half arcs, because a full one has nowhere to start and end.</summary>
        private static string Ellipse(float cx, float cy, float rx, float ry)
        {
            var radii = Number(rx) + "," + Number(ry) + " 0 1 0 ";
            return Move(cx - rx, cy) +
                   "A" + radii + Number(cx + rx) + "," + Number(cy) +
                   "A" + radii + Number(cx - rx) + "," + Number(cy) + "Z";
        }

        private static string Rectangle(XElement element)
        {
            var w = Length(element, "width") ?? 0;
            var h = Length(element, "height") ?? 0;
            if (w <= 0 || h <= 0) return null;

            var x = Length(element, "x") ?? 0;
            var y = Length(element, "y") ?? 0;

            // Either radius stands in for the other when only one is given, which is how every
            // rounded rectangle in the library states it.
            var rx = Length(element, "rx");
            var ry = Length(element, "ry");
            var cornerX = Math.Min(rx ?? ry ?? 0, w / 2);
            var cornerY = Math.Min(ry ?? rx ?? 0, h / 2);

            if (cornerX <= 0 || cornerY <= 0)
            {
                return Move(x, y) + "H" + Number(x + w) + "V" + Number(y + h) + "H" + Number(x) + "Z";
            }

            var arc = "A" + Number(cornerX) + "," + Number(cornerY) + " 0 0 1 ";
            return Move(x + cornerX, y) +
                   "H" + Number(x + w - cornerX) +
                   arc + Number(x + w) + "," + Number(y + cornerY) +
                   "V" + Number(y + h - cornerY) +
                   arc + Number(x + w - cornerX) + "," + Number(y + h) +
                   "H" + Number(x + cornerX) +
                   arc + Number(x) + "," + Number(y + h - cornerY) +
                   "V" + Number(y + cornerY) +
                   arc + Number(x + cornerX) + "," + Number(y) + "Z";
        }

        private static string Points(XElement element, bool close)
        {
            var raw = (string)element.Attribute("points");
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var numbers = raw
                .Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => { float v; return TryFloat(t, out v) ? (float?)v : null; })
                .ToList();

            if (numbers.Count < 4 || numbers.Any(n => !n.HasValue)) return null;

            var d = Move(numbers[0].Value, numbers[1].Value);
            for (var i = 2; i + 1 < numbers.Count; i += 2)
                d += "L" + Number(numbers[i].Value) + "," + Number(numbers[i + 1].Value);

            return close ? d + "Z" : d;
        }

        private static string Move(float x, float y)
        {
            return "M" + Number(x) + "," + Number(y);
        }

        private static string Number(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
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
                    var stroked = !shape.Stroke.Nothing;

                    // A subpath the data left open is closed to fill it and left alone to
                    // stroke it. GDI+ closes a figure to fill it regardless, so when a shape is
                    // both, open is right for both.
                    using (var path = SvgPath.Parse(shape.Data, closeOpenFigures: !stroked))
                    {
                        if (!shape.Fill.Nothing)
                        {
                            using (var brush = new SolidBrush(shape.Ink(shape.Fill, colour)))
                                graphics.FillPath(brush, path);
                        }

                        if (stroked)
                        {
                            using (var pen = new Pen(shape.Ink(shape.Stroke, colour), shape.StrokeWidth))
                            {
                                pen.StartCap = shape.Cap;
                                pen.EndCap = shape.Cap;
                                pen.LineJoin = shape.Join;

                                // Round joins want a round inner corner too, or GDI+ fills the
                                // notch with a mitre and the corner grows a spur.
                                if (shape.Join == LineJoin.Round) pen.MiterLimit = 1f;

                                graphics.DrawPath(pen, path);
                            }
                        }
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

        private static float? Length(XElement element, string attribute)
        {
            var raw = (string)element.Attribute(attribute);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            float value;
            return TryFloat(raw.Trim().TrimEnd('p', 'x', 'P', 'X'), out value) ? value : (float?)null;
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
        /// What an element says about one kind of paint. Three answers, and they are not the
        /// same: a colour of its own, "none" meaning paint nothing at all, and nothing said -
        /// which for a fill means the caller's colour and for a stroke means no stroke.
        /// </summary>
        private struct Paint
        {
            public Paint(Color? colour, bool nothing)
            {
                Colour = colour;
                Nothing = nothing;
            }

            /// <summary>A colour the file states for itself. Null defers to the caller.</summary>
            public Color? Colour { get; }

            /// <summary>Paint nothing. What "none" means, and what a stroke is until one is set.</summary>
            public bool Nothing { get; }

            public static readonly Paint Caller = new Paint(null, false);
            public static readonly Paint None = new Paint(null, true);

            /// <summary>
            /// The paint an attribute asks for, or what was inherited when it says nothing.
            /// "currentColor" is the caller's colour, which is what the whole tool is built on.
            /// </summary>
            public static Paint Read(string raw, Paint inherited)
            {
                if (string.IsNullOrWhiteSpace(raw)) return inherited;

                var text = raw.Trim();
                if (text.Equals("none", StringComparison.OrdinalIgnoreCase)) return None;
                if (text.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return None;
                if (text.Equals("currentColor", StringComparison.OrdinalIgnoreCase)) return Caller;

                var colour = ParseColour(text);
                return colour.HasValue ? new Paint(colour, false) : inherited;
            }
        }

        /// <summary>Everything about how a shape is painted that an element inherits.</summary>
        private struct Style
        {
            public Style(Paint fill, Paint stroke, float strokeWidth, LineCap cap, LineJoin join, float? opacity)
            {
                Fill = fill;
                Stroke = stroke;
                StrokeWidth = strokeWidth;
                Cap = cap;
                Join = join;
                Opacity = opacity;
            }

            public Paint Fill { get; }
            public Paint Stroke { get; }
            public float StrokeWidth { get; }
            public LineCap Cap { get; }
            public LineJoin Join { get; }
            public float? Opacity { get; }

            /// <summary>What SVG starts with: filled, unstroked, and a stroke width of one.</summary>
            public static readonly Style Initial =
                new Style(Paint.Caller, Paint.None, 1f, LineCap.Flat, LineJoin.Miter, null);

            /// <summary>This style, with whatever the element says about it applied over the top.</summary>
            public Style Inherit(XElement element)
            {
                var width = ReadLength(element, "stroke-width") ?? StrokeWidth;

                return new Style(
                    Paint.Read((string)element.Attribute("fill"), Fill),
                    Paint.Read((string)element.Attribute("stroke"), Stroke),
                    width,
                    ReadCap((string)element.Attribute("stroke-linecap"), Cap),
                    ReadJoin((string)element.Attribute("stroke-linejoin"), Join),
                    // Not inherited, unlike the rest: SVG opacity is a property of the group or
                    // the shape it is written on, and passing it down would apply it twice.
                    ParseOpacity((string)element.Attribute("opacity"))
                        ?? ParseOpacity((string)element.Attribute("fill-opacity")));
            }

            private static LineCap ReadCap(string raw, LineCap inherited)
            {
                if (string.IsNullOrWhiteSpace(raw)) return inherited;
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "round": return LineCap.Round;
                    case "square": return LineCap.Square;
                    case "butt": return LineCap.Flat;
                    default: return inherited;
                }
            }

            private static LineJoin ReadJoin(string raw, LineJoin inherited)
            {
                if (string.IsNullOrWhiteSpace(raw)) return inherited;
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "round": return LineJoin.Round;
                    case "bevel": return LineJoin.Bevel;
                    case "miter": return LineJoin.Miter;
                    default: return inherited;
                }
            }
        }

        /// <summary>
        /// A colour written the way SVG writes one: #rgb, #rrggbb, or one of the few names the
        /// bundled icons actually use. Null when it is none of those, which leaves the caller's
        /// colour in charge rather than guessing at a colour nobody asked for.
        /// </summary>
        private static Color? ParseColour(string text)
        {
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                var hex = text.Substring(1);
                try
                {
                    if (hex.Length == 3)
                    {
                        return Color.FromArgb(
                            Convert.ToInt32(new string(hex[0], 2), 16),
                            Convert.ToInt32(new string(hex[1], 2), 16),
                            Convert.ToInt32(new string(hex[2], 2), 16));
                    }
                    if (hex.Length == 6)
                    {
                        return Color.FromArgb(
                            Convert.ToInt32(hex.Substring(0, 2), 16),
                            Convert.ToInt32(hex.Substring(2, 2), 16),
                            Convert.ToInt32(hex.Substring(4, 2), 16));
                    }
                }
                catch (FormatException)
                {
                    return null;
                }
                return null;
            }

            var known = Color.FromName(text);
            return known.IsKnownColor ? known : (Color?)null;
        }

        private sealed class Shape
        {
            public Shape(string data, Style style)
            {
                Data = data;
                Fill = style.Fill;
                Stroke = style.Stroke;
                StrokeWidth = style.StrokeWidth;
                Cap = style.Cap;
                Join = style.Join;
                Opacity = style.Opacity;
            }

            public string Data { get; }
            public Paint Fill { get; }
            public Paint Stroke { get; }
            public float StrokeWidth { get; }
            public LineCap Cap { get; }
            public LineJoin Join { get; }
            public float? Opacity { get; }

            /// <summary>The colour to actually put down: the file's if it has one, else the caller's.</summary>
            public Color Ink(Paint paint, Color caller)
            {
                var colour = paint.Colour ?? caller;
                return Opacity.HasValue
                    ? Color.FromArgb((int)Math.Round(colour.A * Opacity.Value), colour)
                    : colour;
            }
        }
    }
}
