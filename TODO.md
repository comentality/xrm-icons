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
- [ ] Give an icon the words it already answers to upstream. Two halves, the first one done, and
      `docs/search-vocabulary.md` is the brief — sources, counts, traps and the order to do them
      in. The first half was a debt and was paid on 14 September 2026: the build drops 683 Font
      Awesome alias files because they are one drawing under several names, which is right, but
      the names went with them, so `vcard` found nothing and the drawing it names sat in the
      bundle as `address-card`. The names now travel without the files, in a `vocabulary.v1.tsv`
      the archive carries beside its icons — 579 of them against 479 icons, keyed by family and
      name so the weights share one list, with a kind column so the tags can land in the same
      file under a different score.
      A hit on one is worth 100, the same as the icon's own name, because that is what it is: a
      name the set published and later stopped leading with, not a hint about the drawing the way
      a synonym is. The consequence was taken knowingly rather than worked around - an alias ties
      with an icon really called that word, and the tie breaks on the family order, which is the
      rule that already settles two icons of one name. `contact card` still leads with Fluent's
      `contact_card`. The words are kept out of `IconEntry.Words`, which would have put them in
      the grid label and charged every icon carrying them for words no query accounted for.
      Nothing seeds on them: `WorthSeeding` was not touched, and no standard table's suggestion
      moved.
      The second is the larger question behind it, and is still open.
      Four of the six sets publish per-icon tags — Tabler 66,254
      words, Phosphor 25,512, Font Awesome 20,374, Lucide 15,656, Solar a thin 1,603 — and the
      build has never read any of them. Fluent publishes none, which is the part to settle first
      rather than last: Fluent leads every tie in `IconSearch.Rank` by the enum order, on purpose,
      and giving the other five 129,399 words it has not got means they stop tying and start
      scoring where Fluent cannot reach. The other measured constraints are that the words cannot
      go into `IconEntry.Words` without corrupting every display name and penalising every icon
      that carries them, and that search is 7 ms a keystroke across the whole library today at
      2.18 words an icon — this is 5.96 more.
      Fluent's asymmetry was researched rather than guessed at on 14 September 2026, and it is
      not one: Fluent publishes a per-icon vocabulary after all. Not in the npm package this
      build fetches, which carries 21,682 SVGs and no JSON at all - in the repository, one
      `assets/<Icon Name>/metadata.json` an icon, with a `metaphor` array of about five words.
      2,654 of them against 2,989 asset folders, so about nine in ten of the names bundled here;
      2,601 carry a word that is not already in the icon's own name; 12,926 terms, 1,458
      distinct. MIT, the same licence the drawings come under. `Cloud` answers to storage,
      upload, download, save, sync - which is the sort of thing `Synonyms` was hand-written to
      supply. So the order turned round, and Fluent went in the same day rather than being
      protected: 12,482 words against 2,630 of its drawings, scored at 45 - under the 100 a name
      scores, the 60 a name prefix scores and the 50 this tool's own curated Dataverse word
      scores. A description is true about the drawing, is not what it is called, and is a gloss
      written for icon browsers rather than for Dataverse. That order was measured rather than
      argued: the descriptions went in at 60 and 27 of the curated map's 92 words lost their
      hand-picked answer out of the top of the grid - `account` stopped offering a building, a
      briefcase or an organization, and `product` ranked a Surface tablet above a box - because
      the curated tier scored 15, below even a substring match, so a description did not
      outrank it so much as delete it. At 50 against 45 that is 9 of 92, and the nine are
      variants rather than answers. Fifty-one searches were read before and after to settle it;
      `docs/search-vocabulary.md` has the whole measurement. The build script grew a seventh source for it and the
      first that is not a package, a sparse clone of microsoft/fluentui-system-icons pinned by
      commit in ABOUT.txt, because no package ships this and issue #788 asking for one has been
      open since December 2024. Words the icon's own name already has are dropped at bundle
      time: they cannot raise a score, and every one kept is another comparison on every
      keystroke.
      It moved eleven standard tables onto a better search, which the seeding rule allowed
      rather than had to be argued into - SeedFor offers a table's whole name when the whole
      name answers, and eleven could not answer whole before. Recurring Appointment asked for
      `appointment` and got a video call, and now leads with a repeating calendar; Invoice Line
      and Quote Line asked for `line` and got a straight line, and now lead with a receipt;
      Purchase Order led with a reorder arrow and now leads with a cart. DescriptionTests names
      all eleven.
      What is left is Tabler and then the other four, and the thing to carry in is how to
      measure. Half as many words again cost 8-13% of a keystroke, not the doubling the first
      measurement claimed: SearchSpeedTests was run inside the full suite once and alone the
      other time, and run alone twice it said the words had made search faster. Neither number
      meant anything. One process, both libraries, alternating rounds. Most of what a keystroke
      costs is the walk over 21,698 entries rather than the words, which is why the words are
      nearly free and why the walk is where room would have to come from if room is ever needed.
      `docs/search-vocabulary.md` has the numbers, the traps - the data is December 2024 vintage,
      case-inconsistent and holds duplicates, and its `keyword` field is which Microsoft product
      the icon belongs to rather than a search word - and what else the search turned up.
- [ ] Make the table filter a three-way choice rather than a tick box: no icon, has one, all.
      The box from the item above asks one of the three questions and hides the other — there
      is no way to ask for the tables that already have something, which is the list you want
      when you have come to change one rather than to fill one in. The default stays the no-icon
      list, for the reason that box was ticked to begin with. Locked stays on its own box: it is
      a different question and it crosses all three.
      In the `all` state the row has to say which kind it is, because the code knows three and
      the list shows none of them — stored, where `CurrentIcon` names a web resource; stock,
      where an app draws one and nothing is stored, which is every standard table; and neither,
      which is what `HasNoIcon` is. The marking is a word rather than a symbol: four ways of
      marking an icon's family with a glyph were tried in the grid and all four reverted, and
      `_tableBox` is a plain ListBox drawing `TableItem.ToString`, so a word costs nothing and a
      glyph costs an owner-draw.
- [ ] A button that takes a table's icon off, for a table this session never applied to. Undo
      already does the write — an `IconRestoreRequest` with an empty `VectorName`, which is the
      one thing about it that had to be measured rather than assumed: empty clears and null means
      leave this one alone, and `tests/e2e.ps1` asks the platform that on every run. What is
      missing is the way in when there is nothing on the undo stack.
      Remove and uncustomize are the same action here, with one exception to check. There is no
      state where a table has no icon at all: clearing sends a standard table back to the font
      glyph the app draws for it and a custom one back to the placeholder, so the button's words
      have to say which of those the table lands on rather than promise it will have none. The
      web resource stays where it is, which is the rule undo already follows. Settle whether the
      three classic PNG slots go with the vector — `RestoreRasterIcons` is already on the request.
      The exception is a managed layer holding the icon, where removing writes empty over the top
      and uncustomising would drop the unmanaged layer and let the managed value reappear. That
      was measured against a real org on 17 September 2026, and it settles the question the other
      way round from how it was asked: uncustomising a table is real, and it is not what this
      button should do.
      `RemoveActiveCustomizations` is in the org's `sdkmessage` and is public. The docs describe
      only the singular `RemoveActiveCustomization`, which is a different message taking
      `LogicalName` and `Id`, and neither has an SDK class — every caller sends an untyped
      `OrganizationRequest` by name, which is how `SetPreferredSolution` is already called here.
      It takes `SolutionComponentName` and `ComponentId` and nothing else: the org's own
      `sdkmessagerequestfield` rows say two, and the five tools in the decompiled corpus that call
      it pass those two. Entity is accepted — asked for `Entity` with a guid that cannot exist it
      answers that it cannot find the component "and Component Type 1", where a name it does not
      know answers "Invalid Platform Name", so the type resolved and only the lookup failed. That
      is the name resolving rather than a removal completing — the platform gates some types after
      the lookup, which is how it refuses SLA — but Solution Layers Explorer and Unmanaged Layer
      Bulk Remover both take entity layers off through this same message, so the gate is not on
      Entity.
      Which makes it a table's whole unmanaged layer or nothing. There is no way to name the icon,
      so every other unmanaged change to that table's own metadata goes with it; Microsoft's
      warning on it is that it "can't be reversed or undone", which puts it outside what the undo
      stack can cover; and it is documented, though this was not measured here, to refuse outright
      when the unmanaged layer is the only one — which is every custom table made in the org you
      are looking at. The button writes the empty string.
      Uncustomising is a bigger and one-way operation, and this tool is not where it should be
      reached from.


- [x] Say which tables cannot take an icon *before* one is written for them, and stop offering
      the ones that never can. Measured against a real org on 18 September 2026, and the answer
      is bigger than the table it started with.
      It started with Attribute, which refuses an icon with error -2147158445 -
      `CannotUpdateSystemEntityIcons`, "System entity icons cannot be updated." - while reporting
      `IsCustomizable` true, so the tool's existing guard walked straight past it. Nothing else in
      a definition tells these tables apart either: `IsRenameable`, `CanCreateForms`,
      `CanCreateViews`, `IsValidForAdvancedFind` are all false on Attribute and all false on
      `chat`, which does carry an icon.
      What the read-only pass found was the shape of the answer rather than the answer: of 881
      entities, 648 the tool would list, exactly 31 carry an `IconVectorName` and every one of the
      31 is `IsCustomEntity` true. Not one standard table in the org carries any icon at all.
      So a real icon was written, one table at a time, and taken off again - eighteen standard
      tables asked, eighteen refused, all with the same error. The metadata ones (attribute,
      entity, optionset), the ordinary business ones (account, contact), the people ones
      (systemuser, team, businessunit, role), the activity ones (task, appointment), and the
      platform's own (solution, workflow, savedquery, organization, audit, queue, annotation).
      Account refuses exactly as Attribute does.
      **A table the platform made cannot be given an icon. Only a custom table can.** That is
      `IsCustomEntity`, and nothing else - `IsCustomizable` says nothing about it.
      What changed: `TableItem.PlatformsOwn` carries it, the hide box is now "Hide the ones that
      cannot take an icon" and covers both dead ends, the line under the table box says which of
      the two reasons this one is, Apply is dead for them, and the stock-icon line stopped saying
      "choosing an icon below replaces it" - which was a promise the platform does not keep, made
      on 449 of the 648 tables the tool lists. The runtime catch stays behind it, for an
      environment whose definitions say something this does not expect.
      What this puts a question mark over is the stock-icon reading. It is a round trip a table -
      bounded, eight at a time - and its whole job is to keep standard tables out of the
      unset-only list. Those tables are now hidden by a flag that costs nothing. It still earns
      its place for the line that says what a table is on when somebody unticks the box, and for
      an environment that answers oddly, but the case is thinner than it was and it is worth
      re-measuring before the next release.
