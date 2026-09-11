param(
    [string[]]$Files,
    [switch]$SkipImpact,
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "JourneysImpact.ps1")

$root = Get-JourneysRoot
Set-Location $root

if (-not $SkipImpact) {
    & (Join-Path $PSScriptRoot "docs-impact.ps1") -Files $Files
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & (Join-Path $PSScriptRoot "graph-impact.ps1") -Files $Files
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$sln = Join-Path $root "Journeys.sln"
dotnet build $sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$changed = @()
if ($Files) {
    $changed = @(Get-JourneysChangedFiles -Root $root -Files $Files)
}
$needTests = $RunTests -or ($changed | Where-Object { $_ -like "Journeys.Tests/*" })
if ($needTests) {
    $testProj = Join-Path $root "Journeys.Tests\Journeys.Tests.csproj"
    dotnet test $testProj --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "agent-verify: OK"
exit 0
