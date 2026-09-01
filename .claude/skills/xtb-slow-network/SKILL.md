# xtb-slow-network — Icons specifics

The project half of the global `xtb-slow-network` skill: what this tool asks, where its slow
bench lives, and how to run it. Read the global skill first; nothing there is repeated here.

## The control

`IconsControl` in `Icons\IconsControl.cs` (state and callbacks) and `Icons\IconsControl.Ui.cs`
(layout and event wiring). Three `WorkAsync` calls:

- **LoadTables** — one `RetrieveAllEntitiesRequest`, then chains LoadSolutions on success.
- **LoadSolutions** — one `RetrieveMultiple` over `solution` joined to `publisher`. Failure is
  non-fatal: the tool falls back to the default solution and must say so.
- **Apply** — the only cancelable one. Hands off to `IconApplier.Apply`: five round trips
  without raster icons (find, create-or-update, table-read, table-update, publish), eleven
  with them (a find and a write per raster slot), a cancellation check between each.

The in-flight state is `_loadingTables`, `_loadingSolutions`, `_applying`, and the stale-result
guard is `_generation`. The invariant the callbacks keep: **a callback may only touch the flags
if its generation is current.** The generation only moves on through `UpdateConnection`, which
resets every flag itself (and cancels an in-flight apply, with a trouble line, since the
discarded callback will never report). A stale callback owns nothing.

## The bench

`tests\Icons.SlowHarness\` — run through:

```powershell
.\tests\slow.ps1                              # every scenario
.\tests\slow.ps1 -Scenario cancel-apply       # one
.\tests\slow.ps1 -NoBuild                     # reuse the last build
```

Shots and `report.txt` land in `tests\.slow\<scenario>\`, one PNG per beat. The windows open
past the desktop edge; `ICONS_HARNESS_ONSCREEN=1` puts them in front to watch.

- `SlowService.cs` — the fake org. Answers, by label: `tables` (RetrieveAllEntities),
  `solutions` (the joined query, including the `p.customizationprefix` aliased value),
  `webresource-find` (RetrieveUnpublishedMultiple), `webresource-create` (CreateRequest),
  `webresource-update` (Update), `table-read` (RetrieveEntity), `table-update` (UpdateEntity),
  `publish` (PublishXml). Script latency and failures per call:
  `s.Latency = call => call.Label == "tables" && call.Nth == 1 ? 2500 : 20`.
- `Bench.cs` — Scenario/Beat/Run, the dialog sweeper, QuietForm, Capture. The sweeper answers
  the `Set table icon` confirm with its OK button and closes everything else; WM_CLOSE on an
  OKCancel box is its Cancel, which would silently call off every apply.
- `Probe.cs` — the controls and flags, by reflection.
- `Scenarios.cs` — the suite. Every scenario has been shown able to fail: three were born red
  against real defects, the rest were mutation-tested (break the guard, watch it go red,
  put it back).

## Traps already stepped in

- `UpdateConnection(service, detail, actionName, parameter)` needs `actionName: ""` — with
  anything non-empty (including null) the base class goes looking for a method of that name.
- A disposed control does not reliably throw: WinForms lets most of the tables callback run
  against a dead window. The disposal pin asserts the dead control *starts nothing* (no
  solutions fetch after dispose), not merely that nothing throws.
- The staleness pin needs the old org's answer to land **last** (`stale-answer-discarded`);
  in the ordering where the new answer lands last, a missing generation check self-heals and
  the scenario cannot fail.
- Only the tables callback's flag ordering is pinned (`switch-mid-load`). The same
  generation-before-flags ordering in the solutions and apply callbacks is symmetric but
  unpinned: the arrangement that would catch it - a new apply already in flight on the new
  org when the old org's cancelled callback lands - is not worth the scenario it would take.
