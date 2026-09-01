# Drives the tool through the whole workflow against a real Dataverse, and asks the org what
# actually got written. It creates a table of its own (new_e2eicontest), gives it an icon in a
# folder of its own so the tool has a convention to read, puts icons on it - apply, re-apply,
# the classic PNGs, a path typed by hand - verifies every write through the SDK, and deletes
# everything on the way out.
#
#   .\tests\e2e.ps1            # run it
#   .\tests\e2e.ps1 -Keep      # leave the table and web resources there to look at
#   .\tests\e2e.ps1 -FreshAuth # borrow the sandbox's sign in again from scratch
#
# The sign in is borrowed from the xtb.ps1 sandbox: run that once and sign in, and this can
# run forever after with nobody at the keyboard. The borrowed cache lives in tests\.e2e and
# refreshes itself; -FreshAuth re-copies it when it has gone stale.
#
# The environment comes from the active pac auth profile, like everything else here. Pass
# -Environment and -Username to aim somewhere else - somewhere the sandbox has signed into.

param(
    [string]$Environment,
    [string]$Username,
    [switch]$Keep,
    [switch]$FreshAuth
)

$ErrorActionPreference = "Stop"

$tests = $PSScriptRoot
$out = Join-Path $tests ".e2e"
$cache = Join-Path $out "token-cache"

if (-not $Environment -or -not $Username) {
    $who = pac org who --json | ConvertFrom-Json
    if (-not $who -or -not $who.PSObject.Properties['OrgUrl'] -or -not $who.OrgUrl) {
        throw "pac has no active organization. Run 'pac auth create' or pass -Environment and -Username."
    }
    if (-not $Environment) { $Environment = $who.OrgUrl.TrimEnd('/') }
    if (-not $Username) { $Username = $who.UserEmail }
}

# The sandbox keys its token cache by the deterministic guid of its connection name, the same
# way the XtbSandbox module derives it. That cache is the sign in this run borrows.
$md5 = [System.Security.Cryptography.MD5]::Create()
try {
    $connectionId = [guid]::new($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes("Icons E2E")))
} finally {
    $md5.Dispose()
}
$sandboxCache = Join-Path ([IO.Path]::GetTempPath()) "{$connectionId}"

New-Item -ItemType Directory -Force -Path $out | Out-Null

if ($FreshAuth -and (Test-Path $cache)) { Remove-Item $cache -Force }
if (-not (Test-Path $cache)) {
    if (-not (Test-Path $sandboxCache)) {
        throw "No sign in to borrow. Run ..\xtb.ps1 once and sign in; this run rides that session."
    }
    Copy-Item $sandboxCache $cache -Force
}

$project = Join-Path $tests "Icons.E2eHarness\Icons.E2eHarness.csproj"
dotnet build $project -v quiet --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

$exe = Join-Path $tests "Icons.E2eHarness\bin\Debug\net48\Icons.E2eHarness.exe"
$extra = @(); if ($Keep) { $extra += "--keep" }

& $exe $Environment $Username $cache $out @extra
if ($LASTEXITCODE -eq 3 -and (Test-Path $sandboxCache)) {
    # The borrowed cache went stale but the sandbox has a fresher sign in: borrow it again.
    Write-Host "Borrowing the sandbox's sign in again."
    Copy-Item $sandboxCache $cache -Force
    & $exe $Environment $Username $cache $out @extra
}

exit $LASTEXITCODE
