# Icons

An [XrmToolBox](https://www.xrmtoolbox.com/) tool for finding an icon and putting it on a
Dataverse table — search a library, pick one, and have the tool create the web resource, set the
table's icon, and publish, without leaving XrmToolBox.

This is a **clean-room reimplementation** of the functionality of the closed-source tool
*IconEngine* (`PLIO.IconEngine.XrmToolBox`, by Rolf Stifter). The goal is the capability, not the
code, the name, or the look.

## Status

**Empty. Nothing built yet.** Do not go looking for code that is not here.

## The clean-room rule

IconEngine ships no source. Everything in this project must be derived from:

- the author's public description ([rolfstifter.be/xrmtoolbox/iconengine](https://rolfstifter.be/xrmtoolbox/iconengine/), [store page](https://www.xrmtoolbox.com/plugins/PLIO.IconEngine.XrmToolBox/))
- Microsoft's public Dataverse documentation
- our own design decisions

**Off limits:** decompiling or disassembling the shipped DLL, fetching the NuGet package to
inspect it, and — specifically — the binary-derived analysis of IconEngine that lives in the
sibling repo `xrm-librarian` (`reports/blast/PLIO.IconEngine.XrmToolBox.md` and the per-assembly
rows in `data/blast-radius.json` / `data/capabilities.json`). Those were produced by walking the
assembly's call graph. Reading them to inform this implementation would defeat the point.

Functionality is not copyrightable; a specific implementation is. Stay on the right side of that.

## What it needs to do

Derived from the public description only:

1. **Search an icon library** — text search and filtering over a sizable collection.
2. **Preview** — see candidates before committing.
3. **Customize** — at minimum colour; possibly padding/size.
4. **Apply to Dynamics** — create the icon as a web resource and set it as a table's icon.
5. **Publish** — so the change is actually visible.

## Open questions

Decide these on purpose rather than drifting into them.

- **Where do the icons come from?** IconEngine uses Flaticon, which needs an API key and carries
  attribution terms. A permissively licensed set (Fluent UI System Icons — MIT, and Microsoft's
  own design language — or Tabler/Lucide/Material Symbols) may fit a free tool better, and can be
  bundled for offline use. This choice drives the whole architecture: online API client vs.
  embedded set.
- **SVG only, or PNG too?** Modern model-driven apps use the vector icon; the classic small/medium
  /large PNG slots still exist. Which does the tool write?
- **Scope of "apply"** — one table at a time, or batch-assign across many?
- **How would anyone know it worked?** Unit tests over the non-UI logic (SVG recolour, web
  resource naming, publish XML) plus a manual pass in a real org, following the house pattern.

## Leads — unverified, evaluate before using

- **House pattern for a Comentality XrmToolBox tool:** `~/Code/xrm-events-2-code` — SDK-style
  net48 csproj with the `XrmToolBoxPackage` reference, a `.nuspec`, `build.ps1` / `deploy.ps1` /
  `publish.ps1`, `tests/` for the non-UI logic, `docs/images` for README screenshots. Copy the
  shape; do not copy the content.
- **`xtb-slow-network` skill** — read before writing the plugin control. Long fetches over a slow
  connection are where XrmToolBox tools go wrong.
- **Dataverse specifics to verify against Microsoft docs, not memory:** the entity metadata
  attribute names for icons, the web resource type code for SVG, and whether publishing needs both
  the web resource and the entity named in the publish XML.
