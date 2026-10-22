# gh-release — Icons

Only what this project does differently. The defaults in the global skill still apply.

## The version lives in two files

`<Version>` in `Icons/Icons.csproj` **and** `<version>` in `Icons/Icons.nuspec`. The csproj sets
`<NuspecFile>`, so `dotnet pack` takes the version from the *nuspec* — a csproj bumped alone
packs the old number and says nothing. Move both, in step 3 and again in step 7.

The commit is stamped into the assembly by the SDK's own source link, with no package
reference to arrange: the About box reads `1.0.0 (949a756)` out of
`AssemblyInformationalVersion`. Check it after building by grepping
`Icons/obj/Release/net48/*.cs` for `AssemblyInformationalVersionAttribute` — it must name the
commit being tagged, not the one before it.

## Step 7 has a third part

Reopening the dev version means csproj, nuspec, **and** a fresh `## Unreleased` heading at the
top of `CHANGELOG.md`. Releasing consumes that heading, and a changelog with nowhere to write
the next entry is how a feature commit ends up bumping a version.

## Build and artifacts

`dotnet pack Icons/Icons.csproj -c Release -o <dir>`. Attach **both** to the GitHub release:

- `Icons/bin/Release/net48/Comentality.Icons.dll` — the README's manual-install route points at
  the release page for this exact file.
- `Comentality.Icons.<version>.nupkg`.

Not `dotnet publish` exes: this is a library that XrmToolBox hosts.

`NU5100` (assembly outside `lib/`) and `NU5128` (no `lib` for the declared framework) are
expected and correct — an XrmToolBox plugin ships its DLL under `Plugins\`, which is why the
package is not referenceable and does not want to be.

## Publishing is the Tool Library

```
dotnet nuget push Comentality.Icons.<version>.nupkg --api-key <KEY> --source https://api.nuget.org/v3/index.json
```

The key is the same one the sibling tools keep in `.nuget-apikey`; read it from there without
printing it. Once a tool is listed, the Tool Library picks every new version up from nuget.org by
itself and nothing else is submitted.

The first version is different: the tool has to be registered once at
https://www.xrmtoolbox.com/plugins/new from the package id, after nuget.org has indexed it, and an
administrator approves it. The form takes only the package id and reads the logo from nuget.org,
and it refuses with "Logo Url is not valid" when nuget.org has stored the embedded icon as
`application/octet-stream` instead of `image/png` — which it does now and then, for an identical
PNG (1.0.0 got octet-stream; every sibling package got image/png). Check before registering:
`curl -sI https://api.nuget.org/v3-flatcontainer/comentality.icons/<version>/icon`. If it is not
`image/png`, the only fix is another version: nuget.org never re-reads a version's icon. The
whole list it is checked against is XrmToolBox's
[validation checklist](https://www.xrmtoolbox.com/documentation/for-developers/deploy-your-plugin-in-plugins-store/plugin-validation-check-list/).

## Before tagging

- The repo must be public. The README ships inside the nupkg as the package page, and every
  screenshot in it and in the changelog is a `raw.githubusercontent.com` URL; the README's
  install link points at `releases/latest`. Private means broken images on the package page.
- Run the whole bench, not the unit tests: `dotnet test`, `tests\ui.ps1`, `tests\slow.ps1`
  (all scenarios, never one), and `tests\e2e.ps1` against a real org. The last one is the only
  thing that asks the platform whether its answers are still what the tool was built on.
- Put a **Release** build in the sandbox and use it: `xtb.ps1` builds Debug, so a manual pass is
  otherwise of a cousin of the artifact. XrmToolBox holds the DLL open — close it before copying
  over `tests\.xtb\Plugins\Comentality.Icons.dll`.

## Release notes

`<releaseNotes>` in the nuspec is the package page, and it says what the tool **is** — not what
changed. Write it from the changelog's bolded leads rather than pasting them: a lead written as
a change ("no longer counts as", "instead of an empty outline") is about a version nobody
reading the package page has run. Last line links the full changelog at the new tag.
