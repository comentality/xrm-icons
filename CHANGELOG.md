# Changelog

Every released version, newest first. The bolded sentence of each entry is the one that goes on
the package page, so it has to stand on its own.

## Unreleased

- **About now says which build you are running and where the tool lives, instead of only who drew the icons.** It was a message box of licence text with nothing in it about the tool itself - no version, so nobody reporting a bug could say what they had, and no way back to the project. It is a window now: the name, the description and the tile are read out of the plugin's own metadata, which is where XrmToolBox reads them, so the box cannot end up calling the tool something the tools list does not; the version and the commit come out of the assembly the build stamped.

  ![The name, the build and the links](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/about-identity.png)

- **Tell us it is wrong from inside the tool: one button opens mail to feedback@comentality.com with your versions already in it.** Copy diagnostics puts the same four lines on the clipboard for a machine with no mail client set up. They are four lines about software - tool, XrmToolBox, .NET, Windows - and nothing about your environment: no organisation url, no user, no environment id, because a diagnostics block gets pasted into mail by people who have not read it yet.

  ![Something wrong, or missing?](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/about-feedback.png)

- **The icon licences fold out of the same window rather than being the whole of it.** All four sets, their versions, their copyright holders and what the bundle did to each drawing, in a box you can select and copy out of - and the window gives the height back when you fold them away. It is the part of the box that is about this tool rather than about tools in general, which is what makes the rest of it worth sharing with the other four.

  ![Icon licences and credits, open](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/about-licences.png)

- `Icons\About\` holds the two files - `ToolAbout` and `AboutBox` - meant to be copied verbatim into Document X-Ray, Events2Code, Plugin Step Codegen and Ribbon. Their namespace is deliberately not this tool's. A tool supplies three strings: its page slug, its repository, and its notices if it has any.

- `IHelpPlugin` and `IAboutPlugin` on the control, which is where the twelve installed tools that implement them put them. XrmToolBox's own Help menu gets a *Help for Icons* entry pointing at the tool's page.

- The UI harness photographs the About box folded and unfolded, and fails if opening the licences does not make the window taller, changes its width, or leaves the notices empty.

- **A table that already has an icon is told which one it is, and the library goes to it.** It used to say only the name of the web resource, which is named for the table and so says nothing about whether it is a building or a briefcase. Three things can answer, and the first that can is believed: the provenance comment this tool writes into every icon it stores, then the drawing itself matched against all 18,637 bundled ones — shape for shape, and hollow or solid, because Tabler says which corner a transform-point icon means by filling that one square, then the CSS class Lucide and Tabler ship theirs with — so an icon somebody pasted in from those sites by hand is recognised as surely as one this tool wrote. Nothing is guessed. An icon that is none of the bundled ones is called none of them rather than the nearest thing; a drawing two names share is left unnamed rather than given one of them; and an icon under somebody else's publisher prefix is still named as the web resource it is, because those are never read. The drawings are read on the last link of the load, behind everything anybody is waiting for, and a read that fails costs only the sentence.

  ![The icon a table is carrying, named and gone to](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/carrying-an-icon.png)

- **The preview is a piece of an app torn off and laid on the tool, rather than a rectangle of it that stops where the pane stops.** A square edge said nothing about where the app ended and this window began, so the blank bars beside the navigation read as the tool's own furniture instead of somebody else's page. It now has a torn right edge, a torn foot and a shadow under it. The scroll bar went with the square edge — a rectangular Windows control floating beside a torn one reads as a bug — and nothing was lost by it: the ragged foot is why the rows stop, the wheel takes you down them, and the row you are choosing for is still scrolled to, now stopping above the tear rather than into it.

  ![The torn preview, on both themes](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/torn-preview.png)

- **The tables you can pick from are listed in the open, instead of hiding behind a dropdown you had to click.** The box above the list narrows it and the list is the answer — two halves of one errand, and the half that was the answer was the half you could not see. How many tables are left to do now reads without a click, the arrow keys walk them, and Enter or Down out of the *Find table* box lands you in the list — the same handoff the icon search has always made into the grid. What you typed stays in the box when you pick, so *Only tables with no icon yet* reads as the working list it is: one you go down, rather than something to keep retyping your way back into.

  ![The table list, open under the box that narrows it](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/table-list.png)

- **The form on the right takes the height it actually needs, so the pane holding it no longer grows a pair of scroll bars.** The height it was given was a number written down once, and the form is taller than that number the moment the chosen table is locked, the warning line has something to say, or your machine draws at a different size. One row too many put a scroll bar down the side, the bar took the width the widest row was using, a second bar appeared along the foot, and that took back more height than the first bar was about. The splitter above the form now gives way instead, the widest row drops its link onto a second line rather than off the edge, and the form only ever scrolls downwards — once you drag that splitter yourself, the height is your answer and nothing moves it.

  ![The form pane, before and after](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/no-scroll-bars.png)

- **Reading the environment is a refresh button beside the table list it fills, rather than a caption in the toolbar.** Nothing but that button puts tables in that box, so it belongs beside the box rather than at the top of a window with everything else. The arrow is drawn out of the library the tool is built to browse, at the moment it opens, rather than kept as a resource of its own.

  ![The refresh beside the table list](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/refresh-table.png)

- **Set as table icon moved to the bottom right of the form it sends, which is where a window that fills something in keeps its last button.** It is the last thing done to what the form says — a table, a solution, a name, a colour, and then this — and it was sitting three inches away from all of it. The red line that says why a write failed still reads above it, so the reason and the retry are in the same place.

  ![Set as table icon, bottom right](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/apply-button.png)

- **Undo and redo are drawn as the two arrows everybody already knows, out of the same bundled library.** The captions were only taking width: what those buttons would take back is a table and an icon by name, which changes with every write, so the meaning was in the tooltip all along and still is.

  ![Undo and redo, as arrows](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/undo-redo.png)

- **The colour swatch sits on the Fixed colour line, and the PNG question on the line of the box it answers.** Each pair was reading as two settings on two rows when it is one setting and its answer. Which colour, right there; and where to find out whether to tick the box, right beside it.

  ![The colour swatch and the PNG link, inline](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/inline-pairs.png)

- **The window opens with Only tables with no icon yet ticked, because that list is the errand.** Giving icons to the tables that have none is why anybody opens this tool; the full list is one click away, for when you have come to change an icon that is already there. A table still leaves the narrowed list the moment you give it one.

  ![Only tables with no icon yet, ticked](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/unset-only-open.png)

- **The icon is drawn into an app's navigation inside the tool's window, where two colour swatches were.** It used to be a window you had to ask for, and the pane it now fills was spending its height on the same drawing twice, big on white and big on black. Those answered "does this survive both themes", which the navigation answers too — through its own *Dark* box, in the app's own colours rather than a swatch's — and they could not answer the question actually being asked, which is not what the drawing looks like but whether the row reads beside the rows it will be beside. Choosing an app still reads that app's own site map; the app list is now read at the end of the load rather than when a window opened, and an app list that will not come back costs the preview and nothing else.

  ![The icon in an app's navigation, in the tool's own window](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/in-context-embedded.png)

- **Tables nobody can customise are left out of the list, and picking one says why under the table box.** They gather in *Only tables with no icon yet*, because a table that will never take an icon has never been given one, and a list that only offers what can be done with it is worth more than a complete one. Untick the box to see them. The line explaining the dead end moves out of the red trouble line at the foot of the pane, which is for round trips that failed: nothing was attempted here.

  ![A locked table, and the line under the box](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/locked-table.png)

- **The library opens on outline weight rather than both, so no drawing is in the grid twice.** Both weights put the same icon filled and unfilled adjacent in the ranking, and half the cells in the first look at the library were the alternative to the cell beside them. Outline is the weight a site map is drawn in, and the other two are one dropdown away.

  ![Weight, on Outline](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/weight.png)

- Two faults only a live preview made visible went with it. The chosen row is filled in as it is painted rather than when the navigation arrived, so picking a different icon changes what is drawn — before, the row kept whatever had been chosen when the window opened. And *Follow the app theme* no longer sends the light theme's #242424 into a dark navigation and calls the result a preview: no colour is passed at all, and the theme on show resolves `currentColor` itself.

- The fingerprint of every bundled icon is worked out at bundle time and carried inside the archive, rather than redone on every connection. It was half a second of unzipping and parsing 18,637 SVGs, and about twenty megabytes of garbage, every time a tab connected — for an answer that cannot change between builds. It is nine milliseconds of reading a file now, for 209 KB on the size of the DLL — a ninety-six bit fingerprint rather than a whole digest, since eighteen thousand drawings collide in that space about once in 10^21 tries. `tools\index-catalog.ps1` writes it through the tool's own canonicaliser rather than a copy of those rules in PowerShell; the version of that canonical form is in the file's name, so a build can only read an index it speaks; and an index that does not fit the archive it is in is refused outright. A bundle rebuilt without reindexing goes back to working it out, never to recognising the wrong thing.
- The fake Dataverse behind the slow harness now holds web resources with drawings in them and answers a query that asks for a batch of them by name, which is what reading an environment's own icons looks like. The scenario on top of it pins both halves of the answer: the name of the web resource while that is all the window knows, and the icon once the drawing has landed. `slow.ps1` with no `-Scenario` passed a null argument that Start-Process refuses, so the whole suite could only be run one scenario at a time.
- The slow-connection harness gained the app read as a fourth thing that can be slow or fail, and a scenario saying it must cost the preview only. The window harness photographs the preview in place, on both themes, and checks that a locked table is hidden and then explained. It also measures the form pane for scroll bars at three moments — on opening, with the locked note showing, and with the pane dragged as narrow as it goes — because a bar down the side and a bar along the foot each cause the other, and neither is visible in a screenshot anybody thought to take.

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
