# TODO

- [ ] More icon sources compatible in style with Fluent (beyond the current set). Worth knowing
      before starting: this will not widen coverage. Every standard table that used to seed
      nothing was a missing word, not a missing icon, and no other pack names an icon
      "entitlement" either. Do this for a different look, if at all.
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
