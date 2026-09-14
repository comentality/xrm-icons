# TODO

- [x] More icon sources compatible in style with Fluent. Phosphor (regular, bold, fill), Lucide
      and Tabler (outline, filled) are bundled alongside it: 18,637 icons in four families, all
      MIT or ISC. Done for the look rather than for coverage, as the note below said it had to
      be - though it turned out to widen coverage too, because 16 of the words in Synonyms that
      Fluent never named are named outright by one of the others. Two of those are wrong in the
      way codicons are wrong: quote is quotation marks in Lucide and Tabler, and log is a wooden
      log in Phosphor. Both score 100 and lead the grid. See the ranking item below.
      Codicons (microsoft/vscode-codicons, 639 icons) were measured too and left out. They are
      the same look, not a different one - both sets are Microsoft drawing the same language,
      and side by side at 16, 32 and 64 only `person` reads differently. 104 names are shared
      outright and another 217 are covered by a Fluent name; of the 318 left, 164 are IDE
      furniture and every general one probed - trash, gear, package, pie-chart, thumbsup, law,
      checklist, layers - Fluent already draws under another name. The whole delta is magnet,
      telescope and percentage. Worse, the four Dataverse words codicons name that Fluent does
      not - account, quote, report, milestone - mean the developer sense: quote is quotation
      marks, report is a speech bubble reporting a bug, account is a user avatar. They are also
      CC-BY-4.0, which would put an attribution obligation on what gets written into a
      customer's org and cost us the line in docs/clean-room.md saying there is none.
      Phosphor's thin, light and duotone weights are left out: beside Fluent they read as a
      different tool rather than a different set.
- [x] Generate an attribution web resource, and make it mandatory for any icon that asks for one.
      `new_/icons/ATTRIBUTION.html`, in whatever folder the environment keeps its icons, listing
      every icon this tool has written there: the name it is stored under, the set, the copyright
      holder, the licence, the link, and what was done to the drawing. Each stored SVG carries its
      own provenance in a comment, and the page is rebuilt from those on every apply and published
      with the icon and the table - so it says what is in that folder now rather than what any one
      session remembers, and an icon somebody replaced by hand stops being credited the moment it
      stops being there. An icon carrying no comment is left out rather than guessed at.
      Both postures are built. For the four families bundled today the page is a courtesy, so a
      page that will not write does not cost the table its icon - the status line says the page
      could not be written and the apply goes on. `IconLicence.RequiresNotice` is the other one:
      it stops the apply before the table is touched, leaving the drawing stored with nothing
      pointing at it. Nothing bundled today sets it, and a test says so, so the licence table and
      the page's own claims are checked against `ABOUT.txt` rather than trusted to stay in step.
      One thing could only be measured, and was, against a real org on 10 September 2026: the
      platform stores an SVG with a comment in it and hands it back with the comment intact, and
      takes `ATTRIBUTION.html` as an HTML web resource in the icons' own folder. Every icon in
      the org would have been uncreditable if it did not, with every local test still green, so
      `tests/e2e.ps1` asks it on every run.
      The two things to settle first were settled on 10 September 2026. A page nobody opens is
      attribution "reasonable to the medium": CC BY 4.0 s3(a)(2) allows attribution in any
      reasonable manner for the medium and names a link to a resource holding the information as
      its own example, and a web resource has a URI. And the sentence in docs/clean-room.md and the
      README saying nothing stored carries an obligation stays true - no CC BY set ships, only the
      machinery - so both were added to rather than retracted: the page is named as where a credit
      line would live.
- [x] Bundle a set whose licence asks for a credit line, now that there is somewhere for one to
      live. This is what the page was for. Font Awesome Free went in on 14 September 2026: 1,591
      icons in solid and regular, CC-BY-4.0, and the first set for which
      `IconLicence.RequiresNotice` is true - so its icons are not put on a table unless the
      attribution page goes with them, and the library says so under the preview while one is
      being chosen. Brands is left out, 609 logos. Two things were not obvious going in. The set
      ships every name it has ever used as its own file - address-card also arrives as
      contact-card and as vcard - and taken as they come those 683 alias files put 1,255 of its
      icons into the shared-drawing set, where a drawing under more than one name is recognised
      as none of them; the package's own metadata\icon-families.json says which name is real, and
      asking it leaves 2. And it is the one set not drawn square, 512 tall by 320 to 640 wide,
      bundled at the width it was drawn because padding a viewBox out to square is changing the
      drawing to make it look like the other four.
      What is left of this item is the rest of the list below. It reopened the sources refused so far - codicons, and
      the free tier of Noun Project - because the objection to both was that the obligation landed
      on the customer with nothing carrying it, not that it existed. It does not reopen bundling
      Noun Project: its API forbids distributing icons at all, so the route there is
      import-your-own-SVG with the page taking the credit line.
      What it unlocks is exactly the licences whose whole demand is a credit line: CC BY in any
      version, and the sets under it - Font Awesome Free's icons, codicons, Noun Project's
      free tier by import. CC BY 4.0 also asks that changes be indicated, and recolouring and
      the dropped class are changes, which the page already says of each icon. It does not unlock
      terms that ask for more: share-alike, which the recoloured copy would inherit; a licence
      that forbids redistribution, which is Noun Project's API and Flaticon; or a copyleft set
      inside an MIT DLL. The test for a candidate set is whether a notice is all it wants.
      Solar went in on 14 September 2026, after a false start worth recording. It was refused
      first on a GPL-3.0 licence field read off npm's solar-icon-set - which is not the author's
      package at all but a third party's React wrapper (chebarash), GPL on their own code. 480
      Design's repo carries no LICENSE; their distribution is the Figma community file, which
      Iconify records as CC BY 4.0 and which @iconify-json/solar declares on the package. The
      lesson is the general one: read the licence off the author's own distribution, and check
      who published the package the field came from.
      What is bundled is Iconify's rendition rather than the author's export, because the author
      publishes no SVGs - Solar is a Figma file, and @iconify-json/solar is what can be fetched
      and checked at build time. That is a change of hand rather than of drawing, which CC BY
      asks be indicated, so it is the set's BundleChange and reaches every stored icon.
      Two of six styles: linear whole, and outline only where it draws something its linear does
      not. The two are the same outline stroked and filled, so no comparison of the files would
      ever have matched them - but rendered, 1,146 of the 1,273 drawings that exist in both share
      more than 95% of their ink, and the grid would have offered the same picture twice. Bold
      and the duotones are left out the way Phosphor's duotone is, broken because its gapped
      strokes are a signature rather than a weight.
      It also paid for fill-rule support: 1,202 of the outline icons punch their holes with
      even-odd and this renderer filled everything nonzero, which turns a calendar into a black
      rectangle. No other bundled set says evenodd, so nothing that shipped changed.
      What passes that test today, counted from Iconify's registry on 5 September 2026 and
      filtered to monochrome, current, drawn-for-UI sets: 22 sets, 31,449 icons, against the
      18,637 bundled now. Counts include style variants, as the bundle's own figure does.
      Solar (7,608 in six styles, about 1,270 drawings); Streamline's free families, about
      11,300 across core, Ultimate, Sharp, Flex, Plump, Freehand, Cyber and Block; Font Awesome
      7 Free, 2,272 in solid and regular; Pepicons, 3,825 in Pop, Print and Pencil; IconaMoon,
      1,781; Lets Icons, 1,528; codicons, 651; and NRK Core, CoreUI Free, Basil, coolicons and
      Guidance, about 2,500 together. Left out, all CC BY too: colour and emoji sets, about
      22,000, because the app themes a table icon; logo sets, about 10,000, because a
      trademark is not answered by a copyright notice; older Font Awesome and archived sets,
      about 3,500; and thematic ones - game, maps, audio, pixel art - about 6,600. Iconify's
      licence field is a claim, so each set is checked against its own repo before bundling,
      and each is measured the way codicons was before it goes in: a wider pool is not the
      goal, a table nothing matches is.
- [ ] Coverage parity with IconEngine: no search for a table concept comes back empty. Not its
      icons — those are Flaticon's, fetched through a keyed API, and not being that is the point
      (docs/clean-room.md).
- [x] Explain in the tool's UI when the classic PNG icons are worth writing, and when they are not.
- [x] Filter to show only entities without an icon.
- [x] Stop seeding a search the library only appears to answer. It is not a floor on the score:
      a number would not have decided any of them, because the wrong answers scored 98 as often
      as 28. The fallback word now has to be one of two things - literal, meaning some icon is
      called exactly that, or curated, meaning `Synonyms` says what the Dataverse word looks
      like - and anything else moves on to the word before it. Substrings and prefixes are still
      how search works for somebody typing, where "fold" should reach folder; they are just not
      how a box nobody filled in gets filled in.
      Measured over the 162 standard tables. Eighteen were seeding on a word the library only
      appeared to answer: Payment Term on `terminal`, Hierarchy Rule on `ruler`, Social Profile
      and Field Security Profile on `vehicle_car_profile`, Business Unit on `shelving-unit`,
      Rating Model on `box-model`, List Member on `member-of` - the set-theory symbol - Rollup
      Query on Google BigQuery's logo, and Time Entry on a brand called Sentry. Eleven of the
      eighteen found a better word in their own name by falling back one further: Hierarchy Rule
      seeds `hierarchy`, Payment Term seeds `payment`, Social Profile seeds `social`, Queue Item
      seeds `queue`, Project Team Member seeds `team`. The other seven had no word before it
      either, and got the five entries `app`, `business`, `duplicate`, `rating` and `time` in
      Synonyms - which is what the fixture always said the answer to a silent table was. No
      standard table seeds an empty box, and none of the eighteen seeds its wrong word.
- [ ] Rank a synonym answer against an exact name that means something else. This is the other
      half of the item above, and the seeding rule does not touch it: an exact name hit scores
      100 and a synonym answer 15, so a family that happens to name an icon after a Dataverse
      word leads the grid whatever it actually drew. Quote gets Lucide and Tabler's quotation
      marks; Log gets Phosphor's wooden log. Seeding better made this more visible rather than
      less - Rating Model now seeds `rating` and leads with `rating_mature`, a content rating;
      Field Security Profile seeds `security` and leads with a CCTV camera; Work Order Service
      seeds `order` and leads with `re_order`. The seed is right in all three and the ranking is
      not. The other fourteen words the four families turned up are right, so the rule cannot be
      to distrust the newer families, and it cannot be to distrust an exact hit either: the whole
      point of `person` leading with person is that an exact hit usually is the answer.
- [x] Preview the icon in context, in a window of its own. Narrowed to the navigation: the
      grid and form header were mocked and dropped, because the navigation is where the icon
      is actually looked at. `See it in context` opens it; with a connection it reads an
      app’s own site map, and without one it shows a stand-in and says so.

- [x] Measure the navigation, and replace the guesses. Run on 9 September 2026 against Power
      Pages Management in a demo org, with a `navChrome()` added to `probe.js` for the numbers
      that hang off the pane rather than off an icon. Every size but the label's 14px was wrong,
      and three were wrong in kind rather than degree: the pane is 200 and not 224, the row 35
      and not 40, the icon box 16 and not 20, the rail 44 and not 48. Selection is *lighter*
      than the pane — the selected row is painted the page's own white on grey, and in the rail
      there is no fill at all, only the 4px bar — where the mock had it darker. The selected label is not bolded; the
      mock was bolding it. A group header is a row-sized label at weight 600 in the same
      #242424, not a smaller one in a second grey, so there is no second grey in the
      navigation. And `currentColor` in the nav resolves to #424242, not the label's #242424,
      which is the difference between an icon that sits in the row and one that shouts. The
      app inlines the web resource, `<style>` block and all, and draws a 24-viewBox file at 16.
      A clone with a baked `fill` kept it inside the nav, so Fixed mode is not painted over and
      the preview can be trusted about both colour modes. What is still GUESS is dark: that
      environment has no dark theme — Set Personal Options offers high contrast and nothing else
      — and deriving one from the light palette is a guess wearing a measurement's clothes.
      `docs/ui-measurements/notes.md` has the table and says what was not walked.
- [x] Ctrl+Z / Ctrl+Y — undo and redo. Of the write, not of the window: everything else here is
      two clicks to do again by hand, and applying is the one thing that costs real effort to take
      back now that it moves you off the table and drops it out of the unset-only list. Undo points
      the table back at whatever it named before — or at nothing — puts back the drawing the apply
      wrote over, and publishes; redo sends the same request again. It asks first, because this is
      another write to somebody's environment rather than a keystroke against a document, and it
      lands back on the table it just changed instead of the next one waiting. Inside a text box
      the keystroke is left alone.
      What it does not undo: the web resource itself. One this tool created stays where it is with
      nothing pointing at it, because deleting out of a solution the customer ships is its own
      decision. Nor does it survive the connection changing — the stack names a table in one org.
      One thing had to be measured rather than assumed, and it was the opposite of what it looks
      like: clearing an icon is an empty string, not null. A null in IconVectorName is read as
      "leave this one alone", so an undo written that way publishes, reports success and changes
      nothing. `tests/e2e.ps1` asks the platform that question on every run.
- [x] Smooth the UX overall — fewer clicks to get an icon assigned. The table seeds the search,
      Enter reaches the results, a filter of one table is that table, a finished write reports on
      the status line rather than in a dialog, and applying lands on the next table waiting.
