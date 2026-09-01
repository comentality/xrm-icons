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

            /// <summary>
            /// What the three classic slots point at now. Read with everything else because it
            /// costs nothing to - the same call already answers it - and because a write that
            /// includes the PNGs takes these over without touching what they point at now.
            /// </summary>
            public string CurrentSmall;
            public string CurrentMedium;
            public string CurrentLarge;

            public override string ToString()
            {
                return DisplayName + "  (" + LogicalName + ")";
            }
        }

        private sealed class SolutionItem
        {
            public Guid Id;
            public string UniqueName;
            public string FriendlyName;
            public string Prefix;

            /// <summary>
            /// Whether this is the solution the environment says the user works in. The only
            /// thing that gets chosen for somebody: everything else is a guess about where their
            /// icons belong, and the wrong guess is one nobody notices until it ships.
            /// </summary>
            public bool Preferred;

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

        /// <summary>
        /// Where this org already keeps its icons, learned from the tables and solutions that
        /// were just read. Until a load lands it knows nothing and suggests the convention.
        /// </summary>
        private IconLocations _locations = IconLocations.None;

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
        private CheckBox _unsetOnlyBox;
        private ComboBox _solutionBox;
        private TextBox _nameBox;

        /// <summary>The last name SuggestName wrote, so a hand-edited one is recognisable.</summary>
        private string _suggestedName = "";

        /// <summary>Which table that name was for, so a hand-edit can be told from a table change.</summary>
        private string _suggestedFor;

        /// <summary>
        /// The folder out of the last name typed by hand, and the folder every suggestion after
        /// it uses. Somebody who files one icon somewhere is filing the next one there too;
        /// emptying the box asks for the org's own convention back.
        /// </summary>
        private string _typedFolder;

        /// <summary>What is worth knowing about the chosen icon, the solution, and the name.</summary>
        private string _iconConcern;
        private string _nameConcern;
        private string _solutionConcern;
        private RadioButton _themeColour;
        private RadioButton _fixedColour;
        private Button _colourButton;
        private CheckBox _rasterBox;
        private LinkLabel _rasterHelp;
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
            _unsetOnlyBox.Enabled = !Busy && _tables.Count > 0;
            _solutionBox.Enabled = !Busy && _solutions.Count > 0;
            _nameBox.Enabled = !_applying;
            _rasterBox.Enabled = !_applying;
            _themeColour.Enabled = !_applying;
            _fixedColour.Enabled = !_applying;
            _colourButton.Enabled = !_applying && _fixedColour.Checked;

            // Writing needs all four: something to draw, a table to put it on, an unmanaged
            // solution to file the new web resource in, and a name that Dataverse will accept.
            // Offering Apply without them buys one round trip and an error dialog.
            //
            // The solution is required rather than optional because the alternative is not
            // "no solution" - it is the default solution, where a web resource is invisible to
            // whatever the org actually ships, and moving it afterwards is somebody's afternoon.
            var table = _tableBox.SelectedItem as TableItem;
            _applyButton.Enabled =
                !Busy &&
                _selectedIcon != null &&
                table != null &&
                table.Customizable &&
                _solutionBox.SelectedItem is SolutionItem &&
                WebResourceName.Validate(_nameBox.Text) == null;

            _nameConcern = NameConcern();
            _solutionConcern = SolutionConcern();
            ShowConcerns();

            UpdateStatus();
        }

        /// <summary>
        /// Why nothing is chosen in the solution box, or null. Only ever the case when the
        /// environment has no preferred solution to name, because that is the one thing this tool
        /// will choose for somebody - so the line says what to do rather than what went wrong.
        /// </summary>
        private string SolutionConcern()
        {
            if (Busy || _solutions.Count == 0) return null;
            if (_solutionBox.SelectedItem is SolutionItem) return null;

            return "Choose the solution new web resources go into. This environment has no " +
                   "preferred solution set, so there is nothing to choose for you - and the " +
                   "default solutions are not it.";
        }

        /// <summary>
        /// Why the name in the box may not be writable through the solution in the box, or null.
        ///
        /// Deliberately not a refusal. A web resource that already exists is updated where it
        /// already lives, whatever prefix it carries - which is exactly what a table whose icon
        /// somebody else filed under another publisher needs. Only a create has to be named for
        /// the solution's publisher, and only the org knows which of the two this will be.
        /// </summary>
        private string NameConcern()
        {
            var solution = _solutionBox.SelectedItem as SolutionItem;
            if (solution == null) return null;

            var typed = _nameBox.Text.Trim();

            // A name Dataverse would reject outright is a bigger problem, and it is already said
            // out loud by Apply being dead and by the dialog that names the character.
            if (WebResourceName.Validate(typed) != null) return null;

            var publishes = (solution.Prefix ?? "").Trim().TrimEnd('_').ToLowerInvariant();
            var carries = IconLocations.PrefixOf(typed);
            if (publishes.Length == 0 || carries == null || carries == publishes) return null;

            return "This name is under " + carries + "_, and " + solution.FriendlyName + " publishes " +
                   publishes + "_. A web resource of this name that already exists is updated where " +
                   "it is; one that does not exist yet will be refused, because a new web resource " +
                   "has to be named for the publisher of the solution it goes into.";
        }

        /// <summary>
        /// The one place the warning line is written. Two things can be worth saying at once - an
        /// icon that carries its own colours, a name under another publisher's prefix - and a
        /// fixed-height label would swallow the second.
        /// </summary>
        private void ShowConcerns()
        {
            var text = string.Join("  ", new[] { _iconConcern, _solutionConcern, _nameConcern }
                .Where(concern => !string.IsNullOrEmpty(concern)));

            _warningLabel.Text = text;
            _warningLabel.Visible = text.Length > 0;
            if (text.Length == 0) return;

            var room = Math.Max(80, _warningLabel.Width - _warningLabel.Padding.Horizontal);
            var needed = TextRenderer.MeasureText(
                text, _warningLabel.Font, new Size(room, 0), TextFormatFlags.WordBreak);
            _warningLabel.Height = Math.Max(46, needed.Height + _warningLabel.Padding.Vertical + 4);
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
                            CurrentIcon = e.IconVectorName,
                            CurrentSmall = e.IconSmallName,
                            CurrentMedium = e.IconMediumName,
                            CurrentLarge = e.IconLargeName
                        })
                        .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                },
                PostWorkCallBack = args =>
                {
                    // The tab can be closed while this was on the wire; the callback still runs.
                    if (IsDisposed) return;

                    // A stale answer's flags are not its own to touch: the generation only moves
                    // on through UpdateConnection, which reset them - and a newer fetch may have
                    // raised one again, which this callback must not lower mid-flight.
                    if (generation != _generation) return;

                    // Whatever happens below, this flag comes up - including on the error path,
                    // or one timeout leaves the button dead for the rest of the session.
                    _loadingTables = false;

                    if (args.Error != null)
                    {
                        _tables = new List<TableItem>();
                        _tableBox.Items.Clear();
                        // What was learned about this org came off the list that just went; a
                        // convention read from tables the window no longer shows is not one.
                        _locations = IconLocations.None;
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

                    var solutions = Service.RetrieveMultiple(query).Entities
                        .Select(e => new SolutionItem
                        {
                            Id = e.Id,
                            UniqueName = e.GetAttributeValue<string>("uniquename"),
                            FriendlyName = e.GetAttributeValue<string>("friendlyname"),
                            Prefix = (e.GetAttributeValue<AliasedValue>("p.customizationprefix")?.Value as string) ?? "new"
                        })
                        .Where(s => !string.IsNullOrWhiteSpace(s.UniqueName))
                        .ToList();

                    // Whose choice this is, when the environment has one to give. A catch-all
                    // coming back is not one: the platform answers GetPreferredSolution with the
                    // CDS default solution when nobody has set a preference, so that answer means
                    // "nobody said", and nobody said is not permission to pick.
                    var preferred = PreferredSolution(Service);
                    foreach (var solution in solutions)
                    {
                        solution.Preferred = preferred != Guid.Empty
                                             && solution.Id == preferred
                                             && !IsACatchAll(solution);
                    }

                    // The one that should be chosen goes to the top of the list rather than being
                    // chosen quietly wherever it happens to sort. A dropped-down list that opens
                    // on its third row reads as an arbitrary default; one that opens on its first
                    // reads as the answer, with the catch-alls kept below it for the org that
                    // really does write through one. Ordering is stable, so within each group the
                    // query's own by-name order survives.
                    args.Result = solutions.OrderBy(Rank).ToList();
                },
                PostWorkCallBack = args =>
                {
                    if (IsDisposed) return;

                    // Same as the tables: a stale answer may not touch the flags.
                    if (generation != _generation) return;

                    _loadingSolutions = false;

                    if (args.Error != null)
                    {
                        // Fatal to applying, though not to browsing: a new web resource has to be
                        // filed somewhere, and the only somewhere left would be the default
                        // solution. Empty the box as well as saying so, or it keeps offering last
                        // time's solution as though this load had answered.
                        _solutions = new List<SolutionItem>();
                        _solutionBox.Items.Clear();
                        _locations = IconLocations.From(_tables.Select(t => t.CurrentIcon), null);
                        _trouble = "Could not load solutions, and an icon needs one to go in, so nothing " +
                                   "can be applied until this load works. Press Load tables again: " +
                                   args.Error.Message;
                        SyncEnabled();
                        return;
                    }

                    _solutions = (List<SolutionItem>)args.Result;

                    // Before the box is filled, because selecting a row in it suggests a name and
                    // the suggestion is only as good as what has been learned by then.
                    _locations = IconLocations.From(
                        _tables.Select(t => t.CurrentIcon),
                        _solutions.Select(s => s.Prefix));

                    _solutionBox.BeginUpdate();
                    _solutionBox.Items.Clear();
                    foreach (var solution in _solutions) _solutionBox.Items.Add(solution);
                    _solutionBox.EndUpdate();

                    if (_solutionBox.Items.Count > 0)
                    {
                        // Only a solution somebody actually chose gets chosen here, which is the
                        // preferred one and nothing else - it sorts first, so this is row 0 when
                        // there is one and no row at all when there is not. Alphabetical order is
                        // not a preference, and a catch-all is where icons go to be invisible to
                        // everything the environment ships.
                        _solutionBox.SelectedIndex = _solutions.FindIndex(s => s.Preferred);
                    }
                    else
                    {
                        // Nothing to file a web resource in, which is not a state to discover at
                        // the end of an apply that cannot be made.
                        _trouble = "This connection sees no unmanaged solution to put a web resource in, " +
                                   "so an icon cannot be applied. It needs one whose publisher prefix the " +
                                   "web resource can be named with.";
                    }

                    // The box may have selected nothing to raise an event about, and the folder
                    // just learned is worth a better suggestion either way.
                    SuggestName();
                    SyncEnabled();
                }
            });
        }

        /// <summary>The Default solution's fixed id, the same in every environment.</summary>
        private static readonly Guid DefaultSolution = new Guid("fd140aaf-4df4-11dd-bd17-0019b9312238");

        /// <summary>The Active solution's fixed id. Not normally visible, but asked for anyway.</summary>
        private static readonly Guid ActiveSolution = new Guid("fd140aae-4df4-11dd-bd17-0019b9312238");

        /// <summary>
        /// The CDS default solution's fixed id. This one is worth an id rather than a name: its
        /// unique name is generated per environment - Cr89693 and the like - and its friendly
        /// name is English, so a localised environment would not be recognised by name at all.
        /// </summary>
        private static readonly Guid CdsDefaultSolution = new Guid("00000001-0000-0000-0001-00000000009b");

        /// <summary>
        /// The solutions everything unmanaged falls into when nobody chooses. Matched by id
        /// first, and by name as well, so an environment where one of these ids is not what it is
        /// here is still recognised - and one that has renamed them still is too.
        /// </summary>
        private static bool IsACatchAll(SolutionItem solution)
        {
            return solution.Id == DefaultSolution
                   || solution.Id == ActiveSolution
                   || solution.Id == CdsDefaultSolution
                   || solution.UniqueName.Equals("Default", StringComparison.OrdinalIgnoreCase)
                   || solution.UniqueName.Equals("Active", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(solution.FriendlyName, "Common Data Services Default Solution",
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Where a solution sorts: the environment's own answer first, then the real solutions in
        /// the order the query gave them, then the catch-alls.
        /// </summary>
        private static int Rank(SolutionItem solution)
        {
            if (solution.Preferred) return 0;
            return IsACatchAll(solution) ? 2 : 1;
        }

        /// <summary>
        /// The solution this user has told the maker portal they work in, or an empty id.
        ///
        /// GetPreferredSolution is an unbound function with no parameters, whose one response
        /// field is the whole solution row. It is asked here rather than guessed at, because a
        /// preference somebody has actually stated beats anything a sort order can infer.
        /// </summary>
        private static Guid PreferredSolution(IOrganizationService service)
        {
            try
            {
                var response = service.Execute(new OrganizationRequest("GetPreferredSolution"));

                object value;
                if (!response.Results.TryGetValue("PreferredSolution", out value)) return Guid.Empty;

                var row = value as Entity;
                return row?.Id ?? Guid.Empty;
            }
            catch (Exception)
            {
                // An environment from before the preferred solution existed, or one with the
                // feature switched off, answers this by refusing it. Nothing is wrong; there is
                // just nothing to hear, and the ordering below it stands on its own.
                return Guid.Empty;
            }
        }

        private void ShowTables()
        {
            var filter = (_tableFilterBox.Text ?? "").Trim();
            var unsetOnly = _unsetOnlyBox.Checked;
            var matching = _tables.Where(t =>
                (!unsetOnly || string.IsNullOrEmpty(t.CurrentIcon)) &&
                (filter.Length == 0 ||
                 t.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 t.LogicalName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0));

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

            _iconConcern = null;

            if (icon != null && _catalog != null)
            {
                try
                {
                    var warnings = SvgOutput.Inspect(_catalog.ReadSvg(icon));
                    if (warnings.Count > 0) _iconConcern = string.Join(" ", warnings);
                }
                catch (Exception ex)
                {
                    _iconConcern = "This icon would not read: " + ex.Message;
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
            var solution = _solutionBox.SelectedItem as SolutionItem;
            if (table == null || icon == null || _catalog == null) return;

            // SyncEnabled will not have let it get here. Said again anyway, because this is the
            // one thing between an icon and the default solution, and the button is not the only
            // way in - a double-click on a cell applies too.
            if (solution == null)
            {
                MessageBox.Show(this,
                    "Pick the unmanaged solution the new web resource should go in first. Without one it " +
                    "would land in the default solution, where nothing you ship can see it.",
                    "No solution chosen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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

            // Only when the PNGs are being written: nothing happens to the classic slots
            // otherwise, and an unasked-for opinion about the table's older icons is not this
            // tool's to give.
            var handover = _rasterBox.Checked
                ? RasterIcons.Handover(name, table.CurrentSmall, table.CurrentMedium, table.CurrentLarge)
                : null;
            if (handover != null) handover = "\n\n" + handover;

            // The last gate before the round trip is the place to say it again: from here on it
            // is the platform's answer rather than this window's.
            var concern = NameConcern();
            if (concern != null) concern = "\n\n" + concern;

            var confirm = MessageBox.Show(this,
                "Set " + icon.DisplayName + " as the icon for " + table.DisplayName + "?\n\n" +
                "This writes the web resource " + name + ", adding it to " + solution.FriendlyName +
                " if it is new, and publishes the table." + replacing + handover + concern,
                "Set table icon", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

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
                SolutionUniqueName = solution.UniqueName,
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

                    // Same as the loads: a stale answer may not touch the flags. A connection
                    // change already cancelled this worker, reset the flags and said so.
                    if (generation != _generation) return;

                    _applying = false;

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
                    if (request.IncludeRasterIcons)
                    {
                        table.CurrentSmall = RasterIcons.NameFor(name, IconSlot.Small);
                        table.CurrentMedium = RasterIcons.NameFor(name, IconSlot.Medium);
                        table.CurrentLarge = RasterIcons.NameFor(name, IconSlot.Large);
                    }

                    // And the list on screen disagrees with the list in memory unless it is
                    // rebuilt: a table just given an icon has no business still being offered
                    // under "only tables with no icon yet". It drops out of the combo, which is
                    // the point - that one is done.
                    ShowTables();

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

            // A write still out belongs to the org this control is leaving. The call on the
            // wire cannot be recalled, but the ones behind it can - and must be, or the tool
            // keeps changing an environment the window no longer shows.
            var applying = _applying;

            // A different org has different tables. Bump the generation so anything still on
            // the wire for the old one is discarded rather than shown as this one's.
            _generation++;
            _loadingTables = false;
            _loadingSolutions = false;
            _applying = false;

            _tables = new List<TableItem>();
            _solutions = new List<SolutionItem>();
            // Where the last org kept its icons says nothing about where this one does, and
            // neither does the folder somebody chose for a table that is not in this one.
            _locations = IconLocations.None;
            _typedFolder = null;
            _suggestedFor = null;
            _tableBox.Items.Clear();
            _solutionBox.Items.Clear();
            _trouble = null;
            _outcome = null;

            if (applying)
            {
                // Only the apply is marked cancelable, so only the apply may be cancelled here.
                // Its callback will land with a stale generation and say nothing, so this line
                // is the only report the user gets of what the old org may be left holding.
                CancelWorker();
                _trouble = "The connection changed while an icon was being applied. The previous " +
                           "environment may or may not have been changed - reconnect to it to see.";
            }

            SyncEnabled();
        }

        private void ShowAbout()
        {
            var text = _catalog?.About ?? "The icon library is not loaded.";
            MessageBox.Show(this, text, "About the icons", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// When the three PNGs are worth writing, which is not often. Everything here is either
        /// Microsoft's documented position or a property of this tool; nothing is guessed about
        /// where a given icon renders today.
        /// </summary>
        private void ShowRasterHelp()
        {
            var text =
                "Model-driven apps use the vector icon, and Microsoft's guidance is to set that " +
                "one instead of these three. Off is the right default.\n" +
                "\n" +
                "It is not only tidiness. A PNG has its colour baked in, so it cannot follow the " +
                "app theme the way the vector icon does - and a table whose two icons disagree " +
                "about its colour looks worse than a table with one icon.\n" +
                "\n" +
                "The three are the properties that predate the Unified Interface:\n" +
                "\n" +
                "    16x16  IconSmallName - the classic \"icon in web application\".\n" +
                "    32x32  IconMediumName - the classic \"icon for entity forms\".\n" +
                "    66x48  IconLargeName - of which Microsoft's own SDK reference says:\n" +
                "           \"This size icon is not currently used by the application.\"\n" +
                "\n" +
                "So tick this when something that reads the older properties is still in the " +
                "picture: a classic or on-premises web client, or an integration, report or " +
                "client of your own that reads IconSmallName rather than IconVectorName. If " +
                "everything that shows this table is a model-driven app, the vector icon on its " +
                "own is enough.";

            MessageBox.Show(this, text, "The classic PNG icons",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SuggestName()
        {
            var table = _tableBox.SelectedItem as TableItem;
            var solution = _solutionBox.SelectedItem as SolutionItem;
            if (table == null) return;

            var typed = _nameBox.Text;

            // An empty box is the one thing a user can do to the name that says nothing about
            // where they want it, so it is how the org's own convention is asked for again.
            if (typed.Length == 0) _typedFolder = null;

            if (typed.Length > 0 && typed != _suggestedName)
            {
                // The folder out of a hand-typed name travels: the next table somebody picks
                // belongs beside this one, not back where the majority of the org votes.
                var folder = IconLocations.FolderOf(typed);
                if (folder != null) _typedFolder = folder;

                // The name itself does not travel. It is this table's, and carrying it to the
                // next one would quietly point two tables at a single web resource. So a
                // hand-edited name is left alone for as long as its own table is the one chosen -
                // which is every reload and every table-filter keystroke that re-selects it.
                if (table.LogicalName == _suggestedFor) return;
            }

            _suggestedFor = table.LogicalName;
            _suggestedName = Suggest(table, solution);
            _nameBox.Text = _suggestedName;
        }

        /// <summary>
        /// Where this table's icon should go, in the order the answers are worth having: the
        /// name it already uses, then the folder somebody here typed, then the folder the org
        /// keeps its icons in. The prefix is always the chosen solution's, because that is the
        /// publisher a new web resource has to be named for.
        /// </summary>
        private string Suggest(TableItem table, SolutionItem solution)
        {
            // An icon the table already has, that this connection can write, wins everything:
            // applying then updates one web resource rather than leaving a second beside it.
            if (_locations.Reusable(table.CurrentIcon)) return table.CurrentIcon.Trim();

            var prefix = solution?.Prefix ?? "new";

            return _typedFolder != null
                ? WebResourceName.ForTable(prefix, table.LogicalName, _typedFolder)
                : _locations.NameFor(null, prefix, table.LogicalName);
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
