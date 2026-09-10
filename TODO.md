# TODO

- [x] More icon sources compatible in style with Fluent. Phosphor (regular, bold, fill), Lucide
      and Tabler (outline, filled) are bundled alongside it: 18,637 icons in four families, all
      MIT or ISC. Done for the look rather than for coverage, as the note below said it had to
      be - though it turned out to widen coverage too, because 16 of the words in Synonyms that
      Fluent never named are named outright by one of the others. Two of those are wrong in the
      way codicons are wrong: quote is quotation marks in Lucide and Tabler, and log is a wooden
      log in Phosphor. Both score 100 and lead the grid. See the seeding item below.
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
- [ ] Bundle a set whose licence asks for a credit line, now that there is somewhere for one to
      live. This is what the page was for. It reopens the sources refused so far - codicons, and
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
- [ ] Stop seeding a search the library only appears to answer. Measuring the standard tables
      turned up confident nonsense as well as gaps: Social Profile seeds `profile` and leads with
      `vehicle_car_profile` at 96, Payment Term leads with `checkbox_indeterminate`, Hierarchy Rule
      with `ruler`. `SeedFor`'s last-word fallback takes a substring hit as a match, and a wrong
      suggestion reads worse than an untouched box. Probably a floor on the score that justifies
      seeding, but it needs a rule deciding rather than a number guessing.
      Four families make this worse in a new way: an exact name hit scores 100 and a synonym
      answer 15, so a family that happens to name an icon after a Dataverse word leads the grid
      whatever it actually drew. Quote gets Lucide and Tabler's quotation marks; Log gets
      Phosphor's wooden log. The other fourteen words this turned up are right, so the rule
      cannot be to distrust the newer families.
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
