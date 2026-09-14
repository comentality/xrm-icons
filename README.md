# ![](https://raw.githubusercontent.com/comentality/xrm-icons/main/Icons/icon.png) Icons

An [XrmToolBox](https://www.xrmtoolbox.com/) tool for putting a decent icon on a Dataverse
table. Search a library of Fluent, Phosphor, Lucide, Tabler, Font Awesome and Solar icons, pick one, and the tool
writes the SVG web resource, points the table at it, and publishes — without leaving XrmToolBox.

![The library, searched for "account", with an icon chosen](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/overview.png)

Setting a table icon by hand means finding an SVG somewhere, checking its licence, making a web
resource with exactly the right name and type, pasting the base64 in, finding the table, typing
the web resource name into the icon property, and publishing both. Icons does all of it from one
window.

## Install

- **XrmToolBox Tool Library** — search for *Icons* and install it.
- **Manually** — download `Comentality.Icons.dll` from the
  [latest release](https://github.com/comentality/xrm-icons/releases/latest), copy it into your
  XrmToolBox `Plugins` folder, and restart XrmToolBox.

Needs XrmToolBox 1.2025.7+, .NET Framework 4.8, and a connection whose user can create web
resources, update table definitions and publish customisations.

## What it does

- **Search six icon families at once** — Microsoft's [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons),
  [Phosphor](https://phosphoricons.com/), [Lucide](https://lucide.dev/),
  [Tabler](https://tabler.io/icons), [Font Awesome Free](https://fontawesome.com) and
  [Solar](https://www.figma.com/community/file/1166831539721848736), bundled
  inside the tool. No API key, no account, and no outbound host beyond Dataverse itself, so it
  works from behind a customer's proxy. Filter to
  one family when you want a house style, and turn on *Show family* to see which set drew each
  icon — six of them draw a folder and all six are called Folder.
- **Search in Dynamics words** — an icon set names its icons after what they depict, and
  Dataverse names tables after what they mean. Searching for `account` finds the buildings and briefcases,
  `contact` finds the people, `appointment` finds the calendars, `case` finds the headsets. An
  icon actually named for your word still wins; the mapping only fills in where nothing is.
- **Icons answer to what they are for, not only to what they are called** — Microsoft writes a
  line about each Fluent icon, and the tool reads it: `keychain` finds the key, `patient` finds
  the person, `sync` finds the cloud. 12,482 of those words, on about nine Fluent icons in ten.
  A word an icon is *described* by is worth less than a word it is *named* by, so an icon called
  what you typed still leads the grid.
- **The names a set has retired still work** — Font Awesome ships every name it has ever used,
  so the card it now calls `address-card` used to be `contact-card` and `vcard`. The library
  holds one drawing per icon rather than the same picture three times, but it kept the names:
  579 of them, and searching for one finds the icon it named. A name is worth what a name is
  worth, so where something in the library really is called your word, that icon still leads.
- **The table you pick asks for you** — choosing a table puts its own word in the search box, so
  the grid is showing buildings by the time you look at it. A two-word table falls back to the
  word the library knows — *Sales Order* searches for the order, *Hierarchy Rule* for the
  hierarchy — though it only falls back when it has to: *Recurring Appointment* asks for the
  whole thing and gets a repeating calendar, and *Invoice Line* gets a receipt rather than a
  straight line — and it only falls back to a word some icon is really called, or one the tool has
  been taught. Nothing is seeded on a word the library merely produces results for: *Payment
  Term* is not a terminal and *Social Profile* is not a car in profile. A table it can say
  nothing about seeds nothing rather than a confident wrong answer. It only ever writes over its
  own last suggestion: a search you typed is yours.
- **Only the tables you can actually do something with** — a table locked against customisation
  cannot be given an icon by anybody, so it is left out of the list; untick the box to see them,
  and choosing one says under the table box why it is a dead end.
- **See which tables still need one** — *Only tables with no icon yet* narrows the list to the
  ones that really do look unfinished, which takes two answers rather than one. Nothing stored on
  the table, which the load reads along with everything else; and no stock icon either. A standard
  table like Account has never had an icon stored on it, and never looked as though it needed one:
  a model-driven app draws it from a font of its own, and the org serves the vector of that same
  glyph, so the tool asks and leaves Account out. Without that, every standard table in the
  environment was in the list — 196 against 31 in a stock one, which is not a short list. The box
  is ticked when the window opens, because that list is the errand; the full one is a click away,
  and picking Account there says why it was not in the short one. A table leaves the narrowed list
  the moment you give it an icon, so it stays a working list rather than something to re-read.
  An environment that will not answer — a proxy in the way, an on-premise deployment that serves
  none of this — falls back to listing every empty table, which is what the tool did before.
- **Names the icon a table already has** — picking a table that is not empty says which of the
  bundled icons it is carrying, under the box you picked it in, and puts the library on that
  icon so you can see what it is beside what it could be. Three things can say which one it is,
  and the first that can is believed: the comment this tool writes into every icon it stores,
  then the drawing itself matched shape for shape against all 21,698, then the CSS class Lucide
  and Tabler ship theirs with. So an icon somebody pasted in by hand is recognised as surely as
  one this tool wrote. Nothing is guessed: an icon that is none of the bundled ones is called
  none of them rather than the nearest thing, and an icon under another publisher's prefix is
  named as the web resource it is, because those are not read at all.
- **Guesses where your icons live** — the same load reads where every icon this environment
  already has is stored, so the name it suggests follows your convention rather than this tool's.
  A table that already has an icon of yours is offered that exact name, so applying updates it
  instead of leaving a second one beside it. Otherwise the name is built in the folder the rest
  of your icons are in — only ever counting the ones under your own publishers' prefixes, so
  Microsoft's hundreds cannot outvote the handful somebody here chose. Whatever it works out, the
  name is a text box: type any path you like over it and that is what gets written. A path you
  type is then the one the next table gets too, with its own file name — emptying the box asks
  for the environment's convention back.
- **See it before you commit** — beside the library, the icon is drawn into an app's navigation,
  at the size the app draws it, among the icons it will sit beside. Without a connection that is
  a stand-in navigation and it says so; with one, choose any app in the environment and the
  preview is read from that app's own site map. A *Dark* box puts it on a dark theme, which for
  an icon that follows the theme is the other half of the question.
- **Colour that survives a theme change** — by default the icon is written with
  `fill="currentColor"`, so the app colours it and it stays legible in dark mode. This is
  [what Microsoft asks for](https://learn.microsoft.com/en-us/power-apps/developer/model-driven-apps/image-web-resources)
  in a table icon. A fixed colour is one radio button away when you need a brand one.
- **One button to apply** — stores the SVG as a web resource, sets the table's `IconVectorName`,
  and publishes the table and the web resource together in a single call. Publishing only one of
  them is how you get an app that shows the old icon, or asks for one that is not there yet.
- **Ctrl+Z takes it back** — undo puts the table back where the apply found it: pointing at the
  icon it named before, or at no icon at all, with the drawing that was written over back as it
  was. Ctrl+Y does it again. It is another write and another publish rather than a keystroke
  against something in memory, so it asks first, and it lands you back on the table it just put
  back. It never deletes: a web resource this tool wrote stays where it is, with nothing pointing
  at it.
- **A page beside the icons saying where they came from** — each stored SVG carries a comment
  naming the set that drew it, its copyright holder, its licence and what was changed on the way
  in, and every apply — and every undo — rebuilds `ATTRIBUTION.html` in the same folder from those
  comments. It says what is stored there now rather than what any one session did, so an icon
  somebody replaced by hand stops being credited the moment it stops being there. For the four
  sets under MIT and ISC it is a courtesy; for Font Awesome and Solar, whose licences ask for a
  credit line, it is the price of the icon — one of their icons is not put on a table unless
  the page goes with it.
- **The classic PNG icons too, if you want them** — optionally generates and stores the 16×16,
  32×32 and 66×48 images for the older `IconSmallName`, `IconMediumName` and `IconLargeName`
  properties. Off by default, because Microsoft's guidance is to use the vector instead.

## Workflow

1. Open the tool. The library is inside it, so you can browse and search before connecting.
2. Connecting reads the environment's tables and its unmanaged solutions by itself — half a
   second against a real environment, which is not worth a click. The refresh button beside the
   table list is there for reading them again after somebody has changed something.
3. Pick the table, and the unmanaged solution new web resources go into. The tables are listed in
   the open rather than behind a dropdown: type into *Find table* to narrow them, then press Enter
   or Down to move into the list, where the arrow keys walk it. The list starts on the
   tables with no icon yet; untick *Only tables with no icon yet* for all of them. Type enough of a table's
   name to leave one and that one is chosen, and what you typed stays put when you pick — so the
   narrowed list holds still while you work down it. The solution is required:
   without one they would land in the default solution, where nothing you ship can see them. The
   solution you set as your preferred one in the maker portal is chosen for you and is the row the
   list opens on. If you have not set one, nothing is chosen and the tool says so: alphabetical
   order is not a preference, and the default solutions — where the platform points when nobody
   has chosen — are where an icon goes to be invisible to everything you ship. The
   web resource name is suggested from where this environment already keeps its icons — the name
   the table already uses, or the folder its neighbours are in, or `new_/icons/account.svg` in an
   environment with no icons yet to learn from — and you can type any path over it.
4. The table's own word is already in the search box, so the grid is showing what that table is
   about before you type anything. Type your own search over it when it is not — yours is never
   written over — and press Enter to move into the results, where the arrow keys walk them.
5. Choose the theme colour or a fixed one, and look at the navigation above the form: that is
   the row your icon will be, beside the rows it will be beside.
6. **Set as table icon**, bottom right, and confirm. The status line says what was set on what; hover it for
   which web resources were created, which were updated, and what was published.
7. Wrong icon on that one? **Ctrl+Z**, and confirm — the table goes back to what it had, and the
   window lands on it again with the library still open. **Ctrl+Y** puts it back. Inside a text
   box Ctrl+Z is left alone: there it still means the name you were halfway through typing.

With *Only tables with no icon yet* ticked — which is how the window opens — applying drops
that table out of the list and lands on the next one waiting — a name suggested for it, a search already made for it — so a run down the
list is pick an icon, apply, pick an icon, apply.

Running it a second time on the same table updates the same web resource rather than leaving a
trail of near-duplicates.

## What it refuses, and when

The point of each of these is that it happens *before* the round trip, not as an error dialog
after it:

- A table locked against customisation is called out the moment you select it, and cannot be
  applied to.
- An apply with no unmanaged solution chosen is not offered at all, and a solutions load that
  fails says so and stops there rather than quietly writing to the default solution.
- A web resource name Dataverse would reject is caught with the offending character named — the
  platform's own message does not say which one it disliked.
- A name already used by a different kind of web resource, or by a managed one, stops the write
  rather than half-doing it.
- A name under one publisher's prefix, going into a solution published by another, is called out
  before the write — Dataverse will not create a web resource that is not named for its solution's
  publisher. It is a warning rather than a refusal: a web resource that already exists is updated
  where it already lives, and that is exactly what a table somebody else gave an icon to needs.
- An icon that carries its own colours — a handful do, such as the pride flags — warns that
  recolouring will not take, rather than silently ignoring your colour.
- Asking for the PNGs on a table whose classic icons somebody else set names those icons,
  and what the write would point the three slots at instead. Taking a slot over is not the
  same as replacing it: what it points at now is left in the solution, and this tool never
  deletes a web resource.
- **Cancel** actually cancels. The call already on the wire cannot be recalled, but on a slow
  link the ones behind it are most of the wait, and those are stopped.

## Where the icons come from

Four sets, bundled whole:

| Family | Weights bundled | Licence |
|---|---|---|
| [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons), © Microsoft Corporation | regular, filled | MIT |
| [Phosphor](https://github.com/phosphor-icons/core), © Phosphor Icons | regular, bold, fill | MIT |
| [Lucide](https://github.com/lucide-icons/lucide), © Lucide Contributors | one | ISC |
| [Tabler](https://github.com/tabler/tabler-icons), © Paweł Kuna | outline, filled | MIT |
| [Font Awesome Free](https://fontawesome.com), © Fonticons, Inc. | solid, regular | CC-BY-4.0 |
| [Solar](https://www.figma.com/community/file/1166831539721848736), © 480 Design | linear, outline | CC-BY-4.0 |

Fluent is taken at the optical size it is drawn most completely — 24px where there is one,
falling back through the others. Phosphor's thin, light and duotone weights are left out: beside
Fluent they read as a different tool rather than a different set. Font Awesome's brands weight is
left out too — 609 company logos, which is not what a table icon is — and so are the 683 names
it keeps as aliases of another icon, so that one drawing is offered once rather than as
`address-card`, `contact-card` and `vcard`. Otherwise the SVGs are as
their authors ship them, bar two things done at bundle time — a `class` attribute dropped,
because Microsoft advises against CSS classes in a table icon, and Tabler's invisible
full-canvas backdrop rectangle removed.

Font Awesome is the one set not drawn square: 512 tall and anywhere from 320 to 640 wide. It is
bundled at the width it was drawn, and the tool fits and centres a viewBox rather than
stretching it — so one of its icons sits in a square slot with room either side of it, which is
how its author drew it.

Solar is the one set that is not its author's own files. 480 Design publish it as a Figma
community file rather than as SVGs, so what is bundled is [Iconify](https://iconify.design)'s
rendition of that file — which is a change of hand rather than of drawing, and is recorded as
such on every Solar icon the tool stores. Two of its six styles are taken: `linear` whole, and
`outline` only where it draws something its `linear` does not. The two are the same outline
stroked and filled, so no comparison of the files would ever match them — but rendered, most of
the set is identical either way, and the grid would have offered the same picture twice. A
handful more are left out because this tool cannot draw them: a mask, a transform, or a
zero-length line meant to come out as a dot under a round cap.

**About** in the toolbar shows the exact package versions and the licences, under *Icon licences and credits*.
`tools\build-catalog.ps1` regenerates the bundle from newer releases, and fingerprints
every icon into the archive as it goes, so recognising the icon a table already has costs a few
tens of milliseconds rather than half a second of working it out again on every connection.

Four of the six ask only that their notice travel with the software, which is what `ABOUT.txt`
in the bundle is, so what you store from them carries no obligation of its own. Font Awesome and
Solar are the exceptions, and deliberately so: CC BY 4.0 asks for a credit line wherever the
drawings go, and for changes to be indicated. Both live on the `ATTRIBUTION.html` the tool writes beside the
icons — every icon it has put there, with its family, author, licence and what was changed — so
the obligation lands on a page in the solution rather than on you. It is also why applying one
of their icons stops if that page cannot be written: the credit is the condition of the drawing
rather than a courtesy, and the library says so under the preview before you pick one.

## Building it

```powershell
.\build.ps1                 # build and drop Icons.dll into your XrmToolBox Plugins folder
.\xtb.ps1                   # build and launch a private XrmToolBox containing nothing but this tool
dotnet test tests\Icons.Tests\Icons.Tests.csproj
.\tests\ui.ps1              # open the real control in a bare window and photograph it
.\tests\slow.ps1            # drive it against a Dataverse that answers in seconds, and misbehaves
.\tests\e2e.ps1             # drive it against a real Dataverse, on a table of its own, and clean up
.\tools\build-catalog.ps1   # rebuild the icon bundle from all four upstream packages
.\tools\index-catalog.ps1   # refingerprint that bundle, which build-catalog.ps1 also does
.\tools\make-tool-icon.ps1  # redraw the tool's own icon, using the tool's own renderer
```

The tool ships as a single DLL with no dependencies beyond XrmToolBox itself. That is why it
parses and rasterises SVG rather than referencing a library: restricted to the families it
bundles, an icon is one `<svg>` of paths and simple shapes, filled or stroked, with no
transforms, gradients or stylesheets — so the whole of what has to be understood is the path
grammar and a pen. The test suite draws every icon in the bundle and checks each leaves ink on
the canvas, both as it ships and after the rewrite it is stored with.

## Provenance

This is a clean-room reimplementation of the *functionality* of
[IconEngine](https://www.xrmtoolbox.com/plugins/PLIO.IconEngine.XrmToolBox/), a closed-source
XrmToolBox tool by Rolf Stifter that also finds icons and applies them to Dynamics. None of its
code was available, read, decompiled or otherwise consulted; everything here derives from its
public description, Microsoft's documentation, and our own decisions. It differs where it
matters: the icon library is Microsoft's own permissively licensed set bundled offline rather
than a keyed third-party API.

The rule, and what is specifically off limits to anyone working on this, is written down in
[docs/clean-room.md](docs/clean-room.md).

## Related

- [Events2Code](https://github.com/comentality/xrm-events-2-code) — turn form designer event
  registrations into JavaScript.
- [Document Template X-Ray](https://github.com/comentality/xrm-document-x-ray) — see what fields
  a Word template actually uses.

## Licence

MIT — see [LICENSE](LICENSE). The bundled icons are MIT from Microsoft; see **Where the icons
come from** above.
