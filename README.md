# ![](https://raw.githubusercontent.com/comentality/xrm-icons/main/Icons/icon.png) Icons

An [XrmToolBox](https://www.xrmtoolbox.com/) tool for putting a decent icon on a Dataverse
table. Search a library of Microsoft's Fluent icons, pick one, and the tool writes the SVG web
resource, points the table at it, and publishes — without leaving XrmToolBox.

![The library, searched for "account", with an icon chosen](https://raw.githubusercontent.com/comentality/xrm-icons/main/docs/images/overview.png)

Setting a table icon by hand means finding an SVG somewhere, checking its licence, making a web
resource with exactly the right name and type, pasting the base64 in, finding the table, typing
the web resource name into the icon property, and publishing both. Icons does all of it from one
window.

## Install

- **XrmToolBox Tool Library** — search for *Icons* and install it.
- **Manually** — download `Comentality.Icons.dll` from the
  [latest release](https://github.com/comentality/xrm-icons/releases/latest), copy it into your
  XrmToolBox `Plugins` folder, and restart XrmToolBox.

Needs XrmToolBox 1.2025.7+, .NET Framework 4.8, and a connection whose user can create web
resources, update table definitions and publish customisations.

## What it does

- **Search 5,867 icons** — Microsoft's [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)
  in both the regular and filled weights, bundled inside the tool. No API key, no account, and no
  outbound host beyond Dataverse itself, so it works from behind a customer's proxy.
- **Search in Dynamics words** — Fluent names icons after what they depict, and Dataverse names
  tables after what they mean. Searching for `account` finds the buildings and briefcases,
  `contact` finds the people, `appointment` finds the calendars, `case` finds the headsets. An
  icon actually named for your word still wins; the mapping only fills in where nothing is.
- **See it before you commit** — the chosen icon is previewed on light and on dark, at a size
  that shows what it is and at the size a site map really renders it.
- **Colour that survives a theme change** — by default the icon is written with
  `fill="currentColor"`, so the app colours it and it stays legible in dark mode. This is
  [what Microsoft asks for](https://learn.microsoft.com/en-us/power-apps/developer/model-driven-apps/image-web-resources)
  in a table icon. A fixed colour is one radio button away when you need a brand one.
- **One button to apply** — stores the SVG as a web resource, sets the table's `IconVectorName`,
  and publishes the table and the web resource together in a single call. Publishing only one of
  them is how you get an app that shows the old icon, or asks for one that is not there yet.
- **The classic PNG icons too, if you want them** — optionally generates and stores the 16×16,
  32×32 and 66×48 images for the older `IconSmallName`, `IconMediumName` and `IconLargeName`
  properties. Off by default, because Microsoft's guidance is to use the vector instead.

## Workflow

1. Open the tool. The library is inside it, so you can browse and search before connecting.
2. **Load tables** to read the environment's tables and its unmanaged solutions.
3. Search for an icon and select it.
4. Pick the table, and the solution new web resources should go into. The web resource name is
   suggested from the solution's publisher prefix and the table name — `new_/icons/account.svg` —
   and you can change it.
5. Choose the theme colour or a fixed one.
6. **Set as table icon**, confirm, done.

Running it a second time on the same table updates the same web resource rather than leaving a
trail of near-duplicates.

## What it refuses, and when

The point of each of these is that it happens *before* the round trip, not as an error dialog
after it:

- A table locked against customisation is called out the moment you select it, and cannot be
  applied to.
- A web resource name Dataverse would reject is caught with the offending character named — the
  platform's own message does not say which one it disliked.
- A name already used by a different kind of web resource, or by a managed one, stops the write
  rather than half-doing it.
- An icon that carries its own colours — a handful do, such as the pride flags — warns that
  recolouring will not take, rather than silently ignoring your colour.
- **Cancel** actually cancels. The call already on the wire cannot be recalled, but on a slow
  link the ones behind it are most of the wait, and those are stopped.

## Where the icons come from

[Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons), © Microsoft
Corporation, under the MIT licence. The tool bundles one SVG per icon and weight, taken from the
largest optical size Fluent draws it at, otherwise unmodified. **About the icons** in the toolbar
shows the exact version and the licence. `tools\build-catalog.ps1` regenerates the bundle from a
newer release of the npm package.

The icons are MIT, so what you store in your environment carries no attribution obligation — but
the licence travels with the bundle so you can check that for yourself.

## Building it

```powershell
.\build.ps1                 # build and drop Icons.dll into your XrmToolBox Plugins folder
dotnet test tests\Icons.Tests\Icons.Tests.csproj
.\tests\ui.ps1              # open the real control in a bare window and photograph it
.\tests\slow.ps1            # drive it against a Dataverse that answers in seconds, and misbehaves
.\tools\build-catalog.ps1   # rebuild the icon bundle from @fluentui/svg-icons
.\tools\make-tool-icon.ps1  # redraw the tool's own icon, using the tool's own renderer
```

The tool ships as a single DLL with no dependencies beyond XrmToolBox itself. That is why it
parses and rasterises SVG rather than referencing a library: restricted to the weights it
bundles, every icon is one `<svg>` containing only `<path>` elements — no strokes, transforms,
gradients or stylesheets — so the whole of what has to be understood is the path grammar. The
test suite draws all 5,867 of them and checks each leaves ink on the canvas.

## Provenance

This is a clean-room reimplementation of the *functionality* of
[IconEngine](https://www.xrmtoolbox.com/plugins/PLIO.IconEngine.XrmToolBox/), a closed-source
XrmToolBox tool by Rolf Stifter that also finds icons and applies them to Dynamics. None of its
code was available, read, decompiled or otherwise consulted; everything here derives from its
public description, Microsoft's documentation, and our own decisions. It differs where it
matters: the icon library is Microsoft's own permissively licensed set bundled offline rather
than a keyed third-party API.

The rule, and what is specifically off limits to anyone working on this, is written down in
[docs/clean-room.md](docs/clean-room.md).

## Related

- [Events2Code](https://github.com/comentality/xrm-events-2-code) — turn form designer event
  registrations into JavaScript.
- [Document Template X-Ray](https://github.com/comentality/xrm-document-x-ray) — see what fields
  a Word template actually uses.

## Licence

MIT — see [LICENSE](LICENSE). The bundled icons are MIT from Microsoft; see **Where the icons
come from** above.
