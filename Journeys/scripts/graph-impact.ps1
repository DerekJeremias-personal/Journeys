param(
    [string[]]$Files
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "JourneysImpact.ps1")

$root = Get-JourneysRoot
$mapPath = Join-Path $root "docs\product\graph\path-map.yaml"
$entries = Read-JourneysYamlEntries -Path $mapPath
$changed = @(Get-JourneysChangedFiles -Root $root -Files $Files)
$codeChanges = @($changed | Where-Object { $_ -notmatch '^docs/' })

if ($codeChanges.Count -eq 0) {
    Write-Host "graph-impact: no code paths in change set; OK"
    exit 0
}

$productUpdated = @($changed | Where-Object { $_ -like "docs/product/*" -and $_ -notlike "docs/product/graph/waivers/.gitkeep" }).Count -gt 0
$waiver = @($changed | Where-Object { $_ -like "docs/product/graph/waivers/*" -and $_ -notlike "*.gitkeep" }).Count -gt 0

$required = New-Object System.Collections.Generic.List[string]
foreach ($file in $codeChanges) {
    $entry = Find-JourneysBestEntry -RelPath $file -Entries $entries
    if ($null -eq $entry) { continue }
    if ($entry.meaningOptional) { continue }
    foreach ($node in $entry.nodes) { $required.Add($node) | Out-Null }
}

$required = @($required | Select-Object -Unique)
if ($required.Count -eq 0) {
    Write-Host "graph-impact: no required meaning nodes (optional prefixes only); OK"
    exit 0
}

if ($productUpdated -or $waiver) {
    $nodeList = $required -join ', '
    Write-Host "graph-impact: OK nodes=$nodeList productUpdate=$productUpdated waiver=$waiver"
    exit 0
}

$nodeList = $required -join ', '
Write-Host "graph-impact: FAIL - nodes $nodeList need a docs/product update or waiver"
exit 1
