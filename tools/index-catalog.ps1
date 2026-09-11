<#
.SYNOPSIS
    Writes the drawing index into the icon bundle, using this tool's own fingerprint.

.DESCRIPTION
    The tool recognises an icon already sitting in an environment by matching its drawing
    against every bundled one, which means having the fingerprint of all 18,637 in hand. Worked
    out at run time that is half a second and about twenty megabytes of garbage, on every
    connection and in every XrmToolBox tab. The bundle cannot change between builds, so it is
    worked out once, here, and carried inside the archive.

    The fingerprints are taken with [Icons.Catalog.IconFingerprint], the same code that reads
    them back, rather than a copy of the rules written out again in PowerShell - the way
    make-tool-icon.ps1 draws the tool's icon with the tool's own renderer. A canonical form that
    drifts between the writer and the reader would not fail: it would quietly stop recognising
    anything.

    The file is a plain dump of fingerprint and archive path, one line per icon, and holds no
    rule about what to do with them. Deciding that two icons sharing a drawing answer with
    neither is the reader's, in one place, for both the bundled index and the one it works out
    for itself when there is none.

    Nothing here reads the library through IconCatalog.Load: that would fingerprint whatever
    bundle was embedded in the DLL last time it was built, not the archive being indexed.

.EXAMPLE
    .\tools\index-catalog.ps1
    .\tools\index-catalog.ps1 -Zip C:\somewhere\icons.zip
#>
[CmdletBinding()]
param(
    [string]$Zip
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
if (-not $Zip) { $Zip = Join-Path $repo "Icons\Catalog\icons.zip" }
if (-not (Test-Path $Zip)) { throw "No icon bundle at $Zip" }

# Any build of the tool will do. Only IconFingerprint is used, and it depends on nothing but
# the SVG it is handed - not on the bundle this DLL happens to carry, which is usually the one
# being replaced.
$dll = Join-Path $repo "Icons\bin\Debug\net48\Comentality.Icons.dll"
if (-not (Test-Path $dll)) { throw "Build the tool first: dotnet build Icons\Icons.csproj" }

Add-Type -Path $dll
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$name = [Icons.Catalog.IconCatalog]::IndexEntryName

$archive = [System.IO.Compression.ZipFile]::Open($Zip, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    # Every index this build cannot read goes, not just the one being replaced: an archive
    # carrying two of them is an archive somebody will read the wrong one out of one day.
    foreach ($old in @($archive.Entries | Where-Object { $_.FullName -like "drawings.v*.tsv" })) {
        $old.Delete()
    }

    $svgs = @($archive.Entries | Where-Object { $_.FullName.EndsWith(".svg") } | Sort-Object FullName)
    if ($svgs.Count -eq 0) { throw "No icons in $Zip to index" }

    $lines = New-Object System.Text.StringBuilder
    $drawn = 0
    $blank = 0

    foreach ($entry in $svgs) {
        $reader = New-Object System.IO.StreamReader($entry.Open(), [System.Text.Encoding]::UTF8)
        try { $svg = $reader.ReadToEnd() } finally { $reader.Dispose() }

        # An SVG with nothing in it to draw gets a line with no fingerprint rather than no line.
        # One line per icon is how the reader tells a whole index from one written against some
        # earlier bundle, and that check is worth more than the bytes these cost.
        $print = [Icons.Catalog.IconFingerprint]::Of($svg)
        if ($print) { $drawn++ } else { $blank++; $print = "" }

        [void]$lines.Append($print).Append("`t").Append($entry.FullName).Append("`n")
    }

    $written = $archive.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
    $writer = New-Object System.IO.StreamWriter($written.Open(), (New-Object System.Text.UTF8Encoding($false)))
    try { $writer.Write($lines.ToString()) } finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

$shared = 0
$seen = @{}
foreach ($line in $lines.ToString().Split("`n")) {
    if (-not $line) { continue }
    $print = $line.Split("`t")[0]
    if (-not $print) { continue }
    if ($seen.ContainsKey($print)) { $shared++ } else { $seen[$print] = $true }
}

$kb = [math]::Round((Get-Item $Zip).Length / 1KB)
Write-Host "Wrote $name into $Zip ($kb KB): $drawn drawings, $blank with nothing to draw."
Write-Host "$shared of them are a drawing another name already had, so neither will be named."
