using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Icons.Logic
{
    /// <summary>
    /// Turns SVG path data into a GDI+ figure.
    ///
    /// This exists so the tool has no rendering dependency. The bundled library is a narrow
    /// dialect - every icon is one svg element containing only path elements, with no strokes,
    /// transforms, gradients or stylesheets - so the whole of what has to be understood is the
    /// d attribute, and that is a few hundred lines. An SVG library would be several assemblies
    /// dropped next to the plugin, each one a chance to collide with what XrmToolBox already
    /// loaded.
    ///
    /// The grammar is the full path grammar all the same (M L H V C S Q T A Z, absolute and
    /// relative), because Fluent uses all of it.
    /// </summary>
    public static class SvgPath
    {
        /// <summary>
        /// Builds the figure described by an SVG d attribute.
        /// </summary>
        /// <exception cref="FormatException">The path data is not valid SVG.</exception>
        public static GraphicsPath Parse(string d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));

            // SVG fills nonzero by default, and the bundled icons rely on it: a hole is a
            // subpath wound the other way. Alternate, which is what GDI+ starts with, would
            // punch holes wherever two shapes merely overlap.
            var path = new GraphicsPath(FillMode.Winding);
            try
            {
                Build(d, path);
                return path;
            }
            catch
            {
                path.Dispose();
                throw;
            }
        }

        private static void Build(string d, GraphicsPath path)
        {
            var reader = new Reader(d);

            var current = PointF.Empty;      // where the pen is
            var start = PointF.Empty;        // where the current subpath began, for Z
            var lastControl = PointF.Empty;  // for the smooth forms, S and T
            var lastCommand = '\0';
            var open = false;                // has the current subpath any segments yet

            char command;
            while (reader.TryReadCommand(out command))
            {
                var relative = char.IsLower(command);
                var upper = char.ToUpperInvariant(command);

                if (upper == 'Z')
                {
                    if (open)
                    {
                        path.CloseFigure();
                        open = false;
                    }
                    current = start;
                    lastCommand = upper;
                    continue;
                }

                // A command letter is followed by as many argument groups as were written; the
                // command repeats over each. A repeated M is an implicit L, which is the one
                // case where the repeat is not the same command.
                var first = true;
                do
                {
                    switch (upper)
                    {
                        case 'M':
                        {
                            var p = reader.ReadPoint(relative, current);
                            if (first)
                            {
                                if (open) path.CloseFigure();
                                path.StartFigure();
                                open = false;
                                start = p;
                            }
                            else
                            {
                                path.AddLine(current, p);
                                open = true;
                            }
                            current = p;
                            lastControl = p;
                            break;
                        }

                        case 'L':
                        {
                            var p = reader.ReadPoint(relative, current);
                            path.AddLine(current, p);
                            current = p;
                            lastControl = p;
                            open = true;
                            break;
                        }

                        case 'H':
                        {
                            var x = reader.ReadNumber();
                            var p = new PointF(relative ? current.X + (float)x : (float)x, current.Y);
                            path.AddLine(current, p);
                            current = p;
                            lastControl = p;
                            open = true;
                            break;
                        }

                        case 'V':
                        {
                            var y = reader.ReadNumber();
                            var p = new PointF(current.X, relative ? current.Y + (float)y : (float)y);
                            path.AddLine(current, p);
                            current = p;
                            lastControl = p;
                            open = true;
                            break;
                        }

                        case 'C':
                        {
                            var c1 = reader.ReadPoint(relative, current);
                            var c2 = reader.ReadPoint(relative, current);
                            var end = reader.ReadPoint(relative, current);
                            path.AddBezier(current, c1, c2, end);
                            lastControl = c2;
                            current = end;
                            open = true;
                            break;
                        }

                        case 'S':
                        {
                            // The first control point is the reflection of the previous one,
                            // but only if the previous command was itself a cubic.
                            var c1 = (lastCommand == 'C' || lastCommand == 'S')
                                ? Reflect(lastControl, current)
                                : current;
                            var c2 = reader.ReadPoint(relative, current);
                            var end = reader.ReadPoint(relative, current);
                            path.AddBezier(current, c1, c2, end);
                            lastControl = c2;
                            current = end;
                            open = true;
                            break;
                        }

                        case 'Q':
                        {
                            var c = reader.ReadPoint(relative, current);
                            var end = reader.ReadPoint(relative, current);
                            AddQuadratic(path, current, c, end);
                            lastControl = c;
                            current = end;
                            open = true;
                            break;
                        }

                        case 'T':
                        {
                            var c = (lastCommand == 'Q' || lastCommand == 'T')
                                ? Reflect(lastControl, current)
                                : current;
                            var end = reader.ReadPoint(relative, current);
                            AddQuadratic(path, current, c, end);
                            lastControl = c;
                            current = end;
                            open = true;
                            break;
                        }

                        case 'A':
                        {
                            var rx = reader.ReadNumber();
                            var ry = reader.ReadNumber();
                            var rotation = reader.ReadNumber();
                            // The two flags are single characters and in minified path data are
                            // not delimited from what follows: "a10 10 0 1 0-20 0" is one arc.
                            var largeArc = reader.ReadFlag();
                            var sweep = reader.ReadFlag();
                            var end = reader.ReadPoint(relative, current);
                            AddArc(path, current, end, rx, ry, rotation, largeArc, sweep);
                            current = end;
                            lastControl = end;
                            open = true;
                            break;
                        }

                        default:
                            throw new FormatException("Unsupported path command '" + command + "'.");
                    }

                    // After the first group of an M, the command becomes L per the SVG grammar.
                    if (upper == 'M' && first) upper = 'L';
                    first = false;
                }
                while (reader.AtNumber());

                lastCommand = upper == 'L' && char.ToUpperInvariant(command) == 'M' ? 'M' : upper;
            }

            if (open) path.CloseFigure();
        }

        private static PointF Reflect(PointF control, PointF about)
        {
            return new PointF(2 * about.X - control.X, 2 * about.Y - control.Y);
        }

        /// <summary>
        /// GDI+ has no quadratic segment, so raise the degree. The conversion is exact.
        /// </summary>
        private static void AddQuadratic(GraphicsPath path, PointF from, PointF control, PointF to)
        {
            var c1 = new PointF(
                from.X + 2f / 3f * (control.X - from.X),
                from.Y + 2f / 3f * (control.Y - from.Y));
            var c2 = new PointF(
                to.X + 2f / 3f * (control.X - to.X),
                to.Y + 2f / 3f * (control.Y - to.Y));
            path.AddBezier(from, c1, c2, to);
        }

        /// <summary>
        /// An SVG elliptical arc, as cubic segments.
        ///
        /// SVG states an arc by where it ends and which of the four candidate arcs to take;
        /// drawing it needs the centre. This is the endpoint-to-centre conversion from the SVG
        /// specification (appendix F.6.5), followed by the standard approximation of each
        /// quarter turn or less by one cubic. GraphicsPath.AddArc is no use here because it
        /// cannot express a rotated ellipse.
        /// </summary>
        private static void AddArc(GraphicsPath path, PointF from, PointF to,
                                   double rx, double ry, double rotationDegrees,
                                   bool largeArc, bool sweep)
        {
            // Degenerate radii mean a straight line, per the specification. So does going
            // nowhere, which additionally has no defined centre.
            if (Math.Abs(rx) < 1e-9 || Math.Abs(ry) < 1e-9 ||
                (Math.Abs(from.X - to.X) < 1e-9 && Math.Abs(from.Y - to.Y) < 1e-9))
            {
                if (Math.Abs(from.X - to.X) > 1e-9 || Math.Abs(from.Y - to.Y) > 1e-9)
                    path.AddLine(from, to);
                return;
            }

            rx = Math.Abs(rx);
            ry = Math.Abs(ry);

            var phi = rotationDegrees * Math.PI / 180.0;
            var cosPhi = Math.Cos(phi);
            var sinPhi = Math.Sin(phi);

            var dx2 = (from.X - to.X) / 2.0;
            var dy2 = (from.Y - to.Y) / 2.0;

            var x1p = cosPhi * dx2 + sinPhi * dy2;
            var y1p = -sinPhi * dx2 + cosPhi * dy2;

            // Radii too small to reach both endpoints are scaled up until they just do.
            var lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
            if (lambda > 1)
            {
                var scale = Math.Sqrt(lambda);
                rx *= scale;
                ry *= scale;
            }

            var numerator = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
            var denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
            var coefficient = Math.Sqrt(Math.Max(0.0, numerator / denominator));
            if (largeArc == sweep) coefficient = -coefficient;

            var cxp = coefficient * rx * y1p / ry;
            var cyp = -coefficient * ry * x1p / rx;

            var cx = cosPhi * cxp - sinPhi * cyp + (from.X + to.X) / 2.0;
            var cy = sinPhi * cxp + cosPhi * cyp + (from.Y + to.Y) / 2.0;

            var theta1 = Angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
            var delta = Angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);

            if (!sweep && delta > 0) delta -= 2 * Math.PI;
            else if (sweep && delta < 0) delta += 2 * Math.PI;

            // One cubic per quarter turn keeps the error far below a pixel at icon sizes.
            var segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2) - 1e-9));
            var step = delta / segments;
            var alpha = 4.0 / 3.0 * Math.Tan(step / 4.0);

            var pointStart = from;
            for (var i = 0; i < segments; i++)
            {
                var t1 = theta1 + i * step;
                var t2 = t1 + step;

                var e2 = Ellipse(cx, cy, rx, ry, cosPhi, sinPhi, t2);
                var d1 = EllipseDerivative(rx, ry, cosPhi, sinPhi, t1);
                var d2 = EllipseDerivative(rx, ry, cosPhi, sinPhi, t2);

                var c1 = new PointF(
                    (float)(pointStart.X + alpha * d1.X),
                    (float)(pointStart.Y + alpha * d1.Y));
                var c2 = new PointF(
                    (float)(e2.X - alpha * d2.X),
                    (float)(e2.Y - alpha * d2.Y));

                // The last segment lands on the stated endpoint exactly, not on the endpoint
                // recomputed from the centre, so rounding cannot leave a gap in the outline.
                var endPoint = i == segments - 1 ? to : new PointF((float)e2.X, (float)e2.Y);

                path.AddBezier(pointStart, c1, c2, endPoint);
                pointStart = endPoint;
            }
        }

        private static PointD Ellipse(double cx, double cy, double rx, double ry,
                                      double cosPhi, double sinPhi, double t)
        {
            var cosT = Math.Cos(t);
            var sinT = Math.Sin(t);
            return new PointD(
                cx + rx * cosT * cosPhi - ry * sinT * sinPhi,
                cy + rx * cosT * sinPhi + ry * sinT * cosPhi);
        }

        private static PointD EllipseDerivative(double rx, double ry,
                                                double cosPhi, double sinPhi, double t)
        {
            var cosT = Math.Cos(t);
            var sinT = Math.Sin(t);
            return new PointD(
                -rx * sinT * cosPhi - ry * cosT * sinPhi,
                -rx * sinT * sinPhi + ry * cosT * cosPhi);
        }

        private static double Angle(double ux, double uy, double vx, double vy)
        {
            var dot = ux * vx + uy * vy;
            var length = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
            if (length < 1e-12) return 0;

            var cos = dot / length;
            if (cos > 1) cos = 1;
            else if (cos < -1) cos = -1;

            var angle = Math.Acos(cos);
            return (ux * vy - uy * vx) < 0 ? -angle : angle;
        }

        private struct PointD
        {
            public readonly double X;
            public readonly double Y;
            public PointD(double x, double y) { X = x; Y = y; }
        }

        /// <summary>
        /// A scanner over path data. Everything awkward about the format lives here: numbers
        /// that run together (".5.5" is two numbers), flags that are one character wide, and
        /// separators that are optional wherever they are not needed.
        /// </summary>
        private sealed class Reader
        {
            private readonly string _text;
            private int _index;

            public Reader(string text)
            {
                _text = text;
                _index = 0;
            }

            private void SkipSeparators()
            {
                while (_index < _text.Length &&
                       (char.IsWhiteSpace(_text[_index]) || _text[_index] == ','))
                {
                    _index++;
                }
            }

            public bool TryReadCommand(out char command)
            {
                SkipSeparators();
                if (_index < _text.Length && char.IsLetter(_text[_index]))
                {
                    command = _text[_index++];
                    return true;
                }

                if (_index < _text.Length)
                {
                    throw new FormatException(
                        "Expected a path command at offset " + _index + ", found '" + _text[_index] + "'.");
                }

                command = '\0';
                return false;
            }

            /// <summary>Whether another argument group follows, i.e. the command repeats.</summary>
            public bool AtNumber()
            {
                SkipSeparators();
                if (_index >= _text.Length) return false;
                var c = _text[_index];
                return c == '-' || c == '+' || c == '.' || (c >= '0' && c <= '9');
            }

            public double ReadNumber()
            {
                SkipSeparators();
                var start = _index;

                if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+')) _index++;
                while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9') _index++;

                // At most one decimal point: ".5.5" has to scan as 0.5 then 0.5.
                if (_index < _text.Length && _text[_index] == '.')
                {
                    _index++;
                    while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9') _index++;
                }

                if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    var beforeExponent = _index;
                    _index++;
                    if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+')) _index++;

                    if (_index < _text.Length && char.IsDigit(_text[_index]))
                    {
                        while (_index < _text.Length && char.IsDigit(_text[_index])) _index++;
                    }
                    else
                    {
                        // Not an exponent after all - the letter starts the next command.
                        _index = beforeExponent;
                    }
                }

                if (_index == start)
                {
                    throw new FormatException("Expected a number at offset " + start + " of the path data.");
                }

                var token = _text.Substring(start, _index - start);
                double value;
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    throw new FormatException("'" + token + "' is not a number.");

                return value;
            }

            public bool ReadFlag()
            {
                SkipSeparators();
                if (_index >= _text.Length)
                    throw new FormatException("Path data ended where an arc flag was expected.");

                var c = _text[_index++];
                if (c == '0') return false;
                if (c == '1') return true;

                throw new FormatException("An arc flag must be 0 or 1, found '" + c + "'.");
            }

            public PointF ReadPoint(bool relative, PointF origin)
            {
                var x = ReadNumber();
                var y = ReadNumber();
                return relative
                    ? new PointF(origin.X + (float)x, origin.Y + (float)y)
                    : new PointF((float)x, (float)y);
            }
        }
    }
}
