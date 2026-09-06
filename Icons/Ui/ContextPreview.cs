using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Icons.Logic;

namespace Icons.Ui
{
    /// <summary>
    /// The chosen icon where it will actually be seen: in an app's navigation, at the size the
    /// app draws it, among the icons it will sit beside.
    ///
    /// A window of its own rather than a panel in the tool, because a navigation is as tall as
    /// the app makes it and the tool's right-hand pane is not. It owns no connection: the app
    /// list and the navigation are handed to it, and asking for one is an event the control
    /// answers, so everything that can be slow stays where the tool already guards against it.
    /// </summary>
    public sealed class ContextPreview : Form
    {
        private readonly Surface _surface;
        private readonly ToolStripComboBox _appBox;
        private readonly ToolStripButton _themeButton;
        private readonly ToolStripLabel _noteLabel;

        private bool _dark;
        private bool _filling;

        public ContextPreview()
        {
            Text = "The icon in context";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(560, 420);
            Size = new Size(720, 720);
            ShowInTaskbar = false;
            MinimizeBox = false;

            _appBox = new ToolStripComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 260,
                ToolTipText = "Which app's navigation to show the icon in"
            };
            _appBox.SelectedIndexChanged += App_Changed;

            _themeButton = new ToolStripButton("Dark")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true,
                ToolTipText = "The icon follows the app theme, so it is worth seeing on both"
            };
            _themeButton.CheckedChanged += (s, e) =>
            {
                _dark = _themeButton.Checked;
                _surface.Theme = _dark ? NavTheme.Dark : NavTheme.Light;
            };

            // Named so a harness can find this one rather than whichever label comes first.
            _noteLabel = new ToolStripLabel("") { ForeColor = SystemColors.GrayText, Name = "note" };

            var toolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                RenderMode = ToolStripRenderMode.System
            };
            toolbar.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripLabel("App"), _appBox,
                new ToolStripSeparator(),
                _themeButton,
                new ToolStripSeparator(),
                _noteLabel
            });

            _surface = new Surface { Dock = DockStyle.Fill };

            Controls.Add(_surface);
            Controls.Add(toolbar);
        }

        /// <summary>Raised when a navigation is wanted and only the caller can fetch it.</summary>
        public event EventHandler<AppNavigation> AppChosen;

        /// <summary>The icon being chosen, and the colour it will be drawn in.</summary>
        public void ShowIcon(SvgImage icon, string label, Color colour)
        {
            _surface.Chosen = icon;
            _surface.ChosenColour = colour;
            _surface.ChosenLabel = string.IsNullOrWhiteSpace(label) ? "This table" : label;
            _surface.Invalidate();
        }

        /// <summary>
        /// The apps this environment has. An empty list is not an error: the tool opens without
        /// a connection, and the stand-in navigation is what it shows until there is one.
        /// </summary>
        public void ShowApps(IReadOnlyList<AppNavigation> apps)
        {
            _filling = true;
            try
            {
                _appBox.Items.Clear();
                foreach (var app in apps ?? new List<AppNavigation>()) _appBox.Items.Add(app);
                _appBox.Enabled = _appBox.Items.Count > 0;
            }
            finally
            {
                _filling = false;
            }
        }

        /// <summary>
        /// The rows to draw, and which of them is the table being iconed. The note says where
        /// they came from, because a preview of an invented navigation and a preview of the
        /// environment's own are worth very different amounts and must never look alike.
        /// </summary>
        public void ShowNavigation(IList<NavRow> rows, int selected, string note)
        {
            _surface.Rows = rows ?? new List<NavRow>();
            _surface.Selected = selected;
            _noteLabel.Text = note ?? "";
            _surface.Invalidate();
        }

        public void ShowBusy(string what)
        {
            _noteLabel.Text = what ?? "";
            _appBox.Enabled = string.IsNullOrEmpty(what) && _appBox.Items.Count > 0;
        }

        private void App_Changed(object sender, EventArgs e)
        {
            if (_filling) return;

            var app = _appBox.SelectedItem as AppNavigation;
            if (app == null) return;

            var handler = AppChosen;
            if (handler != null) handler(this, app);
        }

        /// <summary>
        /// The drawing. A scrolling panel of its own so a navigation taller than the window is
        /// scrolled rather than cut, which for a long site map is most of them.
        /// </summary>
        private sealed class Surface : Panel
        {
            private NavTheme _theme = NavTheme.Light;

            public Surface()
            {
                AutoScroll = true;
                DoubleBuffered = true;
                ResizeRedraw = true;
                Rows = new List<NavRow>();
                ChosenColour = Color.FromArgb(0x24, 0x24, 0x24);
            }

            public IList<NavRow> Rows { get; set; }
            public int Selected { get; set; }
            public SvgImage Chosen { get; set; }
            public string ChosenLabel { get; set; }
            public Color ChosenColour { get; set; }

            public NavTheme Theme
            {
                get { return _theme; }
                set
                {
                    _theme = value ?? NavTheme.Light;
                    Invalidate();
                }
            }

            /// <summary>CSS pixels to this screen's pixels, so the drawing is the size the app is.</summary>
            private float Scale
            {
                get { return DeviceDpi / 96f; }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;
                using (var brush = new SolidBrush(_theme.Page)) g.FillRectangle(brush, ClientRectangle);

                var painter = new NavPainter(_theme, Scale);
                var rows = Rows ?? new List<NavRow>();

                // The scroll position, so the navigation moves with the scrollbar.
                var x = AutoScrollPosition.X;
                var y = AutoScrollPosition.Y;

                painter.Draw(g, x, y, rows, Selected, ChosenColour);

                var navWidth = painter.Width;
                var navHeight = painter.Height(rows);

                // The navigation column runs to the bottom of the window the way a real one
                // does, rather than stopping at its last entry in mid-air.
                if (y + navHeight < ClientSize.Height)
                {
                    using (var brush = new SolidBrush(_theme.NavBack))
                        g.FillRectangle(brush, x, y + navHeight, navWidth, ClientSize.Height - (y + navHeight));
                }

                using (var pen = new Pen(_theme.Line))
                    g.DrawLine(pen, x + navWidth, 0, x + navWidth, ClientSize.Height);

                Scenery(g, x + navWidth, y);

                AutoScrollMinSize = new Size(navWidth + Round(240), navHeight + Round(16));
            }

            /// <summary>
            /// Everything that is not the navigation, drawn as blank bars and nothing else. It
            /// is here so the navigation has a window to be the left edge of; it says nothing
            /// about where else an icon appears, because this preview does not know.
            /// </summary>
            private void Scenery(Graphics g, int left, int top)
            {
                var was = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var x = left + Round(24);
                var y = top + Round(28);

                using (var brush = new SolidBrush(_theme.Line))
                {
                    Pill(g, brush, x, y, Round(150), Round(14));
                    y += Round(40);

                    for (var i = 0; i < 40 && y < ClientSize.Height; i++)
                    {
                        Pill(g, brush, x, y, Round(i % 3 == 0 ? 180 : 130), Round(8));
                        Pill(g, brush, x + Round(220), y, Round(84), Round(8));
                        y += Round(30);
                    }
                }

                g.SmoothingMode = was;
            }

            private static void Pill(Graphics g, Brush brush, int x, int y, int w, int h)
            {
                if (w <= h) { g.FillRectangle(brush, x, y, w, h); return; }

                using (var path = new GraphicsPath())
                {
                    path.AddArc(x, y, h, h, 90, 180);
                    path.AddArc(x + w - h, y, h, h, 270, 180);
                    path.CloseFigure();
                    g.FillPath(brush, path);
                }
            }

            private int Round(int cssPixels)
            {
                return (int)Math.Round(cssPixels * Scale);
            }
        }
    }
}
