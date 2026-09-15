using System;
using System.Drawing;
using System.Windows.Forms;
using Icons.Catalog;
using Icons.Logic;
using Icons.Ui;
using Label = System.Windows.Forms.Label;

namespace Icons
{
    /// <summary>
    /// The window, built in code rather than by the designer so that what it does and how it is
    /// laid out sit next to each other and neither can drift.
    /// </summary>
    public partial class IconsControl
    {
        /// <summary>
        /// The icon in a grid cell, in pixels of the screen rather than lines of text. A drawing
        /// does not grow because somebody chose a bigger font; it grows because the screen it is
        /// drawn on is finer, which is what DeviceDpi answers. <see cref="ApplyMetrics"/> keeps it
        /// there.
        /// </summary>
        private int _thumbnailSize = 40;

        /// <summary>
        /// What the family line under a name costs in cell height when it is showing. A line of
        /// text, so it is the font's height and not a number.
        /// </summary>
        private int _familyLineHeight = 15;

        /// <summary>Everything above the grid: the search box and the two rows of filter buttons.</summary>
        private Panel _libraryHeader;

        /// <summary>
        /// The two rows in the header that do not wrap, which are what the library pane's width
        /// is asked of. The row of families is left out on purpose: it is as wide as six spelled
        /// out names and it is built to fold rather than to be given the room, so letting it
        /// have a say would hand the library a third of the window it does not need.
        /// </summary>
        private Control _searchRow;
        private Control _weightRow;

        /// <summary>Where the two rows the form has to be told the height of sit among its rows,
        /// so they can be told again in a new font. Every other row is as tall as what stands
        /// in it.</summary>
        private const int TableRow = 3;
        private const int SolutionRow = 6;

        private void BuildUi()
        {
            SuspendLayout();

            Dock = DockStyle.Fill;
            BackColor = SystemColors.Window;

            var toolbar = BuildToolbar();

            // A SplitContainer starts 150 pixels wide, whatever it is about to be docked into,
            // and refuses any minimum or distance that does not fit that. So the proportions
            // are set on the first layout that has a real width, not here.
            _body = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };

            _body.Panel1.Controls.Add(BuildLibraryPane());
            _body.Panel2.Controls.Add(BuildTargetPane());

            Controls.Add(_body);
            Controls.Add(toolbar);

            SizeChanged += (s, e) => LaySplitter();

            ResumeLayout(false);
            PerformLayout();

            ApplyMetrics();
            LaySplitter();

            // The library is inside this assembly, so it opens without a connection and the
            // window has something in it from the first moment.
            LoadCatalog();

            // And once it is open the buttons can be drawn out of it.
            DressButtons();

            // And so does the preview: a navigation nobody's environment ever had, saying so.
            ShowStandIn("A stand-in navigation. Only your icon is real.");

            SyncEnabled();
        }

        /// <summary>
        /// Gives every size that is written in pixels rather than measured the value it has in
        /// the font the window is actually drawn in.
        ///
        /// Nothing asks for this in XrmToolBox as it ships. Its manifest asks for "Per-monitor",
        /// which is not one of the strings Windows parses - "true", "true/pm", "per monitor" -
        /// so the process is DPI-unaware and Windows bitmap-stretches the whole window at 150%:
        /// blurry, but laid out exactly as at 100%. This is for when that stops being true.
        /// Somebody who ticks "Override high DPI scaling behaviour" on XrmToolBox.exe - the
        /// standing remedy for a blurry app, and what the reporter of MscrmTools/XrmToolBox#1232
        /// did - gets a real 144 dpi today, and #1260 would give it to everybody. Then the font
        /// grows and a row written down as 28 pixels clips the box standing in it.
        ///
        /// Two scales, because there are two kinds of size here. What holds text is measured in
        /// the font - and mostly by the layout itself, which is why there is less to do here than
        /// there are numbers in this file. What is a drawing is measured against the screen.
        /// </summary>
        private void ApplyMetrics()
        {
            if (_form == null || _libraryHeader == null) return;

            _form.SuspendLayout();
            _libraryHeader.SuspendLayout();

            try
            {
                _familyLineHeight = Font.Height;
                _thumbnailSize = (int)Math.Round(40 * DeviceDpi / 96f);

                // The filter buttons, each as wide as the word standing on it. This is the same
                // job the two boxes' columns used to be given here and an easier one: a
                // drop-down had to be made wide enough for a word it was not showing.
                foreach (var button in _familyButtons) SizeToggle(button);
                SizeToggle(_outlineButton);
                SizeToggle(_solidButton);

                // Seven rows of list - see where the row is added for why seven.
                _form.RowStyles[TableRow].Height = Font.Height * 7 + 6;

                // The one line above the form, and the air around it.
                _iconLabel.Height = Font.Height + 7;

                // The solution row is told its height rather than asked for it. A ComboBox is the
                // one control here that will not be made shorter than it wants to be and will
                // not say so when asked: a row as tall as what stands in it comes out the height
                // of the box with no room for its margins, and the box hangs three pixels into
                // the row below. The two boxes in the filter row are saved from it by the
                // captions beside them, which are taller.
                _form.RowStyles[SolutionRow].SizeType = SizeType.Absolute;
                _form.RowStyles[SolutionRow].Height = Font.Height + 15;

                // Square, and the height of the box it fills. The swatch is a colour rather than
                // a word, but it sits on a line of them and a line is what it has to match.
                _loadButton.Size = new Size(Font.Height + 8, Font.Height + 8);
                _colourButton.Size = new Size(Font.Height * 2 + 10, Font.Height + 7);

                // The cells are the thumbnail plus however many lines of text go under it, so
                // both halves of that have just changed. The thumbnails already drawn are the
                // old size.
                ClearThumbnails();
                SizeCells();
            }
            finally
            {
                _libraryHeader.ResumeLayout(false);
                _form.ResumeLayout(true);
            }
        }

        /// <summary>
        /// The host handing down a different font, which is what a change of DPI looks like from
        /// in here - and what the UI harness does to stand in for one, since no machine it runs
        /// on can make XrmToolBox report anything but 96 dpi.
        /// </summary>
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);

            // A font of its own, so it is not carried down with the rest.
            if (_iconLabel != null)
            {
                var replacing = _iconLabel.Font;
                _iconLabel.Font = new Font(Font, FontStyle.Bold);
                if (replacing != null) replacing.Dispose();
            }

            ApplyMetrics();

            // Both splitters' minimums were worked out in the old font and are too small for this
            // one, so they are worked out again. That does move a splitter somebody may have
            // dragged - but a window whose text has just changed size is being re-laid anyway,
            // and leaving the library pane at a width its own filter row no longer fits in is
            // not honouring their drag, it is clipping it.
            _splitterLaid = false;
            _targetSplitLaid = false;
            LaySplitter();
        }

        /// <summary>
        /// Gives the two panes their proportions once there is enough width to hold them, and
        /// then leaves the splitter between them alone so a drag is not undone by the next
        /// resize. The splitter inside the target pane is kept on the form's height instead -
        /// see <see cref="FitForm"/> - until somebody drags that one too.
        /// </summary>
        private void LaySplitter()
        {
            if (_body == null || _targetSplit == null) return;

            // The navigation is 200 CSS pixels wide before the page beside it, and this pane is
            // where it lives now, so the target side is wider than it was when it held a form.
            //
            // Each side is asked what it needs and held to the width it was tuned to at 96 dpi,
            // whichever is more. The numbers alone were right in one font: the filter row and the
            // form are both as wide as their own words, and in a bigger font they are wider than
            // any minimum written down here.
            //
            // The rows that wrap are not asked. A row of six families would say it wants about
            // 470 pixels and mean it, and every one of them would be a pixel taken off the
            // target side to spare the filter a second line it is built to take.
            var wantsLibrary = Math.Max(320, Math.Max(
                _searchRow.PreferredSize.Width,
                _weightRow.PreferredSize.Width) + 12);
            var wantsTarget = Math.Max(340, _form.PreferredSize.Width + 16);

            // Whether the window is laid out at all is still the old question asked of the old
            // two numbers. What the panes would like is bigger than those - 484 and 355 in the
            // font this was written in, and half as much again in a larger one - and a window
            // too narrow for the sum of that is a window to divide up as well as it can be, not
            // one to leave at a SplitContainer's fifty-pixel default. A restored-down
            // XrmToolBox is exactly that window.
            if (!_splitterLaid && _body.Width >= 320 + 340 + _body.SplitterWidth)
            {
                var spare = _body.Width - _body.SplitterWidth;
                var target = Math.Min(wantsTarget, spare - 320);
                var library = Math.Min(wantsLibrary, spare - target);

                _body.Panel1MinSize = library;
                _body.Panel2MinSize = target;
                _body.SplitterDistance = Math.Max(library, _body.Width - Math.Max(430, target + 90));
                _splitterLaid = true;
            }

            var previewMinimum = Math.Max(120, Font.Height * 8);
            var formMinimum = Math.Max(200, Font.Height * 13);

            if (!_targetSplitLaid && _targetSplit.Height >= formMinimum + previewMinimum + _targetSplit.SplitterWidth)
            {
                _targetSplit.Panel1MinSize = previewMinimum;
                _targetSplit.Panel2MinSize = formMinimum;
                _targetSplitLaid = true;
            }

            FitForm();
        }

        /// <summary>
        /// Gives the lower pane the height the form is actually asking for, so it opens whole and
        /// the preview takes what is over.
        ///
        /// Asking beats a constant: the form is ten rows at one font and ten rows at another, it
        /// grows a line when the chosen table is locked, and the warning line above it comes and
        /// goes. A number written here would be right on the machine it was measured on and a
        /// scroll bar everywhere else.
        ///
        /// Once the splitter has been dragged this does nothing. How much navigation is worth
        /// looking at is a question somebody has then answered, and the form scrolls instead.
        /// </summary>
        private void FitForm()
        {
            RefitSplit();

            if (!_targetSplitLaid || _targetSplitDragged || _layingTargetSplit) return;

            FitOnce();
        }

        /// <summary>
        /// One attempt at giving the form its height. Says whether it moved the splitter.
        /// </summary>
        private bool FitOnce()
        {
            // Only what is inside the splitter's lower pane. The button's strip and the red
            // line are docked to the target pane outside it, so they have already been taken
            // off _targetSplit.Height by the time this asks what room there is.
            //
            // What the form is about to be, not what it is: its rows are as tall as what stands
            // in them, so a change of width settles a layout before the height that follows from
            // it is in Height. Asked at the width it has, so the row that wraps is measured
            // wrapped.
            //
            // Added up rather than measured as the difference between the pane and the form's
            // host. The difference is the truer number and it cannot be used: it is wrong by
            // whatever the pane has not caught up with yet - see SettlePane - so a fit that
            // trusted it moved the splitter, which changed it, which asked for another fit.
            var chrome = _iconLabel.Height + (_warningLabel.Visible ? _warningLabel.Height : 0);
            var needed = _form.GetPreferredSize(new Size(_form.Width, 0)).Height + chrome;

            var room = _targetSplit.Height - _targetSplit.SplitterWidth;
            var distance = Math.Min(room - _targetSplit.Panel2MinSize, room - needed);
            if (distance < _targetSplit.Panel1MinSize) distance = _targetSplit.Panel1MinSize;
            if (distance > room - _targetSplit.Panel2MinSize) return false;
            if (_targetSplit.SplitterDistance == distance) return false;

            _layingTargetSplit = true;
            try
            {
                _targetSplit.SplitterDistance = distance;
            }
            finally { _layingTargetSplit = false; }

            SettlePane();

            return true;
        }

        /// <summary>
        /// Makes what is docked in the lower pane catch up with a splitter that has just moved.
        ///
        /// The same fixed-Panel2 habit <see cref="RefitSplit"/> is about, one level further in.
        /// Moving the splitter resizes the pane and leaves the panel's own children the height
        /// they had: the pane reads 381 and the form's host is still 317, so a form that has
        /// just been given the room it asked for grows a scroll bar in a host that never heard
        /// about it. Asking the panel to lay out settles it - but not from in here. Every path
        /// to this point is already inside a layout, where the request is dropped, so it is
        /// posted instead and runs when that layout is over.
        /// </summary>
        private void SettlePane()
        {
            if (!IsHandleCreated || IsDisposed || _settlingPane) return;

            _settlingPane = true;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || _targetSplit == null) { _settlingPane = false; return; }

                // Under the same flag the splitter's own moves are made, so the layout this
                // sets off cannot come back round as another fit. Catching up is not new
                // information; without this the two of them run for as long as you let them.
                _layingTargetSplit = true;
                try { _targetSplit.Panel2.PerformLayout(); }
                finally
                {
                    _layingTargetSplit = false;
                    _settlingPane = false;
                }
            }));
        }

        /// <summary>
        /// Puts the lower pane back inside the splitter on the resizes where WinForms leaves it
        /// hanging over the edge.
        ///
        /// A SplitContainer fixed on Panel2 answers a change of height by moving the splitter
        /// and leaving Panel2 the height it had. Once the splitter is down on Panel1MinSize
        /// there is nothing left to move, and from there the container goes on shrinking while
        /// the panel does not: its foot ends up below the window, taking whatever is docked
        /// there with it. Growing back does not undo it either - the panel is still the size it
        /// was, so it stays too big until the window is tall enough to hold it again.
        ///
        /// The distance setter re-measures both panels, but only when the value it is given is
        /// not the one already set - so the way to ask for a re-measure is to change it, one
        /// pixel out and back. The setter clamps what it is given to the two minimums, which is
        /// what does the work: the panel comes back inside the pane and the preview above gives
        /// up the height, down to the form's own minimum.
        ///
        /// This runs even when the splitter has been dragged. Where the form sits is somebody's
        /// answer to a question; hanging off the bottom of the window is not an answer.
        /// </summary>
        private void RefitSplit()
        {
            if (_targetSplit == null || _layingTargetSplit) return;

            var fits = _targetSplit.Height - _targetSplit.SplitterDistance - _targetSplit.SplitterWidth;
            if (_targetSplit.Panel2.Height == fits) return;

            var distance = _targetSplit.SplitterDistance;

            _layingTargetSplit = true;
            try
            {
                _targetSplit.SplitterDistance = distance + 1;
                _targetSplit.SplitterDistance = distance;
            }
            finally { _layingTargetSplit = false; }
        }

        private ToolStrip BuildToolbar()
        {
            // Tooltips rather than captions, because what they would take back is a table and
            // an icon by name and that changes with every write. SyncUndo keeps them true.
            _undoButton = new ToolStripButton("Undo");
            _undoButton.Click += (s, e) => Undo();

            _redoButton = new ToolStripButton("Redo");
            _redoButton.Click += (s, e) => Redo();

            _cancelButton = new ToolStripButton("Cancel")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Stop before the next call. The one already sent cannot be recalled."
            };
            _cancelButton.Click += (s, e) => CancelWorker();

            _aboutButton = new ToolStripButton("About")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Alignment = ToolStripItemAlignment.Right,
                ToolTipText = "What this is, which build you have, and where to say it is wrong."
            };
            _aboutButton.Click += (s, e) => ShowAbout();

            _statusLabel = new ToolStripLabel("") { ForeColor = SystemColors.GrayText };

            var toolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                RenderMode = ToolStripRenderMode.System
            };
            // What is left up here is what is about the window rather than about the table in
            // it: taking a write back, stopping one that is out, what the last one did. Loading
            // and applying have gone to the two places they are about - the table list and the
            // foot of the form that fills in the write.
            toolbar.Items.AddRange(new ToolStripItem[]
            {
                _undoButton,
                _redoButton,
                new ToolStripSeparator(),
                _cancelButton,
                new ToolStripSeparator(),
                _statusLabel,
                _aboutButton
            });

            return toolbar;
        }

        /// <summary>
        /// Puts drawings on the three buttons that read better as one than as words: the reload,
        /// and the two that move through what has been written. They are drawn out of the library this tool is
        /// for, at the moment it opens, rather than kept as resources of their own - the icons
        /// are already in the assembly and a second copy of an arrow would be a second thing to
        /// keep true.
        ///
        /// Nothing here is required for the window to work. A library that would not open leaves
        /// the buttons as they were built, which is why each one carries its own words too.
        /// </summary>
        private void DressButtons()
        {
            var refresh = Glyph("arrow_clockwise");
            if (refresh != null) _loadButton.Image = refresh;

            Dress(_undoButton, "arrow_undo");
            Dress(_redoButton, "arrow_redo");
        }

        private void Dress(ToolStripButton button, string name)
        {
            var glyph = Glyph(name);
            if (glyph == null) return;

            button.Image = glyph;

            // The caption goes, and the text stays: it is what the button answers to from a
            // test bench and what a screen reader has to read out.
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
        }

        /// <summary>
        /// One icon out of the bundled library, at the size a button wants it, or null when the
        /// library has nothing by that name - which is a build that was made without it, and a
        /// window with far bigger problems than a wordless button.
        /// </summary>
        private Image Glyph(string name)
        {
            if (_catalog == null) return null;

            try
            {
                var entry = new IconEntry(IconFamily.Fluent, name, "regular", IconStyle.Regular);
                return SvgImage.Parse(_catalog.ReadSvg(entry)).Render(16, SystemColors.ControlText);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// One filter button: a check box drawn as a button, which is the one control in this
        /// toolkit that is lit or not lit and says so from across the room.
        ///
        /// Not AutoSize, which for this control is a trap: it measures the word and ignores
        /// the Padding meant to keep the word off the edges, so the button comes out the width
        /// of the text and then the text is drawn inside the padding and clipped. Fluent came
        /// out reading "Flu". <see cref="SizeToggle"/> measures it instead.
        /// </summary>
        private static CheckBox Toggle(string text)
        {
            return new CheckBox
            {
                Text = text,
                AutoSize = false,
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 3, 4, 3)
            };
        }

        /// <summary>A filter button as wide as the word on it, in the font it is drawn in.</summary>
        private void SizeToggle(CheckBox button)
        {
            button.Size = new Size(
                TextRenderer.MeasureText(button.Text, Font).Width + Font.Height + 8,
                Font.Height + 10);
        }

        /// <summary>The word in front of a row of filter buttons, sat on the same line as they are.</summary>
        private static Label Caption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 4, 6, 0)
            };
        }

        /// <summary>
        /// A row of filter buttons that wraps rather than runs off the edge of a narrow pane.
        /// </summary>
        private static FlowLayoutPanel FilterRow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
        }

        private Control BuildLibraryPane()
        {
            _searchBox = new TextBox { Dock = DockStyle.Fill };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            _searchBox.KeyDown += Search_KeyDown;

            // Six families and two weights is few enough to put them all on the wall rather
            // than behind a list each. A list of eight things costs a click to find out what
            // is in it and a second to change your mind; eight buttons say what the library
            // holds without being asked, and say which of it you are looking at from across
            // the room. It also makes the filter a set rather than a choice - Fluent and
            // Lucide together was not something a drop-down could be asked for.
            _familyButtons.Clear();
            foreach (IconFamily family in Enum.GetValues(typeof(IconFamily)))
            {
                var button = Toggle(family.ToString());
                button.Tag = family;
                button.CheckedChanged += (s, e) => ApplyFilter();
                _familyButtons.Add(button);
            }

            _outlineButton = Toggle("Outline");
            _solidButton = Toggle("Solid");

            // Outline alone rather than both, because both means every icon twice: the same
            // drawing filled and unfilled, adjacent in the ranking, and a grid where half the
            // cells are the alternative to the cell beside them is a worse first look at the
            // library than one weight of it. Outline is the one a site map is drawn in.
            _outlineButton.Checked = true;
            _outlineButton.CheckedChanged += (s, e) => ApplyFilter();
            _solidButton.CheckedChanged += (s, e) => ApplyFilter();

            // A tick rather than one of the toggles beside it, and deliberately: the toggles
            // say what is in the grid and this says how it is drawn. Two questions that look
            // alike is how you end up clicking the wrong one.
            _familyToggle = new CheckBox
            {
                Text = "Show family",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _familyToggle.CheckedChanged += (s, e) => ShowFamilyChanged();

            _grid = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.LargeIcon,
                VirtualMode = true,
                OwnerDraw = true,
                MultiSelect = false,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Window,
                // No images ever go in this list. It is here because the cell size in an icon
                // view comes from the image list, and owner drawing does not change that.
                LargeImageList = CellSizer(false)
            };
            _grid.RetrieveVirtualItem += Grid_RetrieveVirtualItem;
            _grid.DrawItem += Grid_DrawItem;
            _grid.SelectedIndexChanged += Grid_SelectedIndexChanged;
            _grid.ItemActivate += (s, e) => { if (_applyButton.Enabled) Apply(); };

            // Three rows: the search on its own, the families, and the weights beside the one
            // thing here that changes how the grid is drawn rather than what is in it.
            //
            // The search row is a table, because a box that fills the pane has to be told to.
            // The other two are flow panels that wrap, and that is the whole reason the rows
            // are apart: six families spelled out is wider than this pane is ever guaranteed
            // to be, and a filter with two of its families off the right edge is worse than a
            // filter two rows tall. Each is docked to the top of a plain panel rather than
            // sitting in a table cell, because a flow panel only wraps against a width it has
            // been given, and docking is what gives it one.
            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            searchRow.Controls.Add(Caption("Search"), 0, 0);
            searchRow.Controls.Add(_searchBox, 1, 0);

            var familyRow = FilterRow();
            familyRow.Controls.Add(Caption("Family"));
            foreach (var button in _familyButtons) familyRow.Controls.Add(button);

            var weightRow = FilterRow();
            weightRow.Controls.Add(Caption("Weight"));
            weightRow.Controls.Add(_outlineButton);
            weightRow.Controls.Add(_solidButton);
            _familyToggle.Margin = new Padding(16, 3, 0, 3);
            weightRow.Controls.Add(_familyToggle);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6, 3, 6, 3)
            };

            // Added bottom row first: a docked control takes its edge from whatever is left
            // after the ones added after it, so the last one added is the topmost.
            header.Controls.Add(weightRow);
            header.Controls.Add(familyRow);
            header.Controls.Add(searchRow);

            _libraryHeader = header;
            _searchRow = searchRow;
            _weightRow = weightRow;

            var pane = new Panel { Dock = DockStyle.Fill };
            pane.Controls.Add(_grid);
            pane.Controls.Add(header);
            return pane;
        }

        private Control BuildTargetPane()
        {
            // The icon where it will be seen, in the place two big swatches of it used to be.
            // Light and dark side by side answered "does this drawing survive both themes";
            // this answers that too, through its own Dark box, and also the question the
            // swatches could not - whether the row reads beside the rows it will be beside, at
            // the size the app really draws it.
            _context = new ContextPreview { Dock = DockStyle.Fill };
            _context.AppChosen += Context_AppChosen;

            // One line, in whatever font it is drawn in - ApplyMetrics gives it the height - and
            // cut with an ellipsis rather than wrapped when the pane is too narrow for the name.
            //
            // Not AutoSize, though the two lines below it are. This line stands between the
            // splitter and the form, so its height is height the form does not get; a line that
            // silently becomes two at some widths moves the splitter under FitForm while FitForm
            // is working out where to put it.
            _iconLabel = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                AutoEllipsis = true,
                Text = "No icon chosen",
                Padding = new Padding(8, 3, 8, 0),
                Font = new Font(Font, FontStyle.Bold)
            };

            _warningLabel = new Label
            {
                Dock = DockStyle.Top,
                // Not AutoSize: a label that sizes itself measures its text as one line, and
                // docking then clamps that line to the pane - which is a sentence cut off
                // mid-word rather than a sentence wrapped. Its height is worked out from the
                // width it has instead, in SizeWarning.
                AutoSize = false,
                Visible = false,
                ForeColor = Color.FromArgb(0x8A, 0x53, 0x00),
                Padding = new Padding(8, 2, 8, 4)
            };

            // The pane hands the label its width, and the height follows from it: a drag that
            // narrows the form turns two lines into three.
            _warningLabel.SizeChanged += (sender, e) => SizeWarning();

            _tableFilterBox = new TextBox { Dock = DockStyle.Fill };
            _tableFilterBox.TextChanged += (s, e) => ShowTables();
            _tableFilterBox.KeyDown += TableFilter_KeyDown;

            // The common errand is "which of these looks unfinished", and two different things
            // have to be true for a table to qualify. Nothing stored on it - the load reads
            // IconVectorName with everything else - and no app drawing one for it either, which
            // the load asks the org about. A standard table is the second case: Account has
            // never had an icon stored on it, and has never looked as though it needed one.
            //
            // Ticked to begin with, because that errand is the reason anybody opens this tool:
            // the list of tables with nothing is the work, and the full list is what you ask for
            // when you have come to change one that is already there.
            _unsetOnlyBox = new CheckBox
            {
                Text = "Only tables with no icon yet",
                Checked = true,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _hints.SetToolTip(_unsetOnlyBox,
                "Tables with nothing stored on them and nothing drawn for them either.\r\n" +
                "A standard table like Account is on its stock icon, so it counts as\r\n" +
                "having one - untick to see every table, including those.");
            _unsetOnlyBox.CheckedChanged += (s, e) => ShowTables();

            // A table locked against customisation cannot be given an icon by anybody, so it is
            // not a candidate - and the unset-only list is where they gather, because a table
            // that will never take an icon has never been given one. Hidden by default: a list
            // that only offers what can be done with it is worth more than a complete one.
            // Untick it to see them, and the line under the box says why the one you picked is
            // a dead end.
            _hideLockedBox = new CheckBox
            {
                Text = "Hide the ones locked against customisation",
                Checked = true,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _hideLockedBox.CheckedChanged += (s, e) => ShowTables();

            // A list, not a dropdown. The box above it narrows and this one points, which are
            // two halves of one errand rather than two settings - and a dropdown hides the half
            // that is the answer. Open, how much is left to do is readable without a click, the
            // arrow keys walk it, and the filter survives being pointed at, which is what lets
            // the unset-only list be worked down one table at a time.
            _tableBox = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _tableBox.SelectedIndexChanged += Table_SelectedIndexChanged;

            // Beside the list it fills, rather than a caption at the top of the window. Reading
            // the environment is what puts tables in that box and nothing else does, so this is
            // where somebody looking at an empty box looks - and a circling arrow is the one
            // drawing that needs no caption anywhere it appears.
            _loadButton = new Button
            {
                Width = 23,
                Height = 23,
                Margin = new Padding(0, 1, 6, 0),
                Padding = new Padding(0),
                AccessibleName = "Load tables"
            };
            _loadButton.Click += (s, e) => LoadEverything();
            _hints.SetToolTip(_loadButton, "Read the tables and solutions from the connected environment");

            // Under the box it is about, rather than in the red line at the foot of the pane
            // with the round trips that failed. Nothing has gone wrong here: this is a fact
            // about the table just chosen, and it belongs where the choosing happened.
            _lockedLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Visible = false,
                ForeColor = Color.FromArgb(0xA4, 0x26, 0x2C),
                Padding = new Padding(0, 1, 0, 5)
            };

            // What the table is carrying now, under the box it was chosen in, beside the line
            // that says when it cannot carry anything. Grey rather than red: this is a fact
            // about the table, not a complaint about it. The link is how somebody gets to that
            // icon in the library when the filters or the search are not showing it - a click,
            // because re-searching the grid under somebody who is using it is not the tool's
            // to do unasked.
            _currentLabel = new LinkLabel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Visible = false,
                LinkArea = new LinkArea(0, 0),
                LinkBehavior = LinkBehavior.HoverUnderline,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(0, 1, 0, 5)
            };
            _currentLabel.LinkClicked += (s, e) => ShowCurrentIconInLibrary();

            _solutionBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _solutionBox.SelectedIndexChanged += (s, e) => { SuggestName(); SyncEnabled(); };

            _nameBox = new TextBox { Dock = DockStyle.Fill };
            _nameBox.TextChanged += (s, e) => SyncEnabled();

            _themeColour = new RadioButton
            {
                Text = "Follow the app theme",
                Checked = true,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _themeColour.CheckedChanged += Colour_ModeChanged;

            // The swatch sits on the same line as the radio button that turns it on, because it
            // is what "fixed colour" means: which colour, right there, rather than a row below
            // it reading as one more setting of its own.
            _fixedColour = new RadioButton
            {
                Text = "Fixed colour",
                AutoSize = true,
                Margin = new Padding(0, 3, 0, 0)
            };
            _fixedColour.CheckedChanged += Colour_ModeChanged;

            _colourButton = new Button
            {
                Text = "",
                Width = 40,
                Height = 22,
                BackColor = _colour,
                Margin = new Padding(10, 0, 0, 0)
            };
            _colourButton.Click += Colour_Pick;

            _rasterBox = new CheckBox
            {
                Text = "Also write the classic PNG icons",
                AutoSize = true,
                Margin = new Padding(0, 3, 0, 0)
            };

            // Off by default, and the answer to "should I tick this?" is a paragraph rather
            // than a caption. It goes behind a link, in the same dialog the icon licence uses -
            // and on the line of the box it is the answer for, which is where a question about
            // that box gets asked.
            _rasterHelp = new LinkLabel
            {
                Text = "When do the PNGs matter?",
                AutoSize = true,
                Margin = new Padding(10, 3, 0, 0)
            };
            _rasterHelp.LinkClicked += (s, e) => ShowRasterHelp();

            // At the end of the form rather than the top of the window, because it is the last
            // thing done to what the form says: a table, a solution, a name and a colour, and
            // then this. Bottom right is where a window that fills something in puts the button
            // that sends it.
            _applyButton = new Button
            {
                Text = "Set as table icon",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 1, 8, 1),
                Margin = new Padding(0)
            };
            _applyButton.Click += (s, e) => Apply();
            _hints.SetToolTip(_applyButton,
                "Store the chosen icon as a web resource, point the table at it, and publish");

            _troubleLabel = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Visible = false,
                ForeColor = Color.FromArgb(0xA4, 0x26, 0x2C),
                Padding = new Padding(8, 4, 8, 4)
            };

            // Sized to its rows and docked to the top of a host that scrolls, rather than filling
            // a host and scrolling itself. A form that scrolls itself is as wide as its widest row
            // wants to be, so a pane one scroll bar too narrow grows a second bar along the foot,
            // which takes another line of height, which lengthens the first bar. Docked to the
            // top it is exactly as wide as there is room for and only ever scrolls downwards.
            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 11,
                Padding = new Padding(8, 6, 8, 6)
            };
            // The captions' column is as wide as the longest caption, whatever font that is in.
            // At 86 pixels "Web resource" wrapped onto three lines in anything bigger than the
            // font it was measured in.
            form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(form, 0, "Find table", _tableFilterBox);
            AddRow(form, 1, "", _unsetOnlyBox);
            AddRow(form, 2, "", _hideLockedBox);
            // Seven rows of list, measured in the font it will be drawn in rather than written
            // down in pixels - see FitForm for why a number here would be right on one machine.
            // Seven is enough to show a filter narrowed to a handful as a handful, and few
            // enough that the navigation above it keeps most of the height.
            AddRow(form, 3, _loadButton, "Table", _tableBox, Font.Height * 7 + 6);
            AddNoteRow(form, 4, _lockedLabel);
            AddNoteRow(form, 5, _currentLabel);
            AddRow(form, 6, "Solution", _solutionBox);
            AddRow(form, 7, "Web resource", _nameBox);
            AddRow(form, 8, "Colour", _themeColour);
            AddRow(form, 9, "", Beside(_fixedColour, _colourButton, last: false));
            AddWrappingRow(form, 10, Beside(_rasterBox, _rasterHelp, last: true, wrap: true));

            _form = form;
            _form.SizeChanged += (s, e) => FitForm();

            _formHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            _formHost.Controls.Add(form);

            // Inside the pane that scrolls, above the form, rather than pinned in the chrome
            // over it. A warning that wraps is three or four lines, and pinned it took them off
            // the form every time - on a screen XrmToolBox is running unaware of, that is most
            // screens. Here the lines it takes are lines of the same scroll as the rest, and a
            // pane too short for both is a pane somebody scrolls rather than a form squeezed by
            // a sentence. It is added after the form, because what is added last docks first.
            _formHost.Controls.Add(_warningLabel);

            // Right, because that is the corner a window that fills something in keeps its last
            // button in, and its own strip below everything else so the red line that says why a
            // write failed reads above the button that would try it again.
            //
            // It is docked to the target pane itself rather than to the form's side of the
            // splitter, and that is load-bearing rather than tidy. A SplitContainer fixed on
            // Panel2 stops shrinking Panel2 once the splitter has been pushed down to
            // Panel1MinSize: from there the panel keeps the height it had and simply hangs off
            // the bottom of the window, and anything docked to its foot goes with it. On a
            // 1080p screen with the solution line showing, that starts at a target pane of
            // about 580 - a restored-down XrmToolBox - and the button went under the status
            // bar. Out here it is the last strip of the pane at any height, and what runs short
            // is the form, which scrolls.
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Padding = new Padding(8, 4, 8, 6)
            };
            actions.Controls.Add(_applyButton);

            // The two lines that are about the form - which icon is chosen, and what is worth
            // saying about the choice - stay with the form and come out of its height. The two
            // that are about the write - the red line and the button - do not: see the note on
            // the strip above.
            var below = new Panel { Dock = DockStyle.Fill };
            below.Controls.Add(_formHost);
            below.Controls.Add(_iconLabel);

            // The preview above, the form below, and a splitter between them, because how much
            // navigation is worth looking at is not the same question every time: a long site
            // map wants the height, and a table that is halfway through being named does not.
            _targetSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,

                // Growing the window lengthens the navigation above and leaves the form the
                // height it was given, which is the height it asked for.
                FixedPanel = FixedPanel.Panel2
            };
            _targetSplit.Panel1.Controls.Add(_context);
            _targetSplit.Panel2.Controls.Add(below);

            // SplitterMoving and not SplitterMoved: the second is raised by every set of the
            // distance, this one only by a hand on the splitter.
            _targetSplit.SplitterMoving += (s, e) => _targetSplitDragged = true;

            // Fill first and the strips after it: WinForms docks the last control in the
            // collection against the edge first, so the button owns the foot of the pane, the
            // red line sits directly above it, and the splitter gets what is left.
            var pane = new Panel { Dock = DockStyle.Fill };
            pane.Controls.Add(_targetSplit);
            pane.Controls.Add(_troubleLabel);
            pane.Controls.Add(actions);
            return pane;
        }

        private static void AddRow(TableLayoutPanel form, int row, string caption, Control control)
        {
            // As tall as what stands in it. A row written down as 28 pixels is a row that fits a
            // combo box in one font and clips it in the next one - see ApplyMetrics.
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (caption.Length > 0)
            {
                form.Controls.Add(new Label
                {
                    Text = caption,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Padding = new Padding(0, 5, 6, 0)
                }, 0, row);
            }
            form.Controls.Add(control, 1, row);
        }

        /// <summary>
        /// A row whose caption has a button in front of it. The button is about the row's own
        /// control - it is what fills the box beside it - so it goes in the caption column with
        /// the caption rather than trailing off the end of the row.
        /// </summary>
        private static void AddRow(TableLayoutPanel form, int row, Control before, string caption,
                                   Control control, int height)
        {
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

            var head = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                // Top, not centred: on a row as tall as a list, a caption floating down the
                // middle of it has stopped labelling the first thing in the list.
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            head.Controls.Add(before);

            head.Controls.Add(new Label
            {
                Text = caption,
                AutoSize = true,
                Margin = new Padding(0, 5, 6, 0)
            });

            form.Controls.Add(head, 0, row);
            form.Controls.Add(control, 1, row);
        }

        /// <summary>
        /// Two controls on one row: a toggle and the thing that belongs to it - the colour it
        /// would use, the paragraph that answers whether to tick it. On rows of their own they
        /// read as two more settings; side by side they read as the one setting they are.
        ///
        /// The last row of the form is the one the layout hands its leftover height to, so what
        /// sits in it is anchored to the top as well, or it floats down the middle of that
        /// height and reads as belonging to nothing.
        ///
        /// A wrapping pair is the widest thing in the form - a tickbox and the question it
        /// answers - and it is allowed to drop the second onto a line of its own when the pane is
        /// narrow, which is what keeps the form inside its own width at any width.
        /// </summary>
        private static Control Beside(Control first, Control second, bool last, bool wrap = false)
        {
            var pair = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = wrap,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Anchor = wrap
                    ? AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
                    : last ? AnchorStyles.Left | AnchorStyles.Top : AnchorStyles.Left
            };

            pair.Controls.Add(first);
            pair.Controls.Add(second);
            return pair;
        }

        /// <summary>
        /// A row that takes the height of what is in it, for the one row that is allowed to
        /// become two lines when there is not the width for one.
        /// </summary>
        private static void AddWrappingRow(TableLayoutPanel form, int row, Control control)
        {
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(control, 1, row);
        }

        /// <summary>
        /// A line that belongs to the row above it, in the same column as the box it is about.
        /// The row sizes to what is in it, so a hidden note takes no height at all rather than
        /// leaving a gap under the table box for the times there is nothing to say.
        /// </summary>
        private static void AddNoteRow(TableLayoutPanel form, int row, Control control)
        {
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(control, 1, row);
        }

        // ===== events =====

        /// <summary>
        /// Ctrl+Z and Ctrl+Y, wherever the focus is in this window - except inside a text box,
        /// where Ctrl+Z already means the thing it has always meant. Taking it over there would
        /// charge somebody a publish for wanting the web resource name they were halfway through
        /// typing back.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            // Only while there is something to take back. The key does what the button does,
            // including being nobody's when the button is dead: a window that has written
            // nothing has no business swallowing a keystroke meant for whatever is behind it.
            if (!(FocusedControl() is TextBoxBase))
            {
                if (keyData == (Keys.Control | Keys.Z) && _undoButton.Enabled) { Undo(); return true; }
                if (keyData == (Keys.Control | Keys.Y) && _redoButton.Enabled) { Redo(); return true; }
            }

            return base.ProcessCmdKey(ref message, keyData);
        }

        /// <summary>
        /// The control the keystroke was meant for. ActiveControl names the child of a container
        /// that holds the focus, and for anything nested that child is another container - so it
        /// is followed down rather than asked once.
        /// </summary>
        private Control FocusedControl()
        {
            Control control = this;

            while (control is ContainerControl)
            {
                var inside = ((ContainerControl)control).ActiveControl;
                if (inside == null) break;
                control = inside;
            }

            return control;
        }

        private void Grid_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            // Virtual because the library is thousands of icons and a ListView that is handed
            // all of them spends its first second building items nobody scrolls to.
            if (e.ItemIndex < 0 || e.ItemIndex >= _shown.Count)
            {
                e.Item = new ListViewItem("");
                return;
            }

            e.Item = new ListViewItem(_shown[e.ItemIndex].DisplayName);
        }

        /// <summary>
        /// A ListView in icon view takes its cell size from the image list, and no icon of this
        /// tool's ever goes in one - so the list is here purely to say how big a cell is, and
        /// showing the family means handing the control a taller one. Owner drawing does not
        /// get a say in it.
        /// </summary>
        private ImageList CellSizer(bool withFamily)
        {
            // The drawing, the air around it, and the line under the name when it is showing.
            // The name's own line is added by the control on top of this.
            return new ImageList
            {
                ImageSize = new Size(
                    _thumbnailSize + 24,
                    _thumbnailSize + 12 + (withFamily ? _familyLineHeight : 0))
            };
        }

        /// <summary>
        /// Makes room for the family line, or takes it back. The chosen icon is not disturbed:
        /// this changes how the grid is drawn and nothing about what is in it.
        /// </summary>
        private void ShowFamilyChanged()
        {
            SizeCells();
        }

        /// <summary>Hands the grid a cell the size the drawing and the lines under it now need.</summary>
        private void SizeCells()
        {
            var replacing = _grid.LargeImageList;
            _grid.LargeImageList = CellSizer(_familyToggle.Checked);
            if (replacing != null) replacing.Dispose();

            // The control keeps the old cell metrics until something makes it lay out again.
            _grid.Invalidate();

            if (_grid.SelectedIndices.Count > 0) _grid.EnsureVisible(_grid.SelectedIndices[0]);
        }

        private void Grid_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _shown.Count || _catalog == null) return;

            var icon = _shown[e.ItemIndex];

            // Not e.State: a virtual ListView reports every item as selected, because the item
            // it hands back was built a moment ago by RetrieveVirtualItem and never told
            // otherwise. The selection the control actually keeps is the one to ask.
            var selected = _grid.SelectedIndices.Count > 0 && _grid.SelectedIndices[0] == e.ItemIndex;

            if (selected)
            {
                using (var brush = new SolidBrush(SystemColors.Highlight))
                    e.Graphics.FillRectangle(brush, e.Bounds);
            }

            var thumbnail = Thumbnail(icon, _thumbnailSize, selected ? SystemColors.HighlightText : _colour);
            var x = e.Bounds.X + (e.Bounds.Width - _thumbnailSize) / 2;
            e.Graphics.DrawImage(thumbnail, x, e.Bounds.Y + 6, _thumbnailSize, _thumbnailSize);

            var showFamily = _familyToggle.Checked;
            var captionHeight = e.Bounds.Height - _thumbnailSize - 10 - (showFamily ? _familyLineHeight : 0);

            var caption = new Rectangle(
                e.Bounds.X + 2,
                e.Bounds.Y + _thumbnailSize + 8,
                e.Bounds.Width - 4,
                captionHeight);

            TextRenderer.DrawText(
                e.Graphics,
                icon.DisplayName,
                Font,
                caption,
                selected ? SystemColors.HighlightText : SystemColors.ControlText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (!showFamily) return;

            // Under the name, and quieter than it: which set drew this one is context for the
            // name rather than part of it. Four families draw a folder and they are all called
            // Folder, so with the toggle on the grid stops being a list of things that look
            // alike for no stated reason.
            var origin = new Rectangle(caption.X, caption.Bottom, caption.Width, _familyLineHeight);

            TextRenderer.DrawText(
                e.Graphics,
                icon.Origin,
                Font,
                origin,
                selected ? SystemColors.HighlightText : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        /// <summary>
        /// Enter, out of the search box and into the results. Reaching for the mouse is the
        /// longest part of choosing an icon; from here it is the arrow keys, and Enter on a cell
        /// activates it, which applies.
        /// </summary>
        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            // Whether or not there is anywhere to go: a text box on a form with no accept button
            // beeps at Enter, and being beeped at for pressing it is not an answer.
            e.SuppressKeyPress = true;
            e.Handled = true;

            if (_shown.Count == 0) return;

            _grid.Focus();
            if (_grid.SelectedIndices.Count == 0) _grid.SelectedIndices.Add(0);
            _grid.EnsureVisible(_grid.SelectedIndices[0]);
        }

        /// <summary>
        /// Enter or Down, out of the filter box and into the list under it. Narrowing and then
        /// pointing is one errand, and the two controls it takes should not cost a reach for the
        /// mouse in between. The same handoff the search box makes into the icons.
        /// </summary>
        private void TableFilter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Down) return;

            // Enter only, and whether or not there is anywhere to go: a text box on a form with
            // no accept button beeps at it, and being beeped at for pressing it is not an answer.
            // Down in a one-line box is already silent, and swallowing it would cost nothing but
            // would also say nothing.
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
            }

            if (_tableBox.Items.Count == 0) return;

            _tableBox.Focus();
            if (_tableBox.SelectedIndex < 0) _tableBox.SelectedIndex = 0;
        }

        private void Grid_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_grid.SelectedIndices.Count == 0) { SelectIcon(null); return; }

            var index = _grid.SelectedIndices[0];
            SelectIcon(index >= 0 && index < _shown.Count ? _shown[index] : null);
        }

        private void Table_SelectedIndexChanged(object sender, EventArgs e)
        {
            SuggestName();

            var table = _tableBox.SelectedItem as TableItem;

            // Asked once, before the seed writes into the search box, since what makes the box
            // the tool's is what is in it now - and asked with where this arrived from, because
            // leaving one table for another is what makes an icon in hand the last table's.
            var mine = TheLibraryIsMineToMove(_lastTable != null && _lastTable != table?.LogicalName);

            SeedSearch(table, mine);

            ShowLocked(table);
            ShowCurrentIcon(table, mine);

            // Only a real table, so a filter that empties the list on its way to the next one
            // does not read as arriving somewhere for the first time.
            if (table != null) _lastTable = table.LogicalName;

            SyncEnabled();
        }

        private void Colour_ModeChanged(object sender, EventArgs e)
        {
            // Thumbnails are drawn in the chosen colour, so they all have to go.
            ClearThumbnails();
            _grid.Invalidate();
            SyncEnabled();
        }

        private void Colour_Pick(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog { Color = _colour, FullOpen = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                _colour = dialog.Color;
                _colourButton.BackColor = _colour;
                ClearThumbnails();
                _grid.Invalidate();
                RefreshContextIcon();
            }
        }
    }
}
