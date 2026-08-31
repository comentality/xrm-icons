using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Icons.Catalog;
using Icons.Logic;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.Extensibility;
using Label = System.Windows.Forms.Label;

namespace Icons
{
    public partial class IconsControl : PluginControlBase
    {
        private sealed class TableItem
        {
            public string LogicalName;
            public string DisplayName;
            public bool Customizable;
            public string CurrentIcon;

            public override string ToString()
            {
                return DisplayName + "  (" + LogicalName + ")";
            }
        }

        private sealed class SolutionItem
        {
            public string UniqueName;
            public string FriendlyName;
            public string Prefix;

            public override string ToString()
            {
                return FriendlyName + "  [" + Prefix + "_]";
            }
        }

        // ===== what is on the wire =====
        //
        // Everything this tool asks Dataverse is started by a click, and on a slow link every
        // one of those clicks is seconds wide. The panel XrmToolBox draws while it waits is a
        // small box in the middle of the tab: it is not modal and it disables nothing, so for
        // the whole of that wait every control here is live. The tool has to know for itself
        // what is outstanding, and whose answer an arriving one is.

        /// <summary>
        /// Which question is current. Bumped by every new one and checked by every answer, so
        /// the slower reply to an earlier question cannot land on top of a later one.
        /// </summary>
        private int _generation;

        private bool _loadingTables;
        private bool _loadingSolutions;
        private bool _applying;

        /// <summary>What went wrong with the last thing asked, or null. Outlives its dialog.</summary>
        private string _trouble;

        /// <summary>What the last change actually did, for the status line.</summary>
        private string _outcome;

        private IconCatalog _catalog;
        private IReadOnlyList<IconEntry> _library = new IconEntry[0];
        private IReadOnlyList<IconEntry> _shown = new IconEntry[0];
        private IconEntry _selectedIcon;

        private List<TableItem> _tables = new List<TableItem>();
        private List<SolutionItem> _solutions = new List<SolutionItem>();

        /// <summary>Thumbnails already drawn. Cleared when the colour changes.</summary>
        private readonly Dictionary<string, Bitmap> _thumbnails = new Dictionary<string, Bitmap>();

        private Color _colour = Color.FromArgb(0x24, 0x24, 0x24);

        private SplitContainer _body;
        private bool _splitterLaid;

        private ToolStripButton _loadButton;
        private ToolStripButton _applyButton;
        private ToolStripButton _cancelButton;
        private ToolStripButton _aboutButton;
        private ToolStripLabel _statusLabel;

        private TextBox _searchBox;
        private ComboBox _styleBox;
        private ListView _grid;
        private ComboBox _tableBox;
        private TextBox _tableFilterBox;
        private ComboBox _solutionBox;
        private TextBox _nameBox;

        /// <summary>The last name SuggestName wrote, so a hand-edited one is recognisable.</summary>
        private string _suggestedName = "";
        private RadioButton _themeColour;
        private RadioButton _fixedColour;
        private Button _colourButton;
        private CheckBox _rasterBox;
        private Panel _preview;
        private Label _iconLabel;
        private Label _warningLabel;
        private Label _troubleLabel;

        public IconsControl()
        {
            BuildUi();
        }

        // ===== what may be done right now =====

        private bool Busy
        {
            get { return _loadingTables || _loadingSolutions || _applying; }
        }

        /// <summary>
        /// Everything that acts on the connection, enabled only when its inputs are actually
        /// present. Called after every state change; nothing else sets Enabled.
        /// </summary>
        private void SyncEnabled()
        {
            if (IsDisposed) return;

            _loadButton.Enabled = !Busy;
            _cancelButton.Enabled = _applying;

            _tableBox.Enabled = !Busy && _tables.Count > 0;
            _tableFilterBox.Enabled = !Busy && _tables.Count > 0;
            _solutionBox.Enabled = !Busy && _solutions.Count > 0;
            _nameBox.Enabled = !_applying;
            _rasterBox.Enabled = !_applying;
            _themeColour.Enabled = !_applying;
            _fixedColour.Enabled = !_applying;
            _colourButton.Enabled = !_applying && _fixedColour.Checked;

            // Writing needs all three: something to draw, somewhere to put it, and a name that
            // Dataverse will accept. Offering Apply without them buys one round trip and an
            // error dialog.
            var table = _tableBox.SelectedItem as TableItem;
            _applyButton.Enabled =
                !Busy &&
                _selectedIcon != null &&
                table != null &&
                table.Customizable &&
                WebResourceName.Validate(_nameBox.Text) == null;

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (IsDisposed) return;

            string text;
            if (_loadingTables) text = "Loading tables...";
            else if (_loadingSolutions) text = "Loading solutions...";
            else if (_applying) text = "Applying...";
            else if (_tables.Count == 0) text = _library.Count + " icons. Load tables to choose where one goes.";
            else
            {
                // Say what is on screen against what there is, so a filtered grid never reads
                // like the whole library.
                var shown = _shown.Count == _library.Count
                    ? _library.Count + " icons"
                    : _shown.Count + " of " + _library.Count + " icons";
                text = shown + " · " + _tables.Count + " tables";
                if (_outcome != null) text += " · " + _outcome;
            }

            _statusLabel.Text = text;

            _troubleLabel.Text = _trouble ?? "";
            _troubleLabel.Visible = _trouble != null;
        }

        // ===== tables and solutions =====

        private void LoadEverything()
        {
            if (Busy) return;
            ExecuteMethod(LoadTables);
        }

        private void LoadTables()
        {
            if (_loadingTables) return;

            var generation = ++_generation;
            _loadingTables = true;
            _trouble = null;
            _outcome = null;
            SyncEnabled();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading tables...",
                Work = (worker, args) =>
                {
                    var response = (RetrieveAllEntitiesResponse)Service.Execute(new RetrieveAllEntitiesRequest
                    {
                        EntityFilters = EntityFilters.Entity,
                        RetrieveAsIfPublished = true
                    });

                    args.Result = response.EntityMetadata
                        .Where(e => e.IsIntersect != true && e.IsPrivate != true)
                        .Select(e => new TableItem
                        {
                            LogicalName = e.LogicalName,
                            DisplayName = e.DisplayName?.UserLocalizedLabel?.Label ?? e.SchemaName ?? e.LogicalName,
                            Customizable = e.IsCustomizable == null || e.IsCustomizable.Value,
                            CurrentIcon = e.IconVectorName
                        })
                        .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                },
                PostWorkCallBack = args =>
                {
                    // The tab can be closed while this was on the wire; the callback still runs.
                    if (IsDisposed) return;

                    // Whatever happens below, this flag comes up - including on the error path,
                    // or one timeout leaves the button dead for the rest of the session.
                    _loadingTables = false;

                    if (generation != _generation) { SyncEnabled(); return; }

                    if (args.Error != null)
                    {
                        _tables = new List<TableItem>();
                        _tableBox.Items.Clear();
                        _trouble = "Could not load tables: " + args.Error.Message;
                        SyncEnabled();
                        MessageBox.Show(this, args.Error.Message, "Could not load tables",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _tables = (List<TableItem>)args.Result;
                    ShowTables();
                    SyncEnabled();

                    // Only once the tables are in, so a failure above does not read as an org
                    // with no solutions either.
                    LoadSolutions();
                }
            });
        }

        private void LoadSolutions()
        {
            if (_loadingSolutions) return;

            var generation = _generation;
            _loadingSolutions = true;
            SyncEnabled();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading solutions...",
                Work = (worker, args) =>
                {
                    var query = new QueryExpression("solution")
                    {
                        ColumnSet = new ColumnSet("uniquename", "friendlyname"),
                        Criteria =
                        {
                            Conditions =
                            {
                                new ConditionExpression("isvisible", ConditionOperator.Equal, true),
                                new ConditionExpression("ismanaged", ConditionOperator.Equal, false)
                            }
                        },
                        Orders = { new OrderExpression("friendlyname", OrderType.Ascending) }
                    };

                    // The publisher prefix is the reason for asking at all: it is what the web
                    // resource has to be named with.
                    var publisher = query.AddLink("publisher", "publisherid", "publisherid", JoinOperator.Inner);
                    publisher.EntityAlias = "p";
                    publisher.Columns = new ColumnSet("customizationprefix");

                    args.Result = Service.RetrieveMultiple(query).Entities
                        .Select(e => new SolutionItem
                        {
                            UniqueName = e.GetAttributeValue<string>("uniquename"),
                            FriendlyName = e.GetAttributeValue<string>("friendlyname"),
                            Prefix = (e.GetAttributeValue<AliasedValue>("p.customizationprefix")?.Value as string) ?? "new"
                        })
                        .Where(s => !string.IsNullOrWhiteSpace(s.UniqueName))
                        .ToList();
                },
                PostWorkCallBack = args =>
                {
                    if (IsDisposed) return;

                    _loadingSolutions = false;

                    if (generation != _generation) { SyncEnabled(); return; }

                    if (args.Error != null)
                    {
                        // Not fatal: without a solution the tool still works, it just writes to
                        // the default one. Say so rather than leaving an empty box unexplained.
                        _solutions = new List<SolutionItem>();
                        _trouble = "Could not load solutions, so new web resources will go to the default solution: " +
                                   args.Error.Message;
                        SyncEnabled();
                        return;
                    }

                    _solutions = (List<SolutionItem>)args.Result;

                    _solutionBox.BeginUpdate();
                    _solutionBox.Items.Clear();
                    foreach (var solution in _solutions) _solutionBox.Items.Add(solution);
                    _solutionBox.EndUpdate();

                    if (_solutionBox.Items.Count > 0)
                    {
                        // Prefer an actual solution over the catch-all: writing into Default is
                        // legal and almost never what somebody wants.
                        var index = _solutions.FindIndex(s =>
                            !s.UniqueName.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
                            !s.UniqueName.Equals("Active", StringComparison.OrdinalIgnoreCase));
                        _solutionBox.SelectedIndex = index >= 0 ? index : 0;
                    }

                    SyncEnabled();
                }
            });
        }

        private void ShowTables()
        {
            var filter = (_tableFilterBox.Text ?? "").Trim();
            var matching = _tables.Where(t =>
                filter.Length == 0 ||
                t.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.LogicalName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            var previous = _tableBox.SelectedItem as TableItem;

            _tableBox.BeginUpdate();
            _tableBox.Items.Clear();
            foreach (var table in matching) _tableBox.Items.Add(table);
            _tableBox.EndUpdate();

            if (previous != null)
            {
                var index = _tableBox.Items.Cast<TableItem>()
                    .ToList()
                    .FindIndex(t => t.LogicalName == previous.LogicalName);
                if (index >= 0) _tableBox.SelectedIndex = index;
            }
        }

        // ===== the icon library =====

        private void LoadCatalog()
        {
            try
            {
                _catalog = IconCatalog.Load();
                _library = _catalog.Icons;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _trouble = "The bundled icon library would not open: " + ex.Message;
            }

            SyncEnabled();
        }

        private void ApplyFilter()
        {
            if (_catalog == null) return;

            var wanted = _library;

            var style = _styleBox.SelectedIndex;
            if (style == 1) wanted = wanted.Where(i => i.Style == IconStyle.Regular).ToList();
            else if (style == 2) wanted = wanted.Where(i => i.Style == IconStyle.Filled).ToList();

            // The grid's selection is an index into the list that is about to be replaced, and
            // clearing it raises SelectedIndexChanged, which would throw the chosen icon away.
            var keep = _selectedIcon;

            _shown = IconSearch.Rank(wanted, _searchBox.Text);

            _grid.SelectedIndices.Clear();
            _grid.VirtualListSize = _shown.Count;

            // Follow the icon to wherever it landed in the new ranking. Without this the
            // highlighted cell and the preview drift apart the moment somebody chooses an icon
            // and then keeps typing: index 4 of the old order is a different icon in the new one.
            var index = IndexOf(_shown, keep);
            if (index >= 0)
            {
                _selectedIcon = keep;
                _grid.SelectedIndices.Add(index);
                _grid.EnsureVisible(index);
            }
            else
            {
                SelectIcon(null);
            }

            _grid.Invalidate();
            SyncEnabled();
        }

        private static int IndexOf(IReadOnlyList<IconEntry> icons, IconEntry wanted)
        {
            if (wanted == null) return -1;

            for (var i = 0; i < icons.Count; i++)
                if (ReferenceEquals(icons[i], wanted)) return i;

            return -1;
        }

        private void SelectIcon(IconEntry icon)
        {
            _selectedIcon = icon;

            _iconLabel.Text = icon == null
                ? "No icon chosen"
                : icon.DisplayName + "  ·  " + icon.Style.ToString().ToLowerInvariant();

            _warningLabel.Text = "";
            _warningLabel.Visible = false;

            if (icon != null && _catalog != null)
            {
                try
                {
                    var warnings = SvgOutput.Inspect(_catalog.ReadSvg(icon));
                    if (warnings.Count > 0)
                    {
                        _warningLabel.Text = string.Join(" ", warnings);
                        _warningLabel.Visible = true;
                    }
                }
                catch (Exception ex)
                {
                    _warningLabel.Text = "This icon would not read: " + ex.Message;
                    _warningLabel.Visible = true;
                }
            }

            _preview.Invalidate();
            SyncEnabled();
        }

        /// <summary>
        /// The thumbnail for one icon, drawn once and kept. Rendering is well under a
        /// millisecond, but a grid repaints far more often than it scrolls.
        /// </summary>
        private Bitmap Thumbnail(IconEntry icon, int size, Color colour)
        {
            var key = icon.EntryPath + "|" + size + "|" + colour.ToArgb();

            Bitmap cached;
            if (_thumbnails.TryGetValue(key, out cached)) return cached;

            Bitmap drawn;
            try
            {
                drawn = SvgImage.Parse(_catalog.ReadSvg(icon)).Render(size, colour);
            }
            catch
            {
                // An icon that will not draw must not take the whole grid down with it.
                drawn = new Bitmap(size, size);
            }

            // A bound rather than a policy: the whole library at one size is a few tens of
            // megabytes, and nobody scrolls all of it.
            if (_thumbnails.Count > 4000) ClearThumbnails();

            _thumbnails[key] = drawn;
            return drawn;
        }

        private void ClearThumbnails()
        {
            foreach (var bitmap in _thumbnails.Values) bitmap.Dispose();
            _thumbnails.Clear();
        }

        // ===== writing =====

        private void Apply()
        {
            if (Busy) return;

            var table = _tableBox.SelectedItem as TableItem;
            var icon = _selectedIcon;
            if (table == null || icon == null || _catalog == null) return;

            var name = _nameBox.Text.Trim();
            var problem = WebResourceName.Validate(name);
            if (problem != null)
            {
                MessageBox.Show(this, problem, "That name will not work",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var replacing = string.IsNullOrEmpty(table.CurrentIcon)
                ? ""
                : "\n\nThis replaces its current icon, " + table.CurrentIcon + ".";

            var confirm = MessageBox.Show(this,
                "Set " + icon.DisplayName + " as the icon for " + table.DisplayName + "?\n\n" +
                "This writes the web resource " + name + " and publishes the table." + replacing,
                "Set table icon", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            var solution = _solutionBox.SelectedItem as SolutionItem;
            var mode = _fixedColour.Checked ? IconColourMode.Fixed : IconColourMode.Theme;

            string svg;
            SvgImage image;
            try
            {
                var source = _catalog.ReadSvg(icon);
                svg = SvgOutput.Build(source, mode, _colour);
                image = SvgImage.Parse(source);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "That icon would not convert",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var request = new IconApplyRequest
            {
                TableLogicalName = table.LogicalName,
                Svg = svg,
                WebResourceName = name,
                SolutionUniqueName = solution?.UniqueName,
                IncludeRasterIcons = _rasterBox.Checked,
                Image = image,
                RasterColour = _colour
            };

            var generation = ++_generation;
            _applying = true;
            _trouble = null;
            _outcome = null;
            SyncEnabled();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Applying...",
                // Without this the worker does not support cancellation at all and the Cancel
                // button is decoration.
                IsCancelable = true,
                Work = (worker, args) =>
                {
                    args.Result = IconApplier.Apply(
                        Service,
                        request,
                        () => worker.CancellationPending,
                        message => worker.ReportProgress(0, message));
                },
                ProgressChanged = args =>
                {
                    if (IsDisposed) return;
                    if (generation != _generation) return;
                    _statusLabel.Text = args.UserState as string ?? "Applying...";
                },
                PostWorkCallBack = args =>
                {
                    if (IsDisposed) return;

                    _applying = false;

                    if (generation != _generation) { SyncEnabled(); return; }

                    // Read Cancelled before Result: touching Result after a cancel rethrows.
                    if (args.Cancelled || args.Error is IconApplier.CancelledException)
                    {
                        _trouble = "Cancelled. The table may or may not have been changed, " +
                                   "depending on how far it had got - reload to see.";
                        SyncEnabled();
                        return;
                    }

                    if (args.Error != null)
                    {
                        _trouble = "Could not set the icon: " + args.Error.Message;
                        SyncEnabled();
                        MessageBox.Show(this, args.Error.Message, "Could not set the icon",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var result = (IconApplyResult)args.Result;

                    // The list in memory now disagrees with the org unless this is written back.
                    table.CurrentIcon = name;

                    _outcome = "Set " + icon.DisplayName + " on " + table.DisplayName;
                    SyncEnabled();

                    MessageBox.Show(this,
                        string.Join("\n", result.Steps),
                        "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
        }

        // ===== plumbing =====

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail,
                                              string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            // A different org has different tables. Bump the generation so anything still on
            // the wire for the old one is discarded rather than shown as this one's.
            _generation++;
            _loadingTables = false;
            _loadingSolutions = false;
            _applying = false;

            _tables = new List<TableItem>();
            _solutions = new List<SolutionItem>();
            _tableBox.Items.Clear();
            _solutionBox.Items.Clear();
            _trouble = null;
            _outcome = null;

            SyncEnabled();
        }

        private void ShowAbout()
        {
            var text = _catalog?.About ?? "The icon library is not loaded.";
            MessageBox.Show(this, text, "About the icons", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SuggestName()
        {
            var table = _tableBox.SelectedItem as TableItem;
            var solution = _solutionBox.SelectedItem as SolutionItem;
            if (table == null) return;

            // A name the user has typed over is theirs. This gets called by every reload and by
            // every table-filter keystroke that re-selects the same table, so it may only write
            // into a box that is empty or still holds its own last suggestion.
            if (_nameBox.Text.Length > 0 && _nameBox.Text != _suggestedName) return;

            _suggestedName = WebResourceName.ForTable(solution?.Prefix ?? "new", table.LogicalName);
            _nameBox.Text = _suggestedName;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearThumbnails();
                _catalog?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
