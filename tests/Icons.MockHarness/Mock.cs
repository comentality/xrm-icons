using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Icons.Logic;

namespace Icons.MockHarness
{
    /// <summary>One entry in the navigation: what it is called and what it is drawn with.</summary>
    internal sealed class NavEntry
    {
        public NavEntry(string label, SvgImage icon, bool group = false)
        {
            Label = label;
            Icon = icon;
            IsGroup = group;
        }

        public string Label;
        public SvgImage Icon;

        /// <summary>A group heading rather than a table: a caption, no icon, no highlight.</summary>
        public bool IsGroup;
    }

    /// <summary>
    /// The navigation the mock draws: the icon being chosen, and the entries around it.
    ///
    /// <see cref="Entries"/> is a stand-in cast of fixed names and library icons. Connected,
    /// these would be the environment's own tables - the load already reads every table's
    /// display name and IconVectorName - so the neighbours can become real without a new
    /// round trip. The order they appear in cannot: that lives in an app's site map, which
    /// nothing here reads yet.
    /// </summary>
    internal sealed class Cast
    {
        public SvgImage Chosen;
        public string ChosenLabel = "Accounts";

        /// <summary>Which entry is the table being iconed.</summary>
        public int ChosenAt = 4;

        public List<NavEntry> Entries = new List<NavEntry>();
    }

    /// <summary>
    /// One way of drawing the navigation. Each knows its size in CSS pixels and draws itself at
    /// a given origin and scale.
    /// </summary>
    internal abstract class MockContext
    {
        public abstract string Title { get; }

        /// <summary>Size in CSS pixels, before any scaling.</summary>
        public abstract Size Size { get; }

        protected abstract void Draw(Graphics g, Theme t, Cast cast);

        // Where and how big this is being drawn. Not a Graphics transform: TextRenderer paints
        // through GDI, which knows nothing about the transform, so a translated context moved
        // its icons and left every label behind at the top of the sheet.
        //
        // The scale is the one the real preview will need too: CSS pixels multiplied by
        // DeviceDpi / 96, so the drawing is the physical size on screen that the app is. It is
        // not an upscale of a finished picture - text and curves are drawn at the final size.
        private int _ox, _oy, _scale = 1;

        public void Render(Graphics g, int x, int y, int scale, Theme t, Cast cast)
        {
            _ox = x;
            _oy = y;
            _scale = scale;

            // Grey antialiasing rather than the machine's ClearType. Subpixel fringes are tuned
            // for the screen they are drawn on, and these pixels go into a PNG that is looked at
            // somewhere else; on white they turn into orange and blue edges that read as a
            // broken font.
            var wasText = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            Draw(g, t, cast);

            g.TextRenderingHint = wasText;
            _ox = 0;
            _oy = 0;
            _scale = 1;
        }

        public Size ScaledSize(int scale)
        {
            return new Size(Size.Width * scale, Size.Height * scale);
        }

        // ----- drawing helpers. All take CSS pixels relative to the context. -----

        protected void Fill(Graphics g, Rectangle r, Color c)
        {
            var box = new Rectangle(_ox + r.X * _scale, _oy + r.Y * _scale, r.Width * _scale, r.Height * _scale);
            using (var brush = new SolidBrush(c)) g.FillRectangle(brush, box);
        }

        protected void Line(Graphics g, int x1, int y1, int x2, int y2, Color c)
        {
            using (var pen = new Pen(c, _scale))
                g.DrawLine(pen, _ox + x1 * _scale, _oy + y1 * _scale, _ox + x2 * _scale, _oy + y2 * _scale);
        }

        /// <summary>
        /// A line of text. A width bounds it, and a label too long for its width is cut with an
        /// ellipsis rather than allowed to run out of the panel - which is what the app does,
        /// and the reason a long table name is worth seeing in a preview at all.
        /// </summary>
        protected void Text(Graphics g, string s, int px, FontStyle style, Color c, int x, int y,
            int height, int width = 0)
        {
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            if (width > 0) flags |= TextFormatFlags.EndEllipsis;

            using (var font = new Font(Chrome.FontFamily, px * _scale, style, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(g, s, font,
                    new Rectangle(_ox + x * _scale, _oy + y * _scale,
                        (width > 0 ? width : 4000) * _scale, height * _scale), c, flags);
            }
        }

        /// <summary>An icon, in a square box of the size the app gives it.</summary>
        protected void Icon(Graphics g, SvgImage image, int x, int y, int box, Color colour)
        {
            var at = new RectangleF(_ox + x * _scale, _oy + y * _scale, box * _scale, box * _scale);

            if (image == null)
            {
                using (var pen = new Pen(Color.FromArgb(0x60, colour)))
                    g.DrawRectangle(pen, at.X, at.Y, at.Width - 1, at.Height - 1);
                return;
            }

            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            image.Draw(g, at, colour);
            g.SmoothingMode = was;
        }

        /// <summary>A blank rounded bar, standing in for content the mock does not spell out.</summary>
        protected void Bar(Graphics g, int x, int y, int w, int h, Color c)
        {
            var box = new Rectangle(_ox + x * _scale, _oy + y * _scale, w * _scale, h * _scale);
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(c))
            using (var path = Rounded(box, box.Height / 2))
                g.FillPath(brush, path);
            g.SmoothingMode = was;
        }

        protected static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(r); return path; }
            var d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>The navigation expanded, with the chosen table selected among its neighbours.</summary>
    internal sealed class SiteMapExpanded : MockContext
    {
        private readonly Cast _cast;

        public SiteMapExpanded(Cast cast)
        {
            _cast = cast;
        }

        public override string Title => "Site map";

        // Sized from the cast rather than a constant, so a longer navigation is taller rather
        // than clipped.
        public override Size Size
        {
            get
            {
                int rows = 0, groups = 0;
                foreach (var e in _cast.Entries)
                {
                    if (e.IsGroup) groups++;
                    else rows++;
                }

                return new Size(Chrome.NavWidth,
                    rows * Chrome.NavRowHeight + groups * Chrome.NavGroupHeight + 16);
            }
        }

        protected override void Draw(Graphics g, Theme t, Cast cast)
        {
            Fill(g, new Rectangle(0, 0, Size.Width, Size.Height), t.NavBack);

            var y = 8;
            for (var i = 0; i < cast.Entries.Count; i++)
            {
                var entry = cast.Entries[i];
                if (entry.IsGroup)
                {
                    Text(g, entry.Label, Chrome.NavGroupFontPx, FontStyle.Regular, t.SubText,
                        Chrome.NavPadLeft, y, Chrome.NavGroupHeight,
                        Chrome.NavWidth - Chrome.NavPadLeft * 2);
                    y += Chrome.NavGroupHeight;
                    continue;
                }

                Row(g, t, y, entry, i == cast.ChosenAt);
                y += Chrome.NavRowHeight;
            }
        }

        private void Row(Graphics g, Theme t, int y, NavEntry entry, bool selected)
        {
            if (selected)
            {
                Fill(g, new Rectangle(0, y, Chrome.NavWidth, Chrome.NavRowHeight), t.NavSelected);
                Fill(g, new Rectangle(0, y + 8, 3, Chrome.NavRowHeight - 16), t.Accent);
            }

            var iconY = y + (Chrome.NavRowHeight - Chrome.NavIconBox) / 2;
            Icon(g, entry.Icon, Chrome.NavPadLeft, iconY, Chrome.NavIconBox, t.IconColour);

            var textX = Chrome.NavPadLeft + Chrome.NavIconBox + Chrome.NavGapToText;
            Text(g, entry.Label, Chrome.NavFontPx, selected ? FontStyle.Bold : FontStyle.Regular,
                t.Text, textX, y, Chrome.NavRowHeight, Chrome.NavWidth - textX - Chrome.NavPadLeft);
        }
    }

    /// <summary>The same navigation collapsed to a rail, where the icon is all there is.</summary>
    internal sealed class SiteMapRail : MockContext
    {
        private readonly Cast _cast;

        public SiteMapRail(Cast cast)
        {
            _cast = cast;
        }

        public override string Title => "Site map, collapsed";

        public override Size Size
        {
            get
            {
                var rows = 0;
                foreach (var e in _cast.Entries) if (!e.IsGroup) rows++;
                return new Size(Chrome.RailWidth, rows * Chrome.NavRowHeight + 16);
            }
        }

        protected override void Draw(Graphics g, Theme t, Cast cast)
        {
            Fill(g, new Rectangle(0, 0, Size.Width, Size.Height), t.NavBack);

            var y = 8;
            for (var i = 0; i < cast.Entries.Count; i++)
            {
                var entry = cast.Entries[i];
                if (entry.IsGroup) continue;

                if (i == cast.ChosenAt)
                {
                    Fill(g, new Rectangle(0, y, Chrome.RailWidth, Chrome.NavRowHeight), t.NavSelected);
                    Fill(g, new Rectangle(0, y + 8, 3, Chrome.NavRowHeight - 16), t.Accent);
                }

                Icon(g, entry.Icon,
                    (Chrome.RailWidth - Chrome.RailIconBox) / 2,
                    y + (Chrome.NavRowHeight - Chrome.RailIconBox) / 2,
                    Chrome.RailIconBox, t.IconColour);

                y += Chrome.NavRowHeight;
            }
        }
    }
}
