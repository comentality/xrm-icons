<#
.SYNOPSIS
    Cuts the Comentality mark down to the size the About button wants it.

.DESCRIPTION
    The org logo is decided in the brand repository - see its README, "The org logo": `many`,
    approved 2026-08-17, and shipped as `org.*` so nothing picks the wrong file out of a
    directory of ten near-identical marks. This copies it in at 16px, which is the size a
    ToolStrip draws a button image at and small enough that the flag's silhouette is all that
    survives; letting the toolbar shrink a 32px tile instead comes out visibly soft.

    Unlike make-tool-icon.ps1 this does not go through the tool's own renderer: the org mark is
    a gradient, and that renderer draws an outline in one colour.

.EXAMPLE
    .\tools\make-house-mark.ps1
    .\tools\make-house-mark.ps1 -Brand D:\src\comentality-brand
#>
[CmdletBinding()]
param(
    [string]$Brand = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "comentality-brand"),
    [int]$Size = 16
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
# The 1024 rather than the 32: one high-quality reduction beats a reduction of a reduction.
$source = Join-Path $Brand "logo\org-1024.png"
if (-not (Test-Path $source)) {
    throw "No org logo at $source. Point -Brand at a clone of github.com/comentality/comentality-brand."
}

$target = Join-Path $repo "Icons\About\comentality-$Size.png"

Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile($source)
try {
    $canvas = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)))
    }
    finally { $g.Dispose() }

    $canvas.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
}
finally { $src.Dispose() }

Write-Host "Wrote $target from $source"
