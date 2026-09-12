# Could XrmToolBox scale without breaking every tool in it?

This is not part of the Icons tool. It is one experiment about the host, kept here because it is
what would decide whether taking over [XrmToolBox#1260][pr] is worth anybody's week.

## The question

XrmToolBox is DPI-unaware, and not on purpose. Its manifest says:

```xml
<dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">Per-monitor</dpiAware>
```

`Per-monitor` is not one of the strings Windows parses — the three it takes are `true`,
`true/pm` and `per monitor`. Measured with a probe carrying each of them in turn, on Windows 11
26200:

| manifest value | `GetProcessDpiAwareness` |
| --- | --- |
| `true` | 1, system aware |
| `true/pm` | 2, per-monitor |
| `per monitor` | 2, per-monitor |
| **`Per-monitor`** — what XrmToolBox ships | **0, unaware** |

So the whole window is bitmap-stretched at 150%: blurry, and laid out exactly as at 100%. That
typo is load-bearing. Correcting it alone would make every tool in the box suddenly responsible
for its own scaling on the same day, and most of them — mine included, until this week — are
built out of pixel counts measured at 96 dpi. It is why #1260 has been open since May 2023: the
maintainer's one question on it was *"it seems each and every form must be set to scale to
DPI?"*, and nobody has had an answer.

The answer this spike tests: **no — only the host's own forms.** Tools keep 96 dpi until they
say otherwise.

## Why that could work here

XrmToolBox opens each tool as a form of its own. `NewForm` is an MDI container, the dock panel
is left at DockPanel Suite's `DockingMdi` default, and `DisplayPluginControl` builds the control
with no parent, wraps it in a `PluginForm`, and creates the window at `pluginForm.Show(dpMain,
…)`. One tool, one window, one moment when that window is made. Windows can hold windows of
different DPI awareness in one process, and the unit it can do that for is exactly a window.

## What it found

```
.\run.ps1 -Headless            # asks for mixed hosting
.\run.ps1 -Headless -Default   # the control, which does not
```

| child window | with `SetThreadDpiHostingBehavior(MIXED)` | control |
| --- | --- | --- |
| opted in | `PER_MONITOR_AWARE_V2` | `PER_MONITOR_AWARE_V2` |
| unaware, GDI-scaled | `UNAWARE_GDISCALED` | `PER_MONITOR_AWARE_V2` |
| plain unaware | `UNAWARE` | `PER_MONITOR_AWARE_V2` |

Mixed awareness holds, and the hosting call is what does it: without it every child comes out
the awareness of the process, and "nothing changes for tools that have not opted in" would be a
promise nobody could keep.

`UNAWARE_GDISCALED` is the interesting column. A tool that has opted into nothing would be drawn
by GDI at the real resolution and handed back — the same size on screen as today, and sharper
than today. Not *unchanged*: better.

## What is still unproven, and needs a 150% monitor

Run `.\run.ps1` with no switches on a machine at 150% and look:

- the host's text should be sharp, and its red square should measure 144 real pixels;
- the unaware child's square should measure 96, stretched;
- the GDI-scaled child should be the same size as the plain unaware one, and sharper.

Then drag a child window between two monitors at different scales and read its log:

- **an unaware child that gets resized on a DPI change is being scaled twice.** WinForms 4.8's
  per-monitor support walks a form's control tree on `WM_DPICHANGED` and calls `Scale()`. If it
  does that to a window Windows is already stretching, the promise breaks and this whole scheme
  needs another idea.
- **float one out.** XrmToolBox reparents a `PluginForm` into a `CustomFloatWindow` when you
  drag it off the dock, and the log says whether the context survived. If it did not, the
  awareness has to be set around every handle creation — an override of `PluginForm.CreateHandle`
  — rather than once around the first `Show`.

## If it holds, the shape of the change

1. `SetThreadDpiHostingBehavior(MIXED)` once in `Program.Main`, before `NewForm` exists, guarded
   for Windows older than 1803 — XrmToolBox still lists Windows 8.1 as supported.
2. Per-monitor-v2 in the manifest and `DpiAwareness` in `app.config`, replacing the typo.
3. `PluginForm.CreateHandle` sets `UNAWARE_GDISCALED` around `base.CreateHandle()` unless the
   tool opted in.
4. The opt-in is a marker interface on the tool's factory class, read from cached metadata before
   the control is built: `Metadata.Interfaces.Contains(nameof(IDpiAwareTool))`. That is the same
   idiom `INoConnectionRequired` already uses, and it has to be that idiom rather than a check on
   the control, because the decision comes before `GetControl()`.
5. The host's own forms get real scaling. This is the bulk of the work and the part #1260 shows
   is not free: `NewForm` and `PluginForm` were authored at different design-time baselines
   (`9F,20F` against `8F,16F`), there is a third-party dock library laying out around them, and a
   WebView2.

[pr]: https://github.com/MscrmTools/XrmToolBox/pull/1260
