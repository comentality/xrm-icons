# TODO

- [ ] More icon sources compatible in style with Fluent (beyond the current set). Worth knowing
      before starting: this will not widen coverage. Every standard table that used to seed
      nothing was a missing word, not a missing icon, and no other pack names an icon
      "entitlement" either. Do this for a different look, if at all.
      Codicons (microsoft/vscode-codicons, 639 icons) were measured against this and are a no:
      they are the same look, not a different one - both sets are Microsoft drawing the same
      language, and side by side at 16, 32 and 64 only `person` reads differently. 104 names are
      shared outright and another 217 are covered by a Fluent name; of the 318 left, 164 are IDE
      furniture and every general one probed - trash, gear, package, pie-chart, thumbsup, law,
      checklist, layers - Fluent already draws under another name. The whole delta is magnet,
      telescope and percentage. Worse, the four Dataverse words codicons name that Fluent does
      not - account, quote, report, milestone - mean the developer sense: quote is quotation
      marks, report is a speech bubble reporting a bug, account is a user avatar. Those would
      score 100 and lead the grid over the synonym answers at 15, which is the confident
      nonsense below, on the words the tool exists for. They are also CC-BY-4.0, which would put
      an attribution obligation on what gets written into a customer's org and cost us the line
      in docs/clean-room.md saying there is none.
      Of the sets that would be a different look, only Phosphor (MIT, 1,512 icons in each of
      six weights) can be bundled as it is: flat filled paths, fill="currentColor", nothing
      the renderer does not already draw, and its bold weight sits at Fluent's stroke while
      keeping squarer corners. Lucide is stroked, all 2,050 of them, and 42% use circle, line
      or rect, so it needs a real SVG renderer rather than SvgPath. Tabler's outline weight is
      all paths but all stroked; its filled weight (1,054) would work bar one thing - every
      Tabler icon opens with <path d="M0 0h24v24H0z" fill="none"/>, and SvgImage maps a
      fill of none to the caller's colour, so today it draws a solid black square over the
      icon. Honouring none as paint-nothing is a few lines, and worth doing regardless of
      whether a second set ever lands.
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
- [ ] Preview at different sizes — maybe a simulation of small sections of different CRM UI elements (sitemap entry, grid, form header) so you see the icon in context.
- [ ] Ctrl+Z / Ctrl+Y — undo and redo.
- [x] Smooth the UX overall — fewer clicks to get an icon assigned. The table seeds the search,
      Enter reaches the results, a filter of one table is that table, a finished write reports on
      the status line rather than in a dialog, and applying lands on the next table waiting.
