using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Icons.Catalog;
using Icons.Logic;
using Icons.Ui;
using XrmToolBox.Extensibility;

namespace Icons
{
    /// <summary>
    /// The icon in an app's navigation: the window, and everything the window cannot fetch for
    /// itself.
    ///
    /// The reading lives here rather than in the window because this is where the tool already
    /// guards a slow connection - a generation that moves on when the connection changes, a
    /// disposed check on every callback, and a busy flag that keeps a second click from starting
    /// a second read. A form that fetched its own data would have to repeat all of it.
    /// </summary>
    public partial class IconsControl
    {
        private ContextPreview _context;
        private bool _readingNavigation;

        /// <summary>
        /// The navigations this environment has, read once and kept. Null until asked for; the
        /// list is small and does not change while a tool window is open.
        /// </summary>
        private IReadOnlyList<AppNavigation> _apps;

        private void ShowContext()
        {
            if (_selectedIcon == null || _catalog == null) return;

            if (_context != null && !_context.IsDisposed)
            {
                RefreshContextIcon();
                _context.Activate();
                return;
            }

            _context = new ContextPreview();
            _context.AppChosen += Context_AppChosen;
            _context.FormClosed += (s, e) => _context = null;

            RefreshContextIcon();

            // Something to look at before anything is asked of the connection, and a note that
            // says what it is. An invented navigation and the environment's own are worth very
            // different amounts, and the window must never let them be mistaken for each other.
            var stand = StandInNavigation();
            _context.ShowNavigation(stand.Key, stand.Value,
                "A stand-in navigation. Only your icon is real.");

            _context.Show(this);

            if (Service != null) ReadApps();
        }

        /// <summary>Pushes the current icon and colour into an open window.</summary>
        private void RefreshContextIcon()
        {
            if (_context == null || _context.IsDisposed) return;
            if (_selectedIcon == null || _catalog == null) return;

            var table = _tableBox.SelectedItem as TableItem;
            var colour = _fixedColour.Checked ? _colour : Color.FromArgb(0x24, 0x24, 0x24);

            _context.ShowChosen(Drawable(_selectedIcon), table?.DisplayName ?? _selectedIcon.DisplayName, colour);
        }

        /// <summary>The chosen library icon as something drawable, or null if it will not read.</summary>
        private SvgImage Drawable(IconEntry icon)
        {
            try
            {
                return SvgImage.Parse(_catalog.ReadSvg(icon));
            }
            catch
            {
                // The grid already draws an empty cell for an icon that will not parse; the
                // preview showing an empty slot is the same answer in the same words.
                return null;
            }
        }

        /// <summary>
        /// A navigation to show before the environment has said anything: plausible names, and
        /// icons out of the bundled library. Every row of it is invented, which is why the note
        /// beside it says so.
        /// </summary>
        private KeyValuePair<IList<NavRow>, int> StandInNavigation()
        {
            var table = _tableBox.SelectedItem as TableItem;
            var label = table?.DisplayName ?? "This table";

            var rows = new List<NavRow>
            {
                new NavRow("Home", FromLibrary("home")),
                new NavRow("Recent", FromLibrary("history")),
                new NavRow("Pinned", FromLibrary("pin")),
                new NavRow("Customers", null, true),
                new NavRow(label, Drawable(_selectedIcon)),
                new NavRow("Contacts", FromLibrary("person")),
                new NavRow("Sales", null, true),
                new NavRow("Leads", FromLibrary("target")),
                new NavRow("Opportunities", FromLibrary("trophy")),
                new NavRow("Orders", FromLibrary("receipt"))
            };

            return new KeyValuePair<IList<NavRow>, int>(rows, 4);
        }

        /// <summary>A library icon by exact name, for the stand-in rows. Null when there is none.</summary>
        private SvgImage FromLibrary(string name)
        {
            if (_catalog == null) return null;

            var entry = _catalog.Icons.FirstOrDefault(i =>
                i.Family == IconFamily.Fluent && i.Style == IconStyle.Regular && i.Name == name);

            return entry == null ? null : Drawable(entry);
        }

        // ===== reading from the environment =====

        private void ReadApps()
        {
            if (_readingNavigation || Service == null) return;

            var generation = _generation;
            _readingNavigation = true;
            if (_context != null && !_context.IsDisposed) _context.ShowBusy("Reading this environment's apps...");

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading apps...",
                Work = (worker, args) => { args.Result = SiteMapSource.Apps(Service); },
                PostWorkCallBack = args =>
                {
                    _readingNavigation = false;

                    // Both can have gone while this was on the wire: the tab can be closed, and
                    // the preview window is the user's to close whenever they like.
                    if (IsDisposed || generation != _generation) return;
                    if (_context == null || _context.IsDisposed) return;

                    if (args.Error != null)
                    {
                        _context.ShowBusy("");
                        _context.ShowNavigation(StandInNavigation().Key, 4,
                            "Could not read this environment's apps. Showing a stand-in.");
                        return;
                    }

                    _apps = (IReadOnlyList<AppNavigation>)args.Result;
                    _context.ShowBusy("");
                    _context.ShowApps(_apps);
                    _context.ShowNavigation(StandInNavigation().Key, 4,
                        _apps.Count == 0
                            ? "This environment has no app with a navigation. Showing a stand-in."
                            : "A stand-in navigation. Choose an app to see the real one.");
                }
            });
        }

        private void Context_AppChosen(object sender, AppNavigation app)
        {
            if (app == null || Service == null || _readingNavigation) return;

            var generation = _generation;
            var chosenTable = (_tableBox.SelectedItem as TableItem)?.LogicalName ?? "";
            var chosenLabel = (_tableBox.SelectedItem as TableItem)?.DisplayName;

            // What the site map needs to know about the tables it names. Already in hand from
            // the load, so choosing an app costs the site map and its icons and nothing else.
            var facts = _tables.ToDictionary(
                t => t.LogicalName,
                t => new SiteMapTable { DisplayName = t.DisplayName, VectorIcon = t.CurrentIcon },
                StringComparer.OrdinalIgnoreCase);

            _readingNavigation = true;
            if (_context != null && !_context.IsDisposed) _context.ShowBusy("Reading " + app.Name + "...");

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading " + app.Name + "...",
                Work = (worker, args) =>
                {
                    var entries = SiteMapSource.Navigation(Service, app.SiteMapId, facts);
                    var svg = SiteMapSource.Icons(Service, entries.Select(e => e.IconWebResource));
                    args.Result = new KeyValuePair<IReadOnlyList<SiteMapEntry>, Dictionary<string, string>>(entries, svg);
                },
                PostWorkCallBack = args =>
                {
                    _readingNavigation = false;

                    if (IsDisposed || generation != _generation) return;
                    if (_context == null || _context.IsDisposed) return;

                    _context.ShowBusy("");

                    if (args.Error != null)
                    {
                        _context.ShowNavigation(StandInNavigation().Key, 4,
                            "Could not read " + app.Name + ". Showing a stand-in.");
                        return;
                    }

                    var answer = (KeyValuePair<IReadOnlyList<SiteMapEntry>, Dictionary<string, string>>)args.Result;
                    Fill(app, answer.Key, answer.Value, chosenTable, chosenLabel);
                }
            });
        }

        /// <summary>
        /// Turns the site map into rows, and stands the chosen icon in the place of the table it
        /// is for. A navigation that does not name that table gets the icon in the middle of it
        /// instead, under the table's own name, so there is always something to judge - and the
        /// note says which of the two happened.
        /// </summary>
        private void Fill(AppNavigation app, IReadOnlyList<SiteMapEntry> entries,
            Dictionary<string, string> svg, string chosenTable, string chosenLabel)
        {
            var rows = new List<NavRow>();
            var mine = -1;

            foreach (var entry in entries)
            {
                if (entry.Kind == SiteMapEntryKind.Group)
                {
                    rows.Add(new NavRow(entry.Label, null, true));
                    continue;
                }

                if (entry.TableLogicalName.Length > 0 && chosenTable.Length > 0
                    && string.Equals(entry.TableLogicalName, chosenTable, StringComparison.OrdinalIgnoreCase))
                {
                    mine = rows.Count;
                    rows.Add(new NavRow(entry.Label, Drawable(_selectedIcon)));
                    continue;
                }

                rows.Add(new NavRow(entry.Label, Parse(entry.IconWebResource, svg)));
            }

            var note = app.Name + " - this environment's own navigation.";

            if (mine < 0)
            {
                // The table is not in this app. Rather than show a navigation with nothing of
                // the user's in it, the icon stands in the middle of it, and the note is honest
                // about the substitution.
                var tables = rows.Select((r, i) => new { r, i }).Where(x => !x.r.IsGroup).ToList();
                if (tables.Count > 0)
                {
                    mine = tables[tables.Count / 2].i;
                    rows[mine] = new NavRow(chosenLabel ?? rows[mine].Label, Drawable(_selectedIcon));
                    note = app.Name + " - its own navigation. "
                           + (chosenLabel ?? "This table") + " is not in this app, so the icon is standing in a row of it.";
                }
                else
                {
                    note = app.Name + " has no tables in its navigation.";
                }
            }

            _context.ShowNavigation(rows, mine, note);
        }

        /// <summary>One of the environment's own icons, or null when it will not read.</summary>
        private static SvgImage Parse(string webResource, Dictionary<string, string> svg)
        {
            string text;
            if (string.IsNullOrEmpty(webResource) || !svg.TryGetValue(webResource, out text)) return null;

            try
            {
                return SvgImage.Parse(text);
            }
            catch
            {
                // An icon of the org's that this cannot read is an empty slot, not a failure of
                // the preview. It is also not this tool's to fix.
                return null;
            }
        }
    }
}
