# Shared helpers for docs-impact / graph-impact. Dot-source from sibling scripts.

function Get-JourneysRoot {
    if ($PSScriptRoot) {
        return (Split-Path -Parent $PSScriptRoot)
    }
    return (Get-Location).Path
}

function ConvertTo-JourneysRel {
    param([string]$Path, [string]$Root)
    $full = $Path
    if (-not [System.IO.Path]::IsPathRooted($Path)) {
        $full = Join-Path $Root $Path
    }
    $full = [System.IO.Path]::GetFullPath($full)
    $rootFull = [System.IO.Path]::GetFullPath($Root)
    if ($full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        return ($full.Substring($rootFull.Length).TrimStart('\', '/') -replace '\\', '/')
    }
    return ($Path -replace '\\', '/')
}

function Get-JourneysChangedFiles {
    param(
        [string]$Root,
        [string[]]$Files
    )
    if ($Files -and $Files.Count -gt 0) {
        return @($Files | ForEach-Object { ConvertTo-JourneysRel -Path $_ -Root $Root })
    }
    $gitDir = Join-Path $Root ".git"
    if (Test-Path $gitDir) {
        $names = @()
        $names += @(git -C $Root diff --name-only)
        $names += @(git -C $Root diff --name-only --cached)
        return @($names | Where-Object { $_ } | ForEach-Object { $_ -replace '\\', '/' } | Select-Object -Unique)
    }
    throw "No -Files given and $Root is not a git repo. Pass -Files with the paths in this change set."
}

function Read-JourneysYamlEntries {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing map file: $Path"
    }
    $entries = New-Object System.Collections.Generic.List[hashtable]
    $current = $null
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*#') { continue }
        if ($line -match '^\s*-\s+prefix:\s*(.+)\s*$') {
            if ($null -ne $current) { $entries.Add($current) }
            $current = @{
                prefix          = $Matches[1].Trim()
                nodes           = @()
                docs            = @()
                meaningOptional = $false
            }
        }
        elseif ($null -ne $current -and $line -match '^\s+nodes:\s*\[(.+)\]\s*$') {
            $current.nodes = @($Matches[1].Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        }
        elseif ($null -ne $current -and $line -match '^\s+docs:\s*\[(.+)\]\s*$') {
            $current.docs = @($Matches[1].Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        }
        elseif ($null -ne $current -and $line -match '^\s+meaningOptional:\s*(true|false)\s*$') {
            $current.meaningOptional = ($Matches[1] -eq 'true')
        }
    }
    if ($null -ne $current) { $entries.Add($current) }
    return @($entries)
}

function Find-JourneysBestEntry {
    param(
        [string]$RelPath,
        [hashtable[]]$Entries
    )
    $rel = $RelPath -replace '\\', '/'
    $hits = New-Object System.Collections.Generic.List[hashtable]
    foreach ($entry in $Entries) {
        $p = $entry.prefix -replace '\\', '/'
        if ($rel.Length -lt $p.Length) { continue }
        if (-not $rel.StartsWith($p, [StringComparison]::OrdinalIgnoreCase)) { continue }
        if ($rel.Length -eq $p.Length) {
            $hits.Add($entry)
            continue
        }
        $next = $rel[$p.Length]
        if ($next -eq [char]'/' -or $next -eq [char]'.') {
            $hits.Add($entry)
        }
    }
    if ($hits.Count -eq 0) { return $null }
    return ($hits | Sort-Object { $_.prefix.Length } -Descending | Select-Object -First 1)
}
