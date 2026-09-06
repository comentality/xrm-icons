using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Icons.Logic;

namespace Icons.MockHarness
{
    /// <summary>The icons a mock needs: the one being chosen, and the neighbours around it.</summary>
    internal sealed class Cast
    {
        public SvgImage Chosen;
        public string ChosenLabel = "Account";
        public Dictionary<string, SvgImage> Neighbours = new Dictionary<string, SvgImage>();

        public SvgImage Neighbour(string key)
        {
            SvgImage image;
            return Neighbours.TryGetValue(key, out image) ? image : null;
        }
    }

    /// <summary>
    /// One place in the app the icon is seen. Each knows how big it is and draws itself at the
    /// origin, so a sheet can stack them without knowing what they are.
    /// </summary>
    internal abstract class MockContext
    {
        public abstract string Title { get; }
        public abstract Size Size { get; }
        protected abstract void Draw(Graphics g, Theme t, Cast cast);

        // Where this context is being drawn. Not a Graphics transform: TextRenderer paints
        // through GDI, which knows nothing about the transform, so a translated context would
        // move its icons and leave its labels behind. Every helper adds the origin instead.
        private int _ox, _oy;

        public void Render(Graphics g, int x, int y, Theme t, Cast cast)
        {
            _ox = x;
            _oy = y;
            Draw(g, t, cast);
            _ox = 0;
            _oy = 0;
        }

        // ----- drawing helpers, shared. All take context coordinates. -----

        protected void Fill(Graphics g, Rectangle r, Color c)
        {
            r.Offset(_ox, _oy);
            using (var brush = new SolidBrush(c)) g.FillRectangle(brush, r);
        }

        protected void Border(Graphics g, Rectangle r, Color c)
        {
            r.Offset(_ox, _oy);
            using (var pen = new Pen(c)) g.DrawRectangle(pen, r);
        }

        protected void Line(Graphics g, int x1, int y1, int x2, int y2, Color c)
        {
            using (var pen = new Pen(c)) g.DrawLine(pen, x1 + _ox, y1 + _oy, x2 + _ox, y2 + _oy);
        }

        protected void Text(Graphics g, string s, int px, FontStyle style, Color c, int x, int y, int height)
        {
            using (var font = new Font(Chrome.FontFamily, px, style, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(g, s, font, new Rectangle(x + _ox, y + _oy, 4000, height), c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        protected static int TextWidth(Graphics g, string s, int px, FontStyle style)
        {
            using (var font = new Font(Chrome.FontFamily, px, style, GraphicsUnit.Pixel))
                return TextRenderer.MeasureText(g, s, font).Width;
        }

        /// <summary>An icon, centred in a square box of the size the app gives it.</summary>
        protected void Icon(Graphics g, SvgImage image, int x, int y, int box, Color colour)
        {
            x += _ox;
            y += _oy;

            if (image == null)
            {
                using (var pen = new Pen(Color.FromArgb(0x60, colour)))
                    g.DrawRectangle(pen, x, y, box - 1, box - 1);
                return;
            }

            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            image.Draw(g, new RectangleF(x, y, box, box), colour);
            g.SmoothingMode = was;
        }

        /// <summary>A blank rounded bar, standing in for text the mock does not need to spell out.</summary>
        protected void Bar(Graphics g, int x, int y, int w, int h, Color c)
        {
            x += _ox;
            y += _oy;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(c))
            using (var path = Rounded(new Rectangle(x, y, w, h), h / 2))
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

        protected void Chevron(Graphics g, int x, int y, Color c)
        {
            x += _ox;
            y += _oy;
            var was = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(c, 1.4f))
            {
                g.DrawLine(pen, x, y - 2, x + 4, y + 2);
                g.DrawLine(pen, x + 4, y + 2, x + 8, y - 2);
            }
            g.SmoothingMode = was;
        }
    }

    /// <summary>The left navigation, expanded, with the chosen table selected among its neighbours.</summary>
    internal sealed class SiteMapExpanded : MockContext
    {
        private static readonly string[] Standing = { "Home", "Recent", "Pinned" };

        public override string Title => "Site map";
        public override Size Size => new Size(Chrome.NavWidth, Chrome.NavRowHeight * 7 + 46);

        protected override void Draw(Graphics g, Theme t, Cast cast)
        {
            Fill(g, new Rectangle(0, 0, Size.Width, Size.Height), t.NavBack);

            var y = 8;
            foreach (var name in Standing)
            {
                Row(g, t, y, cast.Neighbour(name.ToLowerInvariant()), name, false);
                y += Chrome.NavRowHeight;
            }

            y += 8;
            Text(g, "Customers", 12, FontStyle.Regular, t.SubText, Chrome.NavPadLeft, y, 20);
            y += 22;

            Row(g, t, y, cast.Chosen, cast.ChosenLabel + "s", true);
            y += Chrome.NavRowHeight;
            Row(g, t, y, cast.Neighbour("contact"), "Contacts", false);
            y += Chrome.NavRowHeight;
            Row(g, t, y, cast.Neighbour("case"), "Cases", false);
            y += Chrome.NavRowHeight;
            Row(g, t, y, cast.Neighbour("order"), "Orders", false);
        }

        private void Row(Graphics g, Theme t, int y, SvgImage image, string label, bool selected)
        {
            if (selected)
            {
                Fill(g, new Rectangle(0, y, Chrome.NavWidth, Chrome.NavRowHeight), t.NavSelected);
                Fill(g, new Rectangle(0, y + 8, 3, Chrome.NavRowHeight - 16), t.Accent);
            }

            var iconY = y + (Chrome.NavRowHeight - Chrome.NavIconBox) / 2;
            Icon(g, image, Chrome.NavPadLeft, iconY, Chrome.NavIconBox, t.IconColour);

            var textX = Chrome.NavPadLeft + Chrome.NavIconBox + Chrome.NavGapToText;
            Text(g, label, Chrome.NavFontPx, selected ? FontStyle.Bold : FontStyle.Regular,
                t.Text, textX, y, Chrome.NavRowHeight);
        }
    }

    /// <summary>The same navigation collapsed to a rail, where the icon is all there is.</summary>
    internal sealed class SiteMapRail : MockContext
    {
        public override string Title => "Site map, collapsed";
        public override Size Size => new Size(Chrome.RailWidth, Chrome.NavRowHeight * 5 + 16);

        protected override void Draw(Graphics g, Theme t, Cast cast)
        {
            Fill(g, new Rectangle(0, 0, Size.Width, Size.Height), t.NavBack);

            var entries = new[]
            {
                cast.Neighbour("home"), cast.Chosen, cast.Neighbour("contact"),
                cast.Neighbour("case"), cast.Neighbour("order")
            };

            for (var i = 0; i < entries.Length; i++)
            {
                var y = 8 + i * Chrome.NavRowHeight;
                if (i == 1)
                {
                    Fill(g, new Rectangle(0, y, Chrome.RailWidth, Chrome.NavRowHeight), t.NavSelected);
                    Fill(g, new Rectangle(0, y + 8, 3, Chrome.NavRowHeight - 16), t.Accent);
                }

                Icon(g, entries[i],
                    (Chrome.RailWidth - Chrome.RailIconBox) / 2,
                    y + (Chrome.NavRowHeight - Chrome.RailIconBox) / 2,
                    Chrome.RailIconBox, t.IconColour);
            }
        }
    }

    /// <summary>The page header over a view, with a couple of grid rows under it for context.</summary>
    internal sealed class GridHeader : MockContext
    {
        public override string Title => "View header";
        public override Size Size => new Size(420, Chrome.HeaderHeight + 100);

        protected override void Draw(Graphics g, Theme t, Cast cast)
        {
            Fill(g, new Rectangle(0, 0, Size.Width, Size.Height), t.Page);

            var iconY = (Chrome.HeaderHeight - Chrome.HeaderIconBox) / 2;
            Icon(g, cast.Chosen, 16, iconY, Chrome.HeaderIconBox, t.IconColour);

            var title = "Active " + cast.ChosenLabel + "s";
            var textX = 16 + Chrome.HeaderIconBox + Chrome.HeaderGap;
            Text(g, title, Chrome.HeaderFontPx, FontStyle.Bold, t.Text, textX, 0, Chrome.HeaderHeight);

            var chevronX = textX + TextWidth(g, title, Chrome.HeaderFontPx, FontStyle.Bold) + 6;
            Chevron(g, chevronX, Chrome.HeaderHeight / 2, t.SubText);

            Line(g, 0, Chrome.HeaderHeight, Size.Width, Chrome.HeaderHeight, t.Line);

            // Column headings and two rows, so the header has something to be the top of.
            var y = Chrome.HeaderHeight + 10;
            Bar(g, 16, y, 90, 8, t.Line);
            Bar(g, 190, y, 70, 8, t.Line);
            Bar(g, 310, y, 60, 8, t.Line);
            y += 18;
            Line(g, 0, y, Size.Width, y, t.Line);

            for (var i = 0; i < 2; i++)
            {
                Text(g, i == 0 ? "Fabrikam, Inc." : "Contoso Ltd", Chrome.ListFontPx,
                    FontStyle.Regular, t.Accent, 16, y, 32);
                Bar(g, 190, y + 12, 84, 8, t.Line);
                Bar(g, 310, y + 12, 52, 8, t.Line);
                y += 32;
            }
        }
    }

    /// <summary>A lookup's results, where the icon is smallest and has to survive being tiny.</summary>
    internal sealed class LookupResults : MockContext
    {
        public override string Title => "Lookup results";
        public override Size Size => new Size(280, 32 + Chrome.ListRowHeight * 3 + 8);

        protected override void Draw(Graphics g, Theme t, Cast cast)
        {
            Fill(g, new Rectangle(0, 0, Size.Width, Size.Height), t.Page);
            Border(g, new Rectangle(0, 0, Size.Width - 1, Size.Height - 1), t.Line);

            Text(g, "Recent records", 12, FontStyle.Regular, t.SubText, Chrome.ListPadLeft, 4, 26);

            var names = new[] { "Fabrikam, Inc.", "Contoso Ltd", "Adventure Works" };
            for (var i = 0; i < names.Length; i++)
            {
                var y = 32 + i * Chrome.ListRowHeight;
                Icon(g, cast.Chosen, Chrome.ListPadLeft,
                    y + (Chrome.ListRowHeight - Chrome.ListIconBox) / 2,
                    Chrome.ListIconBox, t.IconColour);

                Text(g, names[i], Chrome.ListFontPx, FontStyle.Regular, t.Text,
                    Chrome.ListPadLeft + Chrome.ListIconBox + Chrome.ListGapToText, y, Chrome.ListRowHeight);
            }
        }
    }

    /// <summary>A record's own header.</summary>
    internal sealed class FormHeader : MockContext
    {
        public override string Title => "Form header";
        public override Size Size => new Size(420, Chrome.FormHeaderHeight + 44);

        protected override void Draw(Graphics g, Theme t, Cast cast)
        {
            Fill(g, new Rectangle(0, 0, Size.Width, Size.Height), t.Page);

            Icon(g, cast.Chosen, 16, 16, Chrome.FormIconBox, t.IconColour);

            var textX = 16 + Chrome.FormIconBox + Chrome.HeaderGap;
            Text(g, cast.ChosenLabel, Chrome.FormSubFontPx, FontStyle.Regular, t.SubText, textX, 10, 16);
            Text(g, "Fabrikam, Inc.", Chrome.FormTitleFontPx, FontStyle.Bold, t.Text, textX, 26, 28);

            Line(g, 0, Chrome.FormHeaderHeight, Size.Width, Chrome.FormHeaderHeight, t.Line);
            Bar(g, 16, Chrome.FormHeaderHeight + 18, 70, 8, t.Line);
            Bar(g, 110, Chrome.FormHeaderHeight + 18, 90, 8, t.Line);
            Bar(g, 224, Chrome.FormHeaderHeight + 18, 60, 8, t.Line);
        }
    }
}
