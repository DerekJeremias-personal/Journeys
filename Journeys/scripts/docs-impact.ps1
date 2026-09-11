param(
    [string[]]$Files
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "JourneysImpact.ps1")

$root = Get-JourneysRoot
$entries = Read-JourneysYamlEntries -Path (Join-Path $PSScriptRoot "path-docs-map.yaml")
$changed = @(Get-JourneysChangedFiles -Root $root -Files $Files)
$codeChanges = @($changed | Where-Object { $_ -notmatch '^docs/' })

if ($codeChanges.Count -eq 0) {
    Write-Host "docs-impact: no code paths in change set; OK"
    exit 0
}

$missing = New-Object System.Collections.Generic.List[string]
foreach ($file in $codeChanges) {
    $entry = Find-JourneysBestEntry -RelPath $file -Entries $entries
    if ($null -eq $entry) { continue }
    foreach ($doc in $entry.docs) {
        $docRel = $doc -replace '\\', '/'
        $inSet = $changed | Where-Object { $_ -eq $docRel }
        if (-not $inSet) {
            $missing.Add("${file} -> ${docRel}") | Out-Null
        }
    }
}

if ($missing.Count -gt 0) {
    Write-Host 'docs-impact: FAIL - required docs not in the change set:'
    $missing | Select-Object -Unique | ForEach-Object { Write-Host "  $_" }
    exit 1
}

$n = $codeChanges.Count
Write-Host "docs-impact: OK ($n files)"
exit 0
