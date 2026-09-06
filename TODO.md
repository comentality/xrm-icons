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
- [ ] Generate an attribution web resource, and make it mandatory for any icon that asks for one.
      An HTML web resource beside the icons - `new_/icons/ATTRIBUTION.html`, in whatever folder
      the environment keeps them - listing every icon this tool has written there: the name it is
      stored under, the family, the author, the licence, and the link. Regenerated on every apply
      from what the environment holds, not from what this session remembers, so each SVG has to
      carry its own provenance in a comment for the page to be rebuilt from; published together
      with the icon and the table, the way the rest already is. For the four bundled families it
      is a courtesy. For a set licensed CC BY it is the obligation itself, and applying such an
      icon without the page must not be offered. That is what would reopen the sources refused
      so far - codicons, and the free tier of Noun Project - because the objection to both was
      that the obligation landed on the customer with nothing carrying it, not that it existed.
      It does not reopen bundling Noun Project: its API forbids distributing icons at all, so the
      route there is import-your-own-SVG with the page taking the credit line. Two things to
      settle before building it: whether a page in the solution that nobody opens is attribution
      "reasonable to the medium" in the licence's words, and the sentence in docs/clean-room.md
      and the README saying nothing stored carries an obligation, which stops being true the day
      the first attributed icon ships and has to say instead that the page is where it lives.
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
- [ ] Preview at different sizes — maybe a simulation of small sections of different CRM UI elements (sitemap entry, grid, form header) so you see the icon in context.
- [ ] Ctrl+Z / Ctrl+Y — undo and redo.
- [x] Smooth the UX overall — fewer clicks to get an icon assigned. The table seeds the search,
      Enter reaches the results, a filter of one table is that table, a finished write reports on
      the status line rather than in a dialog, and applying lands on the next table waiting.
