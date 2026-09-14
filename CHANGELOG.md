# Changelog

Every released version, newest first, with whatever is waiting for the next one above them. The
bolded sentence of each entry is the one that goes on the package page, so it has to stand on
its own.

## Unreleased — 1.0.1-dev

- **Fluent icons now answer to what Microsoft says they are for, as well as to what they are called.** Search for `keychain` and you get the key; for `patient`, the person; for `sync`, the cloud. Microsoft writes a line about each of its icons — a shield is *security, protection, safety* — and this tool never read a word of it, because the package it bundles ships no metadata at all and nobody had looked past the package. 12,482 of those words now travel with the drawings, covering about nine Fluent icons in ten, under the same licence the drawings already carry. They are worth less than a name on purpose: an icon *described* as security can never lead the grid over an icon *called* security, so the answer you were already getting is still the answer. It can outrank an icon that merely has the letters in it somewhere, which is the point.

  ![Searching for keychain finds the key](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/described-icons.png)

- **Eleven standard tables now suggest a search that means what the table means, instead of half its name.** A table whose whole name found nothing used to fall back to one word of it, and the word it fell back to was often the emptier one. Recurring Appointment asked for *appointment* and got a video call; now it asks for the whole thing and leads with a repeating calendar. Invoice Line and Quote Line asked for *line* and got a straight line; they now lead with a receipt. Purchase Order asked for *order* and led with a reorder arrow; it now leads with a cart. Also Work Order, Web Resource, Resource Group, Unit Group, Document Location, Document Template and Process Session. Nothing was added by hand for any of them — the second word simply has an answer now.

  ![Picking Recurring Appointment searches for the whole name and finds a repeating calendar](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/whole-table-name.png)

- Searching costs about a tenth more per keystroke — 10.5 ms across 21,698 icons rather than 9.4 — for half as many words again. Most of what a keystroke costs is walking the library, not comparing the words, so the words come cheap. The figure is in the repository as a test rather than in somebody's notes, because the other five families publish another 129,000 words between them and that measurement, not the appetite for coverage, is what decides whether they follow.

- **Searching for a name a set has retired now finds the icon it was retired from: `vcard` reaches the address card.** Font Awesome ships every name it has ever used as its own file, and the bundle keeps one file per drawing — so 683 of those files are left out, and until now the names went out with them. 579 of them come back as search words instead, against 479 icons, carried in the bundle beside the drawings rather than written into anybody's name. A name a set published is worth exactly what the icon's own name is worth, because that is what it is; where something in the library really is called the word you typed, that icon still leads the grid, the way it already does between any two icons of one name. Nothing you see is called anything different, and no table's suggested search moved for a retired name — the eleven that did move are the entry above, and they moved on what Microsoft says its icons are for.

  ![Searching for vcard finds the address card](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/retired-names.png)

- **Every table you pick searches the library for that table, including the ones you pick after choosing an icon.** The seed used to work once a session: taking the window up on it made the library yours, and the next table left your search box alone rather than pull the grid out from under you. But moving to another table is the next errand, and a box still searching for the last table's word over the last table's icon is the errand before this one. Two things still hold the library, because both are somebody's own work rather than the window's: a search you typed, which survives every table change, and an icon you picked before choosing any table at all - that icon is the reason you are picking a table.

  ![Picking Invoice searches the library for the invoice](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/search-follows-the-table.png)

- **Solar joins the library: 1,402 more icons, and the sixth family.** 21,698 icons in all. Solar is drawn with corner smoothing turned all the way up, which gives it a rounder, softer line than anything else bundled — and it is CC BY 4.0, so like Font Awesome it is credited on the attribution page the tool writes beside your icons, and one of its icons is not put on a table unless that page goes with it.

  ![Solar icons from the bundle](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/solar.png)

- Solar draws every icon six ways and two of them are bundled: `linear` whole, and `outline` only where it draws something its `linear` does not. They are the same outline stroked and filled, so no comparison of the files would ever match them — but rendered, 1,146 of the 1,273 drawings that exist in both share more than 95% of their ink. Taking both as they come would have offered the same picture twice in the grid and, because the tool recognises a stored icon by what it draws, cost many of them their name. The number of drawings this bundle holds under more than one name is 355, exactly what it was before Solar went in.

- Solar is also the one set that is not its author's own files: 480 Design publish it as a Figma community file rather than as SVGs, so what is bundled is Iconify's rendition. That is a change of hand rather than of drawing, which CC BY asks be indicated — it is recorded on every Solar icon the tool stores. Eighteen of its icons are left out because this renderer cannot draw them: a mask, a transform, or a zero-length line meant to come out as a dot under a round cap.

- Shapes that punch their holes with the even-odd rule now come out right. The renderer filled everything nonzero, which for a set that draws both rings of a hole the same way round means no hole at all — a calendar comes back a black rectangle, a building loses its windows. This was added for Solar, whose outline style asks for even-odd 1,202 times; no icon bundled before it does, so nothing already in the library looks any different for it.

- **Font Awesome Free is bundled: 1,591 more icons, and the first set whose licence asks for a credit line.** The library is five families and 20,296 icons. Font Awesome's are CC BY 4.0, which wants attribution wherever the drawings go and changes indicated — so `ATTRIBUTION.html` stops being a courtesy for these and becomes the condition of the icon: pick one and the library says so under the preview, naming the licence and the page it will write, and an apply that cannot write that page does not put the icon on the table. Four of the five sets still carry no obligation of their own; what changed is that the fifth's lands on a page in your solution rather than on you.

  ![What the library says when you pick a Font Awesome icon](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/credit-line.png)

- Font Awesome ships every name it has ever used, so `address-card` arrives again as `contact-card` and as `vcard`. Those 683 alias files are left out at bundle time, because the tool recognises a stored icon by what it draws, and a drawing under three names is recognised as none of them — taking the set as it comes would have cost 1,255 of its icons their name and offered the same picture three times in the grid. Its brands weight, 609 company logos, is left out too. It is also the one set not drawn square, and is bundled at the width it was drawn rather than padded out to match the other four.

- Solar was measured alongside it and refused. Iconify lists it CC BY 4.0; its author ships it on npm as GPL-3.0. Upstream's own distribution is the one that counts, and an ambiguous licence is worse than a restrictive one for a drawing written into a customer's environment.

- **The warning line under the preview now reads to the end, instead of stopping mid-word at the edge of the pane.** It has been cut off since it was written: a label that sizes itself measures its text as one line, and docking then handed it the pane’s width and drew the first line of it. The one that says a name is under another publisher’s prefix — the longest thing it had to say, and the one it was most worth reading — stopped at “A web resource of th”. It now wraps to as many lines as the sentence takes, re-measured when the pane is dragged narrower. Those lines come out of the preview above rather than off the bottom of the form: the line moved inside the pane that scrolls, so a warning long enough to crowd the form is scrolled to along with it rather than pinned over a form squeezed to make room.

  ![The whole of the prefix warning](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/warning-reads-whole.png)

- Groundwork for a set whose licence asks for a credit line: choosing such an icon says so under the preview, naming the set, the licence, the page that will be written beside the icon and what happens if it cannot be — and the confirmation gives the same reason. No family bundled today asks for one, so nothing about this is on screen yet; it is written and tested now because the first time it matters is the first time somebody applies an icon under a licence this tool has to honour.

- **Undoing an apply now rebuilds the page crediting the icons, rather than putting it back to what it said.** The page describes what is stored in the folder, and an undo does not empty the folder — nothing here deletes, so the drawing whose apply was taken back is still there and is still credited. Putting the page back made it the one thing in the folder claiming that drawing was gone. The case it is really for is applying twice under one name: the drawing goes back to the first icon, and the credit goes back to the set that drew it, which restoring could not do because the page it would restore to was written before that drawing was overwritten.

- **Undo and Redo now name every web resource they are about to write, the way the first apply does.** Redo used to say it would write the SVG “the way it did the first time” and leave the attribution page and the PNG icons to be remembered; Undo said “the web resource that was written stays where it is” about two or more of them, and said nothing about the page it was touching. A gate that stands in front of a write should say what the write is.

## v1.0.0 — 13 September 2026

The first release. It was written up as finished on 10 September and then went on being worked
on for three more days before anything shipped, so this is the whole of it: what the tool does,
and then what those three days changed about it.

### What it does

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

### What the last three days changed

- **A table an app already draws no longer counts as a table with no icon, so *Only tables with no icon yet* is a short list again.** The box read `IconVectorName` and nothing else, so every standard table in the environment qualified — 196 of them against 31 carrying a web resource, which is 31 rows removed from 648 and called a narrowing. Account sat in it looking like something nobody had got to, when Account has never had an icon stored on it and has never looked as though it needed one: a model-driven app draws a standard table from a font of its own, `Dyn CRM Symbol`, by the glyph that belongs to the table — account is U+EED6, connection U+EFD4, activitypointer U+EFF4. The org serves the vector of that same glyph at `/_imgs/svg_<object type code>.svg`, which names each drawing after the glyph it is (`svg_1.svg` is titled `Account_EED6`), so the tool asks the environment rather than carrying a list of Microsoft's tables around going stale. Two things now have to be true to be in the short list: nothing stored on the table, and nothing drawn for it either. Picking a table that is only on its stock icon says so under the box you picked it in, so unticking to find Account explains itself.

  ![The short list without Account, and the full list with it](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/stock-icons-are-not-no-icon.png)

- **The preview draws a standard table's stock icon where it really is, instead of an empty outline.** Every row an app draws from its own font was a hole in the picture the tool showed of the navigation — which made the one icon under consideration look at home among nothing rather than among what is actually there. An outline now means what it says: no web resource and no stock icon, so that row really is empty.

- The stock icons are read during the load, only for tables with nothing in `IconVectorName`, and nothing about them can fail the load. No address, a proxy, a timeout, a host that answers a sign-in page: each is a table nothing is known about, which is a table with no icon, which is what the tool believed before it asked — an environment that serves none of this gets exactly the list it got before. The read is anonymous, because the address answers anonymously, so it cannot spend or invalidate the connection's token; and it is bounded at eight requests in the air and twenty seconds altogether, so a host that hangs cannot hold the table list. Against a real environment, 648 tables is under half a second.

- The slow-network suite stands a host in front of that read which takes the connection and then says nothing — a proxy sitting on the request, which costs the timeout where a refusal costs milliseconds. Sixty tables through a five-second timeout, eight at a time, is thirty-seven seconds of reading; the list is not in at nineteen seconds and is in by twenty-one. So the twenty-second budget is what releases it, and the ceiling on what a dead host can cost somebody is measured rather than written down. The window comes back with every table listed, no trouble line and no dialog, because nothing failed — an org that will not say what it draws is one where every empty table is still an empty table.

- Every scenario now watches for a task the tool walked away from, which is the one kind of failure the harness was blind to: an unobserved fault surfaces on the finaliser thread, seconds after the beat that would have caught it, so `ThreadException` never hears about it and a scenario passes over the top of it. It found the one this release had. Whatever had not come back inside the stock-icon budget was left running while the two things bounding it were disposed out from under it, so a read landing the wrong side of that instant released a disposed gate — a read that was merely late becoming a faulted task with nobody left to catch it, at twenty seconds into exactly the proxied environment the bound is for. The last read takes them down now, and what has not answered by then is cancelled rather than left to go on reading behind a table list already handed over. What it would have cost was worth knowing rather than guessing at, and it is less than it sounds: XrmToolBox does not watch for unobserved faults, and .NET has not ended a process over one since 4.5 — but a tool sharing the process can watch, and two installed ones do, so the price was a line in somebody else's log about a task nothing in their tool started. Not a thing to leave in a release either way.

- **The window is laid out in the font it is drawn in, so a larger font no longer clips its own boxes and captions.** Rows, columns and grid cells were written down as pixels measured at 96 dpi in one font, and a window handed a bigger one kept them: *Web resource* wrapped onto three lines in an 86-pixel caption column, the solution box hung three pixels into the row below it, and the tick boxes lost the bottom of their letters. None of this is reachable in XrmToolBox as it ships — its manifest asks for `Per-monitor`, which is not one of the strings Windows parses (`true`, `true/pm`, `per monitor`), so the process is DPI-unaware and Windows bitmap-stretches the whole window at 150%: blurry, and laid out exactly as it is at 100%. It is reachable for anyone who has ticked *Override high DPI scaling behaviour* on XrmToolBox.exe, which is the standing remedy for a blurry app, and it is what the tool meets on the day [XrmToolBox#1260](https://github.com/MscrmTools/XrmToolBox/pull/1260) is merged. Every row is now as tall as what stands in it; the three that cannot be measured — the seven-row table list, the solution box, the line naming the chosen icon — are told their height in the font they are drawn in; and the thumbnails in the icon grid are the one thing still sized against the screen rather than the text, because a drawing does not grow when somebody picks a bigger font.

  ![The form at a font half again as big, before and after](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/layout-follows-the-font.png)

- The UI harness opens the window a second time at a font half again as big, and fails if any control is drawn shorter than what it holds or runs out of the cell it was put in. Twelve were, against the layout as it was. It cannot test the monitor's own scaling - no process can see 144 dpi through a host that declares itself unaware - and a bigger font is the shape that trouble arrives in anyway.

- The form pane catches up when the splitter above it moves. A SplitContainer fixed on its lower panel moves the splitter out from under what is docked in that panel and leaves it the height it had, so a form that had just been given the room it asked for grew a scroll bar in a host that never heard about it. The same habit as the apply button's, one level further in.

- **The row you are choosing for stays in the preview when XrmToolBox is resized, instead of sliding under the torn edge.** The preview scrolls on the wheel and keeps its own offset, and that offset was only ever reconsidered when a navigation arrived - so a pane that lost height lost it off the foot, and the one row the whole preview exists for was the first thing to go under the tear. Restoring XrmToolBox down from maximised is exactly how that pane loses height, because the form below it is fitted first and the preview takes what is over. It now asks again every time the pane changes shape, and moves the drawing by just what the shrinking edge took rather than half a pane, so dragging the splitter tracks the mouse instead of jumping. Growing the window back gives the scroll up again, unless the wheel has been turned since - where the drawing sits is then somebody's answer and not the pane's to undo.

  ![The chosen row before and after, in a 400-pixel window](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/chosen-row-stays-in-view.png)

- **A navigation short enough to be shown from its own top now opens at its own top, rather than scrolled to put an early row in the middle.** Going to the chosen row centred it whether or not there was anything to scroll for, so a table near the top of a site map cost you the head of the navigation for nothing.

- The UI harness sweeps the window's height with the chosen row far down a forty-row navigation, and fails if that row is ever cut off by the tear or the foot - and with a row near the top, fails if the preview scrolls at a height where it already fitted. Against the pane as it was the row is cut off by 66 pixels at 400 tall.

- **Set as table icon stays in the window when XrmToolBox is not maximised, instead of sliding under the status bar.** The form sits in the lower half of a splitter fixed on that half, and a splitter fixed on one panel answers a shrinking window by moving the splitter rather than the panel — so once the splitter is down against the preview's minimum there is nothing left to move, the window goes on shrinking, the panel does not, and its foot leaves the window with the button on it. Growing the window back did not undo it either. It took a pane under about 580 pixels to see, which a maximised 1080p window never is, and the *choose a solution* line — the one an environment with no preferred solution opens on — is what pushed it over the edge. The button and the red line that says why a write failed now dock to the pane itself rather than inside the splitter, so they are the last two strips of the pane at any height and what runs short is the form, which scrolls.

  ![Set as table icon, before and after](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/release/apply-button-stays.png)

- The UI harness sweeps the window's height down and back up with the solution line showing, and fails if the apply button or the form pane ever leaves the window. Every height above the break was fine, so the single-size shot it took before would have gone on passing.

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
