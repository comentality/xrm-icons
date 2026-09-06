using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Icons.MockHarness
{
    /// <summary>
    /// The candidate layouts. Each takes the same contexts and the same icon and arranges them
    /// differently, because the open question is not what to draw but how much of it to show
    /// at once and how close to look.
    /// </summary>
    internal static class Sheets
    {
        private const int Pad = 20;
        private const int CaptionHeight = 22;

        private static MockContext[] All()
        {
            return new MockContext[]
            {
                new SiteMapExpanded(), new SiteMapRail(), new GridHeader(),
                new LookupResults(), new FormHeader()
            };
        }

        // ===== A: every context, light beside dark =====

        /// <summary>
        /// One column per theme, every context stacked. The most information per pixel, and the
        /// only one that answers "does it hold up on dark" without a second look.
        /// </summary>
        public static Bitmap ContactSheet(Cast cast)
        {
            var contexts = All();
            var colWidth = 0;
            var height = Pad;
            foreach (var c in contexts)
            {
                colWidth = Math.Max(colWidth, c.Size.Width);
                height += CaptionHeight + c.Size.Height + Pad;
            }

            var width = Pad + colWidth + Pad + colWidth + Pad;
            var sheet = new Bitmap(width, height);
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(0xEC, 0xEC, 0xEC));

                var themes = new[] { Theme.Light, Theme.Dark };
                for (var t = 0; t < themes.Length; t++)
                {
                    var x = Pad + t * (colWidth + Pad);
                    var y = Pad;
                    foreach (var c in contexts)
                    {
                        Caption(g, c.Title + "  ·  " + themes[t].Name, x, y);
                        y += CaptionHeight;
                        Tile(g, c, themes[t], cast, x, y);
                        y += c.Size.Height + Pad;
                    }
                }
            }

            return sheet;
        }

        // ===== B: one composed screen =====

        /// <summary>
        /// A single fake app window: navigation down the left, a view header, a grid, and a
        /// lookup open over it. Every place the icon lands, landing at once, which is how a
        /// user meets it.
        /// </summary>
        public static Bitmap AppScreen(Cast cast, Theme t)
        {
            const int width = 900;
            const int height = 460;

            var sheet = new Bitmap(width, height);
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(t.Page);

                // A command bar across the top, so the window has a lid.
                using (var brush = new SolidBrush(t.NavBack))
                    g.FillRectangle(brush, 0, 0, width, 40);
                using (var pen = new Pen(t.Line)) g.DrawLine(pen, 0, 40, width, 40);
                DrawText(g, "Dynamics 365", 14, FontStyle.Bold, t.Text, 16, 0, 40);
                DrawText(g, "Sales Hub", 13, FontStyle.Regular, t.SubText, 130, 0, 40);

                // The navigation, drawn by the same context the sheets use.
                var nav = new SiteMapExpanded();
                using (Clip(g, 0, 40, nav.Size.Width, height - 40))
                    nav.Render(g, 0, 40, t, cast);
                using (var brush = new SolidBrush(t.NavBack))
                    g.FillRectangle(brush, 0, 40 + nav.Size.Height, nav.Size.Width, height);
                using (var pen = new Pen(t.Line))
                    g.DrawLine(pen, nav.Size.Width, 40, nav.Size.Width, height);

                // The view header and grid.
                var header = new GridHeader();
                header.Render(g, nav.Size.Width + 1, 40, t, cast);

                // Two more grid rows below what the header context draws, to fill the window.
                var rowY = 40 + header.Size.Height;
                for (var i = 0; i < 4 && rowY < height - 24; i++)
                {
                    DrawText(g, i % 2 == 0 ? "Northwind Traders" : "Litware, Inc.", 14,
                        FontStyle.Regular, t.Accent, nav.Size.Width + 17, rowY, 32);
                    Pill(g, nav.Size.Width + 191, rowY + 12, 84, 8, t.Line);
                    Pill(g, nav.Size.Width + 311, rowY + 12, 52, 8, t.Line);
                    rowY += 32;
                }

                // A lookup floating over the grid, where the icon is at its smallest.
                var lookup = new LookupResults();
                var lx = width - lookup.Size.Width - 40;
                var ly = 96;
                Shadow(g, lx, ly, lookup.Size.Width, lookup.Size.Height);
                lookup.Render(g, lx, ly, t, cast);
            }

            return sheet;
        }

        // ===== C: true size beside a magnifier =====

        /// <summary>
        /// Each context at its real size, and again at four times it. The real size says whether
        /// it reads; the magnification says why it does not.
        /// </summary>
        public static Bitmap Magnified(Cast cast, Theme t)
        {
            const int zoom = 3;
            const int sliceH = 40;
            var contexts = new MockContext[] { new SiteMapExpanded(), new LookupResults(), new GridHeader() };

            var width = Pad;
            var height = 0;
            foreach (var c in contexts)
            {
                width += c.Size.Width + Pad;
                height = Math.Max(height, c.Size.Height);
            }

            var top = CaptionHeight;
            var stripTop = top + height + Pad + CaptionHeight;
            var total = new Bitmap(width, stripTop + sliceH * zoom + Pad);
            using (var g = Graphics.FromImage(total))
            {
                g.Clear(Color.FromArgb(0xEC, 0xEC, 0xEC));

                var x = Pad;
                foreach (var c in contexts)
                {
                    Caption(g, c.Title + "  ·  actual size", x, top - CaptionHeight + 4);
                    Tile(g, c, t, cast, x, top);
                    x += c.Size.Width + Pad;
                }

                Caption(g, "the row with the icon in it, at " + zoom + "x", Pad, stripTop - CaptionHeight + 2);

                x = Pad;
                foreach (var c in contexts)
                {
                    // The slice is as wide as the tile divided by the zoom, so the magnified
                    // strip sits exactly under the thing it magnifies rather than running off
                    // the sheet.
                    var srcW = Math.Max(8, c.Size.Width / zoom);
                    using (var piece = new Bitmap(srcW, sliceH))
                    {
                        using (var pg = Graphics.FromImage(piece))
                        {
                            pg.Clear(t.Page);
                            c.Render(pg, 0, -TopRowOf(c), t, cast);
                        }

                        var box = new Rectangle(x, stripTop, srcW * zoom, sliceH * zoom);
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(piece, box);
                        using (var pen = new Pen(Color.FromArgb(0xA0, 0xA0, 0xA0))) g.DrawRectangle(pen, box);
                    }

                    x += c.Size.Width + Pad;
                }
            }

            return total;
        }

        /// <summary>Where in each context the row worth magnifying starts.</summary>
        private static int TopRowOf(MockContext c)
        {
            if (c is SiteMapExpanded) return Chrome.NavRowHeight * 3 + 38;
            if (c is LookupResults) return 32;
            return 0;
        }

        // ===== shared furniture =====

        private static void Tile(Graphics g, MockContext c, Theme t, Cast cast, int x, int y)
        {
            using (var shadow = new SolidBrush(Color.FromArgb(0x22, 0, 0, 0)))
                g.FillRectangle(shadow, x + 1, y + 1, c.Size.Width, c.Size.Height);

            c.Render(g, x, y, t, cast);

            using (var pen = new Pen(Color.FromArgb(0xC8, 0xC6, 0xC4)))
                g.DrawRectangle(pen, x, y, c.Size.Width, c.Size.Height);
        }

        private static void Caption(Graphics g, string s, int x, int y)
        {
            using (var font = new Font(Chrome.FontFamily, 12, FontStyle.Regular, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, s, font, new Point(x, y), Color.FromArgb(0x50, 0x50, 0x50));
        }

        private static void DrawText(Graphics g, string s, int px, FontStyle style, Color c, int x, int y, int h)
        {
            using (var font = new Font(Chrome.FontFamily, px, style, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, s, font, new Rectangle(x, y, 4000, h), c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private static void Pill(Graphics g, int x, int y, int w, int h, Color c)
        {
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(c))
            using (var path = new GraphicsPath())
            {
                var d = h;
                path.AddArc(x, y, d, d, 90, 180);
                path.AddArc(x + w - d, y, d, d, 270, 180);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
            g.SmoothingMode = was;
        }

        private static void Shadow(Graphics g, int x, int y, int w, int h)
        {
            for (var i = 6; i >= 1; i--)
                using (var brush = new SolidBrush(Color.FromArgb(6, 0, 0, 0)))
                    g.FillRectangle(brush, x - i, y - i + 2, w + i * 2, h + i * 2);
        }

        private static IDisposable Clip(Graphics g, int x, int y, int w, int h)
        {
            var state = g.Save();
            g.SetClip(new Rectangle(x, y, w, h));
            return new Restore(g, state);
        }

        private sealed class Restore : IDisposable
        {
            private readonly Graphics _g;
            private readonly GraphicsState _state;
            public Restore(Graphics g, GraphicsState state) { _g = g; _state = state; }
            public void Dispose() { _g.Restore(_state); }
        }
    }
}
