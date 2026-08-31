# Copies the last Debug build into an XrmToolBox Plugins folder, without rebuilding.
#
#   .\deploy.ps1
#   .\deploy.ps1 -PluginDir D:\XrmToolBox\Plugins

param(
    [string]$PluginDir
)

if (-not $PluginDir) { $PluginDir = $env:XRMTOOLBOX_PLUGINS }
if (-not $PluginDir) { $PluginDir = Join-Path $env:APPDATA "MscrmTools\XrmToolBox\Plugins" }

if (-not (Test-Path $PluginDir)) { New-Item -ItemType Directory -Path $PluginDir -Force | Out-Null }

$source = Join-Path $PSScriptRoot "Icons\bin\Debug\net48\Icons.dll"
if (-not (Test-Path $source)) { throw "No Debug build at $source - run .\build.ps1 first" }

Copy-Item $source -Destination $PluginDir -Force

Write-Host "Deployed Icons.dll to $PluginDir"
