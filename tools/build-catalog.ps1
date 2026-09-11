<#
.SYNOPSIS
    Rebuilds Icons\Catalog\icons.zip from four npm icon sets.

.DESCRIPTION
    The tool ships its icon library inside the DLL, so it works with no network and no API key.
    This script is what produces that library. Run it to pick up newer icon releases; the
    result is committed, so a normal build does not need npm, node or an internet connection.

    Four sets go in, each from its own npm package, each under a permissive licence:

        fluent    @fluentui/svg-icons     MIT   regular, filled
        phosphor  @phosphor-icons/core    MIT   regular, bold, fill
        lucide    lucide-static           ISC   regular
        tabler    @tabler/icons           MIT   outline, filled

    The archive layout is <family>/<weight>/<name>.svg plus one ABOUT.txt, and nothing else.
    Weight is the family's own word for it, not a normalised one: Phosphor's fill and Tabler's
    filled are the same idea, but a person reading "phosphor/fill" should find it where
    Phosphor says it is. The reader maps those words onto its own two-way filter.

    Each family keeps its own naming convention -- Fluent separates words with underscores,
    the other three with dashes -- because renaming would make the entry disagree with the
    upstream file it came from, and search splits on both characters anyway.

    Fluent is the one set that needs choosing from. That package holds every icon at every
    optical size, 20k files, which is more than a table icon needs: one SVG scales. So for each
    (name, style) we keep exactly one file, preferring the size Fluent draws most carefully and
    falling back through the others when an icon is not drawn at that size.

    Normalisation happens here, at bundle time, so that the runtime reader stays dumb: it opens
    an entry and hands the bytes to the renderer. See the two patterns below for what is
    stripped and why. Everything else is left byte-identical, including Lucide's leading
    licence comment, which is the ISC attribution and has to travel with the icon. That is also
    why the editing is done with regex on the text rather than an XML round-trip: a round-trip
    would reformat all 15k files and drop that comment on the floor.

.PARAMETER Source
    An already-extracted @fluentui/svg-icons package directory (the one containing icons\).
    Without it the script downloads the version named by -Version.

.PARAMETER Version
    Version of @fluentui/svg-icons to download. "latest" resolves through the npm registry.

.PARAMETER PhosphorVersion
    Version of @phosphor-icons/core to download.

.PARAMETER LucideVersion
    Version of lucide-static to download.

.PARAMETER TablerVersion
    Version of @tabler/icons to download.

.EXAMPLE
    .\tools\build-catalog.ps1
    .\tools\build-catalog.ps1 -Source C:\tmp\svg-icons
    .\tools\build-catalog.ps1 -TablerVersion 3.31.0
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$Version = "latest",
    [string]$PhosphorVersion = "latest",
    [string]$LucideVersion = "latest",
    [string]$TablerVersion = "latest"
)

$ErrorActionPreference = "Stop"

# Windows PowerShell renders a progress bar for every buffer Invoke-WebRequest reads, which on a
# 25 MB tarball costs more than the download. Harmless under pwsh, so it is set unconditionally.
$ProgressPreference = "SilentlyContinue"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo "Icons\Catalog\icons.zip"

# Fluent draws each icon separately per optical size. 24 is the size the set is most complete
# at and the one the design system treats as canonical, so it leads; the rest are here only so
# that an icon drawn at 20 or 16 only is still offered rather than silently dropped.
$sizePreference = @(24, 20, 32, 28, 16, 48)

# Lucide and Tabler both put a class attribute on their elements, naming a stylesheet that only
# exists on their own web pages. The tool warns when a table icon carries a CSS class, because
# Microsoft advises against it -- Dataverse renders the SVG without any stylesheet, so the class
# either does nothing or hides the drawing. Left in, every single icon from those two families
# would raise a warning that says nothing about the icon.
$classPattern = '\s+class=("[^"]*"|''[^'']*'')'

# Tabler opens every icon with a full-canvas rectangle: <path stroke="none" d="M0 0h24v24H0z"
# fill="none" />. It is invisible in a browser and gives the icon a hit area there. Our renderer
# paints paths rather than honouring fill="none" on them, so it would paint that rectangle solid
# over the icon and every Tabler preview would be a filled square.
$backdropPattern = '<path\s+stroke="none"\s+d="M0\s*0h24v24H0z"\s+fill="none"\s*/?>'

# Printed beside the real counts as a sanity check. Not asserted: the upstream sets grow, and a
# build that refuses to run because Lucide gained nine icons would be worse than one that says so.
$expected = [ordered]@{
    "fluent"           = 5867
    "phosphor/regular" = 1512
    "phosphor/bold"    = 1512
    "phosphor/fill"    = 1512
    "lucide/regular"   = 2050
    "tabler/outline"   = 5130
    "tabler/filled"    = 1054
}

# ---------------------------------------------------------------------------------------------

function Get-NpmPackage {
    <#
        Downloads one npm package and untars it, returning the extracted package directory.

        The registry answers /<name>/<version-or-tag> for a pinned version and for a dist-tag
        alike, and the answer carries the tarball URL it wants used. Asking beats composing the
        URL from a filename convention the registry is free to change, and it is the only form
        that gets scoped names right without special-casing the slash.
    #>
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$VersionOrTag,
        [Parameter(Mandatory)][string]$Root
    )

    # Every npm tarball extracts to a directory called "package", so each one needs its own
    # parent or the four would land on top of each other.
    $dir = Join-Path $Root ($Name -replace '[^A-Za-z0-9]+', '-')
    $tgz = Join-Path $dir "package.tgz"

    for ($attempt = 1; ; $attempt++) {
        try {
            if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
            New-Item -ItemType Directory -Path $dir | Out-Null

            $meta = Invoke-RestMethod "https://registry.npmjs.org/$Name/$VersionOrTag"
            Write-Host "  $Name $($meta.version)"
            Invoke-WebRequest -Uri $meta.dist.tarball -OutFile $tgz -UseBasicParsing

            # Relative, from inside the directory, rather than tar -xzf C:\...\package.tgz -C
            # C:\...: whichever tar is first on PATH may be Windows' bsdtar or the GNU tar that
            # ships with Git, and GNU tar reads "C:\..." as host C, path \... and tries to open
            # an rsh connection to it. A bare file name is unambiguous to both.
            Push-Location $dir
            try {
                & tar -xzf "package.tgz"
                if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }
            }
            finally { Pop-Location }

            $package = Join-Path $dir "package"
            if (-not (Test-Path $package)) { throw "$Name did not extract a package\ directory" }
            return $package
        }
        catch {
            # One retry. A reset connection on a 10 MB download should not cost the whole
            # catalogue; a second failure is real and is allowed to stop the build, because a
            # half-built archive is worse than no new archive.
            if ($attempt -ge 2) { throw }
            Write-Warning "  $Name failed: $($_.Exception.Message). Retrying once."
        }
    }
}

function Get-PackageFacts {
    <#
        Version, licence and copyright line for one extracted package, read from the package
        itself. The copyright comes out of the shipped LICENSE rather than from memory, so the
        notice we redistribute says what the package says it should, and follows the package
        when it changes hands. -Fallback covers a package that ships no LICENSE at all.
    #>
    param(
        [Parameter(Mandatory)][string]$Dir,
        [string]$Fallback
    )

    $pkg = Get-Content (Join-Path $Dir "package.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $license = if ($pkg.license -is [string]) { $pkg.license } else { [string]$pkg.license.type }

    $copyright = $null
    foreach ($name in @("LICENSE", "LICENSE.md", "LICENSE.txt", "license", "license.md")) {
        $path = Join-Path $Dir $name
        if (Test-Path $path) {
            # Every line, not the first: Lucide forked Feather and its LICENSE names both
            # holders, so taking one would ship half the attribution. -cmatch rather than
            # -match because PowerShell compares case-insensitively by default and the MIT
            # boilerplate line "copyright notice and this permission notice..." would match.
            $copyright = (Get-Content $path -Encoding UTF8 |
                Where-Object { $_ -cmatch '^\s*Copyright' } |
                ForEach-Object { $_.Trim() }) -join "`n"
            break
        }
    }
    if (-not $copyright) {
        if (-not $Fallback) { throw "$Dir ships no LICENSE and no -Fallback copyright was given" }
        $copyright = $Fallback
    }

    [pscustomobject]@{ Version = $pkg.version; License = $license; Copyright = $copyright }
}

function Get-NormalizedSvg {
    <#
        Reads one SVG and applies the two edits described at the top of this file. Reports what
        it changed so the build can say how many icons each pattern actually touched -- a class
        attribute turning up in Fluent, or a Tabler icon without the backdrop, means an upstream
        layout changed and the caller should look.
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [switch]$StripBackdrop
    )

    $text = [System.IO.File]::ReadAllText($Path)

    $classes = ([regex]$classPattern).Matches($text).Count
    if ($classes -gt 0) { $text = [regex]::Replace($text, $classPattern, "") }

    $backdrops = 0
    if ($StripBackdrop) {
        $backdrops = ([regex]$backdropPattern).Matches($text).Count
        if ($backdrops -gt 0) { $text = [regex]::Replace($text, $backdropPattern, "") }
    }

    [pscustomobject]@{ Text = $text; Classes = $classes; Backdrops = $backdrops }
}

# ---------------------------------------------------------------------------------------------

$records = New-Object System.Collections.Generic.List[object]
$seen = @{}
$notes = New-Object System.Collections.Generic.List[string]

function Add-Icon {
    <#
        Records one archive entry. The hashtable is not decoration: two files landing on the
        same entry path would mean one icon silently replacing another, and after the Phosphor
        suffix stripping that is a real possibility rather than a theoretical one.
    #>
    param(
        [Parameter(Mandatory)][string]$Entry,
        [Parameter(Mandatory)][string]$Path,
        [switch]$StripBackdrop
    )

    if ($seen.ContainsKey($Entry)) {
        throw "Two files claim $Entry`n  $($seen[$Entry])`n  $Path"
    }
    $seen[$Entry] = $Path
    $records.Add([pscustomobject]@{ Entry = $Entry; Path = $Path; StripBackdrop = [bool]$StripBackdrop })
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("icon-catalog-" + [Guid]::NewGuid().ToString("N"))
try {
    New-Item -ItemType Directory -Path $temp | Out-Null

    # --- Fluent ---------------------------------------------------------------------------
    Write-Host "Fetching packages..."
    if ($Source) { Write-Host "  @fluentui/svg-icons from $Source" }
    else { $Source = Get-NpmPackage -Name "@fluentui/svg-icons" -VersionOrTag $Version -Root $temp }
    $fluentDir = Join-Path $Source "icons"
    if (-not (Test-Path $fluentDir)) { throw "No icons\ directory under $Source" }
    # @fluentui/svg-icons ships package.json, a changelog, a readme and 20k SVGs, and no LICENSE
    # file, so its copyright holder cannot be read out of the package the way the other three
    # can. This is the notice from the fluentui-system-icons repository the package is built from.
    $fluent = Get-PackageFacts $Source -Fallback "Copyright (c) Microsoft Corporation."

    $phosphorRoot = Get-NpmPackage -Name "@phosphor-icons/core" -VersionOrTag $PhosphorVersion -Root $temp
    $phosphor = Get-PackageFacts $phosphorRoot

    $lucideRoot = Get-NpmPackage -Name "lucide-static" -VersionOrTag $LucideVersion -Root $temp
    $lucide = Get-PackageFacts $lucideRoot

    $tablerRoot = Get-NpmPackage -Name "@tabler/icons" -VersionOrTag $TablerVersion -Root $temp
    $tabler = Get-PackageFacts $tablerRoot

    Write-Host ""
    Write-Host "Selecting icons..."

    # One file per (name, style), best available size.
    $best = @{}
    foreach ($file in [System.IO.Directory]::EnumerateFiles($fluentDir, "*.svg")) {
        $leaf = [System.IO.Path]::GetFileNameWithoutExtension($file)
        if ($leaf -notmatch '^(?<name>.+)_(?<size>\d+)_(?<style>regular|filled)$') { continue }

        $key = "$($Matches.style)|$($Matches.name)"
        $rank = $sizePreference.IndexOf([int]$Matches.size)
        if ($rank -lt 0) { continue }

        if (-not $best.ContainsKey($key) -or $rank -lt $best[$key].Rank) {
            $best[$key] = [pscustomobject]@{ Path = $file; Rank = $rank }
        }
    }
    foreach ($key in $best.Keys) {
        $parts = $key -split '\|', 2
        Add-Icon -Entry "fluent/$($parts[0])/$($parts[1]).svg" -Path $best[$key].Path
    }

    # --- Phosphor -------------------------------------------------------------------------
    # Thin, light and duotone are in the package too and are left out: beside Fluent at the same
    # pixel size they read as grey rather than as a drawing.
    foreach ($weight in @("regular", "bold", "fill")) {
        $dir = Join-Path $phosphorRoot "assets\$weight"
        if (-not (Test-Path $dir)) { throw "No assets\$weight under $phosphorRoot" }

        $kept = 0
        foreach ($file in [System.IO.Directory]::EnumerateFiles($dir, "*.svg")) {
            $name = [System.IO.Path]::GetFileNameWithoutExtension($file)

            # Phosphor names the file after the weight as well as the icon: folder-bold.svg,
            # folder-fill.svg. The suffix has to come off, because the name is what search
            # matches on -- left in, every fill icon carries "fill" as a searchable word, so
            # typing "fill" returns 1512 unrelated icons and every icon whose actual subject is
            # a fill loses to them. Regular files carry no suffix, so only bold and fill.
            if ($weight -ne "regular") {
                if ($name.EndsWith("-$weight")) {
                    $name = $name.Substring(0, $name.Length - $weight.Length - 1)
                }
                else {
                    $notes.Add("phosphor/$weight/$name.svg has no -$weight suffix to strip")
                }
            }

            Add-Icon -Entry "phosphor/$weight/$name.svg" -Path $file
            $kept++
        }
        if ($kept -eq 0) { throw "assets\$weight held no SVGs" }
    }

    # --- Lucide ---------------------------------------------------------------------------
    # One weight, drawn as an outline. The package also ships fonts and a sprite sheet, both of
    # which we skip: the tool wants one file per icon.
    $dir = Join-Path $lucideRoot "icons"
    if (-not (Test-Path $dir)) { throw "No icons\ directory under $lucideRoot" }
    foreach ($file in [System.IO.Directory]::EnumerateFiles($dir, "*.svg")) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($file)
        Add-Icon -Entry "lucide/regular/$name.svg" -Path $file
    }

    # --- Tabler ---------------------------------------------------------------------------
    foreach ($weight in @("outline", "filled")) {
        $dir = Join-Path $tablerRoot "icons\$weight"
        if (-not (Test-Path $dir)) { throw "No icons\$weight under $tablerRoot" }

        foreach ($file in [System.IO.Directory]::EnumerateFiles($dir, "*.svg")) {
            $name = [System.IO.Path]::GetFileNameWithoutExtension($file)
            if ($name.EndsWith("-$weight")) {
                $notes.Add("tabler/$weight/$name.svg carries a -$weight suffix in its file name")
            }
            Add-Icon -Entry "tabler/$weight/$name.svg" -Path $file -StripBackdrop
        }
    }

    # Ordinal, so the archive is in the order a byte-wise reader would put it, not the order the
    # current culture would.
    $records.Sort([System.Comparison[object]] { param($a, $b) [string]::CompareOrdinal($a.Entry, $b.Entry) })
    Write-Host "Selected $($records.Count) icons"

    # --- Write ----------------------------------------------------------------------------
    $classHits = @{}
    $backdropHits = @{}
    $backdropMisses = 0

    if (Test-Path $out) { Remove-Item $out }
    $zip = [System.IO.Compression.ZipFile]::Open($out, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($record in $records) {
            $family = $record.Entry.Split('/')[0]
            $svg = Get-NormalizedSvg -Path $record.Path -StripBackdrop:$record.StripBackdrop

            if ($svg.Classes -gt 0) { $classHits[$family] = [int]$classHits[$family] + 1 }
            if ($record.StripBackdrop) {
                if ($svg.Backdrops -eq 1) { $backdropHits[$family] = [int]$backdropHits[$family] + 1 }
                else { $backdropMisses++ }
            }

            $entry = $zip.CreateEntry($record.Entry, [System.IO.Compression.CompressionLevel]::Optimal)
            $writer = New-Object System.IO.StreamWriter($entry.Open())
            try { $writer.Write($svg.Text) } finally { $writer.Dispose() }
        }

        # Provenance travels with the icons. The tool shows this in its About box, and a bundle
        # that cannot say where it came from or under what terms is a liability. Each block names
        # the package actually resolved at build time and what this script changed about it.
        $fallbacks = $sizePreference[1..($sizePreference.Count - 1)] -join ', '
        $about = @"
Comentality Icons bundles four icon sets, each redistributed under its own licence
and unmodified except as noted below.

Microsoft Fluent UI System Icons
@fluentui/svg-icons $($fluent.Version)
https://github.com/microsoft/fluentui-system-icons
Licensed under the $($fluent.License) License. $($fluent.Copyright)

Repackaged for Comentality Icons by tools\build-catalog.ps1: one SVG per icon and
style, at $($sizePreference[0]) pixels where Fluent draws one, otherwise the first available
of $fallbacks, contents otherwise unmodified.

Phosphor Icons
@phosphor-icons/core $($phosphor.Version)
https://github.com/phosphor-icons/core
Licensed under the $($phosphor.License) License. $($phosphor.Copyright)

Repackaged for Comentality Icons by tools\build-catalog.ps1: the regular, bold and
fill weights only, with the weight dropped from each file name so that folder-fill.svg
becomes phosphor/fill/folder.svg, contents otherwise unmodified.

Lucide
lucide-static $($lucide.Version)
https://lucide.dev
Licensed under the $($lucide.License) License. $($lucide.Copyright)

Repackaged for Comentality Icons by tools\build-catalog.ps1: the class attribute
removed from every element, contents otherwise unmodified, including the licence
comment each file carries.

Tabler Icons
@tabler/icons $($tabler.Version)
https://tabler.io/icons
Licensed under the $($tabler.License) License. $($tabler.Copyright)

Repackaged for Comentality Icons by tools\build-catalog.ps1: the class attribute and
the leading full-canvas <path fill="none" /> removed from every icon, contents
otherwise unmodified.
"@
        $entry = $zip.CreateEntry("ABOUT.txt", [System.IO.Compression.CompressionLevel]::Optimal)
        $writer = New-Object System.IO.StreamWriter($entry.Open())
        try { $writer.Write($about) } finally { $writer.Dispose() }
    }
    finally { $zip.Dispose() }

    # --- Index ----------------------------------------------------------------------------
    # The fingerprint of every icon, worked out once and carried inside the archive, so the
    # tool does not spend half a second on it per connection. Here rather than left to be
    # remembered: a bundle without one still works, which is exactly why nobody would notice.
    & (Join-Path $PSScriptRoot "index-catalog.ps1") -Zip $out

    # --- Report ---------------------------------------------------------------------------
    # Counted back out of the finished file rather than off the list that built it, because the
    # question worth answering is what is in the archive.
    $check = [System.IO.Compression.ZipFile]::OpenRead($out)
    try {
        $furniture = @("ABOUT.txt") + @($check.Entries | Where-Object { $_.FullName -like "drawings.v*.tsv" } | ForEach-Object { $_.FullName })
        $svgs = $check.Entries | Where-Object { $furniture -notcontains $_.FullName }
        $stray = $svgs | Where-Object { $_.FullName -notmatch '^(fluent|phosphor|lucide|tabler)/[a-z]+/[^/]+\.svg$' }
        if ($stray) { throw "Unexpected entries: $(($stray | Select-Object -First 5 -ExpandProperty FullName) -join ', ')" }

        Write-Host ""
        foreach ($group in $svgs | Group-Object { ($_.FullName -split '/')[0] } | Sort-Object Name) {
            $want = if ($expected.Contains($group.Name)) { " (expected about $($expected[$group.Name]))" } else { "" }
            Write-Host ("{0,-9} {1,6}{2}" -f $group.Name, $group.Count, $want)
            foreach ($sub in $group.Group | Group-Object { ($_.FullName -split '/')[1] } | Sort-Object Name) {
                $key = "$($group.Name)/$($sub.Name)"
                $want = if ($expected.Contains($key)) { " (expected about $($expected[$key]))" } else { "" }
                Write-Host ("  {0,-16} {1,6}{2}" -f $sub.Name, $sub.Count, $want)
            }
        }
        Write-Host ("{0,-9} {1,6}" -f "total", $svgs.Count)
    }
    finally { $check.Dispose() }

    Write-Host ""
    foreach ($family in $classHits.Keys | Sort-Object) { Write-Host "Removed a class attribute from $($classHits[$family]) $family icons" }
    foreach ($family in $backdropHits.Keys | Sort-Object) { Write-Host "Removed the backdrop rectangle from $($backdropHits[$family]) $family icons" }
    if ($backdropMisses -gt 0) { Write-Warning "$backdropMisses Tabler icons did not have exactly one backdrop rectangle" }
    foreach ($note in $notes | Select-Object -First 20) { Write-Warning $note }
    if ($notes.Count -gt 20) { Write-Warning "...and $($notes.Count - 20) more" }

    $kb = [math]::Round((Get-Item $out).Length / 1KB)
    Write-Host ""
    Write-Host "Wrote $out ($kb KB)"
}
finally {
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
}
