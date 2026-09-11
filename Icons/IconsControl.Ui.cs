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
        private const int ThumbnailSize = 40;
        private const int CellSize = 76;

        /// <summary>What the family line under a name costs in cell height when it is showing.</summary>
        private const int FamilyLineHeight = 15;

        /// <summary>The family filter's first entry, which is not a family.</summary>
        private const string AllFamilies = "All families";

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
            const int libraryMinimum = 320;
            const int targetMinimum = 340;

            if (!_splitterLaid && _body.Width >= libraryMinimum + targetMinimum + _body.SplitterWidth)
            {
                _body.Panel1MinSize = libraryMinimum;
                _body.Panel2MinSize = targetMinimum;
                _body.SplitterDistance = Math.Max(libraryMinimum, _body.Width - 430);
                _splitterLaid = true;
            }

            const int previewMinimum = 120;
            const int formMinimum = 200;

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
        /// grows a line when the chosen table is locked, and the warning and trouble lines come
        /// and go above and below it. A number written here would be right on the machine it was
        /// measured on and a scroll bar everywhere else.
        ///
        /// Once the splitter has been dragged this does nothing. How much navigation is worth
        /// looking at is a question somebody has then answered, and the form scrolls instead.
        /// </summary>
        private void FitForm()
        {
            if (!_targetSplitLaid || _targetSplitDragged || _layingTargetSplit) return;

            var needed = _form.Height
                         + _iconLabel.Height
                         + Math.Max(_formActions.Height, _formActions.PreferredSize.Height)
                         + (_warningLabel.Visible ? _warningLabel.Height : 0)
                         + (_troubleLabel.Visible ? _troubleLabel.Height : 0);

            var room = _targetSplit.Height - _targetSplit.SplitterWidth;
            var distance = Math.Min(room - _targetSplit.Panel2MinSize, room - needed);
            if (distance < _targetSplit.Panel1MinSize) distance = _targetSplit.Panel1MinSize;
            if (distance > room - _targetSplit.Panel2MinSize) return;
            if (_targetSplit.SplitterDistance == distance) return;

            _layingTargetSplit = true;
            try { _targetSplit.SplitterDistance = distance; }
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

            _aboutButton = new ToolStripButton("About the icons")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Alignment = ToolStripItemAlignment.Right
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

        private Control BuildLibraryPane()
        {
            _searchBox = new TextBox { Dock = DockStyle.Fill };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            _searchBox.KeyDown += Search_KeyDown;

            _familyBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _familyBox.Items.Add(AllFamilies);
            foreach (IconFamily family in Enum.GetValues(typeof(IconFamily))) _familyBox.Items.Add(family);
            _familyBox.SelectedIndex = 0;
            _familyBox.SelectedIndexChanged += (s, e) => ApplyFilter();

            _styleBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _styleBox.Items.AddRange(new object[] { "Both weights", "Outline", "Solid" });
            // Outline rather than both, because both means every icon twice: the same drawing
            // filled and unfilled, adjacent in the ranking, and a grid where half the cells are
            // the alternative to the cell beside them is a worse first look at the library than
            // one weight of it. Outline is the one a site map is drawn in.
            _styleBox.SelectedIndex = 1;
            _styleBox.SelectedIndexChanged += (s, e) => ApplyFilter();

            _familyToggle = new CheckBox
            {
                Text = "Show family",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 2, 0, 0)
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

            // Two rows: the search on its own, and the three things that narrow what it searches
            // under it. On one row the search box is the only control that can give up width,
            // and with four families to choose between there is not enough of it to give.
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 6,
                RowCount = 2,
                Height = 58,
                Padding = new Padding(6, 3, 6, 3)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));

            header.Controls.Add(new Label { Text = "Search", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 4, 6, 0) }, 0, 0);
            header.Controls.Add(_searchBox, 1, 0);
            header.SetColumnSpan(_searchBox, 5);

            header.Controls.Add(_familyToggle, 1, 1);
            header.Controls.Add(new Label { Text = "Family", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(10, 4, 6, 0) }, 2, 1);
            header.Controls.Add(_familyBox, 3, 1);
            header.Controls.Add(new Label { Text = "Weight", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(10, 4, 6, 0) }, 4, 1);
            header.Controls.Add(_styleBox, 5, 1);

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

            _iconLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "No icon chosen",
                Padding = new Padding(8, 3, 8, 0),
                Font = new Font(Font, FontStyle.Bold)
            };

            _warningLabel = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 46,
                Visible = false,
                ForeColor = Color.FromArgb(0x8A, 0x53, 0x00),
                Padding = new Padding(8, 2, 8, 4)
            };

            _tableFilterBox = new TextBox { Dock = DockStyle.Fill };
            _tableFilterBox.TextChanged += (s, e) => ShowTables();
            _tableFilterBox.KeyDown += TableFilter_KeyDown;

            // The common errand is "which of these has nobody given an icon yet", and the
            // answer is already in hand: the load reads IconVectorName with everything else,
            // so this narrows the list without another round trip.
            //
            // Ticked to begin with, because that errand is the reason anybody opens this tool:
            // the list of tables with no icon is the work, and the full list is what you ask for
            // when you have come to change one that is already there.
            _unsetOnlyBox = new CheckBox
            {
                Text = "Only tables with no icon yet",
                Checked = true,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
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
                AutoSize = false,
                Height = 52,
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
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
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

            // Right, because that is the corner a window that fills something in keeps its last
            // button in, and its own strip below everything else so the red line that says why a
            // write failed reads above the button that would try it again.
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
            _formActions = actions;

            var below = new Panel { Dock = DockStyle.Fill };
            below.Controls.Add(_formHost);
            below.Controls.Add(_troubleLabel);
            below.Controls.Add(_warningLabel);
            below.Controls.Add(_iconLabel);
            below.Controls.Add(actions);

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

            var pane = new Panel { Dock = DockStyle.Fill };
            pane.Controls.Add(_targetSplit);
            return pane;
        }

        private static void AddRow(TableLayoutPanel form, int row, string caption, Control control)
        {
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
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
                                   Control control, int height = 28)
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
        private static ImageList CellSizer(bool withFamily)
        {
            return new ImageList
            {
                ImageSize = new Size(CellSize - 12, CellSize - 24 + (withFamily ? FamilyLineHeight : 0))
            };
        }

        /// <summary>
        /// Makes room for the family line, or takes it back. The chosen icon is not disturbed:
        /// this changes how the grid is drawn and nothing about what is in it.
        /// </summary>
        private void ShowFamilyChanged()
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

            var thumbnail = Thumbnail(icon, ThumbnailSize, selected ? SystemColors.HighlightText : _colour);
            var x = e.Bounds.X + (e.Bounds.Width - ThumbnailSize) / 2;
            e.Graphics.DrawImage(thumbnail, x, e.Bounds.Y + 6, ThumbnailSize, ThumbnailSize);

            var showFamily = _familyToggle.Checked;
            var captionHeight = e.Bounds.Height - ThumbnailSize - 10 - (showFamily ? FamilyLineHeight : 0);

            var caption = new Rectangle(
                e.Bounds.X + 2,
                e.Bounds.Y + ThumbnailSize + 8,
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
            var origin = new Rectangle(caption.X, caption.Bottom, caption.Width, FamilyLineHeight);

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
            // Somebody has picked an icon, so the grid is theirs from here and the window stops
            // moving it for them. Not while the tool is the one moving it: that selection is
            // still an offer rather than a choice.
            if (!_choosingCurrent) _chosenFor = null;

            if (_grid.SelectedIndices.Count == 0) { SelectIcon(null); return; }

            var index = _grid.SelectedIndices[0];
            SelectIcon(index >= 0 && index < _shown.Count ? _shown[index] : null);
        }

        private void Table_SelectedIndexChanged(object sender, EventArgs e)
        {
            SuggestName();

            var table = _tableBox.SelectedItem as TableItem;

            // Asked before the seed writes into the search box, since what makes the box the
            // tool's is what is in it now.
            var mine = TheLibraryIsMineToMove();

            SeedSearch(table);

            ShowLocked(table);
            ShowCurrentIcon(table, mine);

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
