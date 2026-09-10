# Changelog

Every released version, newest first. The bolded sentence of each entry is the one that goes on
the package page, so it has to stand on its own.

## Unreleased

- **The icon is drawn into an app's navigation inside the tool's window, where two colour swatches were.** It used to be a window you had to ask for, and the pane it now fills was spending its height on the same drawing twice, big on white and big on black. Those answered "does this survive both themes", which the navigation answers too — through its own *Dark* box, in the app's own colours rather than a swatch's — and they could not answer the question actually being asked, which is not what the drawing looks like but whether the row reads beside the rows it will be beside. Choosing an app still reads that app's own site map; the app list is now read at the end of the load rather than when a window opened, and an app list that will not come back costs the preview and nothing else.

  ![The icon in an app's navigation, in the tool's own window](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/in-context-embedded.png)

- **Tables nobody can customise are left out of the list, and picking one says why under the table box.** They gather in *Only tables with no icon yet*, because a table that will never take an icon has never been given one, and a list that only offers what can be done with it is worth more than a complete one. Untick the box to see them. The line explaining the dead end moves out of the red trouble line at the foot of the pane, which is for round trips that failed: nothing was attempted here.

  ![A locked table, and the line under the box](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/locked-table.png)

- **The library opens on outline weight rather than both, so no drawing is in the grid twice.** Both weights put the same icon filled and unfilled adjacent in the ranking, and half the cells in the first look at the library were the alternative to the cell beside them. Outline is the weight a site map is drawn in, and the other two are one dropdown away.

  ![Weight, on Outline](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/weight.png)

- Two faults only a live preview made visible went with it. The chosen row is filled in as it is painted rather than when the navigation arrived, so picking a different icon changes what is drawn — before, the row kept whatever had been chosen when the window opened. And *Follow the app theme* no longer sends the light theme's #242424 into a dark navigation and calls the result a preview: no colour is passed at all, and the theme on show resolves `currentColor` itself.

- The slow-connection harness gained the app read as a fourth thing that can be slow or fail, and a scenario saying it must cost the preview only. The window harness photographs the preview in place, on both themes, and checks that a locked table is hidden and then explained.

## v1.0.0 — 10 September 2026

The first release.

- **Search 18,637 icons from four families with no API key, no account, and no outbound host but Dataverse itself.** Microsoft's Fluent UI System Icons, Phosphor, Lucide and Tabler are bundled inside the DLL, all MIT or ISC, so the picker opens from behind a customer's proxy. Filter to one family when you want a house style — four of them draw a folder and all four are called Folder.

- **Search in Dynamics words and get the icon rather than the word.** `account` finds the buildings and briefcases, `contact` finds the people, `appointment` finds the calendars, `case` finds the headsets. An icon actually named for your word still wins; the mapping only fills in where nothing is.

- **The table you pick types the search for you, and says nothing when it cannot say anything true.** *Sales Order* searches for the order, *Hierarchy Rule* for the hierarchy — and only ever for a word some icon is really called or one the tool has been taught. *Payment Term* is not a terminal and *Social Profile* is not a car in profile, so those tables get an empty box rather than a confident wrong answer.

- **Narrow the table list to the ones nobody has got to yet.** The load reads every table's current icon along with everything else, so this costs no extra round trip, and a table leaves the list the moment you give it an icon.

  ![Only tables with no icon yet](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/unset-only.png)

- **The web resource name follows your environment's convention rather than this tool's.** The same load reads where the icons you already have are stored, counting only your own publishers' prefixes so Microsoft's hundreds cannot outvote the handful somebody here chose. A table that already has an icon is offered that exact name, so applying updates it instead of leaving a second one beside it. Type any path over it and that is what gets written.

- **See the icon on light and on dark before you commit, at the size a site map really renders it.**

  ![The chosen icon previewed light and dark](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/preview.png)

- **See it in context draws your icon into a real app's navigation, read from that app's own site map.** Every size in it was measured against a live model-driven app rather than guessed: the pane is 200px, the row 35, the icon box 16, and `currentColor` in the nav resolves to #424242. Without a connection it shows a stand-in and says so.

  ![The icon in an app's navigation](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/in-context.png)

- **Colour follows the app theme by default, which is what Microsoft asks for in a table icon.** The SVG is written with `fill="currentColor"` so it stays legible when the theme changes. A fixed colour is one radio button away for brand work.

  ![Follow the app theme, or a fixed colour](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/colour.png)

- **One button stores the SVG web resource, points the table's `IconVectorName` at it, and publishes both in a single call.** Publishing only one of them is how you get an app that shows the old icon, or asks for one that is not there yet.

  ![The toolbar](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/toolbar.png)

- **Ctrl+Z takes the write back: the table, the drawing that was written over, and a publish.** Ctrl+Y does it again. It is another write to somebody's environment rather than a keystroke against a document, so it asks first. It never deletes — a web resource this tool wrote stays where it is.

- **An `ATTRIBUTION.html` beside the icons credits every one this tool has put there.** Each stored SVG carries a comment naming the set, the copyright holder, the licence and what was changed on the way in, and every apply rebuilds the page from those comments — so it says what is in the folder now rather than what any one session did.

  ![The attribution page](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/attribution.png)

- **The three classic PNG icons too, if you want them.** 16×16, 32×32 and 66×48 for the older `IconSmallName`, `IconMediumName` and `IconLargeName` properties. Off by default, because Microsoft's guidance is to use the vector instead.

- **It says what is wrong before the round trip rather than after it.** An icon that paints its own colours will not follow the theme; a table locked against customisation cannot take one; a name Dataverse would refuse is refused here first, where the message can say which character it disliked.

- Ships as one DLL with no dependencies beyond XrmToolBox, which is why it parses and rasterises SVG itself rather than referencing a library. The suite draws all 18,637 icons in the bundle, both as they ship and after the rewrite they are stored with. Three harnesses sit alongside the unit tests: one drives the real window against a Dataverse that answers in seconds, to see which buttons stay live and whether the answer landing last belongs to the question asked last; one photographs the real control; and one drives the whole workflow against a real environment and then asks the org what actually got written, because some questions only the platform can answer.
