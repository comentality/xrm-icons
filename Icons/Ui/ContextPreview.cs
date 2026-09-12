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

            // As tall as the box and the tickbox in it, rather than 26 pixels in a strip of 30:
            // those two numbers were the right ones for one font, and the box is a line of text
            // like everything else here.
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(8, 4, 8, 0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

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
            var named = string.IsNullOrWhiteSpace(label) ? "This table" : label;

            // Called after every change the window makes, most of which are not changes to any
            // of these three - and repainting a navigation costs a font per row.
            if (ReferenceEquals(_surface.Chosen, icon)
                && _surface.ChosenFixed == fixedColour
                && _surface.ChosenLabel == named) return;

            _surface.Chosen = icon;
            _surface.ChosenFixed = fixedColour;
            _surface.ChosenLabel = named;
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
            _surface.ScrollToChosen();
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
        /// The drawing. A navigation is taller than the pane - the stand-in is 382 css pixels of
        /// rows in about 236, and a real site map is three or four times that - so it scrolls,
        /// on the wheel, with no bar.
        ///
        /// The bar went because of the shape rather than the other way round: a torn edge with a
        /// rectangular Windows control floating in the grey beside it reads as a bug. What the
        /// bar was saying, the tear now says - a ragged foot is why the rows stop - and the row
        /// being judged is scrolled to anyway, which was the other half of it.
        /// </summary>
        private sealed class Surface : Panel
        {
            // The tear's own numbers, in css pixels. They are the mock's furniture and not the
            // app's, like Scenery's bars: nothing here was measured off anything, and nothing
            // here belongs in NavChrome beside the numbers that were.
            private const int TearInset = 38;       // where the right edge tears, in from the right
            private const int TearAmplitude = 9;    // how ragged. +-4 reads as a printing fault, +-16 eats the page
            private const int TearStep = 16;        // a point every 16px: at this spacing corners read as fibre
            private const int TearFoot = 22;        // where the bottom edge tears, up from the foot
            // The foot is the same tear carried round the corner, so it wants the same ratio of
            // step to amplitude. Stepping further for a shallower wander reads as a wave.
            private const int TearFootAmplitude = 9;
            private const int TearFootStep = 20;

            // GDI+ has no blur, so the shadow is three copies of the tear offset under it. The
            // alphas add up where they overlap, which is what gives the falloff.
            private static readonly int[] LiftBy = { 1, 2, 3 };
            private static readonly int[] LiftAlpha = { 0x16, 0x14, 0x10 };

            private NavTheme _theme = NavTheme.Light;
            private IList<NavRow> _rows;
            private int _offset;

            // The scroll a shrinking pane took to keep the chosen row above the tear, and where
            // the drawing sat before it took it - so a window grown back gives it up again. Two
            // numbers rather than a flag because the offset is also the proof: while it is still
            // the one this pane set, nobody has wheeled since, and it is still this pane's to
            // undo. _fitAt of -1 is nothing owed.
            private int _fitAt = -1;
            private int _fitFrom;

            public Surface()
            {
                AutoScroll = false;
                DoubleBuffered = true;
                ResizeRedraw = true;

                // Clicking it can focus it, for the machines where hovering does not deliver the
                // wheel. Windows has scrolled whatever is under the pointer since 10, so this is
                // the fallback and not the path.
                SetStyle(ControlStyles.Selectable, true);

                _rows = new List<NavRow>();
            }

            /// <summary>
            /// The rows, and a scroll position that cannot outlive them. A long navigation
            /// scrolled down and then replaced by a short one would otherwise leave the pane
            /// showing blank page under a tear.
            /// </summary>
            public IList<NavRow> Rows
            {
                get { return _rows; }
                set
                {
                    _rows = value ?? new List<NavRow>();
                    Clamp();
                }
            }
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

            /// <summary>
            /// Brings the chosen row into view, with the rows around it for company. A real site
            /// map is forty rows where the stand-in is ten, and a preview whose whole point is
            /// the row you are choosing for is worth nothing if that row is below the fold of a
            /// pane this size. A row already in view is left where it is: scrolling under
            /// somebody who can see what they asked for is its own kind of wrong.
            /// </summary>
            public void ScrollToChosen()
            {
                BringChosenIntoView(true);
            }

            /// <summary>
            /// The chosen row back above the fold, centred when a navigation has just arrived
            /// and by the smallest amount that will do when the pane is being resized.
            ///
            /// A pane that changes height changes what is in view without the drawing moving an
            /// inch, which is why this is not only a job for a new navigation: XrmToolBox
            /// restored down from maximised takes the height off this pane first, and the row
            /// the whole preview exists for was the one that went under the tear.
            ///
            /// Which is also why a resize nudges rather than centres. A drag that answered every
            /// pixel of the splitter with half a pane of scroll would read as the drawing
            /// fighting the mouse; moving it by just what the shrinking edge took keeps the row
            /// on the same side of the tear it was on. And it is given back on the way up: a
            /// window maximised again would otherwise keep the top of the navigation cut off
            /// for a shortage of height that is over.
            /// </summary>
            private void BringChosenIntoView(bool centre)
            {
                var rows = Rows;
                if (rows == null || Selected < 0 || Selected >= rows.Count) return;
                if (ClientSize.Height <= 0) return;

                if (centre) _fitAt = -1;
                else if (_fitAt >= 0 && _offset == _fitAt)
                {
                    // What a previous shrink took, handed back before anything is asked about
                    // what is in view - so the question is put to where the drawing belongs
                    // rather than to where this pane last had to put it.
                    _fitAt = -1;
                    Offset(_fitFrom);
                }

                var painter = new NavPainter(_theme, PixelScale);
                var top = painter.Top(rows, Selected);
                var height = painter.RowHeight;

                // In view means in view above the tear. A row in the torn foot has been half
                // eaten by it, which for the one row the whole preview exists for will not do.
                var seen = ClientSize.Height - Fold;

                int to;
                if (seen <= height)
                {
                    // A pane with no room for a whole row shows the row's head, where the icon
                    // is, rather than centring it and pushing the icon off the top instead.
                    to = top;
                }
                else if (top >= _offset && top + height <= _offset + seen)
                {
                    return;
                }
                else if (centre)
                {
                    // Centred, unless everything above the row fits beside it - a navigation
                    // short enough to be shown from its own top is shown from its own top,
                    // rather than having its head cut off to centre a row that was already
                    // going to be on the page.
                    to = top + height <= seen ? 0 : top - (seen - height) / 2;
                }
                else
                {
                    to = top < _offset ? top : top + height - seen;
                }

                var from = _offset;
                Offset(to);

                // What to hand back later, and the proof it is still this pane's to hand back.
                // An arrival owes nothing - a centred offset is where the drawing belongs -
                // except when the pane was too short to hold a row at all, which is a shortage
                // of height like any other and ends when the height comes back.
                if (_offset != from && (!centre || seen <= height))
                {
                    _fitFrom = centre ? 0 : from;
                    _fitAt = _offset;
                }
            }

            /// <summary>CSS pixels to this screen's pixels, so the drawing is the size the app is.</summary>
            private float PixelScale
            {
                get { return DeviceDpi / 96f; }
            }

            /// <summary>How much of the foot the tear has taken, and so is not worth scrolling into.</summary>
            private int Fold
            {
                get { return Round(TearFoot + TearFootAmplitude); }
            }

            /// <summary>
            /// How tall the drawing is. NavPainter.Height already carries the mock's own 16px
            /// margin; the fold is added because the last row has to be able to clear the tear,
            /// which is a different 16 pixels and not the same one twice.
            /// </summary>
            private int Reach
            {
                get
                {
                    var painter = new NavPainter(_theme, PixelScale);
                    return Math.Max(0, painter.Height(Rows) + Fold - ClientSize.Height);
                }
            }

            private void Offset(int to)
            {
                var was = _offset;
                _offset = Math.Max(0, Math.Min(Reach, to));
                if (_offset != was) Invalidate();
            }

            private void Clamp()
            {
                if (_offset > Reach) Offset(Reach);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);

                var lines = SystemInformation.MouseWheelScrollLines;
                var by = lines > 0
                    ? lines * new NavPainter(_theme, PixelScale).RowHeight
                    : Math.Max(1, ClientSize.Height - Fold);

                Offset(_offset - e.Delta * by / 120);

                // Handled, or the splitter this sits in gets the same turn of the wheel and the
                // whole tool pane moves under a preview that has just scrolled.
                var handled = e as HandledMouseEventArgs;
                if (handled != null) handled.Handled = true;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Focus();
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);

                // The pane just changed how much of the drawing is in view, so what is in view
                // has to be asked again. Clamping alone only stops the last row running out of
                // page; it says nothing about the one row this preview is for - and it runs
                // second, because moving the offset first would cost the fit its proof that
                // nobody has wheeled since.
                BringChosenIntoView(false);
                Clamp();
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

                // How far down the drawing has been wheeled. Negative, because it is where the
                // top of the drawing now sits rather than how far it has gone.
                const int x = 0;
                var y = -_offset;

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

                Tear(g);
            }

            /// <summary>
            /// Takes the drawing off the page: everything outside the torn edge painted back to
            /// the tool's own colour, and a shadow laid under the piece so it reads as lying on
            /// the tool rather than as a hole cut through it.
            ///
            /// It is done by filling rather than clipping, and that is the whole trick.
            /// Graphics.SetClip takes a region, a region is whole pixels, and a clipped tear
            /// comes out a staircase. A path filled with FillMode.Alternate is antialiased like
            /// anything else.
            /// </summary>
            private void Tear(Graphics g)
            {
                // A pane too small to hold the tear is left square. Better a rectangle than a
                // shape that has eaten the thing it was framing.
                if (ClientSize.Width <= Round(TearInset) * 2 || ClientSize.Height <= Round(TearFoot) * 3) return;

                var was = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var keep = TearPath(0, 0))
                {
                    using (var mask = new GraphicsPath(FillMode.Alternate))
                    {
                        mask.AddRectangle(new Rectangle(-1, -1, ClientSize.Width + 2, ClientSize.Height + 2));
                        mask.AddPath(keep, false);

                        // BackColor and not a grey of its own: whatever the tool is painted, the
                        // torn-away part has to be that, or it is not a piece cut out of the
                        // window - it is a coloured rectangle with a shape in it.
                        using (var brush = new SolidBrush(BackColor)) g.FillPath(brush, mask);
                    }

                    for (var i = 0; i < LiftBy.Length; i++)
                    {
                        using (var lifted = TearPath(Round(LiftBy[i]), Round(LiftBy[i])))
                        using (var crescent = new GraphicsPath(FillMode.Alternate))
                        {
                            // The shadow minus the piece, so none of it falls on the drawing.
                            crescent.AddPath(lifted, false);
                            crescent.AddPath(keep, false);

                            using (var brush = new SolidBrush(Color.FromArgb(LiftAlpha[i], 0, 0, 0)))
                                g.FillPath(brush, crescent);
                        }
                    }
                }

                g.SmoothingMode = was;
            }

            /// <summary>
            /// The torn edge as a closed path: square along the top and the left, ragged down the
            /// right and along the foot. Offsetting it by a pixel or two is how the shadow under
            /// it is drawn - the two corners that sit on a square edge stay put, so the shadow
            /// never shows as a hairline along an edge that was never torn.
            /// </summary>
            private GraphicsPath TearPath(int dx, int dy)
            {
                var torn = TearPoints();
                var points = new PointF[torn.Length + 1];
                points[0] = new PointF(0, 0);

                for (var i = 0; i < torn.Length; i++)
                {
                    points[i + 1] = new PointF(
                        torn[i].X + (i == torn.Length - 1 ? 0 : dx),
                        torn[i].Y + (i == 0 ? 0 : dy));
                }

                var path = new GraphicsPath();
                path.AddPolygon(points);
                return path;
            }

            private PointF[] TearPoints()
            {
                var rightAxis = ClientSize.Width - Round(TearInset);
                var footAxis = ClientSize.Height - Round(TearFoot);
                var amplitude = Round(TearAmplitude);
                var footAmplitude = Round(TearFootAmplitude);
                var step = Math.Max(1, Round(TearStep));
                var footStep = Math.Max(1, Round(TearFootStep));

                var points = new List<PointF>();

                for (var at = 0; at < footAxis; at += step)
                    points.Add(new PointF(rightAxis + amplitude * Wobble(at / step), at));

                for (var at = rightAxis; at > 0; at -= footStep)
                    points.Add(new PointF(at, footAxis + footAmplitude * Wobble(2000 + at / footStep)));

                points.Add(new PointF(0, footAxis + footAmplitude * Wobble(1999)));
                return points.ToArray();
            }

            /// <summary>
            /// How far this point of the tear wanders, between -1 and 1. A function of the index
            /// and nothing else, which is the point: the tear is repainted on every click in the
            /// grid, and one that reshuffled itself would flicker the whole time somebody was
            /// choosing an icon.
            ///
            /// The index counts from the top of the pane rather than the top of the drawing,
            /// because the tear is the frame. The rows scroll under it and it does not move -
            /// the way a torn photograph does not change shape when you look further down it -
            /// and dragging the splitter lengthens the same tear rather than drawing another.
            /// </summary>
            private static float Wobble(int index)
            {
                unchecked
                {
                    var h = (uint)index * 2654435761u;
                    h ^= h >> 15;
                    h *= 2246822519u;
                    h ^= h >> 13;
                    return (h & 0xFFFF) / 32767.5f - 1f;
                }
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
