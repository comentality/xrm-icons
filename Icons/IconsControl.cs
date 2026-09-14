using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Icons.Catalog;
using Icons.Logic;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;
using Comentality.Xtb.About;
using Label = System.Windows.Forms.Label;

namespace Icons
{
    public partial class IconsControl : PluginControlBase, IHelpPlugin, IAboutPlugin
    {
        /// <summary>
        /// The page this tool has on the site. XrmToolBox puts it in its own Help menu, as
        /// "Help for Icons", for whoever looks there rather than at the toolbar.
        /// </summary>
        public string HelpUrl
        {
            get { return "https://comentality.com/xrm/xtb/icons"; }
        }

        /// <summary>The host's own way in to the same box the toolbar opens.</summary>
        public void ShowAboutDialog()
        {
            ShowAbout();
        }

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

            /// <summary>
            /// The drawing a model-driven app already puts beside this table without anybody
            /// having given it one, or null for a table no app draws. Empty for every table the
            /// org would not answer for, which is what this tool believed before it asked.
            /// </summary>
            public string Stock;

            /// <summary>Nothing stored on the table, and no app drawing one either.</summary>
            public bool HasNoIcon
            {
                get { return string.IsNullOrEmpty(CurrentIcon) && string.IsNullOrEmpty(Stock); }
            }

            public override string ToString()
            {
                return DisplayName + "  (" + LogicalName + ")";
            }
        }

        /// <summary>
        /// An apply that can be taken back, and put back again.
        ///
        /// It holds names rather than the TableItem it was applied to: a reload builds new ones,
        /// and a stack pointing at the old objects would be putting icons on tables the window
        /// is no longer showing. What the org held before comes from the apply's own result -
        /// the org's answer from a moment before the write, not this window's memory of it.
        /// </summary>
        private sealed class AppliedChange
        {
            public string TableLogicalName;
            public string TableDisplayName;
            public string IconDisplayName;

            public string PreviousVector;
            public string PreviousSmall;
            public string PreviousMedium;
            public string PreviousLarge;

            /// <summary>The drawings this apply wrote over, and what was in each before.</summary>
            public IList<OverwrittenResource> Overwritten;

            /// <summary>The whole write again, for doing it a second time.</summary>
            public IconApplyRequest Request;
        }

        /// <summary>
        /// What the solutions load comes back with: the solutions, and the convention read off
        /// the icons this environment already has, which needs the publisher prefixes that
        /// only this load knows.
        /// </summary>
        private sealed class SolutionLoad
        {
            public List<SolutionItem> Solutions;
            public IconLocations Locations;
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

        /// <summary>
        /// Whether a write is out: an apply, an undo of one, or a redo. All three are the same
        /// shape - a few calls and a publish - and the window may not start a second one.
        /// </summary>
        private bool _applying;

        /// <summary>What the status line calls the write that is out.</summary>
        private string _writeStep = "Applying...";

        /// <summary>
        /// The applies that can be taken back, newest on top, and the ones taken back that can
        /// be done again. Both are emptied when the connection changes: they name web resources
        /// and tables in one org, and mean nothing in the next.
        /// </summary>
        private readonly Stack<AppliedChange> _undoable = new Stack<AppliedChange>();
        private readonly Stack<AppliedChange> _redoable = new Stack<AppliedChange>();

        /// <summary>What went wrong with the last thing asked, or null. Outlives its dialog.</summary>
        private string _trouble;

        /// <summary>What the last change actually did, for the status line.</summary>
        private string _outcome;

        /// <summary>
        /// Every step of it, for the status line's tooltip. A write that worked is not news
        /// worth a dialog and a click to dismiss - but which web resource was created and which
        /// was updated is worth being able to read afterwards, so it is kept rather than shown.
        /// </summary>
        private string _outcomeSteps;

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
        private SplitContainer _targetSplit;
        private bool _targetSplitLaid;

        /// <summary>
        /// Set once somebody drags the splitter between the preview and the form. From then on
        /// the height of the form pane is their answer and not ours, so nothing below re-lays it.
        /// The programmatic set raises the same event, hence the flag that says which is which.
        /// </summary>
        private bool _targetSplitDragged;
        private bool _layingTargetSplit;

        /// <summary>Set while a pane re-layout is posted and has not run yet - see SettlePane.</summary>
        private bool _settlingPane;

        /// <summary>The form itself and its scrolling host - measured together to work out how
        /// much height the lower pane is asking for.</summary>
        private TableLayoutPanel _form;
        private Panel _formHost;

        /// <summary>
        /// What the buttons with no words on them would have said. A drawing is quicker to find
        /// than a caption and says nothing at all on its own, so each one still carries its
        /// sentence - and its accessible name, for the reader that has only that.
        /// </summary>
        private readonly ToolTip _hints = new ToolTip();

        private Button _loadButton;
        private Button _applyButton;
        private ToolStripButton _cancelButton;
        private ToolStripButton _undoButton;
        private ToolStripButton _redoButton;
        private ToolStripButton _aboutButton;
        private ToolStripLabel _statusLabel;

        private TextBox _searchBox;
        private ComboBox _familyBox;
        private ComboBox _styleBox;
        private CheckBox _familyToggle;
        private ListView _grid;
        private ListBox _tableBox;
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

        /// <summary>The last search a table put in the box, so a typed one is recognisable.</summary>
        private string _seededSearch;

        /// <summary>Which table that search was for, so the same table is not seeded twice.</summary>
        private string _seededFor;

        /// <summary>
        /// The last table the window was actually put on. Arriving at a table and leaving one
        /// for another look identical from inside the handler, and they are not the same thing:
        /// an icon chosen before any table is the reason a table is being picked at all, while
        /// an icon chosen with a table up belonged to that table.
        /// </summary>
        private string _lastTable;

        /// <summary>What is worth knowing about the chosen icon, the solution, and the name.</summary>
        private string _iconConcern;
        private string _licenceConcern;

        /// <summary>
        /// Where the window asks what a family is licensed under. A field rather than three
        /// calls to the static table, because no family bundled today asks for a credit line -
        /// so everything the window does about one is unreachable, and unreachable is how it
        /// would stay until the first CC BY set is bundled and somebody finds out in a
        /// customer's org. The harness puts a set that does ask through here instead.
        /// </summary>
        private Func<IconFamily, IconLicence> _licences = IconLicence.For;
        private string _nameConcern;
        private string _solutionConcern;

        /// <summary>
        /// The drawing behind each web resource name this environment keeps an icon under,
        /// read with the tables. Only the ones under this environment's own publisher prefixes:
        /// Microsoft's hundreds are neither ours to recognise nor worth the bytes.
        /// </summary>
        private readonly Dictionary<string, string> _stored =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>What the chosen table is already carrying, once it has been worked out.</summary>
        private IconIdentity _currentIdentity;

        /// <summary>Set while several filters are being moved at once, so the grid is ranked once.</summary>
        private bool _refiltering;

        private RadioButton _themeColour;
        private RadioButton _fixedColour;
        private Button _colourButton;
        private CheckBox _rasterBox;
        private LinkLabel _rasterHelp;
        private CheckBox _hideLockedBox;
        private Label _lockedLabel;
        private LinkLabel _currentLabel;
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

            SyncUndo();

            _tableBox.Enabled = !Busy && _tables.Count > 0;
            _tableFilterBox.Enabled = !Busy && _tables.Count > 0;
            _unsetOnlyBox.Enabled = !Busy && _tables.Count > 0;
            _hideLockedBox.Enabled = !Busy && _tables.Count > 0;
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

            // The preview is showing what is chosen now, not what was chosen a change ago.
            RefreshContextIcon();

            _licenceConcern = _selectedIcon == null
                ? null
                : AttributionNotice.For(_licences(_selectedIcon.Family), _nameBox.Text.Trim());
            _nameConcern = NameConcern();
            _solutionConcern = SolutionConcern();
            ShowConcerns();

            UpdateStatus();
        }

        /// <summary>
        /// The two buttons that write a change out again, backwards or forwards, and what they
        /// say they would do. The keystrokes are the point and the buttons are how anybody finds
        /// out they exist - so the tooltip names the table and the icon rather than explaining
        /// what undo is.
        /// </summary>
        private void SyncUndo()
        {
            _undoButton.Enabled = !Busy && _undoable.Count > 0;
            _redoButton.Enabled = !Busy && _redoable.Count > 0;

            _undoButton.ToolTipText = _undoable.Count == 0
                ? "Nothing to take back yet (Ctrl+Z)"
                : "Put " + _undoable.Peek().TableDisplayName + " back the way it was before " +
                  _undoable.Peek().IconDisplayName + " went on it (Ctrl+Z)";

            _redoButton.ToolTipText = _redoable.Count == 0
                ? "Nothing to do again (Ctrl+Y)"
                : "Set " + _redoable.Peek().IconDisplayName + " on " +
                  _redoable.Peek().TableDisplayName + " again (Ctrl+Y)";
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
        /// How tall the warning line has to be to hold what it says, at the width the pane has
        /// given it. Measured rather than left to the label, which would measure the sentence
        /// as one line and let the pane cut it off mid-word.
        ///
        /// Setting the height brings us back here through SizeChanged, so the guard is what
        /// makes the second visit the last one.
        /// </summary>
        private void SizeWarning()
        {
            if (!_warningLabel.Visible || _warningLabel.Width <= 0) return;

            var room = Math.Max(80, _warningLabel.Width - _warningLabel.Padding.Horizontal);
            var needed = TextRenderer.MeasureText(
                _warningLabel.Text, _warningLabel.Font, new Size(room, 0), TextFormatFlags.WordBreak);

            var height = needed.Height + _warningLabel.Padding.Vertical + 4;
            if (_warningLabel.Height == height) return;

            _warningLabel.Height = height;
            FitForm();
        }

        /// <summary>
        /// The one place the warning line is written. Two things can be worth saying at once - an
        /// icon that carries its own colours, a name under another publisher's prefix - and a
        /// label that cannot grow would swallow the second.
        /// </summary>
        private void ShowConcerns()
        {
            var text = string.Join("  ", new[] { _iconConcern, _licenceConcern, _solutionConcern, _nameConcern }
                .Where(concern => !string.IsNullOrEmpty(concern)));

            _warningLabel.Text = text;
            _warningLabel.Visible = text.Length > 0;

            // What it now says may want more lines than what it said. That height comes out of
            // the form's room, so the pane is asked to grow by as much.
            SizeWarning();
            FitForm();
        }

        /// <summary>
        /// Why the table just chosen cannot take an icon, said under the box it was chosen in.
        /// It is not an error and does not go in the red line at the foot of the pane: nothing
        /// was attempted and nothing failed. With the hiding box ticked no such table is in the
        /// list to choose, so this is what somebody who unticked it sees.
        /// </summary>
        private void ShowLocked(TableItem table)
        {
            var locked = table != null && !table.Customizable;

            _lockedLabel.Text = locked
                ? table.DisplayName + " is locked against customisation, so its icon cannot be changed."
                : "";
            _lockedLabel.Visible = locked;
        }

        /// <summary>
        /// Which library icon the chosen table is already carrying, said under the box it was
        /// chosen in - and put in the grid when the grid is the window's to move.
        ///
        /// There are four things this can know, and they are worth telling apart. It can name
        /// the icon. It can have read the drawing and found it is none of the bundled ones,
        /// which is a hand-drawn icon or one from a set this tool does not carry. It can have
        /// the name of a web resource it never read, which is every icon under somebody else's
        /// publisher prefix - Microsoft's own, mostly. It can have nothing stored on the table
        /// and an app drawing one anyway, which is every standard table and the reason Account is
        /// not in the short list. Or the table has no icon at all, and there is nothing to say.
        /// </summary>
        private void ShowCurrentIcon(TableItem table, bool mine)
        {
            _currentIdentity = null;
            _currentLabel.Text = "";
            _currentLabel.LinkArea = new LinkArea(0, 0);
            _currentLabel.Visible = false;

            var name = (table?.CurrentIcon ?? "").Trim();
            if (name.Length == 0)
            {
                // Nothing stored, but something on screen. Said plainly, because a user who has
                // just unticked the box to find Account is owed the reason it was not there -
                // and because "no icon" and "no icon of its own" are different errands.
                if (string.IsNullOrEmpty(table?.Stock)) return;

                _currentLabel.Text = "On its stock icon - the one an app draws for a standard " +
                                     "table. Nothing is stored on the table, and choosing an " +
                                     "icon below replaces it.";
                _currentLabel.Visible = true;
                return;
            }

            string svg;
            var read = _stored.TryGetValue(name, out svg);
            _currentIdentity = read ? IconIdentifier.Identify(svg, _catalog) : null;

            // The grid follows the table onto whatever it is already carrying, so long as
            // nothing on screen is somebody else's: a search they typed, or an icon they picked
            // before any table was up, which is the icon this table is being picked for. An
            // icon picked with another table up is that table's, and is left behind with it.
            if (mine && _currentIdentity?.Entry != null) Choose(_currentIdentity.Entry);

            string said;
            if (_currentIdentity != null) said = "Carrying " + _currentIdentity.Described + ".";
            else if (read) said = "Carrying " + name + ", which is none of the bundled icons.";
            else said = "Carrying " + name + ".";

            // The link is only worth offering when there is somewhere to go: an icon in the
            // bundle, not already the one in hand.
            var goable = _currentIdentity?.Entry != null && !ReferenceEquals(_currentIdentity.Entry, _selectedIcon);
            var link = goable ? "  Show it in the library" : "";

            _currentLabel.Text = said + link;
            _currentLabel.LinkArea = goable ? new LinkArea(said.Length + 2, link.Length - 2) : new LinkArea(0, 0);
            _currentLabel.Visible = true;
        }

        /// <summary>
        /// Puts the grid on one icon, if that icon is among the ones the filters and the search
        /// are showing. False when it is not: finding it would mean changing what is on screen,
        /// which is a thing to be asked for rather than done.
        /// </summary>
        private bool Choose(IconEntry icon)
        {
            var index = IndexOf(_shown, icon);
            if (index < 0) return false;
            if (_grid.SelectedIndices.Count == 1 && _grid.SelectedIndices[0] == index) return true;

            _grid.SelectedIndices.Clear();
            _grid.SelectedIndices.Add(index);
            _grid.EnsureVisible(index);

            return true;
        }

        /// <summary>
        /// Goes to the icon the table is carrying, widening whatever was hiding it: its own
        /// family, both weights, and its own name in the search box.
        ///
        /// Only ever from the link, never by itself. The search it writes counts as the
        /// window's own, so the next table seeds its search normally rather than finding the
        /// box occupied for the rest of the session.
        /// </summary>
        private void ShowCurrentIconInLibrary()
        {
            var icon = _currentIdentity?.Entry;
            if (icon == null) return;

            _refiltering = true;
            try
            {
                _familyBox.SelectedItem = icon.Family;
                _styleBox.SelectedIndex = 0;
                _seededSearch = icon.Name.Replace('_', ' ').Replace('-', ' ');
                _seededFor = (_tableBox.SelectedItem as TableItem)?.LogicalName;
                _searchBox.Text = _seededSearch;
            }
            finally
            {
                _refiltering = false;
            }

            ApplyFilter();

            // The search is the icon's own name, so it is in the grid - but a name that ranks
            // below the fold of nothing would leave the link doing nothing, and an empty box
            // shows the whole library, where it certainly is.
            if (!Choose(icon))
            {
                _searchBox.Text = "";
                _seededSearch = "";
                Choose(icon);
            }

            ShowCurrentIcon(_tableBox.SelectedItem as TableItem, false);
            _grid.Focus();
        }

        /// <summary>
        /// Whether the library is still the window's to move: the search box holds what the
        /// window last put there or nothing at all, and nothing on screen is work in progress.
        ///
        /// A search somebody typed is theirs, always. An icon picked out of an empty box is
        /// theirs too - right up until they leave the table they picked it for. Both orders are
        /// real: find the icon and then say which table gets it, or pick the table and then go
        /// looking. The first is why an icon in hand holds the library at all; the second is why
        /// it stops holding it at the next table, where a box still searching for the last
        /// table's word over the last table's icon is the errand before this one.
        /// </summary>
        private bool TheLibraryIsMineToMove(bool leavingAnotherTable)
        {
            var current = (_searchBox.Text ?? "").Trim();
            if (current.Length > 0) return current == _seededSearch;

            return leavingAnotherTable || _selectedIcon == null;
        }

        private void UpdateStatus()
        {
            if (IsDisposed) return;

            string text;
            if (_loadingTables) text = "Loading tables...";
            else if (_loadingSolutions) text = "Loading solutions...";
            else if (_applying) text = _writeStep;
            else if (_tables.Count == 0) text = _library.Count + " icons. Refresh the table list to choose where one goes.";
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
            _statusLabel.ToolTipText = _outcomeSteps ?? "";

            _troubleLabel.Text = _trouble ?? "";
            _troubleLabel.Visible = _trouble != null;
            FitForm();
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

            // What was read last time is about to be out of date, and the last link of this
            // load is what refills it. Cleared here rather than there, so a load whose preview
            // link is skipped cannot leave the previous load's drawings standing.
            _stored.Clear();
            _trouble = null;
            _outcome = null;
            _outcomeSteps = null;
            SyncEnabled();

            // Read here rather than in the worker: the connection belongs to the UI thread, and
            // the worker only needs the address out of it. An org that does not say where it
            // lives is one nothing can be asked of, and the stock icons are simply not read.
            var orgUrl = StockIcons.OrgAddress(
                ConnectionDetail?.WebApplicationUrl,
                ConnectionDetail?.OriginalUrl,
                ConnectionDetail?.OrganizationServiceUrl);

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

                    var listed = response.EntityMetadata
                        .Where(e => e.IsIntersect != true && e.IsPrivate != true)
                        .ToList();

                    // Only the empty ones. A table already carrying a web resource is drawn from
                    // it rather than from the app's own font, so what the app would otherwise
                    // have drawn is not what is on screen and not worth a round trip.
                    var empty = listed
                        .Where(e => string.IsNullOrEmpty(e.IconVectorName))
                        .Select(e => e.ObjectTypeCode);
                    var stock = ReadStockIcons(orgUrl, empty);

                    args.Result = listed
                        .Select(e => new TableItem
                        {
                            LogicalName = e.LogicalName,
                            DisplayName = e.DisplayName?.UserLocalizedLabel?.Label ?? e.SchemaName ?? e.LogicalName,
                            Customizable = e.IsCustomizable == null || e.IsCustomizable.Value,
                            CurrentIcon = e.IconVectorName,
                            CurrentSmall = e.IconSmallName,
                            CurrentMedium = e.IconMediumName,
                            CurrentLarge = e.IconLargeName,
                            Stock = Drawing(stock, e.ObjectTypeCode)
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

        /// <summary>
        /// One client for the life of the tool. A new one per load leaks a socket per load, and
        /// this one is only ever pointed at the org the tool is already connected to.
        ///
        /// The timeout is per request and short on purpose: a stock icon is a nicety, and no
        /// single one of them is worth making somebody wait for the table list.
        /// </summary>
        private static readonly HttpClient StockHttp =
            new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        /// <summary>How many of these to have in the air at once.</summary>
        private const int StockAtOnce = 8;

        /// <summary>
        /// The whole read, however many tables there are. Six hundred of them against a real
        /// environment is under half a second; this is the wall for the environment that is not
        /// real, where every one of them is going to time out and the user is waiting on a
        /// nicety. What has come back by then is kept, and the rest are simply not known.
        /// </summary>
        private static readonly TimeSpan StockBudget = TimeSpan.FromSeconds(20);

        /// <summary>
        /// What a model-driven app already draws beside each of these tables.
        ///
        /// On the worker thread, as part of the load, so the list arrives knowing which of its
        /// tables are really empty. Nothing here can fail the load: every failure - no address,
        /// a proxy, a timeout, a host that answers with a sign-in page - comes back as a table
        /// nothing is known about, which is a table with no icon, which is what this tool
        /// believed before it asked.
        /// </summary>
        private static IDictionary<int, string> ReadStockIcons(string orgUrl, IEnumerable<int?> codes)
        {
            if (string.IsNullOrEmpty(orgUrl)) return new Dictionary<int, string>();

            var wanted = codes.Where(c => c.HasValue).Select(c => c.Value).Distinct().ToList();
            var got = new ConcurrentDictionary<int, string>();

            // Not a using between them: the budget and the gate outlive this method by however
            // long the last read takes to notice it is over. Disposing them on the way out of a
            // block races the reads they belong to - a straggler that has just been cancelled
            // still has a gate to release, and releasing a disposed one turns a read that was
            // merely late into a faulted task with nobody left to catch it. They are taken down
            // by the last read instead, below.
            var gate = new SemaphoreSlim(StockAtOnce);
            var budget = new CancellationTokenSource(StockBudget);

            {
                var reads = wanted.Select(async code =>
                {
                    try
                    {
                        await gate.WaitAsync(budget.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // The budget went while this one was queued. Nothing was taken, so
                        // nothing is released.
                        return;
                    }

                    try
                    {
                        var svg = await FetchStockIcon(orgUrl, code, budget.Token).ConfigureAwait(false);
                        if (svg != null) got[code] = svg;
                    }
                    catch (Exception)
                    {
                        // One address that would not answer is one table this tool cannot speak
                        // for, and the others are still worth having.
                    }
                    finally
                    {
                        gate.Release();
                    }
                }).ToArray();

                try
                {
                    Task.WaitAll(reads, StockBudget);
                }
                catch (Exception)
                {
                    // WaitAll throws for the ones that faulted. They have already been counted
                    // as tables nothing is known about.
                }

                // Whatever has not come back by now is not going to be waited for, so stop it
                // rather than leaving eight sockets running out their own clocks behind a table
                // list that has already been handed over. Cancelling is also what lets the last
                // read finish promptly enough to take these two down.
                try
                {
                    budget.Cancel();
                }
                catch (Exception)
                {
                    // A budget that has already fired is already cancelled.
                }

                Task.WhenAll(reads).ContinueWith(done =>
                {
                    // Read, so the aggregate is observed here rather than left to the finaliser
                    // and whatever the host does with one of those.
                    var observed = done.Exception;
                    GC.KeepAlive(observed);

                    gate.Dispose();
                    budget.Dispose();
                }, TaskScheduler.Default);
            }

            // Back through the rules, so what counts as a drawing is decided in one place and
            // the place that decides it is the one under test.
            return StockIcons.DrawnAmong(wanted.Select(c => (int?)c), code =>
            {
                string svg;
                return got.TryGetValue(code, out svg) ? svg : null;
            });
        }

        private static async Task<string> FetchStockIcon(string orgUrl, int code, CancellationToken token)
        {
            var address = StockIcons.Address(orgUrl, code);
            if (address == null) return null;

            using (var response = await StockHttp.GetAsync(address, token).ConfigureAwait(false))
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var type = response.Content.Headers.ContentType?.MediaType;
                return StockIcons.IsDrawing((int)response.StatusCode, type, body?.Length ?? 0)
                    ? body
                    : null;
            }
        }

        /// <summary>
        /// The drawing an app puts beside this table without anybody having given it one, read
        /// during the load. Empty for a table nothing came back for, and for every table when
        /// the org would not answer at all.
        /// </summary>
        private string StockFor(string logicalName)
        {
            if (string.IsNullOrEmpty(logicalName)) return null;

            return _tables
                .FirstOrDefault(t => string.Equals(t.LogicalName, logicalName,
                    StringComparison.OrdinalIgnoreCase))
                ?.Stock;
        }

        /// <summary>What was read for this table, or null for one nothing came back for.</summary>
        private static string Drawing(IDictionary<int, string> stock, int? code)
        {
            string svg;
            return code.HasValue && stock.TryGetValue(code.Value, out svg) ? svg : null;
        }

        private void LoadSolutions()
        {
            if (_loadingSolutions) return;

            var generation = _generation;
            _loadingSolutions = true;
            SyncEnabled();

            // Read here rather than in the worker: the list belongs to the UI thread, and the
            // worker only needs the names out of it.
            var icons = _tables.Select(t => t.CurrentIcon).ToList();

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
                    var ordered = solutions.OrderBy(Rank).ToList();

                    args.Result = new SolutionLoad
                    {
                        Solutions = ordered,
                        Locations = IconLocations.From(icons, ordered.Select(x => x.Prefix))
                    };
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
                                   "can be applied until this load works. Refresh the table list again: " +
                                   args.Error.Message;
                        SyncEnabled();
                        return;
                    }

                    var loaded = (SolutionLoad)args.Result;
                    _solutions = loaded.Solutions;

                    // Before the box is filled, because selecting a row in it suggests a name and
                    // the suggestion is only as good as what has been learned by then.
                    _locations = loaded.Locations;

                    _stored.Clear();

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

                    // Last, and only on the way through: the preview pane is on screen from the
                    // moment the tool opens, and this is what turns its stand-in into this
                    // environment's own navigation. A load that got this far has a connection
                    // that answers, so the query is worth making without being asked for.
                    ReadApps();
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
            var hideLocked = _hideLockedBox.Checked;
            var matching = _tables.Where(t =>
                // A table an app already draws is not one nobody has got to. Account has nothing
                // stored on it and never has had, and the errand this box is for - find the
                // tables that look unfinished - is not served by listing every standard table in
                // the org alongside the ones that really are blank.
                (!unsetOnly || t.HasNoIcon) &&
                (!hideLocked || t.Customizable) &&
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

            // A filter typed down to a single table has already named it, and reaching for the
            // one row left to say so is a click for nothing. Only when something was typed:
            // a list of one because the unset-only list is nearly finished is not somebody
            // pointing at a table.
            if (_tableBox.SelectedIndex < 0 && filter.Length > 0 && _tableBox.Items.Count == 1)
                _tableBox.SelectedIndex = 0;
        }

        /// <summary>
        /// The table to carry on with, once the one just applied to has left the list.
        ///
        /// Only while the unset-only list is showing. That list is a working list and it has
        /// just got shorter, so whatever took the applied table's place is the next one waiting
        /// for an icon, and a run down it is pick, apply, pick, apply. With the box unticked
        /// nothing dropped out and the selection still stands: that is somebody working in their
        /// own order, and moving it under them would be a guess about what they meant next.
        /// </summary>
        private void AdvanceToNextTable(int wasAt)
        {
            if (!_unsetOnlyBox.Checked) return;

            // The rebuild put the selection back, so nothing left the list and there is nothing
            // to advance past.
            if (_tableBox.SelectedIndex >= 0) return;
            if (_tableBox.Items.Count == 0) return;

            // The row that took its index, or the last one when it was the end of the list -
            // skipping the tables locked against customisation. With the hiding box ticked there
            // are none to skip; with it unticked the unset-only list is where they gather, since
            // a table that will never take an icon has never been given one. Landing on a dead
            // end costs the click it was meant to save.
            var start = Math.Min(Math.Max(wasAt, 0), _tableBox.Items.Count - 1);

            for (var i = start; i < _tableBox.Items.Count; i++)
                if (Customizable(i)) { _tableBox.SelectedIndex = i; return; }

            for (var i = start - 1; i >= 0; i--)
                if (Customizable(i)) { _tableBox.SelectedIndex = i; return; }

            // Nothing left in the list that can be given an icon. Leave the box empty rather
            // than land on something that cannot be applied to.
        }

        private bool Customizable(int index)
        {
            var table = _tableBox.Items[index] as TableItem;
            return table != null && table.Customizable;
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

        /// <summary>
        /// Puts the chosen table's own name in the search box, so picking Account shows the
        /// buildings without anybody typing "account" first. The library already knows how to
        /// read a Dataverse word; this is only the tool asking it the question the user was
        /// about to type.
        ///
        /// It writes over its own last suggestion, and over an empty box. A search somebody
        /// typed is theirs and is left alone, and so is an icon picked before any table was
        /// chosen - that icon is what the table is being picked for. Once a table has been up,
        /// what is on screen belongs to it and is left behind with it.
        ///
        /// Whether the library is the window's is worked out by the caller, which knows which
        /// table this arrived from. Asking again here would be asking it of a search box the
        /// answer is about to be written into.
        /// </summary>
        private void SeedSearch(TableItem table, bool mine)
        {
            if (table == null || _catalog == null) return;
            if (_seededFor == table.LogicalName) return;

            if (!mine) return;

            // A table whose name the library has no answer for empties the box rather than
            // leaving the last table's search standing over a grid that is not about it. Asked
            // of what the filters are showing, not of the whole bundle: the point of the seed
            // is a grid with something in it, and Lucide alone answers fewer words than four
            // families do.
            _seededSearch = IconSearch.SeedFor(Narrowed(), table.DisplayName);
            _seededFor = table.LogicalName;
            _searchBox.Text = _seededSearch;
        }

        /// <summary>
        /// The part of the library the two filters leave, before anything is searched. Both the
        /// grid and the seed ask for this, so a seed can never suggest a search that the filters
        /// have already ruled out.
        /// </summary>
        private IReadOnlyList<IconEntry> Narrowed()
        {
            var wanted = _library;

            if (_familyBox.SelectedItem is IconFamily)
            {
                var family = (IconFamily)_familyBox.SelectedItem;
                wanted = wanted.Where(i => i.Family == family).ToList();
            }

            var style = _styleBox.SelectedIndex;
            if (style == 1) wanted = wanted.Where(i => i.Style == IconStyle.Regular).ToList();
            else if (style == 2) wanted = wanted.Where(i => i.Style == IconStyle.Filled).ToList();

            return wanted;
        }

        private void ApplyFilter()
        {
            if (_catalog == null || _refiltering) return;

            var wanted = Narrowed();

            // The grid's selection is an index into the list that is about to be replaced, and
            // clearing it raises SelectedIndexChanged, which would throw the chosen icon away.
            var keep = _selectedIcon;

            _shown = IconSearch.Rank(wanted, _searchBox.Text);

            _grid.SelectedIndices.Clear();
            _grid.VirtualListSize = _shown.Count;

            // Follow the icon to wherever it landed in the new ranking. Without this the
            // highlighted cell and the preview drift apart the moment somebody chooses an
            // icon and then keeps typing: index 4 of the old order is a different icon in
            // the new one.
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

                // Nothing carried over, so this is a different list - and the scroll
                // position from the last one is row forty of it. The best matches are at
                // the top and that is where a new search should be looking.
                if (_shown.Count > 0) _grid.EnsureVisible(0);
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
            _selectedDrawing = icon == null || _catalog == null ? null : Drawable(icon);

            _iconLabel.Text = icon == null
                ? "No icon chosen"
                : icon.DisplayName + "  ·  " + icon.Origin;

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

            // Why the second web resource exists, for a licence where it is the condition of
            // using the drawing rather than a courtesy. The library says this while the icon is
            // being chosen; a gate that is about to write the page is not the place to stop.
            var credit = AttributionNotice.Why(_licences(icon.Family));
            if (credit != null) credit = "\n\n" + credit;

            var confirm = MessageBox.Show(this,
                "Set " + icon.DisplayName + " as the icon for " + table.DisplayName + "?\n\n" +
                "This writes the web resource " + name + " and the attribution page beside it, " +
                AttributionPage.NameFor(name) + ", adding whichever is new to " + solution.FriendlyName +
                ", and publishes the table." + credit + replacing + handover + concern,
                "Set table icon", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            var mode = _fixedColour.Checked ? IconColourMode.Fixed : IconColourMode.Theme;

            string svg;
            SvgImage image;
            try
            {
                var source = _catalog.ReadSvg(icon);
                svg = SvgOutput.Build(source, mode, _colour, null, IconProvenance.For(icon));
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
                RasterColour = _colour,
                AttributionRequired = _licences(icon.Family).RequiresNotice
            };

            var generation = ++_generation;
            _applying = true;
            _writeStep = "Applying...";
            _trouble = null;
            _outcome = null;
            _outcomeSteps = null;
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

                    Remember(new AppliedChange
                    {
                        TableLogicalName = table.LogicalName,
                        TableDisplayName = table.DisplayName,
                        IconDisplayName = icon.DisplayName,
                        PreviousVector = result.PreviousVectorIcon,
                        PreviousSmall = result.PreviousSmallIcon,
                        PreviousMedium = result.PreviousMediumIcon,
                        PreviousLarge = result.PreviousLargeIcon,
                        Overwritten = result.Overwritten,
                        Request = request
                    });

                    // The list in memory now disagrees with the org unless this is written back,
                    // and so does what the window believes is stored under that name - which is
                    // what it will say the table is carrying the next time it is chosen.
                    table.CurrentIcon = name;
                    _stored[name] = request.Svg;
                    if (request.IncludeRasterIcons)
                    {
                        table.CurrentSmall = RasterIcons.NameFor(name, IconSlot.Small);
                        table.CurrentMedium = RasterIcons.NameFor(name, IconSlot.Medium);
                        table.CurrentLarge = RasterIcons.NameFor(name, IconSlot.Large);
                    }

                    // And the list on screen disagrees with the list in memory unless it is
                    // rebuilt: a table just given an icon has no business still being offered
                    // under "only tables with no icon yet". It drops out of the list, which is
                    // the point - that one is done.
                    var wasAt = _tableBox.SelectedIndex;
                    ShowTables();
                    AdvanceToNextTable(wasAt);

                    // No dialog. The status line says it, the table has just left the unset-only
                    // list, and a modal saying "that worked" is a click charged for nothing -
                    // which on a run of a dozen tables is a dozen of them. What it used to say
                    // is on the status line's tooltip, for when the question is which web
                    // resource was created and which was updated.
                    _outcome = "Set " + icon.DisplayName + " on " + table.DisplayName;
                    _outcomeSteps = string.Join("\n", result.Steps);
                    SyncEnabled();
                }
            });
        }


        // ===== taking it back =====
        //
        // Undo here is not a keystroke against a document held in memory. It is another write to
        // somebody's environment and another publish, so it goes out the way an apply does - one
        // worker, cancellable, the generation bumped - and it asks first. A reflex Ctrl+Z that
        // published a customisation unannounced would be worse than the mistake it was reaching
        // for.

        /// <summary>
        /// Keeps an apply so it can be taken back, and drops whatever was waiting to be done
        /// again: a new write is a new branch, and the redo behind it was for a history that no
        /// longer happened.
        /// </summary>
        private void Remember(AppliedChange change)
        {
            _undoable.Push(change);
            _redoable.Clear();
        }

        /// <summary>
        /// Puts the table back the way it was before the last apply: pointing at whatever it
        /// named before, with the drawings that were written over back as they were.
        /// </summary>
        private void Undo()
        {
            if (Busy || _undoable.Count == 0 || Service == null) return;

            // Peeked rather than popped. Every step of the revert writes a value rather than a
            // difference, so one that failed halfway can simply be asked again - and an entry
            // taken off the stack by a write that did not land is an undo nobody can reach for
            // a second time.
            var change = _undoable.Peek();

            // Applying twice to one table reuses the name, so the second undo is not about
            // where the table points - it is about what is inside the thing it points at, and
            // saying "goes back to" the name it already carries would read as doing nothing.
            var samePlace = !string.IsNullOrEmpty(change.PreviousVector) &&
                            string.Equals(change.PreviousVector, change.Request.WebResourceName,
                                          StringComparison.OrdinalIgnoreCase);

            // "Back to having none" is only true of a table that really had none. A standard
            // table had its stock icon before this and gets it back the moment the slot is
            // emptied, which is a different thing to promise and the thing that will be on
            // screen.
            var hadStock = !string.IsNullOrEmpty(StockFor(change.TableLogicalName));

            var back = string.IsNullOrEmpty(change.PreviousVector)
                ? hadStock
                    ? change.TableDisplayName + " had no icon of its own before this, and goes " +
                      "back to the stock one an app draws for it."
                    : change.TableDisplayName + " had no icon before this, and goes back to having none."
                : samePlace
                    ? change.TableDisplayName + " keeps pointing at " + change.PreviousVector + "."
                    : change.TableDisplayName + " goes back to " + change.PreviousVector + ".";

            // The page the apply wrote over is in Overwritten with the drawings, but it is not
            // one of them and is not put back - it is rebuilt - so it is not counted here either.
            var pageName = AttributionPage.NameFor(change.Request.WebResourceName);
            var over = change.Overwritten == null
                ? 0
                : change.Overwritten.Count(
                    o => !string.Equals(o.Name, pageName, StringComparison.OrdinalIgnoreCase));

            var drawings = over == 0
                ? "The web resources that were written stay where they are - nothing here deletes one."
                : "The " + (over == 1 ? "drawing" : "drawings") +
                  " this wrote over " + (over == 1 ? "goes" : "go") +
                  " back to what " + (over == 1 ? "it held" : "they held") +
                  " too, and nothing is deleted.";

            // The page is the one thing here that is not put back to what it said. It describes
            // the folder, and the folder after an undo still holds the icon - so saying it is
            // "restored" would be a promise it does not keep.
            var credit = pageName + " is rewritten from the folder afterwards, so it still credits " +
                         "every icon stored there - including this one, which stays where it is.";

            var confirm = MessageBox.Show(this,
                "Take back setting " + change.IconDisplayName + " on " + change.TableDisplayName + "?\n\n" +
                back + " " + drawings + "\n\n" + credit + "\n\n" +
                "This is another write to the environment, and it publishes the table.",
                "Undo the last change", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            var request = new IconRevertRequest
            {
                TableLogicalName = change.TableLogicalName,
                VectorName = change.PreviousVector,
                RestoreRasterIcons = change.Request.IncludeRasterIcons,
                SmallName = change.PreviousSmall,
                MediumName = change.PreviousMedium,
                LargeName = change.PreviousLarge,
                Overwritten = change.Overwritten,
                WebResourceName = change.Request.WebResourceName,
                SolutionUniqueName = change.Request.SolutionUniqueName
            };

            var generation = ++_generation;
            _applying = true;
            _writeStep = "Putting it back...";
            _trouble = null;
            _outcome = null;
            _outcomeSteps = null;
            SyncEnabled();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Putting it back...",
                IsCancelable = true,
                Work = (worker, args) =>
                {
                    args.Result = IconApplier.Revert(
                        Service,
                        request,
                        () => worker.CancellationPending,
                        message => worker.ReportProgress(0, message));
                },
                ProgressChanged = args =>
                {
                    if (IsDisposed) return;
                    if (generation != _generation) return;
                    _statusLabel.Text = args.UserState as string ?? "Putting it back...";
                },
                PostWorkCallBack = args =>
                {
                    if (IsDisposed) return;
                    if (generation != _generation) return;

                    _applying = false;

                    if (args.Cancelled || args.Error is IconApplier.CancelledException)
                    {
                        _trouble = "Cancelled. The table may or may not have been put back, " +
                                   "depending on how far it had got - reload to see. Undo is still " +
                                   "offered, and asking for it again is safe.";
                        SyncEnabled();
                        return;
                    }

                    if (args.Error != null)
                    {
                        _trouble = "Could not put it back: " + args.Error.Message;
                        SyncEnabled();
                        MessageBox.Show(this, args.Error.Message, "Could not put it back",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var result = (IconRevertResult)args.Result;

                    _undoable.Pop();
                    _redoable.Push(change);

                    // The drawings that were written over are back, so what the window believes
                    // is stored under those names has to go back with them.
                    Restored(change.Overwritten);

                    SettleOn(change.TableLogicalName, change.PreviousVector,
                             change.PreviousSmall, change.PreviousMedium, change.PreviousLarge,
                             change.Request.IncludeRasterIcons);

                    _outcome = string.IsNullOrEmpty(change.PreviousVector)
                        ? "Took " + change.IconDisplayName + " back off " + change.TableDisplayName
                        : "Put " + change.TableDisplayName + " back on " + change.PreviousVector;
                    _outcomeSteps = string.Join("\n", result.Steps);
                    SyncEnabled();
                }
            });
        }

        /// <summary>
        /// Does the last undone apply again: the same request, sent a second time. The web
        /// resource exists by now, so this is an update where the first one was a create - and
        /// what it finds on the table is read fresh, because what was there before a redo is not
        /// what was there before the apply it repeats.
        /// </summary>
        private void Redo()
        {
            if (Busy || _redoable.Count == 0 || Service == null) return;

            var change = _redoable.Peek();

            // The same list the first apply gave, rather than "the way it did the first time":
            // the page and the PNGs are written again here too, and a gate that leaves them to
            // be remembered is a gate that says less than the write it stands in front of.
            var rasters = change.Request.IncludeRasterIcons
                ? " and the three classic PNG icons beside it"
                : "";

            var confirm = MessageBox.Show(this,
                "Set " + change.IconDisplayName + " on " + change.TableDisplayName + " again?\n\n" +
                "This writes the web resource " + change.Request.WebResourceName + rasters +
                ", rewrites the attribution page beside " + (rasters == "" ? "it" : "them") + ", " +
                AttributionPage.NameFor(change.Request.WebResourceName) +
                ", and publishes the table, the way it did the first time.",
                "Do it again", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            var request = change.Request;

            var generation = ++_generation;
            _applying = true;
            _writeStep = "Doing it again...";
            _trouble = null;
            _outcome = null;
            _outcomeSteps = null;
            SyncEnabled();

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Doing it again...",
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
                    _statusLabel.Text = args.UserState as string ?? "Doing it again...";
                },
                PostWorkCallBack = args =>
                {
                    if (IsDisposed) return;
                    if (generation != _generation) return;

                    _applying = false;

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

                    _redoable.Pop();

                    // Built out of this run's own result rather than carried over from the first
                    // one: the undo behind a redo has to put back what was there a moment ago.
                    _undoable.Push(new AppliedChange
                    {
                        TableLogicalName = change.TableLogicalName,
                        TableDisplayName = change.TableDisplayName,
                        IconDisplayName = change.IconDisplayName,
                        PreviousVector = result.PreviousVectorIcon,
                        PreviousSmall = result.PreviousSmallIcon,
                        PreviousMedium = result.PreviousMediumIcon,
                        PreviousLarge = result.PreviousLargeIcon,
                        Overwritten = result.Overwritten,
                        Request = request
                    });

                    _stored[request.WebResourceName] = request.Svg;

                    SettleOn(change.TableLogicalName,
                             request.WebResourceName,
                             RasterIcons.NameFor(request.WebResourceName, IconSlot.Small),
                             RasterIcons.NameFor(request.WebResourceName, IconSlot.Medium),
                             RasterIcons.NameFor(request.WebResourceName, IconSlot.Large),
                             request.IncludeRasterIcons);

                    _outcome = "Set " + change.IconDisplayName + " on " + change.TableDisplayName + " again";
                    _outcomeSteps = string.Join("\n", result.Steps);
                    SyncEnabled();
                }
            });
        }

        /// <summary>
        /// Puts back what the window believes is stored under each name an undo has just
        /// restored. A resource that held nothing before is forgotten rather than remembered
        /// as empty: the next load is what decides what is there now.
        /// </summary>
        private void Restored(IEnumerable<OverwrittenResource> overwritten)
        {
            foreach (var resource in overwritten ?? Enumerable.Empty<OverwrittenResource>())
            {
                if (string.IsNullOrEmpty(resource?.Name)) continue;

                if (string.IsNullOrEmpty(resource.ContentBase64))
                {
                    _stored.Remove(resource.Name);
                    continue;
                }

                try
                {
                    _stored[resource.Name] = Encoding.UTF8.GetString(Convert.FromBase64String(resource.ContentBase64));
                }
                catch (FormatException)
                {
                    _stored.Remove(resource.Name);
                }
            }
        }

        /// <summary>
        /// Writes what the org now holds back into the list in memory, rebuilds the table list,
        /// and lands on the table that just changed.
        ///
        /// Not on the next one waiting, which is where an apply lands. An undo or a redo is
        /// somebody saying "that one, again" - the table they mean is the one that just moved,
        /// and leaving them somewhere else would be answering a question they did not ask.
        /// </summary>
        private void SettleOn(string logicalName, string vector,
                              string small, string medium, string large, bool raster)
        {
            var table = _tables.FirstOrDefault(t => t.LogicalName == logicalName);
            if (table != null)
            {
                table.CurrentIcon = vector;
                if (raster)
                {
                    table.CurrentSmall = small;
                    table.CurrentMedium = medium;
                    table.CurrentLarge = large;
                }
            }

            ShowTables();

            var index = _tableBox.Items.Cast<TableItem>().ToList()
                .FindIndex(t => t.LogicalName == logicalName);
            if (index >= 0) _tableBox.SelectedIndex = index;
        }

        // ===== plumbing =====

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail,
                                              string actionName, object parameter)
        {
            // Everything this window was holding for the old org goes before the base class is
            // called, not after. A connection made because somebody pressed a button arrives
            // carrying that button's method name, and running it is the last thing the base
            // class does - so a reset that came afterwards would throw away the very load the
            // connection was made for, and leave an empty window and a live button behind.
            //
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
            // An undo names a web resource and a table in the org it was written against. In the
            // next one it is at best meaningless, and at worst a write to a table that happens
            // to share a name.
            _undoable.Clear();
            _redoable.Clear();
            // Where the last org kept its icons says nothing about where this one does, and
            // neither does the folder somebody chose for a table that is not in this one.
            _locations = IconLocations.None;
            _typedFolder = null;
            _suggestedFor = null;
            // The search a table of the old org seeded is not this org's table's search either,
            // and no table of this org has been left behind yet.
            _seededFor = null;
            _lastTable = null;
            // And the drawings read out of the old org say nothing about what this one keeps
            // under the same names.
            _stored.Clear();
            _currentIdentity = null;
            if (_currentLabel != null) { _currentLabel.Text = ""; _currentLabel.Visible = false; }
            // The preview is showing the last org's apps, and its icons around ours.
            ResetContext();
            _tableBox.Items.Clear();
            _solutionBox.Items.Clear();
            _trouble = null;
            _outcome = null;
            _outcomeSteps = null;

            if (applying)
            {
                // Only the apply is marked cancelable, so only the apply may be cancelled here.
                // Its callback will land with a stale generation and say nothing, so this line
                // is the only report the user gets of what the old org may be left holding.
                // Cancelled before the base class runs, or a load it starts is what gets cancelled.
                CancelWorker();
                _trouble = "The connection changed while a change was being written. The previous " +
                           "environment may or may not have been changed - reconnect to it to see.";
            }

            base.UpdateConnection(newService, detail, actionName, parameter);

            // Nobody opens this tool to look at an empty window. Reading the tables is half a
            // second against a real environment - measured, in docs/metadata-cache.md - so it is
            // not worth a click, and the window is useful the moment it is connected.
            //
            // Not when there is an action name: the base class has just run that method, and it
            // is the load this connection was made for. Not while a write to the last org was
            // being abandoned either: a fresh load would wipe the only report of what that org
            // may be left holding, before anybody had a chance to read it.
            if (newService != null && string.IsNullOrEmpty(actionName) && !applying)
            {
                LoadTables();
            }

            SyncEnabled();
        }

        /// <summary>
        /// What this is, where it came from, and how to say it is wrong.
        ///
        /// The box is built from the plugin class's own metadata, so the name and description in
        /// it are the ones XrmToolBox shows in its tools list rather than a second copy that
        /// could drift from them. What this tool adds is the licences: four icon families travel
        /// inside the assembly, and the same text that goes into ATTRIBUTION.html goes here.
        /// </summary>
        private void ShowAbout()
        {
            var about = ToolAbout.FromPlugin(
                typeof(IconsPlugin), "icons", "https://github.com/comentality/xrm-icons");

            about.Notices = _catalog?.About;
            about.NoticesCaption = "Icon licences and credits";

            AboutBox.Show(this, about);
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

                // A window of its own, which closing this one would otherwise leave behind.
                _hints.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
