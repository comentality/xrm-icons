<#
.SYNOPSIS
    Rebuilds Icons\Catalog\icons.zip from six npm icon sets.

.DESCRIPTION
    The tool ships its icon library inside the DLL, so it works with no network and no API key.
    This script is what produces that library. Run it to pick up newer icon releases; the
    result is committed, so a normal build does not need npm, node or an internet connection.

    Six sets go in, each from its own npm package:

        fluent       @fluentui/svg-icons            MIT         regular, filled
        phosphor     @phosphor-icons/core           MIT         regular, bold, fill
        lucide       lucide-static                  ISC         regular
        tabler       @tabler/icons                  MIT         outline, filled
        fontawesome  @fortawesome/fontawesome-free  CC-BY-4.0   solid, regular
        solar        @iconify-json/solar            CC-BY-4.0   linear, outline

    The first four ask only that their notice travel with the software, which ABOUT.txt
    below is. Font Awesome and Solar ask for a credit line wherever their drawings go,
    which is a different thing and is why the tool writes an attribution page beside the
    icons it stores: see IconLicence.RequiresNotice. Font Awesome brands - 609 logos - is
    left out, for the reason every logo set is: a table icon is a noun, not a company.

    Solar is the one set that does not arrive as SVG files. Its author publishes it as a
    Figma community file; what npm carries is Iconify's rendition of that, one JSON holding
    every icon as a fragment, so this script writes the SVGs back out. That makes the
    bundled drawings a third party's rendering of the author's work rather than the
    author's own files, which CC BY asks be said, and ABOUT.txt says it.

    Font Awesome is also the one set that is not drawn square. Its icons are 512 tall and
    anywhere from 320 to 640 wide, and they are bundled at the width they were drawn:
    the renderer fits and centres a viewBox rather than stretching it, and padding one to
    square would be changing the drawing to make it look like the other four.

    The archive layout is <family>/<weight>/<name>.svg plus one ABOUT.txt, one vocabulary
    of the words an icon answers to besides its own name, and the drawing index that
    index-catalog.ps1 writes after this script has run. Nothing else.
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
    [string]$TablerVersion = "latest",
    [string]$FontAwesomeVersion = "latest",
    [string]$SolarVersion = "latest",
    [string]$FluentMetadataRef = "main"
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
    "fontawesome/solid"   = 1422
    "fontawesome/regular" = 169
    "solar/linear"        = 1266
    "solar/outline"       = 136
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

function Get-FluentMetadata {
    <#
        Clones just the per-icon metadata out of Microsoft's icon repository, and says which
        commit it got.

        This is the one source here that is not an npm package, and not for want of looking.
        @fluentui/svg-icons carries 21,682 SVGs and no JSON at all - two separate exclusions in
        its own package.json see to that - so the words Microsoft writes about its icons cannot
        be fetched the way everything else in this script is fetched. Issue #788 on the
        repository asks for them to be published as a package; it has been open since December
        2024. Until it is answered, the repository is the distribution.

        A sparse clone rather than a full one: the whole repository is most of a gigabyte of
        SVGs and PDFs, and what is wanted is 2,654 small JSON files. --filter=blob:none fetches
        no file contents until they are asked for, and the sparse pattern is what asks.
    #>
    param(
        [Parameter(Mandatory)][string]$Ref,
        [Parameter(Mandatory)][string]$Root
    )

    $dir = Join-Path $Root "fluent-metadata"

    & git clone --depth 1 --filter=blob:none --sparse --branch $Ref `
        "https://github.com/microsoft/fluentui-system-icons.git" $dir 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not clone the Fluent icon repository (git exit $LASTEXITCODE)" }

    Push-Location $dir
    try {
        & git sparse-checkout set --no-cone '/assets/*/metadata.json' 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not narrow the Fluent clone to its metadata (git exit $LASTEXITCODE)" }
        $commit = (& git rev-parse HEAD).Trim()
    }
    finally { Pop-Location }

    $assets = Join-Path $dir "assets"
    if (-not (Test-Path $assets)) { throw "The Fluent clone has no assets\ directory" }

    $count = @(Get-ChildItem $assets -Directory).Count
    Write-Host "  microsoft/fluentui-system-icons $($commit.Substring(0, 7)) ($count icons with metadata)"

    return [pscustomobject]@{ Path = $dir; Assets = $assets; Commit = $commit }
}

function Get-PackageFacts {
    <#
        Version, licence and copyright line for one extracted package, read from the package
        itself. The copyright comes out of the shipped LICENSE rather than from memory, so the
        notice we redistribute says what the package says it should, and follows the package
        when it changes hands. -Fallback covers a package that ships no LICENSE at all.

        -License and -FirstCopyrightOnly cover a package that ships more than what is taken
        from it. Font Awesome Free is one package holding icons, fonts and code under three
        different licences, so its package.json says "(CC-BY-4.0 AND OFL-1.1 AND MIT)" and
        its LICENSE.txt concatenates all three - four lines of which begin with Copyright,
        two of them the OFL's own boilerplate about what a Copyright Holder may forbid.
        Only the SVGs are taken, so only the SVGs' licence and the first holder line are
        what the notice should carry. Stating three licences for a bundle of drawings under
        one would be less true, not more.
    #>
    param(
        [Parameter(Mandatory)][string]$Dir,
        [string]$Fallback,
        [string]$License,
        [switch]$FirstCopyrightOnly
    )

    $pkg = Get-Content (Join-Path $Dir "package.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $license = if ($License) { $License }
               elseif ($pkg.license -is [string]) { $pkg.license }
               else { [string]$pkg.license.type }

    $copyright = $null
    foreach ($name in @("LICENSE", "LICENSE.md", "LICENSE.txt", "license", "license.md")) {
        $path = Join-Path $Dir $name
        if (Test-Path $path) {
            # Every line, not the first: Lucide forked Feather and its LICENSE names both
            # holders, so taking one would ship half the attribution. -cmatch rather than
            # -match because PowerShell compares case-insensitively by default and the MIT
            # boilerplate line "copyright notice and this permission notice..." would match.
            $lines = @(Get-Content $path -Encoding UTF8 |
                Where-Object { $_ -cmatch '^\s*Copyright' } |
                ForEach-Object { $_.Trim() })
            if ($FirstCopyrightOnly) { $lines = @($lines | Select-Object -First 1) }
            $copyright = $lines -join "`n"
            break
        }
    }
    if (-not $copyright) {
        if (-not $Fallback) { throw "$Dir ships no LICENSE and no -Fallback copyright was given" }
        $copyright = $Fallback
    }

    [pscustomobject]@{ Version = $pkg.version; License = $license; Copyright = $copyright }
}

function Get-InkOverlap {
    <#
        How much of two drawings is the same drawing, as a fraction between 0 and 1: the ink
        they share over the ink either of them has. 1 is the same picture, 0 is no overlap at
        all.

        Rendered rather than compared as text, because two styles of one icon never have the
        same path data - one states a stroke and the other fills the outline that stroke would
        have drawn - and a picture is what a duplicate in the grid would be. Drawn through the
        tool's own renderer for the same reason: it is not a browser, and what it draws is what
        somebody would be choosing between.

        32 pixels is where this is measured. Smaller and a stroke and its filled twin blur
        together whatever they really are; larger costs time for a decision that does not move.
    #>
    param(
        [Parameter(Mandatory)][string]$First,
        [Parameter(Mandatory)][string]$Second
    )

    $a = Get-InkMask $First
    $b = Get-InkMask $Second

    $both = 0
    $either = 0
    for ($i = 0; $i -lt $a.Length; $i++) {
        if ($a[$i] -and $b[$i]) { $both++ }
        if ($a[$i] -or  $b[$i]) { $either++ }
    }

    if ($either -eq 0) { return 1.0 }
    return $both / $either
}

function Get-InkMask {
    <#
        Which pixels of a 32x32 rendering of this SVG were painted at all. The colour is
        whatever was asked for, so alpha is the only channel that says anything.
    #>
    param([Parameter(Mandatory)][string]$Svg)

    $size = 32
    $image = [Icons.Logic.SvgImage]::Parse($Svg)
    $bitmap = $image.Render($size, [System.Drawing.Color]::Black)
    try {
        # One read of the whole bitmap. GetPixel over thousands of icons is minutes of this
        # script; a single LockBits is seconds of it.
        $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
        $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                                 [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes = New-Object byte[] ($data.Stride * $size)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        }
        finally { $bitmap.UnlockBits($data) }
    }
    finally { $bitmap.Dispose() }

    $mask = New-Object bool[] ($size * $size)
    for ($i = 0; $i -lt $mask.Length; $i++) { $mask[$i] = $bytes[$i * 4 + 3] -gt 32 }
    return , $mask
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

    $fontAwesomeRoot = Get-NpmPackage -Name "@fortawesome/fontawesome-free" -VersionOrTag $FontAwesomeVersion -Root $temp
    $fontAwesome = Get-PackageFacts $fontAwesomeRoot -License "CC-BY-4.0" -FirstCopyrightOnly

    # Iconify's rendition rather than the author's own files, and it ships no LICENSE of its
    # own - the terms it states are the author's, in info.json and in package.json.
    $solarRoot = Get-NpmPackage -Name "@iconify-json/solar" -VersionOrTag $SolarVersion -Root $temp
    $solarInfo = Get-Content (Join-Path $solarRoot "info.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $solar = Get-PackageFacts $solarRoot -Fallback "Copyright (c) $($solarInfo.author.name)."

    $fluentMeta = Get-FluentMetadata -Ref $FluentMetadataRef -Root $temp

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

    # --- Font Awesome ---------------------------------------------------------------------
    # Brands is the third weight in the package and is not taken: 609 company logos, which is
    # not what a table icon is for, and every one of them a trademark the licence does not
    # cover. The two that are taken need no renaming and no stripping - a Font Awesome file is
    # one path and the comment that credits it, and that comment is kept on purpose.
    #
    # What they do need is choosing from, the way Fluent does. Font Awesome keeps every name
    # it has ever used: address-card is also shipped as contact-card and as vcard, three files
    # holding one drawing. Left in, the grid offers the same picture three times and - because
    # the tool recognises a stored icon by what it draws - a drawing under more than one name
    # can be recognised as none of them. The package says which name is the real one, so that
    # is what is asked.
    #
    # It also keeps the names it dropped. An alias is a name Font Awesome published for this
    # drawing and later stopped leading with, and it is still what a good many people call the
    # thing - the set answers to vcard, and after the file goes the bundle does not. So the
    # names travel on without the files, in the vocabulary the archive carries beside its
    # icons, and search treats one as worth exactly what the real name is worth.
    $families = Get-Content (Join-Path $fontAwesomeRoot "metadata\icon-families.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $aliases = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $aliasesOf = @{}
    foreach ($icon in $families.PSObject.Properties) {
        # Asked in two steps rather than one. @($icon.Value.aliases.names) on an icon that has
        # no aliases node is not an empty array - it is an array holding one $null, which
        # writes a line with an empty word on it for every icon in the set.
        if (-not $icon.Value.aliases) { continue }
        $names = @($icon.Value.aliases.names | Where-Object { $_ })
        if ($names.Count -eq 0) { continue }
        foreach ($alias in $names) { [void]$aliases.Add($alias) }
        $aliasesOf[$icon.Name] = $names
    }
    if ($aliases.Count -eq 0) { throw "No aliases found in the Font Awesome metadata - has its shape changed?" }

    $aliasHits = 0
    foreach ($weight in @("solid", "regular")) {
        $dir = Join-Path $fontAwesomeRoot "svgs\$weight"
        if (-not (Test-Path $dir)) { throw "No svgs\$weight under $fontAwesomeRoot" }

        foreach ($file in [System.IO.Directory]::EnumerateFiles($dir, "*.svg")) {
            $name = [System.IO.Path]::GetFileNameWithoutExtension($file)
            if ($aliases.Contains($name)) { $aliasHits++; continue }
            Add-Icon -Entry "fontawesome/$weight/$name.svg" -Path $file
        }
    }

    # --- Solar --------------------------------------------------------------------------
    # Two of its six styles. Bold and the two duotones are left out the way Phosphor's
    # duotone is - beside the rest of the library they read as a different tool - and broken,
    # whose gapped strokes are a signature rather than a weight.
    #
    # Linear and outline are the two kept, and they are very nearly the same set: 1,146 of the
    # 1,273 drawings that exist in both share more than 95% of their ink once drawn. One is
    # stroked and the other is the same outline filled, so the path data never matches and
    # nothing downstream would catch it - the grid would simply offer the same picture twice.
    # So linear is taken whole and outline only where it draws something else. The test is a
    # rendering rather than a comparison of the files, because a picture is what the duplicate
    # would be.
    $dll = Join-Path $repo "Icons\bin\Debug\net48\Comentality.Icons.dll"
    if (-not (Test-Path $dll)) { throw "Build the tool first: dotnet build Icons\Icons.csproj" }
    Add-Type -Path $dll
    Add-Type -AssemblyName System.Drawing

    $solarJson = Get-Content (Join-Path $solarRoot "icons.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $solarBodies = @{}
    foreach ($icon in $solarJson.icons.PSObject.Properties) { $solarBodies[$icon.Name] = $icon.Value }

    # Its own aliases - 4k-linear is car-battery-linear under another name - are names rather
    # than drawings, and are left out for the reason Font Awesome's are.
    $solarAliases = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if ($solarJson.aliases) {
        foreach ($alias in $solarJson.aliases.PSObject.Properties) { [void]$solarAliases.Add($alias.Name) }
    }

    $solarDir = Join-Path $temp "solar-svg"
    New-Item -ItemType Directory -Path $solarDir | Out-Null

    $solarUndrawable = 0
    $solarSame = 0
    $solarLinear = @{}

    foreach ($style in @("linear", "outline")) {
        foreach ($name in $solarBodies.Keys) {
            if (-not $name.EndsWith("-$style")) { continue }
            if ($solarAliases.Contains($name)) { continue }

            $body = $solarBodies[$name].body

            $stem = $name.Substring(0, $name.Length - ($style.Length + 1))
            $w = if ($solarBodies[$name].width)  { $solarBodies[$name].width }  else { $solarJson.width }
            $h = if ($solarBodies[$name].height) { $solarBodies[$name].height } else { $solarJson.height }
            $svg = "<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 $w $h"">$body</svg>"

            # Only what this tool can actually draw, asked of the thing that will draw it
            # rather than guessed at from the text. The other five sets are their authors
            # own SVGs and have always parsed; this one is somebody else rendering somebody
            # else's Figma file, and a few of them use a mask, or a transform, or are a
            # zero-length line meant to come out as a dot under a round cap - which a browser
            # draws and this renderer does not. Bundled, each would be a blank cell in the
            # grid and a blank icon on a table.
            $drawing = [Icons.Logic.SvgImage]::Parse($svg)
            if ($drawing.Unsupported.Count -gt 0) {
                $solarUndrawable++
                if ($notes.Count -lt 200) { $notes.Add("solar/$style/$stem.svg: $($drawing.Unsupported[0])") }
                continue
            }

            $painted = 0
            foreach ($lit in (Get-InkMask $svg)) { if ($lit) { $painted++ } }
            if ($painted -lt 8) {
                $solarUndrawable++
                if ($notes.Count -lt 200) { $notes.Add("solar/$style/$stem.svg: draws nothing") }
                continue
            }

            if ($style -eq "outline" -and $solarLinear.ContainsKey($stem)) {
                if ((Get-InkOverlap -First $solarLinear[$stem] -Second $svg) -ge 0.95) { $solarSame++; continue }
            }

            $file = Join-Path $solarDir "$style-$stem.svg"
            [System.IO.File]::WriteAllText($file, $svg, (New-Object System.Text.UTF8Encoding $false))
            Add-Icon -Entry "solar/$style/$stem.svg" -Path $file
            if ($style -eq "linear") { $solarLinear[$stem] = $svg }
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

        # --- Vocabulary -------------------------------------------------------------------
        # The words an icon answers to besides its own name, one line per icon: a kind, a
        # family and name, then the words. Keyed by name rather than by entry path because
        # the words belong to the drawing, not to the weight it happens to be drawn in - so
        # the three weights Phosphor draws one icon in would share one line rather than hold
        # three copies of it.
        #
        # Only Font Awesome has any today, and only its aliases: the names it ships as
        # separate files for one drawing, which this script drops and which would otherwise
        # be lost with them. A name naming an icon that did not make it into the archive is
        # not written - an alias the bundle cannot show is dead weight that reads like
        # coverage, and the reader would skip it anyway. The entry is named by the reader
        # rather than spelled again here, the way index-catalog.ps1 names the drawing index.
        $bundled = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($record in $records) {
            $parts = $record.Entry.Split('/')
            [void]$bundled.Add($parts[0] + "/" + [System.IO.Path]::GetFileNameWithoutExtension($parts[2]))
        }

        $vocabulary = New-Object System.Text.StringBuilder
        $vocabularyWords = 0
        $vocabularyIcons = 0
        $vocabularyDropped = 0
        foreach ($name in $aliasesOf.Keys | Sort-Object) {
            $key = "fontawesome/$name"
            if (-not $bundled.Contains($key)) { $vocabularyDropped++; continue }

            [void]$vocabulary.Append("alias`t").Append($key)
            foreach ($alias in $aliasesOf[$name]) { [void]$vocabulary.Append("`t").Append($alias); $vocabularyWords++ }
            [void]$vocabulary.Append("`n")
            $vocabularyIcons++
        }

        # Fluent's are the other kind, and the larger one: what Microsoft says each icon is
        # about rather than what it is called. A shield is security, protection and safety. The
        # words live one metadata.json per icon in the repository - see Get-FluentMetadata for
        # why they are cloned rather than downloaded - under the same MIT licence as the
        # drawings, so they bring no obligation the bundle does not already carry.
        #
        # The folder is named as a person would write it and the icon is not, so "Access Time"
        # has to be matched up with access_time. Repeated separators are collapsed, because one
        # folder is called "Multiplier _5x"; and where the name matches nothing, the left- and
        # right-to-left pair is tried, because Microsoft keeps one metadata file for Task List
        # while the package ships task_list_ltr and task_list_rtl. Both get the words: they are
        # the same icon, drawn twice for the two directions a language runs in.
        #
        # A word already in the icon's own name is dropped. It cannot add anything - the name
        # scores higher than any description - and every word kept is another comparison on
        # every keystroke.
        $fluentWords = 0
        $fluentIcons = 0
        $fluentUnmapped = 0
        $fluentSilent = 0

        foreach ($dir in Get-ChildItem $fluentMeta.Assets -Directory | Sort-Object Name) {
            $file = Join-Path $dir.FullName "metadata.json"
            if (-not (Test-Path $file)) { continue }

            $meta = Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json
            $metaphors = @($meta.metaphor | Where-Object { $_ })
            if ($metaphors.Count -eq 0) { $fluentSilent++; continue }

            $stem = ($dir.Name.ToLowerInvariant() -replace ' ', '_') -replace '_+', '_'
            $names = @(@($stem, ($stem + "_ltr"), ($stem + "_rtl")) | Where-Object { $bundled.Contains("fluent/$_") })
            if ($names.Count -eq 0) { $fluentUnmapped++; continue }

            foreach ($name in $names) {
                $own = [System.Collections.Generic.HashSet[string]]::new(
                    [string[]]($name -split '_'), [StringComparer]::OrdinalIgnoreCase)
                $kept = [System.Collections.Generic.List[string]]::new()

                foreach ($phrase in $metaphors) {
                    foreach ($word in ([string]$phrase).ToLowerInvariant() -split '[\s_\-/]+') {
                        if (-not $word) { continue }
                        if ($own.Contains($word)) { continue }
                        if ($kept.Contains($word)) { continue }
                        [void]$kept.Add($word)
                    }
                }

                if ($kept.Count -eq 0) { continue }

                [void]$vocabulary.Append("tag`t").Append("fluent/$name")
                foreach ($word in $kept) { [void]$vocabulary.Append("`t").Append($word); $fluentWords++ }
                [void]$vocabulary.Append("`n")
                $fluentIcons++
            }
        }

        $entry = $zip.CreateEntry([Icons.Catalog.IconCatalog]::VocabularyEntryName, [System.IO.Compression.CompressionLevel]::Optimal)
        $writer = New-Object System.IO.StreamWriter($entry.Open(), (New-Object System.Text.UTF8Encoding($false)))
        try { $writer.Write($vocabulary.ToString()) } finally { $writer.Dispose() }

        # Provenance travels with the icons. The tool shows this in its About box, and a bundle
        # that cannot say where it came from or under what terms is a liability. Each block names
        # the package actually resolved at build time and what this script changed about it.
        $fallbacks = $sizePreference[1..($sizePreference.Count - 1)] -join ', '
        $about = @"
Comentality Icons bundles six icon sets, each redistributed under its own licence
and unmodified except as noted below.

Four of them ask only that this notice travel with the software, which is what this
file is. Font Awesome and Solar ask for a credit line wherever their drawings go, so
the tool writes one into every environment it stores one of their icons in, beside the
icon itself: see ATTRIBUTION.html in the folder the icons were written to.

Microsoft Fluent UI System Icons
@fluentui/svg-icons $($fluent.Version)
https://github.com/microsoft/fluentui-system-icons
Licensed under the $($fluent.License) License. $($fluent.Copyright)

Repackaged for Comentality Icons by tools\build-catalog.ps1: one SVG per icon and
style, at $($sizePreference[0]) pixels where Fluent draws one, otherwise the first available
of $fallbacks, contents otherwise unmodified.

The words this tool lets you search Fluent by come from the same project rather than
from the package: @fluentui/svg-icons ships no metadata of any kind, so the per-icon
metadata.json files in the repository are read at commit $($fluentMeta.Commit) for
what Microsoft says each icon is about. Same project, same licence, and no drawing
is taken from it.

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

Font Awesome Free
@fortawesome/fontawesome-free $($fontAwesome.Version)
https://fontawesome.com
Licensed under $($fontAwesome.License). $($fontAwesome.Copyright)

The package holds icons, fonts and code under three licences; only the icons are
taken, and CC-BY-4.0 is theirs. That licence asks for attribution and for changes
to be indicated, and both are given per icon on the attribution page the tool
writes beside the icons it stores.

Repackaged for Comentality Icons by tools\build-catalog.ps1: the solid and regular
weights only - brands, being logos, is left out - and one file per drawing, dropping
the names Font Awesome keeps as aliases of another icon. Contents otherwise
unmodified, including the licence comment each file carries and the width each icon
was drawn at, which for this set is not always its height.

Solar
@iconify-json/solar $($solar.Version)
$($solarInfo.author.url)
Licensed under $($solar.License). $($solar.Copyright)

Drawn by $($solarInfo.author.name) and published as a Figma community file. What is
bundled here is Iconify's rendition of that file rather than the author's own export:
@iconify-json/solar carries every icon as an SVG fragment in one JSON, and this script
writes those fragments back out as SVGs.

Repackaged for Comentality Icons by tools\build-catalog.ps1: the linear and outline
styles only, of the six the set draws. Outline is kept only where it draws something
its linear does not - the two are the same outline stroked and filled, and most of the
set renders identically either way - and icons this tool cannot draw are left out rather
than bundled blank. Contents otherwise unmodified.
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
        $furniture = @("ABOUT.txt", [Icons.Catalog.IconCatalog]::VocabularyEntryName) + @($check.Entries | Where-Object { $_.FullName -like "drawings.v*.tsv" } | ForEach-Object { $_.FullName })
        $svgs = $check.Entries | Where-Object { $furniture -notcontains $_.FullName }
        $stray = $svgs | Where-Object { $_.FullName -notmatch '^(fluent|phosphor|lucide|tabler|fontawesome|solar)/[a-z]+/[^/]+\.svg$' }
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
    Write-Host "Left out $aliasHits fontawesome files that are another icon under an older name"
    Write-Host "Kept $vocabularyWords of those names as search words, against $vocabularyIcons icons"
    Write-Host "Took $fluentWords words Microsoft writes about its own icons, against $fluentIcons of them"
    if ($fluentUnmapped -gt 0) { Write-Host "Left out the words of $fluentUnmapped Fluent icons this bundle does not carry" }
    if ($vocabularyDropped -gt 0) { Write-Host "Left out the names of $vocabularyDropped icons this bundle does not carry" }
    Write-Host "Left out $solarSame solar outline icons that draw what their linear already draws"
    Write-Host "Left out $solarUndrawable solar icons this renderer cannot draw"
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
