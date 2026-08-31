# Opens the real control in a bare window, drives a few gestures and photographs it.
#
#   .\tests\ui.ps1
#
# No connection is involved: the icon library is inside the assembly, so the whole of the
# picker has to work before an environment is ever chosen. Shots land in tests\.ui.

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "tests\Icons.UiHarness\Icons.UiHarness.csproj"
$shots = Join-Path $repo "tests\.ui"

dotnet build $project -c Debug -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (Test-Path $shots) { Remove-Item $shots -Recurse -Force }

& (Join-Path $repo "tests\Icons.UiHarness\bin\Debug\net48\Icons.UiHarness.exe") $shots
exit $LASTEXITCODE
