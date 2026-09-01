<#
.SYNOPSIS
    Rebuilds Icons\Catalog\fluent-icons.zip from Microsoft's Fluent UI System Icons.

.DESCRIPTION
    The tool ships its icon library inside the DLL, so it works with no network and no API key.
    This script is what produces that library. Run it to pick up a newer icon release; the
    result is committed, so a normal build does not need npm, node or an internet connection.

    Icons come from the @fluentui/svg-icons npm package (MIT). That package holds every icon at
    every optical size — 20k files — which is more than a table icon needs: one SVG scales. So
    for each (name, style) we keep exactly one file, preferring the size Fluent draws most
    carefully and falling back through the others when an icon is not drawn at that size.

.PARAMETER Source
    An already-extracted @fluentui/svg-icons package directory (the one containing icons\).
    Without it the script downloads the version named by -Version.

.PARAMETER Version
    Version of @fluentui/svg-icons to download. "latest" resolves through the npm registry.

.EXAMPLE
    .\tools\build-catalog.ps1
    .\tools\build-catalog.ps1 -Source C:\tmp\svg-icons
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo "Icons\Catalog\fluent-icons.zip"

# Fluent draws each icon separately per optical size. 24 is the size the set is most complete
# at and the one the design system treats as canonical, so it leads; the rest are here only so
# that an icon drawn at 20 or 16 only is still offered rather than silently dropped.
$sizePreference = @(24, 20, 32, 28, 16, 48)

$temp = $null
try {
    if (-not $Source) {
        $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("fluent-icons-" + [Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $temp | Out-Null

        if ($Version -eq "latest") {
            Write-Host "Resolving latest @fluentui/svg-icons..."
            $Version = (Invoke-RestMethod "https://registry.npmjs.org/@fluentui/svg-icons/latest").version
        }
        $url = "https://registry.npmjs.org/@fluentui/svg-icons/-/svg-icons-$Version.tgz"
        $tgz = Join-Path $temp "package.tgz"

        Write-Host "Downloading $url (about 25 MB)..."
        Invoke-WebRequest -Uri $url -OutFile $tgz -UseBasicParsing

        Write-Host "Extracting..."
        & tar -xzf $tgz -C $temp
        if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }
        $Source = Join-Path $temp "package"
    }

    $iconDir = Join-Path $Source "icons"
    if (-not (Test-Path $iconDir)) { throw "No icons\ directory under $Source" }

    $pkg = Get-Content (Join-Path $Source "package.json") -Raw | ConvertFrom-Json
    Write-Host "Reading @fluentui/svg-icons $($pkg.version) from $iconDir"

    # Pick one file per (name, style).
    $best = @{}
    foreach ($file in [System.IO.Directory]::EnumerateFiles($iconDir, "*.svg")) {
        $leaf = [System.IO.Path]::GetFileNameWithoutExtension($file)
        if ($leaf -notmatch '^(?<name>.+)_(?<size>\d+)_(?<style>regular|filled)$') { continue }

        $key = "$($Matches.name)|$($Matches.style)"
        $rank = $sizePreference.IndexOf([int]$Matches.size)
        if ($rank -lt 0) { continue }

        if (-not $best.ContainsKey($key) -or $rank -lt $best[$key].Rank) {
            $best[$key] = [pscustomobject]@{ Path = $file; Rank = $rank; Size = [int]$Matches.size }
        }
    }
    Write-Host "Selected $($best.Count) icons"

    if (Test-Path $out) { Remove-Item $out }
    $zip = [System.IO.Compression.ZipFile]::Open($out, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($key in ($best.Keys | Sort-Object)) {
            $parts = $key -split '\|'
            $entry = $zip.CreateEntry("$($parts[1])/$($parts[0]).svg", [System.IO.Compression.CompressionLevel]::Optimal)
            $writer = New-Object System.IO.StreamWriter($entry.Open())
            try { $writer.Write([System.IO.File]::ReadAllText($best[$key].Path)) } finally { $writer.Dispose() }
        }

        # Provenance travels with the icons. The tool shows this in its About box, and a
        # bundle that cannot say where it came from or under what terms is a liability.
        $about = @"
Microsoft Fluent UI System Icons
@fluentui/svg-icons $($pkg.version)
https://github.com/microsoft/fluentui-system-icons
Licensed under the MIT License. Copyright (c) Microsoft Corporation.

Repackaged for Comentality Icons by tools\build-catalog.ps1: one SVG per icon and
style, at $($sizePreference[0]) pixels where Fluent draws one, otherwise the first
available of $($sizePreference[1..($sizePreference.Count - 1)] -join ', '), contents
otherwise unmodified.
"@
        $entry = $zip.CreateEntry("ABOUT.txt", [System.IO.Compression.CompressionLevel]::Optimal)
        $writer = New-Object System.IO.StreamWriter($entry.Open())
        try { $writer.Write($about) } finally { $writer.Dispose() }
    }
    finally { $zip.Dispose() }

    $kb = [math]::Round((Get-Item $out).Length / 1KB)
    Write-Host "Wrote $out ($kb KB)"
}
finally {
    if ($temp -and (Test-Path $temp)) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
}
