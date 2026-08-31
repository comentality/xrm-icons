<#
.SYNOPSIS
    Draws this tool's own icon, using this tool's own renderer.

.DESCRIPTION
    XrmToolBox wants a 32px icon for the list and an 80px one for the tile, both as base64 in
    the plugin's export metadata, plus a PNG for the NuGet package. All three are one Fluent
    icon in the house colours - so rather than keep a hand-made PNG in the repo and hope it
    stays in step, they are generated here through Icons.dll. If the renderer breaks, the
    tool's own icon is the first thing to look wrong.

.EXAMPLE
    .\tools\make-tool-icon.ps1
    .\tools\make-tool-icon.ps1 -Icon color_fill -Style filled
#>
[CmdletBinding()]
param(
    [string]$Icon = "color_fill",
    [ValidateSet("regular", "filled")]
    [string]$Style = "filled",
    [string]$Foreground = "#222512",
    [string]$Background = "#F2F3EC"
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $repo "Icons\bin\Debug\net48\Icons.dll"
if (-not (Test-Path $dll)) { throw "Build the tool first: dotnet build Icons\Icons.csproj" }

Add-Type -AssemblyName System.Drawing
Add-Type -Path $dll

$catalog = [Icons.Catalog.IconCatalog]::Load()
try {
    $style = if ($Style -eq "filled") { [Icons.Catalog.IconStyle]::Filled } else { [Icons.Catalog.IconStyle]::Regular }
    $entry = $catalog.Icons | Where-Object { $_.Name -eq $Icon -and $_.Style -eq $style } | Select-Object -First 1
    if (-not $entry) { throw "No icon called $Icon in the $Style weight" }

    $image = [Icons.Logic.SvgImage]::Parse($catalog.ReadSvg($entry))
    $ink = [System.Drawing.ColorTranslator]::FromHtml($Foreground)
    $paper = [System.Drawing.ColorTranslator]::FromHtml($Background)

    function Save-Icon([int]$size, [string]$path) {
        # The glyph sits in about three quarters of the tile, which is the proportion the rest
        # of the XrmToolBox tool list is drawn at.
        $inset = [int]($size * 0.16)
        $canvas = New-Object System.Drawing.Bitmap($size, $size)
        $g = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $g.Clear($paper)
            $area = New-Object System.Drawing.RectangleF($inset, $inset, ($size - 2 * $inset), ($size - 2 * $inset))
            $image.Draw($g, $area, $ink)
        }
        finally { $g.Dispose() }

        $canvas.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $canvas.Dispose()
        return [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($path))
    }

    $iconPng = Join-Path $repo "Icons\icon.png"
    $small = Join-Path $env:TEMP "icons-small.png"
    $big = Join-Path $env:TEMP "icons-big.png"

    Save-Icon 128 $iconPng | Out-Null
    $smallB64 = Save-Icon 32 $small
    $bigB64 = Save-Icon 80 $big

    Write-Host "Wrote $iconPng"
    Write-Host ""
    Write-Host "SmallImageBase64:"
    Write-Host $smallB64
    Write-Host ""
    Write-Host "BigImageBase64:"
    Write-Host $bigB64

    Set-Content -Path (Join-Path $env:TEMP "icons-base64.txt") -Value "$smallB64`n---`n$bigB64"
    Write-Host ""
    Write-Host "Also written to $env:TEMP\icons-base64.txt"
}
finally { $catalog.Dispose() }
