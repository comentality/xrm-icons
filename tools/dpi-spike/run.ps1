<#
.SYNOPSIS
    Builds and runs the mixed-DPI spike.

.DESCRIPTION
    Answers one question: can a per-monitor-v2 process keep a child window DPI-unaware, in the
    shape XrmToolBox has - an MDI container with one form per tool? If it can, XrmToolBox can be
    made to scale without every tool in it being fixed on the same day, which is what
    MscrmTools/XrmToolBox#1260 foundered on. If it cannot, it cannot.

    Compiled with the .NET Framework compiler rather than the SDK, because the manifest is the
    point and it has to be the one Windows reads at process start. Nothing is installed and
    nothing is left behind outside the output folder.

        .\run.ps1                     # opens the windows; look at them on a 150% monitor
        .\run.ps1 -Headless           # prints what each window was created as, and exits
        .\run.ps1 -Headless -Default  # the same without asking for mixed hosting, as a control

    Headless works on any machine, including this one at 96 dpi: what a window was created as is
    a fact about the window, not about the screen. The rest needs a monitor at 150%.

.PARAMETER Headless
    Report and exit rather than leaving the windows open.

.PARAMETER Default
    Leave the thread's hosting behaviour alone instead of asking for mixed. The control: if the
    children come out unaware without this call too, then the call is not what did it.
#>

param(
    [switch]$Headless,
    [switch]$Default
)

$ErrorActionPreference = "Stop"

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "No .NET Framework compiler at $csc" }

$out = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$exe = Join-Path $out "DpiSpike.exe"

& $csc -nologo `
    -target:exe `
    -win32manifest:(Join-Path $PSScriptRoot "DpiSpike.manifest") `
    -r:System.Windows.Forms.dll `
    -r:System.Drawing.dll `
    -out:$exe `
    (Join-Path $PSScriptRoot "DpiSpike.cs")

if ($LASTEXITCODE -ne 0) { throw "The spike did not compile." }

$switches = @()
if ($Headless) { $switches += "-headless" }
if ($Default) { $switches += "-default-hosting" }

& $exe @switches
exit $LASTEXITCODE
