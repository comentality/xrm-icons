using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Icons.Logic;

namespace Icons.Ui
{
    /// <summary>
    /// The chosen icon where it will actually be seen: in an app's navigation, at the size the
    /// app draws it, among the icons it will sit beside.
    ///
    /// It sits in the tool's own window rather than a window of its own, in the place a preview
    /// on two swatches used to be. A navigation is taller than the pane, which is why the
    /// drawing scrolls; what it buys for the height is the only preview that answers the
    /// question actually being asked, which is not "what does this drawing look like" but "does
    /// this row read, beside the rows it will be beside".
    ///
    /// It owns no connection: the app list and the navigation are handed to it, and asking for
    /// one is an event the control answers, so everything that can be slow stays where the tool
    /// already guards against it.
    /// </summary>
    public sealed class ContextPreview : UserControl
    {
        private readonly Surface _surface;
        private readonly ComboBox _appBox;
        private readonly CheckBox _darkBox;
        private readonly Label _noteLabel;

        private bool _filling;

        public ContextPreview()
        {
            _appBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _appBox.SelectedIndexChanged += App_Changed;

            _darkBox = new CheckBox
            {
                Text = "Dark",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(8, 3, 0, 0)
            };
            _darkBox.CheckedChanged += (s, e) =>
                _surface.Theme = _darkBox.Checked ? NavTheme.Dark : NavTheme.Light;

            // Named so a harness can find this one rather than whichever label comes first.
            // On its own line and wrapping, because what it has to say is a sentence - which
            // app this is, or that the navigation is invented - and the pane is narrow.
            _noteLabel = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Name = "note",
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(8, 0, 8, 4)
            };

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                Height = 30,
                Padding = new Padding(8, 4, 8, 0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            header.Controls.Add(new Label
            {
                Text = "App",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 4, 6, 0)
            }, 0, 0);
            header.Controls.Add(_appBox, 1, 0);
            header.Controls.Add(_darkBox, 2, 0);

            _surface = new Surface { Dock = DockStyle.Fill };

            Controls.Add(_surface);
            Controls.Add(_noteLabel);
            Controls.Add(header);
        }

        /// <summary>Raised when a navigation is wanted and only the caller can fetch it.</summary>
        public event EventHandler<AppNavigation> AppChosen;

        /// <summary>
        /// The icon being chosen, the name to give its row, and the colour it will be drawn in -
        /// null for the colour meaning it follows the app theme, which is not a colour this
        /// control has to be told: it is whatever the theme now showing resolves currentColor
        /// to. Passing the light theme's answer while the dark one is showing is how a preview
        /// draws a dark icon on a dark pane and calls it a preview.
        /// </summary>
        public void ShowChosen(SvgImage icon, string label, Color? fixedColour)
        {
            _surface.Chosen = icon;
            _surface.ChosenFixed = fixedColour;
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
        ///
        /// <paramref name="renameChosen"/> says whether the chosen row's caption is this tool's
        /// to write. In a stand-in it is - the row is there to hold the icon. In an app's own
        /// navigation it is not: the app's label for that table is the one that will be read
        /// beside the icon, and it is not always the table's name.
        /// </summary>
        public void ShowNavigation(IList<NavRow> rows, int selected, string note, bool renameChosen)
        {
            _surface.Rows = rows ?? new List<NavRow>();
            _surface.Selected = selected;
            _surface.RenameChosen = renameChosen;
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
        /// The drawing. A scrolling panel of its own so a navigation taller than the pane is
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
            }

            public IList<NavRow> Rows { get; set; }
            public int Selected { get; set; }
            public SvgImage Chosen { get; set; }
            public string ChosenLabel { get; set; }

            /// <summary>The colour asked for, or null when the icon follows the app theme.</summary>
            public Color? ChosenFixed { get; set; }

            public bool RenameChosen { get; set; }

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
            private float PixelScale
            {
                get { return DeviceDpi / 96f; }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;
                using (var brush = new SolidBrush(_theme.Page)) g.FillRectangle(brush, ClientRectangle);

                var painter = new NavPainter(_theme, PixelScale);
                var rows = Rows ?? new List<NavRow>();

                // The chosen row is filled in here rather than when the navigation arrived,
                // because the navigation is read once and the icon is chosen over and over. A
                // row that kept whatever was chosen when it was built would go stale on the
                // next click in the grid, which in an embedded preview is every click.
                if (Selected >= 0 && Selected < rows.Count && !rows[Selected].IsGroup)
                {
                    rows[Selected].Icon = Chosen;
                    if (RenameChosen && !string.IsNullOrEmpty(ChosenLabel)) rows[Selected].Label = ChosenLabel;
                }

                // The scroll position, so the navigation moves with the scrollbar.
                var x = AutoScrollPosition.X;
                var y = AutoScrollPosition.Y;

                painter.Draw(g, x, y, rows, Selected, ChosenFixed ?? _theme.IconColour);

                var navWidth = painter.Width;
                var navHeight = painter.Height(rows);

                // The navigation column runs to the bottom of the pane the way a real one does,
                // rather than stopping at its last entry in mid-air.
                if (y + navHeight < ClientSize.Height)
                {
                    using (var brush = new SolidBrush(_theme.NavBack))
                        g.FillRectangle(brush, x, y + navHeight, navWidth, ClientSize.Height - (y + navHeight));
                }

                using (var pen = new Pen(_theme.Line))
                    g.DrawLine(pen, x + navWidth, 0, x + navWidth, ClientSize.Height);

                Scenery(g, x + navWidth, y);

                AutoScrollMinSize = new Size(navWidth + Round(120), navHeight + Round(16));
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
                return (int)Math.Round(cssPixels * PixelScale);
            }
        }
    }
}
