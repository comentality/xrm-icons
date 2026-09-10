using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Icons.Catalog;
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
            SyncEnabled();
        }

        /// <summary>
        /// Gives the two panes their proportions once there is enough width to hold them, and
        /// then leaves the splitter alone so a drag is not undone by the next resize.
        /// </summary>
        private void LaySplitter()
        {
            if (_splitterLaid || _body == null) return;

            const int libraryMinimum = 320;
            const int targetMinimum = 300;
            if (_body.Width < libraryMinimum + targetMinimum + _body.SplitterWidth) return;

            _body.Panel1MinSize = libraryMinimum;
            _body.Panel2MinSize = targetMinimum;
            _body.SplitterDistance = Math.Max(libraryMinimum, _body.Width - 380);
            _splitterLaid = true;
        }

        private ToolStrip BuildToolbar()
        {
            _loadButton = new ToolStripButton("Load tables")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Read the tables and solutions from the connected environment"
            };
            _loadButton.Click += (s, e) => LoadEverything();

            _applyButton = new ToolStripButton("Set as table icon")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Store the chosen icon as a web resource, point the table at it, and publish"
            };
            _applyButton.Click += (s, e) => Apply();

            // Tooltips rather than captions, because what they would take back is a table and
            // an icon by name and that changes with every write. SyncUndo keeps them true.
            _undoButton = new ToolStripButton("Undo") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _undoButton.Click += (s, e) => Undo();

            _redoButton = new ToolStripButton("Redo") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _redoButton.Click += (s, e) => Redo();

            _contextButton = new ToolStripButton("See it in context")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = "Show the icon in an app's navigation, at the size the app draws it"
            };
            _contextButton.Click += (s, e) => ShowContext();

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
            toolbar.Items.AddRange(new ToolStripItem[]
            {
                _loadButton,
                new ToolStripSeparator(),
                _applyButton,
                _cancelButton,
                new ToolStripSeparator(),
                _undoButton,
                _redoButton,
                new ToolStripSeparator(),
                _contextButton,
                new ToolStripSeparator(),
                _statusLabel,
                _aboutButton
            });

            return toolbar;
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
            _styleBox.SelectedIndex = 0;
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
            _preview = new Panel { Dock = DockStyle.Top, Height = 140, BackColor = SystemColors.Window };
            _preview.Paint += Preview_Paint;

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

            // The common errand is "which of these has nobody given an icon yet", and the
            // answer is already in hand: the load reads IconVectorName with everything else,
            // so this narrows the list without another round trip.
            _unsetOnlyBox = new CheckBox
            {
                Text = "Only tables with no icon yet",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _unsetOnlyBox.CheckedChanged += (s, e) => ShowTables();

            _tableBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _tableBox.SelectedIndexChanged += Table_SelectedIndexChanged;

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

            _fixedColour = new RadioButton { Text = "Fixed colour", AutoSize = true, Anchor = AnchorStyles.Left };
            _fixedColour.CheckedChanged += Colour_ModeChanged;

            _colourButton = new Button { Text = "", Width = 40, Height = 22, BackColor = _colour, Anchor = AnchorStyles.Left };
            _colourButton.Click += Colour_Pick;

            _rasterBox = new CheckBox
            {
                Text = "Also write the three classic PNG icons",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            // Off by default, and the answer to "should I tick this?" is a paragraph rather
            // than a caption. It goes behind a link, in the same dialog the icon licence uses.
            _rasterHelp = new LinkLabel
            {
                Text = "When do the PNGs matter?",
                AutoSize = true,
                // Top as well as Left: this is the last row, so it is the one the layout hands
                // its leftover height to, and a left-only anchor would float it down the middle
                // of that and read as belonging to nothing.
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Padding = new Padding(0, 4, 0, 0)
            };
            _rasterHelp.LinkClicked += (s, e) => ShowRasterHelp();

            _troubleLabel = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = false,
                Height = 52,
                Visible = false,
                ForeColor = Color.FromArgb(0xA4, 0x26, 0x2C),
                Padding = new Padding(8, 4, 8, 4)
            };

            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(8, 6, 8, 6),
                AutoScroll = true
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(form, 0, "Find table", _tableFilterBox);
            AddRow(form, 1, "", _unsetOnlyBox);
            AddRow(form, 2, "Table", _tableBox);
            AddRow(form, 3, "Solution", _solutionBox);
            AddRow(form, 4, "Web resource", _nameBox);
            AddRow(form, 5, "Colour", _themeColour);
            AddRow(form, 6, "", _fixedColour);
            AddRow(form, 7, "", _colourButton);
            AddRow(form, 8, "", _rasterBox);
            AddRow(form, 9, "", _rasterHelp);

            var pane = new Panel { Dock = DockStyle.Fill };
            pane.Controls.Add(form);
            pane.Controls.Add(_troubleLabel);
            pane.Controls.Add(_warningLabel);
            pane.Controls.Add(_iconLabel);
            pane.Controls.Add(_preview);
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
            SeedSearch(table);

            if (table != null && !table.Customizable)
                _trouble = table.DisplayName + " is locked against customisation, so its icon cannot be changed.";
            else if (_trouble != null && _trouble.Contains("locked against customisation"))
                _trouble = null;

            SyncEnabled();
        }

        private void Colour_ModeChanged(object sender, EventArgs e)
        {
            // Thumbnails are drawn in the chosen colour, so they all have to go.
            ClearThumbnails();
            _grid.Invalidate();
            _preview.Invalidate();
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
                _preview.Invalidate();
            }
        }

        private void Preview_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(SystemColors.Window);

            if (_selectedIcon == null || _catalog == null)
            {
                TextRenderer.DrawText(e.Graphics, "Pick an icon from the library",
                    Font, _preview.ClientRectangle, SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Light and dark side by side, because the default is an icon that takes its colour
            // from the theme and the whole question is whether it survives both.
            var half = _preview.ClientRectangle.Width / 2;
            var light = new Rectangle(0, 0, half, _preview.ClientRectangle.Height);
            var dark = new Rectangle(half, 0, _preview.ClientRectangle.Width - half, _preview.ClientRectangle.Height);

            using (var brush = new SolidBrush(Color.FromArgb(0x1B, 0x1A, 0x19)))
                e.Graphics.FillRectangle(brush, dark);

            var onLight = _fixedColour.Checked ? _colour : Color.FromArgb(0x24, 0x24, 0x24);
            var onDark = _fixedColour.Checked ? _colour : Color.FromArgb(0xF3, 0xF2, 0xF1);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawPreview(e.Graphics, light, onLight);
            DrawPreview(e.Graphics, dark, onDark);
        }

        private void DrawPreview(Graphics graphics, Rectangle area, Color colour)
        {
            const int big = 72;
            const int small = 20;

            var thumbnail = Thumbnail(_selectedIcon, big, colour);
            graphics.DrawImage(thumbnail,
                area.X + (area.Width - big) / 2,
                area.Y + 18,
                big, big);

            // The size it will actually be seen at in a site map, next to the size that shows
            // what it is.
            var tiny = Thumbnail(_selectedIcon, small, colour);
            graphics.DrawImage(tiny, area.X + (area.Width - small) / 2, area.Y + 104, small, small);
        }
    }
}
