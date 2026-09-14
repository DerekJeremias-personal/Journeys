param(
    [string[]]$Files,
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$verify = Join-Path $here "agent-verify.ps1"
if (-not (Test-Path $verify)) { throw "missing $verify" }

if ($Files -and $Files.Count -gt 0) {
    if ($RunTests) {
        & $verify -Files $Files -RunTests
    } else {
        & $verify -Files $Files
    }
} else {
    if ($RunTests) {
        & $verify -RunTests
    } else {
        & $verify
    }
}
exit $LASTEXITCODE
