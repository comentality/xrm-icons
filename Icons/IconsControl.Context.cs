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
    /// The icon in an app's navigation: everything the preview pane cannot fetch for itself.
    ///
    /// The reading lives here rather than in the pane because this is where the tool already
    /// guards a slow connection - a generation that moves on when the connection changes, a
    /// disposed check on every callback, and a busy flag that keeps a second choice from
    /// starting a second read. A control that fetched its own data would repeat all of it.
    /// </summary>
    public partial class IconsControl
    {
        private ContextPreview _context;
        private bool _readingNavigation;

        /// <summary>
        /// Which read the preview is waiting on. Every read takes the next number, and only the
        /// answer carrying the latest one is shown - or allowed to say the reading is over.
        ///
        /// Not the window's generation, which moves for a reload and for every write as well.
        /// An answer checked against that was thrown away whole by a reload pressed while it
        /// was out, "Reading..." and the dead app box included, and nothing was left to put
        /// them back. What makes an answer about the preview stale is a newer question about
        /// the preview, or another environment - and <see cref="ResetContext"/> moves this for
        /// that.
        /// </summary>
        private int _navigationRead;

        /// <summary>
        /// The app somebody chose last, from the moment they chose it rather than from the
        /// moment it arrived, so a reload pressed while it is still on the wire keeps it too.
        /// </summary>
        private AppNavigation _chosenApp;

        /// <summary>
        /// The chosen icon, parsed, kept until a different one is chosen. RefreshContextIcon
        /// runs on every change the window makes - every keystroke in the name box among them -
        /// and reading an SVG out of the bundle and parsing it for each of those is a cost with
        /// nothing to show for it: the drawing has not changed.
        /// </summary>
        private SvgImage _selectedDrawing;

        /// <summary>
        /// The navigations this environment has, read once and kept. Null until asked for; the
        /// list is small and does not change while a tool window is open.
        /// </summary>
        private IReadOnlyList<AppNavigation> _apps;

        /// <summary>
        /// The app whose own navigation is on screen, and what was read for it, kept so that
        /// choosing another table can move the icon to that table's row without a round trip.
        /// Which row is ours is decided when the rows are built, so a navigation built once and
        /// left alone went on showing the icon beside the table chosen when the app was.
        /// Null while the stand-in is showing.
        /// </summary>
        private AppNavigation _shownApp;
        private IReadOnlyList<SiteMapEntry> _shownEntries;

        /// <summary>The app's own icon for each of <see cref="_shownEntries"/>, parsed once.</summary>
        private IList<SvgImage> _shownDrawings;

        /// <summary>
        /// The navigation before the environment has said anything: something to look at from
        /// the first moment the window opens, and a note saying what it is. An invented
        /// navigation and the environment's own are worth very different amounts, and the
        /// preview must never let them be mistaken for each other.
        /// </summary>
        private void ShowStandIn(string note)
        {
            if (_context == null) return;

            _chosenApp = null;
            _shownApp = null;
            _shownEntries = null;
            _shownDrawings = null;

            var stand = StandInNavigation();
            _context.ShowNavigation(stand.Key, stand.Value, note, true);
        }

        /// <summary>
        /// Pushes what is chosen now into the preview: the drawing, the name of the table it is
        /// for, and the colour - or no colour at all, which is how "follow the app theme" is
        /// said. The preview is showing one theme or the other and knows what currentColor
        /// resolves to in it; this window does not.
        ///
        /// Called from SyncEnabled, so it runs after every change and the preview is never
        /// showing the icon before the last one.
        /// </summary>
        private void RefreshContextIcon()
        {
            if (_context == null || _context.IsDisposed) return;

            var table = _tableBox.SelectedItem as TableItem;

            _context.ShowChosen(_selectedDrawing,
                table?.DisplayName ?? _selectedIcon?.DisplayName,
                _fixedColour.Checked ? _colour : (Color?)null);
        }

        /// <summary>
        /// What the preview was told about the last org, thrown away. The apps are that org's,
        /// and so is every icon in the navigation drawn beside ours.
        /// </summary>
        private void ResetContext()
        {
            _apps = null;
            _readingNavigation = false;
            _navigationRead++;
            _chosenApp = null;
            _shownApp = null;
            _shownEntries = null;
            _shownDrawings = null;

            if (_context == null || _context.IsDisposed) return;

            _context.ShowApps(new List<AppNavigation>());
            ShowStandIn("A stand-in navigation. Only your icon is real.");
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
                new NavRow(label, _selectedDrawing),
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

        /// <summary>
        /// The preview's own link of the load chain. It is always on screen now, so the app
        /// list is read for it rather than waiting for a window to be opened - one query, at
        /// the end of a load that has already made two, and the only thing that turns the
        /// stand-in into this environment's own navigation.
        /// </summary>
        private void ReadApps()
        {
            // Not held back by a read still out. This is the end of a load, and a load is a
            // newer question about the environment than any read it overtakes.
            if (Service == null) return;
            if (_context == null || _context.IsDisposed) return;

            var read = StartNavigationRead("Reading this environment's apps...");

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading apps...",
                Work = (worker, args) => { args.Result = SiteMapSource.Apps(Service); },
                PostWorkCallBack = args =>
                {
                    if (!FinishNavigationRead(read)) return;

                    AppNavigation kept = null;

                    if (args.Error != null)
                    {
                        _context.ShowBusy("");
                        ShowStandIn("Could not read this environment's apps. Showing a stand-in.");
                    }
                    else
                    {
                        _apps = (IReadOnlyList<AppNavigation>)args.Result;

                        // A reload of the same environment keeps the app somebody chose. Its
                        // navigation is read again, because a reload is a request for the org as
                        // it is now - but the box going back to empty and the preview to the
                        // stand-in is the window forgetting a choice nobody took back.
                        kept = _chosenApp == null
                            ? null
                            : _apps.FirstOrDefault(a => a.SiteMapId == _chosenApp.SiteMapId);

                        _context.ShowBusy("");
                        _context.ShowApps(_apps, kept);

                        if (kept == null)
                        {
                            ShowStandIn(_apps.Count == 0
                                ? "This environment has no app with a navigation. Showing a stand-in."
                                : "A stand-in navigation. Choose an app to see the real one.");
                        }
                    }

                    // Last of all, and after the preview either way: an app list that will not
                    // come back must cost the preview and nothing else.
                    ReadStoredIcons();

                    // Queued behind the stored icons rather than ahead of them, which is the order
                    // a first load leaves them in: somebody choosing the app by hand chooses it
                    // after this chain is over.
                    if (kept != null) Context_AppChosen(_context, kept);
                }
            });
        }

        /// <summary>
        /// The last link of the chain, and the one nothing at all waits on: the drawings behind
        /// the icons this environment already has, which is what lets the window say which
        /// library icon a table is already carrying.
        ///
        /// It goes last because it is a courtesy. The tables, the solutions, the suggested name
        /// and the preview are what somebody is waiting for, and on a slow link a round trip
        /// spent here ahead of any of them is a round trip spent making them late. A failure
        /// costs only the sentence: the window goes back to naming the web resource, which is
        /// what it knew before.
        ///
        /// Only the icons this environment's own publishers named. Microsoft's first-party
        /// solutions carry hundreds, none of them drawn by a family this tool bundles, and
        /// reading them would be megabytes spent to say nothing about them.
        /// </summary>
        private void ReadStoredIcons()
        {
            if (Service == null) return;

            var ours = _tables.Select(t => t.CurrentIcon).Where(_locations.Reusable).ToList();
            if (ours.Count == 0) return;

            var generation = _generation;
            var catalog = _catalog;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Reading the icons this environment already has...",
                Work = (worker, args) =>
                {
                    // Fingerprinting the whole bundle is half a second - measured in
                    // IdentityTests - and this worker is out anyway. Done here, the drawing is
                    // never the reason an answer about an icon is late.
                    catalog?.Index();
                    args.Result = StoredIcons.Read(Service, ours);
                },
                PostWorkCallBack = args =>
                {
                    if (IsDisposed || generation != _generation) return;

                    // Nothing was disabled for this and nothing is claimed by it, so there is
                    // no flag to put back and nothing to say: the window simply goes on naming
                    // the web resource rather than the icon.
                    if (args.Error != null) return;

                    foreach (var icon in (Dictionary<string, string>)args.Result)
                        _stored[icon.Key] = icon.Value;

                    // A table can already be chosen by now - the list was on screen two links
                    // ago - and until this landed there was nothing to say about what it holds.
                    //
                    // Nobody has left a table for this: it is an answer arriving late about the
                    // one that is up. So an icon picked by hand still holds the library, and the
                    // grid stays where the person using it left it.
                    ShowCurrentIcon(_tableBox.SelectedItem as TableItem, TheLibraryIsMineToMove(false));
                }
            });
        }

        private void Context_AppChosen(object sender, AppNavigation app)
        {
            if (app == null || Service == null || _readingNavigation) return;

            _chosenApp = app;

            // What the site map needs to know about the tables it names. Already in hand from
            // the load, so choosing an app costs the site map and its icons and nothing else.
            var facts = _tables.ToDictionary(
                t => t.LogicalName,
                t => new SiteMapTable { DisplayName = t.DisplayName, VectorIcon = t.CurrentIcon },
                StringComparer.OrdinalIgnoreCase);

            var read = StartNavigationRead("Reading " + app.Name + "...");

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
                    if (!FinishNavigationRead(read)) return;

                    _context.ShowBusy("");

                    if (args.Error != null)
                    {
                        ShowStandIn("Could not read " + app.Name + ". Showing a stand-in.");
                        return;
                    }

                    var answer = (KeyValuePair<IReadOnlyList<SiteMapEntry>, Dictionary<string, string>>)args.Result;
                    _shownApp = app;
                    _shownEntries = answer.Key;
                    _shownDrawings = Drawings(answer.Key, answer.Value);

                    // The table chosen now, not the one chosen when the read went out: on a slow
                    // link somebody has moved on by the time the site map lands.
                    FillForChosenTable();
                }
            });
        }

        /// <summary>The preview is reading something; says what, and returns the read's number.</summary>
        private int StartNavigationRead(string what)
        {
            _readingNavigation = true;
            if (_context != null && !_context.IsDisposed) _context.ShowBusy(what);
            return ++_navigationRead;
        }

        /// <summary>
        /// Whether the answer to this read is the one to show. If it is, the reading is over;
        /// if a newer read has started since, that one says when it is over, not this. False
        /// as well once the tab has been closed - the callback runs anyway.
        /// </summary>
        private bool FinishNavigationRead(int read)
        {
            if (read != _navigationRead) return false;

            _readingNavigation = false;
            return !IsDisposed && _context != null && !_context.IsDisposed;
        }

        /// <summary>
        /// Builds the app's navigation again around whichever table is chosen now. Called when
        /// the table changes, and not on every change the window makes - a navigation costs a
        /// font per row to draw, and the icon, colour and caption already follow through
        /// <see cref="RefreshContextIcon"/>. Only the row they go in needs the rebuild.
        /// </summary>
        private void FillForChosenTable()
        {
            if (_shownEntries == null || _readingNavigation) return;
            if (_context == null || _context.IsDisposed) return;

            var table = _tableBox.SelectedItem as TableItem;
            Fill(_shownApp, _shownEntries, _shownDrawings, table?.LogicalName ?? "", table?.DisplayName);
        }

        /// <summary>
        /// What the app itself draws beside each row. A row with no web resource is not an
        /// empty row: a standard table is drawn from the app's own font, and the org serves the
        /// vector of that same glyph - so the preview can show what is really beside that row
        /// rather than a hole where an icon the user can see plainly is.
        /// </summary>
        private IList<SvgImage> Drawings(IReadOnlyList<SiteMapEntry> entries, Dictionary<string, string> svg)
        {
            return entries
                .Select(e => e.Kind == SiteMapEntryKind.Group
                    ? null
                    : Parse(e.IconWebResource, svg) ?? Draw(StockFor(e.TableLogicalName)))
                .ToList();
        }

        /// <summary>
        /// Turns the site map into rows, and stands the chosen icon in the place of the table it
        /// is for. A navigation that does not name that table gets the icon in the middle of it
        /// instead, under the table's own name, so there is always something to judge - and the
        /// note says which of the two happened.
        /// </summary>
        private void Fill(AppNavigation app, IReadOnlyList<SiteMapEntry> entries,
            IList<SvgImage> drawings, string chosenTable, string chosenLabel)
        {
            var rows = new List<NavRow>();
            var mine = -1;
            var itsOwnRow = false;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry.Kind == SiteMapEntryKind.Group)
                {
                    rows.Add(new NavRow(entry.Label, null, true));
                    continue;
                }

                if (entry.TableLogicalName.Length > 0 && chosenTable.Length > 0
                    && string.Equals(entry.TableLogicalName, chosenTable, StringComparison.OrdinalIgnoreCase))
                {
                    mine = rows.Count;
                    itsOwnRow = true;
                    rows.Add(new NavRow(entry.Label, _selectedDrawing));
                    continue;
                }

                rows.Add(new NavRow(entry.Label, drawings[i]));
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
                    rows[mine] = new NavRow(chosenLabel ?? rows[mine].Label, _selectedDrawing);
                    note = app.Name + " - its own navigation. "
                           + (chosenLabel ?? "This table") + " is not in this app, so the icon is standing in a row of it.";
                }
                else
                {
                    note = app.Name + " has no tables in its navigation.";
                }
            }

            // The chosen row's caption is this window's to write only when it is standing in a
            // row that is not the table's own. Where the app names the table itself, that label
            // is what will be read beside the icon.
            _context.ShowNavigation(rows, mine, note, mine >= 0 && !itsOwnRow);
        }

        /// <summary>One of the environment's own icons, or null when it will not read.</summary>
        private static SvgImage Parse(string webResource, Dictionary<string, string> svg)
        {
            string text;
            if (string.IsNullOrEmpty(webResource) || !svg.TryGetValue(webResource, out text)) return null;

            return Draw(text);
        }

        private static SvgImage Draw(string svg)
        {
            if (string.IsNullOrEmpty(svg)) return null;

            try
            {
                return SvgImage.Parse(svg);
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
