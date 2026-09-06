using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Icons.MockHarness
{
    /// <summary>
    /// The app window the navigation sits in.
    ///
    /// Only the navigation is drawn honestly. Everything to the right of it - the command bar,
    /// the grid - is scenery, and deliberately says nothing: blank bars, no icons, no record
    /// names. The icon appears where the icon appears, and the rest is there so the navigation
    /// has a window to be the left-hand edge of rather than being a strip in mid-air.
    /// </summary>
    internal static class Sheets
    {
        public static Bitmap AppScreen(Cast cast, Theme t, int scale, bool collapsed)
        {
            var nav = collapsed ? (MockContext)new SiteMapRail(cast) : new SiteMapExpanded(cast);

            const int barHeight = 40;
            var cssWidth = nav.Size.Width + 460;
            var cssHeight = System.Math.Max(nav.Size.Height + barHeight, 380);

            var sheet = new Bitmap(cssWidth * scale, cssHeight * scale);
            using (var g = Graphics.FromImage(sheet))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                using (var brush = new SolidBrush(t.Page)) g.FillRectangle(brush, 0, 0, sheet.Width, sheet.Height);

                // The command bar, so the window has a lid.
                using (var brush = new SolidBrush(t.NavBack))
                    g.FillRectangle(brush, 0, 0, sheet.Width, barHeight * scale);
                Rule(g, 0, barHeight * scale, sheet.Width, t.Line, scale);
                Label(g, "Dynamics 365", 14, FontStyle.Bold, t.Text, 16, 0, barHeight, scale);

                // The navigation, drawn true.
                nav.Render(g, 0, barHeight * scale, scale, t, cast);

                // The navigation column continues past its last entry to the bottom of the
                // window, the way a real one does.
                var navBottom = (barHeight + nav.Size.Height) * scale;
                using (var brush = new SolidBrush(t.NavBack))
                    g.FillRectangle(brush, 0, navBottom, nav.Size.Width * scale, sheet.Height - navBottom);
                using (var pen = new Pen(t.Line, scale))
                    g.DrawLine(pen, nav.Size.Width * scale, barHeight * scale, nav.Size.Width * scale, sheet.Height);

                // Scenery. Blank on purpose.
                var x = (nav.Size.Width + 24) * scale;
                Bar(g, x, (barHeight + 22) * scale, 150 * scale, 14 * scale, t.Line);

                var y = barHeight + 62;
                for (var i = 0; i < 9 && (y + 24) * scale < sheet.Height; i++)
                {
                    Bar(g, x, y * scale, (i % 3 == 0 ? 180 : 130) * scale, 8 * scale, t.Line);
                    Bar(g, x + 240 * scale, y * scale, 84 * scale, 8 * scale, t.Line);
                    Bar(g, x + 360 * scale, y * scale, 52 * scale, 8 * scale, t.Line);
                    y += 30;
                }
            }

            return sheet;
        }

        private static void Rule(Graphics g, int x, int y, int w, Color c, int scale)
        {
            using (var pen = new Pen(c, scale)) g.DrawLine(pen, x, y, x + w, y);
        }

        private static void Label(Graphics g, string s, int px, FontStyle style, Color c,
            int x, int y, int h, int scale)
        {
            using (var font = new Font(Chrome.FontFamily, px * scale, style, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, s, font,
                    new Rectangle(x * scale, y * scale, 4000, h * scale), c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private static void Bar(Graphics g, int x, int y, int w, int h, Color c)
        {
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(c))
            using (var path = new GraphicsPath())
            {
                path.AddArc(x, y, h, h, 90, 180);
                path.AddArc(x + w - h, y, h, h, 270, 180);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
            g.SmoothingMode = was;
        }
    }
}
