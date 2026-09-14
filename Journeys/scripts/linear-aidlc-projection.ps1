param(
    [ValidateSet("upsert", "pull-back", "claim", "comment", "complete", "cancel")]
    [string]$Action,

    [string]$IntentRecordDir,
    [string]$Unit,
    [string]$Body,
    [string]$CommitSha,
    [string]$CommitUrl,
    [int]$ApproveCreate = -1,
    [switch]$SkipMain
)

$ErrorActionPreference = "Stop"
$script:LinearGraphqlInvoker = $null
$script:LinearProjectionConfigPath = $null
$script:JourneysRootOverride = $null

function Clear-LinearProjectionHooks {
    $script:LinearGraphqlInvoker = $null
    $script:LinearProjectionConfigPath = $null
    $script:JourneysRootOverride = $null
}

function Assert-UnitNotFrozen {
    param($Map, [string]$Unit)
    if ($Map.units[$Unit].frozen -eq $true) {
        throw "unit '$Unit' is frozen; pull-back refused"
    }
}

function New-LinearGraphqlBody {
    param(
        [ValidateSet("issueCreate", "issueUpdate", "commentCreate", "attachmentCreate", "workflowStates", "issue")]
        [string]$Operation,
        [string]$TeamId,
        [string]$Title,
        [string]$Description,
        [string]$StateId,
        [string]$IssueId,
        [string]$Body,
        [string]$Url,
        [string]$AttachmentTitle
    )
    switch ($Operation) {
        "issueCreate" {
            return (@{
                query     = 'mutation IssueCreate($input: IssueCreateInput!) { issueCreate(input: $input) { success issue { id identifier url } } }'
                variables = @{ input = @{ teamId = $TeamId; title = $Title; description = $Description; stateId = $StateId } }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "issueUpdate" {
            $input = @{}
            if ($Title) { $input.title = $Title }
            if ($Description) { $input.description = $Description }
            if ($StateId) { $input.stateId = $StateId }
            return (@{
                query     = 'mutation IssueUpdate($id: String!, $input: IssueUpdateInput!) { issueUpdate(id: $id, input: $input) { success issue { id identifier } } }'
                variables = @{ id = $IssueId; input = $input }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "commentCreate" {
            return (@{
                query     = 'mutation CommentCreate($input: CommentCreateInput!) { commentCreate(input: $input) { success comment { id } } }'
                variables = @{ input = @{ issueId = $IssueId; body = $Body } }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "attachmentCreate" {
            return (@{
                query     = 'mutation AttachmentCreate($input: AttachmentCreateInput!) { attachmentCreate(input: $input) { success attachment { id } } }'
                variables = @{ input = @{ issueId = $IssueId; title = $AttachmentTitle; url = $Url } }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "workflowStates" {
            return (@{
                query     = 'query States($teamId: String!) { workflowStates(filter: { team: { id: { eq: $teamId } } }) { nodes { id name } } }'
                variables = @{ teamId = $TeamId }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "issue" {
            return (@{
                query     = 'query Issue($id: String!) { issue(id: $id) { id identifier title description state { id name } } }'
                variables = @{ id = $IssueId }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
    }
}

function ConvertTo-LinearYamlDoubleQuoted {
    param([AllowNull()][string]$Value)
    if ($null -eq $Value) { $Value = "" }
    $escaped = $Value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '\r').Replace("`n", '\n')
    return '"' + $escaped + '"'
}

function ConvertFrom-LinearYamlScalar {
    param([string]$Raw)
    if ($null -eq $Raw) { return $null }
    $t = $Raw.Trim()
    if ($t -eq "") { return "" }
    if ($t -eq "true") { return $true }
    if ($t -eq "false") { return $false }
    if ($t -eq "null" -or $t -eq "~") { return $null }

    if ($t.Length -ge 2 -and $t.StartsWith('"') -and $t.EndsWith('"')) {
        $inner = $t.Substring(1, $t.Length - 2)
        $sb = New-Object System.Text.StringBuilder
        for ($i = 0; $i -lt $inner.Length; $i++) {
            $ch = $inner[$i]
            if ($ch -eq [char]'\' -and ($i + 1) -lt $inner.Length) {
                $n = [string]$inner[$i + 1]
                switch ($n) {
                    'n' { [void]$sb.Append([char]10) }
                    't' { [void]$sb.Append([char]9) }
                    'r' { [void]$sb.Append([char]13) }
                    '"' { [void]$sb.Append('"') }
                    '\' { [void]$sb.Append('\') }
                    default { [void]$sb.Append($n) }
                }
                $i++
            }
            else {
                [void]$sb.Append($ch)
            }
        }
        return $sb.ToString()
    }

    if ($t.Length -ge 2 -and $t.StartsWith("'") -and $t.EndsWith("'")) {
        return $t.Substring(1, $t.Length - 2).Replace("''", "'")
    }

    $hash = $t.IndexOf('#')
    if ($hash -ge 0) {
        $t = $t.Substring(0, $hash).TrimEnd()
    }
    return $t
}

function ConvertFrom-YamlOrHashtable {
    param($InputObject)
    if ($null -eq $InputObject) {
        return @{ units = @{} }
    }
    if ($InputObject -is [hashtable] -or $InputObject -is [System.Collections.IDictionary]) {
        return $InputObject
    }

    $text = [string]$InputObject
    $units = @{}
    $currentName = $null
    $inUnits = $false

    foreach ($line in ($text -split '\r?\n')) {
        if ($line -match '^\s*$') { continue }
        if ($line -match '^\s*#') { continue }

        if ($line -match '^units:\s*$') {
            $inUnits = $true
            $currentName = $null
            continue
        }

        if (-not $inUnits) { continue }

        if ($line -match '^  ([^:\s][^:]*):\s*$') {
            $currentName = $Matches[1].Trim()
            $units[$currentName] = @{
                identifier = $null
                issueId    = $null
                frozen     = $false
                title      = $null
            }
            continue
        }

        if ($null -ne $currentName -and $line -match '^    ([^:]+):\s*(.*)$') {
            $key = $Matches[1].Trim()
            $val = ConvertFrom-LinearYamlScalar $Matches[2]
            $units[$currentName][$key] = $val
            continue
        }
    }

    return @{ units = $units }
}

function Read-LinearMap {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return @{ units = @{} } }
    return ConvertFrom-YamlOrHashtable (Get-Content -Raw $Path)
}

function Write-LinearMap {
    param(
        [string]$Path,
        $Map
    )
    $units = @{}
    if ($null -ne $Map -and $null -ne $Map.units) {
        $units = $Map.units
    }

    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("units:")

    foreach ($name in @($units.Keys)) {
        $unit = $units[$name]
        [void]$lines.Add("  ${name}:")
        $identifier = ""
        $issueId = ""
        $title = ""
        $frozen = $false
        if ($null -ne $unit) {
            if ($null -ne $unit.identifier) { $identifier = [string]$unit.identifier }
            if ($null -ne $unit.issueId) { $issueId = [string]$unit.issueId }
            if ($null -ne $unit.title) { $title = [string]$unit.title }
            if ($unit.frozen -eq $true) { $frozen = $true }
        }
        $frozenText = "false"
        if ($frozen) { $frozenText = "true" }
        [void]$lines.Add("    identifier: $identifier")
        [void]$lines.Add("    issueId: $issueId")
        [void]$lines.Add("    frozen: $frozenText")
        [void]$lines.Add("    title: $(ConvertTo-LinearYamlDoubleQuoted $title)")
    }

    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $nl = "`n"
    $text = ($lines -join $nl) + $nl
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $text, $utf8NoBom)
}

function Get-JourneysRoot {
    if ($script:JourneysRootOverride) { return $script:JourneysRootOverride }
    if ($PSScriptRoot) { return Split-Path -Parent $PSScriptRoot }
    return (Get-Location).Path
}

function Get-LinearApiKey {
    return $env:LINEAR_API_KEY
}

function Invoke-LinearGraphql {
    param([string]$Body, [string]$ApiKey)
    if (-not $ApiKey) {
        $ApiKey = $env:LINEAR_API_KEY
    }
    if (-not $ApiKey) {
        throw "LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md"
    }
    if ($script:LinearGraphqlInvoker -is [scriptblock]) {
        return & $script:LinearGraphqlInvoker $Body $ApiKey
    }
    $resp = Invoke-RestMethod -Method Post -Uri "https://api.linear.app/graphql" -Headers @{
        Authorization  = $ApiKey
        "Content-Type" = "application/json"
    } -Body $Body
    if ($null -ne $resp.errors) {
        $msg = ($resp.errors | ForEach-Object { $_.message }) -join "; "
        throw "Linear GraphQL error: $msg"
    }
    return $resp
}

function Read-LinearProjectionConfig {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "missing Linear projection config: $Path (copy aidlc/spaces/default/linear-projection.example.yaml)"
    }
    $cfg = @{
        teamId        = ""
        teamKey       = "JOU"
        githubRepoUrl = ""
        maxCreate     = 25
        states        = @{
            todo        = "Todo"
            inProgress  = "In Progress"
            devComplete = "In Review"
            done        = "Done"
            cancelled   = "Canceled"
        }
    }
    $inStates = $false
    foreach ($line in (Get-Content -LiteralPath $Path)) {
        if ($line -match '^\s*#') { continue }
        if ($line -match '^\s*$') { continue }
        if ($line -match '^states:\s*$') { $inStates = $true; continue }
        if ($inStates -and $line -match '^\S') { $inStates = $false }
        if ($inStates -and $line -match '^\s+(\S+):\s*(.*)$') {
            $cfg.states[$Matches[1].Trim()] = (ConvertFrom-LinearYamlScalar $Matches[2])
            continue
        }
        if ($line -match '^maxCreate:\s*(.*)$') {
            $cfg.maxCreate = [int](ConvertFrom-LinearYamlScalar $Matches[1])
            continue
        }
        if ($line -match '^(teamId|teamKey|githubRepoUrl):\s*(.*)$') {
            $cfg[$Matches[1]] = (ConvertFrom-LinearYamlScalar $Matches[2])
        }
    }
    return $cfg
}

function Get-LinearProjectionConfig {
    $path = $script:LinearProjectionConfigPath
    if (-not $path) {
        $path = Join-Path (Get-JourneysRoot) "aidlc\spaces\default\linear-projection.yaml"
    }
    $cfg = Read-LinearProjectionConfig -Path $path
    if (-not $cfg.teamId) {
        throw "linear-projection.yaml teamId is empty. Copy the example file and set the GraphQL team id."
    }
    return $cfg
}

function Get-AidlcUnitNamesFromDependencyMarkdown {
    param([string]$Markdown)
    if (-not $Markdown) { return @() }
    $text = $Markdown
    $fenceMatches = [regex]::Matches($Markdown, '(?ms)^```(?:yaml|yml)?\s*\r?\n(.*?)```')
    foreach ($m in $fenceMatches) {
        $block = $m.Groups[1].Value
        if ($block -match '(?m)^units:\s*$') {
            $text = $block
            break
        }
    }
    $names = New-Object System.Collections.Generic.List[string]
    $inUnits = $false
    foreach ($line in ($text -split '\r?\n')) {
        if ($line -match '^\s*units:\s*$') {
            $inUnits = $true
            continue
        }
        if ($inUnits -and $line -match '^\S' -and $line -notmatch '^\s*units:') {
            $inUnits = $false
        }
        if ($inUnits -and $line -match '^\s*-\s*name:\s*(.+?)\s*$') {
            [void]$names.Add($Matches[1].Trim().Trim('"').Trim("'"))
        }
        elseif ($inUnits -and $line -match '^\s*name:\s*(.+?)\s*$') {
            [void]$names.Add($Matches[1].Trim().Trim('"').Trim("'"))
        }
    }
    return @($names)
}

function Get-UnitDescriptionFromUnitOfWork {
    param([string]$Markdown, [string]$Unit)
    if (-not $Markdown) { return "# $Unit" }
    $escaped = [regex]::Escape($Unit)
    $m = [regex]::Match($Markdown, "(?ms)^#{1,6}\s+$escaped\b[^\r\n]*\r?\n.*?(?=^#{1,6}\s|\z)")
    if ($m.Success) { return $m.Value.TrimEnd() }
    return "# $Unit`n`n$Markdown".TrimEnd()
}

function Get-LinearMapPath {
    param([string]$IntentRecordDir)
    return (Join-Path $IntentRecordDir "linear-map.yaml")
}

function Get-UnitDependencyPath {
    param([string]$IntentRecordDir)
    return (Join-Path $IntentRecordDir "inception\units-generation\unit-of-work-dependency.md")
}

function Get-UnitOfWorkPath {
    param([string]$IntentRecordDir)
    return (Join-Path $IntentRecordDir "inception\units-generation\unit-of-work.md")
}

function Get-UnitLinearCopyPath {
    param([string]$IntentRecordDir, [string]$Unit)
    return (Join-Path $IntentRecordDir "inception\units-generation\unit-linear-copy\$Unit.md")
}

function Get-AidlcUnitNamesFromRecord {
    param([string]$IntentRecordDir)
    $dep = Get-UnitDependencyPath -IntentRecordDir $IntentRecordDir
    if (-not (Test-Path -LiteralPath $dep)) {
        throw "missing unit-of-work-dependency.md at $dep"
    }
    $names = Get-AidlcUnitNamesFromDependencyMarkdown -Markdown (Get-Content -LiteralPath $dep -Raw)
    if ($names.Count -eq 0) {
        throw "no units found in $dep"
    }
    return $names
}

function Get-LinearWorkflowStateId {
    param($Nodes, [string]$Name)
    foreach ($n in @($Nodes)) {
        if ([string]$n.name -eq $Name) { return [string]$n.id }
    }
    throw "Linear workflow state '$Name' not found for this team"
}

function Get-LinearWorkflowStateNodes {
    param([string]$TeamId, [string]$ApiKey)
    $body = New-LinearGraphqlBody -Operation workflowStates -TeamId $TeamId
    $resp = Invoke-LinearGraphql -Body $body -ApiKey $ApiKey
    return @($resp.data.workflowStates.nodes)
}

function Get-LinearPendingCreateNames {
    param($Map, [string[]]$Names)
    $pending = New-Object System.Collections.Generic.List[string]
    foreach ($name in @($Names)) {
        $entry = $null
        if ($null -ne $Map -and $null -ne $Map.units) {
            $entry = $Map.units[$name]
        }
        if (-not $entry -or -not $entry.issueId) {
            [void]$pending.Add($name)
        }
    }
    return @($pending)
}

function Assert-LinearCreateBudget {
    param(
        [string[]]$PendingNames,
        [int]$MaxCreate,
        [int]$ApproveCreate = -1
    )
    $n = @($PendingNames).Count
    if ($n -eq 0) { return }
    $list = @($PendingNames) -join ", "
    if ($n -gt $MaxCreate) {
        throw "would create $n Linear issues ($list); maxCreate is $MaxCreate. Split the intent or raise maxCreate in linear-projection.yaml."
    }
    if ($ApproveCreate -ne $n) {
        throw "would create $n Linear issues ($list). Re-run with -ApproveCreate $n after a human confirms that count (got $ApproveCreate)."
    }
}

function Ensure-LinearMapUnit {
    param($Map, [string]$Unit)
    if ($null -eq $Map.units) { $Map.units = @{} }
    if ($null -eq $Map.units[$Unit]) {
        $Map.units[$Unit] = @{
            identifier = $null
            issueId    = $null
            frozen     = $false
            title      = $null
        }
    }
    return $Map.units[$Unit]
}

function Invoke-LinearUpsert {
    param(
        [string]$IntentRecordDir,
        [string]$ApiKey,
        [int]$ApproveCreate = -1
    )
    if (-not $IntentRecordDir) { throw "-IntentRecordDir is required" }
    $cfg = Get-LinearProjectionConfig
    $key = $ApiKey
    if (-not $key) { $key = Get-LinearApiKey }
    if (-not $key) { throw "LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md" }

    $names = Get-AidlcUnitNamesFromRecord -IntentRecordDir $IntentRecordDir
    $uowPath = Get-UnitOfWorkPath -IntentRecordDir $IntentRecordDir
    $uow = ""
    if (Test-Path -LiteralPath $uowPath) { $uow = Get-Content -LiteralPath $uowPath -Raw }

    $mapPath = Get-LinearMapPath -IntentRecordDir $IntentRecordDir
    $map = Read-LinearMap -Path $mapPath
    $pending = Get-LinearPendingCreateNames -Map $map -Names $names
    Assert-LinearCreateBudget -PendingNames $pending -MaxCreate $cfg.maxCreate -ApproveCreate $ApproveCreate
    $nodes = Get-LinearWorkflowStateNodes -TeamId $cfg.teamId -ApiKey $key
    $todoId = Get-LinearWorkflowStateId -Nodes $nodes -Name $cfg.states.todo

    foreach ($name in $names) {
        $entry = Ensure-LinearMapUnit -Map $map -Unit $name
        $title = $name
        $description = Get-UnitDescriptionFromUnitOfWork -Markdown $uow -Unit $name
        $firstLine = ($description -split '\r?\n')[0]
        if ($firstLine -match '^#+\s+(.+)$') { $title = $Matches[1].Trim() }

        if ($entry.frozen -eq $true) {
            continue
        }

        if ($entry.issueId) {
            $body = New-LinearGraphqlBody -Operation issueUpdate -IssueId $entry.issueId -Title $title -Description $description -StateId $todoId
            $resp = Invoke-LinearGraphql -Body $body -ApiKey $key
            $issue = $resp.data.issueUpdate.issue
            if ($issue) {
                $entry.identifier = [string]$issue.identifier
                $entry.issueId = [string]$issue.id
            }
        }
        else {
            $body = New-LinearGraphqlBody -Operation issueCreate -TeamId $cfg.teamId -Title $title -Description $description -StateId $todoId
            $resp = Invoke-LinearGraphql -Body $body -ApiKey $key
            $issue = $resp.data.issueCreate.issue
            if (-not $issue) { throw "issueCreate failed for unit '$name'" }
            $entry.identifier = [string]$issue.identifier
            $entry.issueId = [string]$issue.id
        }
        $entry.title = $title
        $entry.frozen = $false
    }

    Write-LinearMap -Path $mapPath -Map $map
    return $map
}

function Invoke-LinearPullBack {
    param(
        [string]$IntentRecordDir,
        [string]$ApiKey,
        $Map,
        [string]$Unit
    )
    if ($PSBoundParameters.ContainsKey("Map") -and $Unit) {
        Assert-UnitNotFrozen -Map $Map -Unit $Unit
        return $Map
    }

    if (-not $IntentRecordDir) { throw "-IntentRecordDir is required" }
    $key = $ApiKey
    if (-not $key) { $key = Get-LinearApiKey }
    if (-not $key) { throw "LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md" }

    $names = Get-AidlcUnitNamesFromRecord -IntentRecordDir $IntentRecordDir
    $mapPath = Get-LinearMapPath -IntentRecordDir $IntentRecordDir
    $map = Read-LinearMap -Path $mapPath

    foreach ($name in $names) {
        $entry = $map.units[$name]
        if (-not $entry -or -not $entry.issueId) {
            throw "unit '$name' has no Linear issueId; run upsert first"
        }
        Assert-UnitNotFrozen -Map $map -Unit $name
        $body = New-LinearGraphqlBody -Operation issue -IssueId $entry.issueId
        $resp = Invoke-LinearGraphql -Body $body -ApiKey $key
        $issue = $resp.data.issue
        if (-not $issue) { throw "Linear issue query failed for unit '$name'" }
        $title = [string]$issue.title
        $description = [string]$issue.description
        $entry.title = $title
        $copyDir = Join-Path $IntentRecordDir "inception\units-generation\unit-linear-copy"
        if (-not (Test-Path -LiteralPath $copyDir)) {
            New-Item -ItemType Directory -Path $copyDir -Force | Out-Null
        }
        $copyPath = Get-UnitLinearCopyPath -IntentRecordDir $IntentRecordDir -Unit $name
        $md = "# $title`n`n$description`n"
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($copyPath, $md, $utf8NoBom)
    }

    Write-LinearMap -Path $mapPath -Map $map
    return $map
}

function Set-LinearClaimLocal {
    param($Map, [string]$Unit)
    $entry = Ensure-LinearMapUnit -Map $Map -Unit $Unit
    $entry.frozen = $true
    return $Map
}

function Invoke-LinearClaim {
    param(
        [string]$IntentRecordDir,
        [string]$Unit,
        [string]$ApiKey
    )
    if (-not $IntentRecordDir) { throw "-IntentRecordDir is required" }
    if (-not $Unit) { throw "-Unit is required" }
    $cfg = Get-LinearProjectionConfig
    $key = $ApiKey
    if (-not $key) { $key = Get-LinearApiKey }
    if (-not $key) { throw "LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md" }

    $mapPath = Get-LinearMapPath -IntentRecordDir $IntentRecordDir
    $map = Read-LinearMap -Path $mapPath
    $entry = $map.units[$Unit]
    if (-not $entry -or -not $entry.issueId) { throw "unit '$Unit' has no Linear issueId; run upsert first" }

    $nodes = Get-LinearWorkflowStateNodes -TeamId $cfg.teamId -ApiKey $key
    $stateId = Get-LinearWorkflowStateId -Nodes $nodes -Name $cfg.states.inProgress
    $body = New-LinearGraphqlBody -Operation issueUpdate -IssueId $entry.issueId -StateId $stateId
    Invoke-LinearGraphql -Body $body -ApiKey $key | Out-Null
    $map = Set-LinearClaimLocal -Map $map -Unit $Unit
    Write-LinearMap -Path $mapPath -Map $map
    return $map
}

function Invoke-LinearComment {
    param(
        [string]$IntentRecordDir,
        [string]$Unit,
        [string]$Body,
        [string]$ApiKey
    )
    if (-not $IntentRecordDir) { throw "-IntentRecordDir is required" }
    if (-not $Unit) { throw "-Unit is required" }
    if (-not $Body) { throw "-Body is required" }
    $key = $ApiKey
    if (-not $key) { $key = Get-LinearApiKey }
    if (-not $key) { throw "LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md" }

    $map = Read-LinearMap -Path (Get-LinearMapPath -IntentRecordDir $IntentRecordDir)
    $entry = $map.units[$Unit]
    if (-not $entry -or -not $entry.issueId) { throw "unit '$Unit' has no Linear issueId; run upsert first" }
    $gql = New-LinearGraphqlBody -Operation commentCreate -IssueId $entry.issueId -Body $Body
    Invoke-LinearGraphql -Body $gql -ApiKey $key | Out-Null
}

function Invoke-LinearComplete {
    param(
        [string]$IntentRecordDir,
        [string]$Unit,
        [string]$CommitSha,
        [string]$CommitUrl,
        [string]$ApiKey
    )
    if (-not $IntentRecordDir) { throw "-IntentRecordDir is required" }
    if (-not $Unit) { throw "-Unit is required" }
    if (-not $CommitSha) { throw "-CommitSha is required for complete" }
    $cfg = Get-LinearProjectionConfig
    $key = $ApiKey
    if (-not $key) { $key = Get-LinearApiKey }
    if (-not $key) { throw "LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md" }

    $mapPath = Get-LinearMapPath -IntentRecordDir $IntentRecordDir
    $map = Read-LinearMap -Path $mapPath
    $entry = $map.units[$Unit]
    if (-not $entry -or -not $entry.issueId) { throw "unit '$Unit' has no Linear issueId; run upsert first" }

    $url = $CommitUrl
    if (-not $url) {
        $base = $cfg.githubRepoUrl.TrimEnd("/")
        $url = "$base/commit/$CommitSha"
    }

    $nodes = Get-LinearWorkflowStateNodes -TeamId $cfg.teamId -ApiKey $key
    $stateId = Get-LinearWorkflowStateId -Nodes $nodes -Name $cfg.states.devComplete
    $upd = New-LinearGraphqlBody -Operation issueUpdate -IssueId $entry.issueId -StateId $stateId
    Invoke-LinearGraphql -Body $upd -ApiKey $key | Out-Null

    $cmt = New-LinearGraphqlBody -Operation commentCreate -IssueId $entry.issueId -Body "commit $CommitSha"
    Invoke-LinearGraphql -Body $cmt -ApiKey $key | Out-Null

    $att = New-LinearGraphqlBody -Operation attachmentCreate -IssueId $entry.issueId -Url $url -AttachmentTitle "commit $CommitSha"
    Invoke-LinearGraphql -Body $att -ApiKey $key | Out-Null
}

function Invoke-LinearCancel {
    param(
        [string]$IntentRecordDir,
        [string]$Unit,
        [string]$ApiKey
    )
    if (-not $IntentRecordDir) { throw "-IntentRecordDir is required" }
    if (-not $Unit) { throw "-Unit is required" }
    $cfg = Get-LinearProjectionConfig
    $key = $ApiKey
    if (-not $key) { $key = Get-LinearApiKey }
    if (-not $key) { throw "LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md" }

    $map = Read-LinearMap -Path (Get-LinearMapPath -IntentRecordDir $IntentRecordDir)
    $entry = $map.units[$Unit]
    if (-not $entry -or -not $entry.issueId) { throw "unit '$Unit' has no Linear issueId; run upsert first" }

    $nodes = Get-LinearWorkflowStateNodes -TeamId $cfg.teamId -ApiKey $key
    $stateId = Get-LinearWorkflowStateId -Nodes $nodes -Name $cfg.states.cancelled
    $upd = New-LinearGraphqlBody -Operation issueUpdate -IssueId $entry.issueId -StateId $stateId
    Invoke-LinearGraphql -Body $upd -ApiKey $key | Out-Null
}

function Invoke-LinearProjectionMain {
    switch ($Action) {
        "upsert" { Invoke-LinearUpsert -IntentRecordDir $IntentRecordDir -ApproveCreate $ApproveCreate | Out-Null }
        "pull-back" { Invoke-LinearPullBack -IntentRecordDir $IntentRecordDir | Out-Null }
        "claim" { Invoke-LinearClaim -IntentRecordDir $IntentRecordDir -Unit $Unit | Out-Null }
        "comment" { Invoke-LinearComment -IntentRecordDir $IntentRecordDir -Unit $Unit -Body $Body }
        "complete" { Invoke-LinearComplete -IntentRecordDir $IntentRecordDir -Unit $Unit -CommitSha $CommitSha -CommitUrl $CommitUrl }
        "cancel" { Invoke-LinearCancel -IntentRecordDir $IntentRecordDir -Unit $Unit }
        default { throw "unknown -Action '$Action'" }
    }
}

if ($MyInvocation.InvocationName -ne '.' -and -not $SkipMain -and $Action) {
    Invoke-LinearProjectionMain
}
