using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Icons.Logic;

namespace Icons.Ui
{
    /// <summary>One row of a navigation, ready to draw.</summary>
    public sealed class NavRow
    {
        public NavRow(string label, SvgImage icon, bool group = false)
        {
            Label = label ?? "";
            Icon = icon;
            IsGroup = group;
        }

        /// <summary>
        /// Settable for one row only: the one the chosen icon stands in, whose caption is the
        /// table's name when the navigation around it is a stand-in.
        /// </summary>
        public string Label { get; set; }

        /// <summary>What draws it, or null when nothing can and the row shows an empty slot.</summary>
        public SvgImage Icon { get; set; }

        /// <summary>A heading rather than a table: a caption, no icon, nothing to select.</summary>
        public bool IsGroup { get; }
    }

    /// <summary>
    /// Draws a navigation the way a model-driven app draws one.
    ///
    /// Everything is stated in CSS pixels and multiplied by a scale, because the whole point is
    /// to be the size the app is: the caller passes DeviceDpi / 96 and the drawing comes out the
    /// physical size on this screen that the browser would give it.
    ///
    /// Nothing here uses a Graphics transform, and that is deliberate rather than stylistic.
    /// TextRenderer paints through GDI, which knows nothing about the transform, so a translated
    /// painter moves its icons and leaves every label behind at the origin.
    ///
    /// The numbers it draws with are in <see cref="NavChrome"/>, and until they have been
    /// measured against a running app they are marked as the guesses they are.
    /// </summary>
    public sealed class NavPainter
    {
        private readonly float _scale;
        private readonly NavTheme _theme;
        private int _ox, _oy;

        /// <summary>
        /// The scale is DeviceDpi / 96, and it is a fraction on purpose. Rounding it to a whole
        /// number would draw a 125% display at 100%, which for a preview whose only claim is
        /// being the right size is the one mistake it cannot make.
        /// </summary>
        public NavPainter(NavTheme theme, float scale)
        {
            _theme = theme ?? NavTheme.Light;
            _scale = scale > 0 ? scale : 1f;
        }

        private int At(int cssPixels)
        {
            return (int)Math.Round(cssPixels * _scale);
        }

        /// <summary>How tall a navigation of these rows is, in device pixels.</summary>
        public int Height(IEnumerable<NavRow> rows)
        {
            var height = 16; // the 8 above and the 8 below, as above
            foreach (var row in rows)
                height += row.IsGroup ? NavChrome.NavGroupHeight : NavChrome.NavRowHeight;

            return At(height);
        }

        /// <summary>
        /// Where a row's top edge sits, in device pixels from the top of the drawing. What a
        /// caller wants it for is scrolling to a row it has just selected, which in a real site
        /// map can be forty rows down.
        /// </summary>
        public int Top(IList<NavRow> rows, int index)
        {
            var top = 8; // the mock's own margin, as in Draw
            for (var i = 0; i < index && i < rows.Count; i++)
                top += rows[i].IsGroup ? NavChrome.NavGroupHeight : NavChrome.NavRowHeight;

            return At(top);
        }

        public int RowHeight
        {
            get { return At(NavChrome.NavRowHeight); }
        }

        public int Width
        {
            get { return At(NavChrome.NavWidth); }
        }

        /// <summary>
        /// Draws the navigation at a point, with one row shown as the selected one. A selected
        /// index outside the list selects nothing, which is what an empty navigation wants.
        /// </summary>
        public void Draw(Graphics g, int x, int y, IList<NavRow> rows, int selected, Color iconColour)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            if (rows == null) return;

            _ox = x;
            _oy = y;

            var wasText = g.TextRenderingHint;
            var wasSmoothing = g.SmoothingMode;

            // Grey antialiasing rather than the machine's ClearType. A preview is looked at
            // beside the thing it previews, and subpixel fringes tuned for one background read
            // as a broken font on another.
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            Fill(g, new Rectangle(0, 0, NavChrome.NavWidth, HeightInCss(rows)), _theme.NavBack);

            // The mock's own margin, not the app's. In the app the tree's padding-top is 0 and
            // the 42px above the first row is the Site Map button, which the preview does not
            // draw - so this 8 is breathing room for a cropped picture, and claims nothing.
            var top = 8;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.IsGroup)
                {
                    Text(g, row.Label, NavChrome.NavGroupFontPx, FontStyle.Bold, _theme.SubText,
                        NavChrome.NavPadLeft, top, NavChrome.NavGroupHeight,
                        NavChrome.NavWidth - NavChrome.NavPadLeft * 2);

                    top += NavChrome.NavGroupHeight;
                    continue;
                }

                DrawRow(g, top, row, i == selected, iconColour);
                top += NavChrome.NavRowHeight;
            }

            g.TextRenderingHint = wasText;
            g.SmoothingMode = wasSmoothing;
        }

        private int HeightInCss(IList<NavRow> rows)
        {
            var height = 16; // the 8 above and the 8 below, as above
            foreach (var row in rows) height += row.IsGroup ? NavChrome.NavGroupHeight : NavChrome.NavRowHeight;
            return height;
        }

        private void DrawRow(Graphics g, int y, NavRow row, bool selected, Color iconColour)
        {
            if (selected)
            {
                Fill(g, new Rectangle(0, y, NavChrome.NavWidth, NavChrome.NavRowHeight), _theme.NavSelected);
                Fill(g, new Rectangle(NavChrome.AccentInset,
                        y + (NavChrome.NavRowHeight - NavChrome.AccentHeight) / 2,
                        NavChrome.AccentWidth, NavChrome.AccentHeight), _theme.Accent);
            }

            var iconY = y + (NavChrome.NavRowHeight - NavChrome.NavIconBox) / 2;
            Icon(g, row.Icon, NavChrome.NavPadLeft, iconY, NavChrome.NavIconBox,
                selected ? iconColour : _theme.IconColour);

            var textX = NavChrome.NavPadLeft + NavChrome.NavIconBox + NavChrome.NavGapToText;
            Text(g, row.Label, NavChrome.NavFontPx, FontStyle.Regular,
                _theme.Text, textX, y, NavChrome.NavRowHeight,
                NavChrome.NavWidth - textX - NavChrome.NavPadLeft);
        }

        // ----- the drawing itself, all in CSS pixels relative to the navigation -----

        private void Fill(Graphics g, Rectangle r, Color c)
        {
            var box = new Rectangle(_ox + At(r.X), _oy + At(r.Y), At(r.Width), At(r.Height));
            using (var brush = new SolidBrush(c)) g.FillRectangle(brush, box);
        }

        /// <summary>
        /// A label, bounded by its width and cut with an ellipsis when it will not fit - which
        /// is what the app does, and the reason a long table name is worth previewing at all.
        /// </summary>
        private void Text(Graphics g, string s, int px, FontStyle style, Color c,
            int x, int y, int height, int width)
        {
            using (var font = new Font(NavChrome.FontFamily, px * _scale, style, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(g, s, font,
                    new Rectangle(_ox + At(x), _oy + At(y), At(width), At(height)), c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }

        /// <summary>
        /// An icon in the square box the app gives it. A row with nothing to draw gets an
        /// outline rather than a guess: the environment has no icon for that table, and saying
        /// so is the point.
        /// </summary>
        private void Icon(Graphics g, SvgImage image, int x, int y, int box, Color colour)
        {
            var at = new RectangleF(_ox + x * _scale, _oy + y * _scale, box * _scale, box * _scale);


            if (image == null)
            {
                using (var pen = new Pen(Color.FromArgb(0x50, colour)))
                    g.DrawRectangle(pen, at.X, at.Y, at.Width - 1, at.Height - 1);
                return;
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            image.Draw(g, at, colour);
        }
    }
}
