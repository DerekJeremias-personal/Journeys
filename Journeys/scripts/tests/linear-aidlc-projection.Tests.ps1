BeforeAll {
    $here = Split-Path -Parent $PSCommandPath
    . (Join-Path (Split-Path $here) "linear-aidlc-projection.ps1")
    Clear-LinearProjectionHooks
}

Describe "Linear map freeze" {
    It "refuses pull-back when frozen" {
        $map = @{
            units = @{
                "u1-demo" = @{ identifier = "JOU-1"; issueId = "abc"; frozen = $true; title = "t" }
            }
        }
        { Assert-UnitNotFrozen -Map $map -Unit "u1-demo" } | Should -Throw
    }
}

Describe "GraphQL bodies" {
    It "issueCreate includes teamId title description stateId" {
        $body = New-LinearGraphqlBody -Operation issueCreate -TeamId "team-1" -Title "U1" -Description "AC" -StateId "state-todo"
        $json = $body | ConvertFrom-Json
        $json.variables.input.teamId | Should -Be "team-1"
        $json.variables.input.title | Should -Be "U1"
        $json.variables.input.stateId | Should -Be "state-todo"
    }
}

Describe "Linear map IO" {
    It "round-trips identifier issueId frozen title" {
        $dir = Join-Path $env:TEMP ("linear-map-test-" + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $dir | Out-Null
        try {
            $path = Join-Path $dir "linear-map.yaml"
            $map = @{
                units = @{
                    "u1-demo" = @{
                        identifier = "JOU-1"
                        issueId    = "uuid"
                        frozen     = $false
                        title      = "hello: quoted title"
                    }
                }
            }
            Write-LinearMap -Path $path -Map $map
            $raw = Get-Content -LiteralPath $path -Raw
            $raw | Should -Match '(?m)^units:'
            $read = Read-LinearMap -Path $path
            $read.units["u1-demo"].identifier | Should -Be "JOU-1"
            $read.units["u1-demo"].issueId | Should -Be "uuid"
            $read.units["u1-demo"].frozen | Should -Be $false
            $read.units["u1-demo"].title | Should -Be "hello: quoted title"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "returns empty units when file is missing" {
        $path = Join-Path $env:TEMP ("no-such-linear-map-" + [guid]::NewGuid() + ".yaml")
        $read = Read-LinearMap -Path $path
        $read.units.Count | Should -Be 0
    }

    It "round-trips frozen true" {
        $dir = Join-Path $env:TEMP ("linear-map-frozen-" + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $dir | Out-Null
        try {
            $path = Join-Path $dir "linear-map.yaml"
            $map = @{
                units = @{
                    "u1-demo" = @{
                        identifier = "JOU-1"
                        issueId    = "abc"
                        frozen     = $true
                        title      = "t"
                    }
                }
            }
            Write-LinearMap -Path $path -Map $map
            $read = Read-LinearMap -Path $path
            $read.units["u1-demo"].frozen | Should -Be $true
            { Assert-UnitNotFrozen -Map $read -Unit "u1-demo" } | Should -Throw
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }
}

Describe "Pull-back freeze" {
    It "refuses Invoke-LinearPullBack when frozen" {
        $map = @{
            units = @{
                "u1-demo" = @{ identifier = "JOU-1"; issueId = "abc"; frozen = $true; title = "t" }
            }
        }
        { Invoke-LinearPullBack -Map $map -Unit "u1-demo" -IntentRecordDir "unused" } | Should -Throw
    }
}

Describe "Claim and complete bodies" {
    It "claim sets frozen true in map object" {
        $map = @{ units = @{ "u1-demo" = @{ issueId = "x"; frozen = $false } } }
        $next = Set-LinearClaimLocal -Map $map -Unit "u1-demo"
        $next.units["u1-demo"].frozen | Should -Be $true
    }

    It "complete body includes attachment url" {
        $body = New-LinearGraphqlBody -Operation attachmentCreate -IssueId "x" -Url "https://github.com/DerekJeremias-personal/Journeys/commit/abc" -AttachmentTitle "commit abc"
        $body | Should -Match "github.com/DerekJeremias-personal/Journeys/commit/abc"
    }
}

Describe "Unit list parsing" {
    It "parses unit names from edge block" {
        $md = @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
  - name: u2-other
    kind: spec
    depends_on: [u1-demo]
"@
        $names = Get-AidlcUnitNamesFromDependencyMarkdown -Markdown $md
        $names | Should -Be @("u1-demo", "u2-other")
    }

    It "parses unit names from fenced yaml block" {
        $fence = '```'
        $md = @"
# Dependency graph

${fence}yaml
units:
  - name: u1-demo
    kind: service
    depends_on: []
  - name: u2-other
    kind: spec
    depends_on: [u1-demo]
${fence}
"@
        $names = Get-AidlcUnitNamesFromDependencyMarkdown -Markdown $md
        $names | Should -Be @("u1-demo", "u2-other")
    }
}

Describe "Unit of work description" {
    It "returns the section for a named unit" {
        $md = @"
# Units

## u1-demo

Do the thing.

## u2-other

Other work.
"@
        $section = Get-UnitDescriptionFromUnitOfWork -Markdown $md -Unit "u1-demo"
        $section | Should -Match "Do the thing"
        $section | Should -Not -Match "Other work"
    }

    It "returns the section when the heading wraps the unit name" {
        $md = @'
# Units of work — closeout

## U1 — `u1-demo`

Do the thing.

## U2 — `u2-other`

Other work.
'@
        $section = Get-UnitDescriptionFromUnitOfWork -Markdown $md -Unit "u1-demo"
        $section | Should -Match "Do the thing"
        $section | Should -Not -Match "Other work"
        $section | Should -Not -Match "Units of work"
    }
}

Describe "Linear GraphQL invoker" {
    It "throws when API key is missing" {
        Clear-LinearProjectionHooks
        $prev = $env:LINEAR_API_KEY
        Remove-Item Env:LINEAR_API_KEY -ErrorAction SilentlyContinue
        try {
            { Invoke-LinearGraphql -Body '{"query":"query { viewer { id } }"}' -ApiKey "" } | Should -Throw "*LINEAR_API_KEY*"
        }
        finally {
            if ($prev) { $env:LINEAR_API_KEY = $prev }
        }
    }
}

Describe "Invoke-LinearPullBack" {
    BeforeEach {
        $script:LinearGraphqlInvoker = {
            param($Body, $ApiKey)
            throw "network should not be called"
        }
        $env:LINEAR_API_KEY = "test-not-a-real-key"
    }

    AfterEach {
        $script:LinearGraphqlInvoker = $null
        Remove-Item Env:LINEAR_API_KEY -ErrorAction SilentlyContinue
    }

    It "throws when unit is frozen" {
        $dir = Join-Path $env:TEMP ("linear-pullback-frozen-" + [guid]::NewGuid())
        $ug = Join-Path $dir "inception\units-generation"
        New-Item -ItemType Directory -Path $ug -Force | Out-Null
        try {
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work-dependency.md") -Value @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
"@
            Write-LinearMap -Path (Join-Path $dir "linear-map.yaml") -Map @{
                units = @{
                    "u1-demo" = @{ identifier = "JOU-1"; issueId = "abc"; frozen = $true; title = "t" }
                }
            }
            { Invoke-LinearPullBack -IntentRecordDir $dir } | Should -Throw "unit 'u1-demo' is frozen; pull-back refused"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "throws when unit lacks issueId" {
        $dir = Join-Path $env:TEMP ("linear-pullback-noid-" + [guid]::NewGuid())
        $ug = Join-Path $dir "inception\units-generation"
        New-Item -ItemType Directory -Path $ug -Force | Out-Null
        try {
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work-dependency.md") -Value @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
"@
            Write-LinearMap -Path (Join-Path $dir "linear-map.yaml") -Map @{
                units = @{
                    "u1-demo" = @{ identifier = $null; issueId = $null; frozen = $false; title = "t" }
                }
            }
            { Invoke-LinearPullBack -IntentRecordDir $dir } | Should -Throw "*issueId*"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "writes overlay markdown and map title from issue query" {
        $dir = Join-Path $env:TEMP ("linear-pullback-ok-" + [guid]::NewGuid())
        $ug = Join-Path $dir "inception\units-generation"
        New-Item -ItemType Directory -Path $ug -Force | Out-Null
        $uowPath = Join-Path $ug "unit-of-work.md"
        try {
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work-dependency.md") -Value @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
"@
            Set-Content -LiteralPath $uowPath -Value @"
# Units

## u1-demo

Original AC.
"@
            $uowBefore = Get-Content -LiteralPath $uowPath -Raw
            Write-LinearMap -Path (Join-Path $dir "linear-map.yaml") -Map @{
                units = @{
                    "u1-demo" = @{ identifier = "JOU-1"; issueId = "issue-uuid"; frozen = $false; title = "old" }
                }
            }
            $script:LinearGraphqlInvoker = {
                param($Body, $ApiKey)
                return @{
                    data = @{
                        issue = @{
                            id          = "issue-uuid"
                            identifier  = "JOU-1"
                            title       = "Reviewed title"
                            description = "Pulled AC"
                            state       = @{ id = "s1"; name = "Todo" }
                        }
                    }
                }
            }
            Invoke-LinearPullBack -IntentRecordDir $dir
            $overlay = Join-Path $ug "unit-linear-copy\u1-demo.md"
            Test-Path -LiteralPath $overlay | Should -Be $true
            $copied = Get-Content -LiteralPath $overlay -Raw
            $copied | Should -Match "Reviewed title"
            $copied | Should -Match "Pulled AC"
            (Get-Content -LiteralPath $uowPath -Raw) | Should -Be $uowBefore
            $map = Read-LinearMap -Path (Join-Path $dir "linear-map.yaml")
            $map.units["u1-demo"].title | Should -Be "Reviewed title"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }
}

Describe "Invoke-LinearUpsert" {
    BeforeEach {
        $env:LINEAR_API_KEY = "test-not-a-real-key"
        $cfgDir = Join-Path $env:TEMP ("linear-cfg-" + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $cfgDir | Out-Null
        $script:LinearProjectionConfigPath = Join-Path $cfgDir "linear-projection.yaml"
        $script:LinearGraphqlInvoker = {
            param($Body, $ApiKey)
            $json = $Body | ConvertFrom-Json
            if ($json.query -match "workflowStates") {
                return @{
                    data = @{
                        workflowStates = @{
                            nodes = @(
                                @{ id = "state-todo-id"; name = "Todo" }
                            )
                        }
                    }
                }
            }
            if ($json.query -match "issueCreate") {
                return @{
                    data = @{
                        issueCreate = @{
                            success = $true
                            issue   = @{ id = "issue-uuid"; identifier = "JOU-1"; url = "https://linear.app/x" }
                        }
                    }
                }
            }
            if ($json.query -match "issueUpdate") {
                return @{
                    data = @{
                        issueUpdate = @{
                            success = $true
                            issue   = @{ id = "issue-uuid"; identifier = "JOU-1" }
                        }
                    }
                }
            }
            throw "unexpected GraphQL query"
        }
    }

    AfterEach {
        if ($script:LinearProjectionConfigPath) {
            $parent = Split-Path $script:LinearProjectionConfigPath
            if (Test-Path -LiteralPath $parent) {
                Remove-Item -LiteralPath $parent -Recurse -Force
            }
        }
        $script:LinearProjectionConfigPath = $null
        $script:LinearGraphqlInvoker = $null
        Remove-Item Env:LINEAR_API_KEY -ErrorAction SilentlyContinue
    }

    It "throws when teamId is empty" {
        Set-Content -LiteralPath $script:LinearProjectionConfigPath -Value @"
teamId: `"`"
teamKey: JOU
githubRepoUrl: https://github.com/DerekJeremias-personal/Journeys
states:
  todo: Todo
  inProgress: In Progress
  devComplete: In Review
  done: Done
  cancelled: Canceled
"@
        $dir = Join-Path $env:TEMP ("linear-upsert-empty-" + [guid]::NewGuid())
        $ug = Join-Path $dir "inception\units-generation"
        New-Item -ItemType Directory -Path $ug -Force | Out-Null
        try {
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work-dependency.md") -Value @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
"@
            { Invoke-LinearUpsert -IntentRecordDir $dir } | Should -Throw "*teamId*"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "creates issue and writes linear-map.yaml" {
        Set-Content -LiteralPath $script:LinearProjectionConfigPath -Value @"
teamId: team-123
teamKey: JOU
githubRepoUrl: https://github.com/DerekJeremias-personal/Journeys
states:
  todo: Todo
  inProgress: In Progress
  devComplete: In Review
  done: Done
  cancelled: Canceled
"@
        $dir = Join-Path $env:TEMP ("linear-upsert-create-" + [guid]::NewGuid())
        $ug = Join-Path $dir "inception\units-generation"
        New-Item -ItemType Directory -Path $ug -Force | Out-Null
        try {
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work-dependency.md") -Value @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
"@
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work.md") -Value @"
# Units

## u1-demo

Implement demo.
"@
            Invoke-LinearUpsert -IntentRecordDir $dir -ApproveCreate 1
            $mapPath = Join-Path $dir "linear-map.yaml"
            Test-Path -LiteralPath $mapPath | Should -Be $true
            $map = Read-LinearMap -Path $mapPath
            $map.units["u1-demo"].issueId | Should -Be "issue-uuid"
            $map.units["u1-demo"].identifier | Should -Be "JOU-1"
            $map.units["u1-demo"].frozen | Should -Be $false
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "does not call issueUpdate for frozen units" {
        Set-Content -LiteralPath $script:LinearProjectionConfigPath -Value @"
teamId: team-123
teamKey: JOU
githubRepoUrl: https://github.com/DerekJeremias-personal/Journeys
states:
  todo: Todo
  inProgress: In Progress
  devComplete: In Review
  done: Done
  cancelled: Canceled
"@
        $dir = Join-Path $env:TEMP ("linear-upsert-frozen-" + [guid]::NewGuid())
        $ug = Join-Path $dir "inception\units-generation"
        New-Item -ItemType Directory -Path $ug -Force | Out-Null
        $script:LinearUpsertOps = New-Object System.Collections.Generic.List[string]
        try {
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work-dependency.md") -Value @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
"@
            Write-LinearMap -Path (Join-Path $dir "linear-map.yaml") -Map @{
                units = @{
                    "u1-demo" = @{ identifier = "JOU-1"; issueId = "issue-uuid"; frozen = $true; title = "frozen title" }
                }
            }
            $script:LinearGraphqlInvoker = {
                param($Body, $ApiKey)
                $json = $Body | ConvertFrom-Json
                if ($json.query -match "workflowStates") {
                    [void]$script:LinearUpsertOps.Add("workflowStates")
                    return @{
                        data = @{
                            workflowStates = @{
                                nodes = @(
                                    @{ id = "state-todo-id"; name = "Todo" }
                                )
                            }
                        }
                    }
                }
                if ($json.query -match "issueUpdate") {
                    [void]$script:LinearUpsertOps.Add("issueUpdate")
                    throw "frozen unit must not be content-updated on upsert"
                }
                if ($json.query -match "issueCreate") {
                    [void]$script:LinearUpsertOps.Add("issueCreate")
                    throw "frozen unit must not be recreated on upsert"
                }
                throw "unexpected GraphQL query"
            }
            Invoke-LinearUpsert -IntentRecordDir $dir
            $script:LinearUpsertOps | Should -Contain "workflowStates"
            $script:LinearUpsertOps | Should -Not -Contain "issueUpdate"
            $script:LinearUpsertOps | Should -Not -Contain "issueCreate"
            $map = Read-LinearMap -Path (Join-Path $dir "linear-map.yaml")
            $map.units["u1-demo"].frozen | Should -Be $true
            $map.units["u1-demo"].title | Should -Be "frozen title"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "refuses create without matching -ApproveCreate" {
        Set-Content -LiteralPath $script:LinearProjectionConfigPath -Value @"
teamId: team-123
teamKey: JOU
githubRepoUrl: https://github.com/DerekJeremias-personal/Journeys
maxCreate: 25
states:
  todo: Todo
  inProgress: In Progress
  devComplete: In Review
  done: Done
  cancelled: Canceled
"@
        $dir = Join-Path $env:TEMP ("linear-upsert-approve-" + [guid]::NewGuid())
        $ug = Join-Path $dir "inception\units-generation"
        New-Item -ItemType Directory -Path $ug -Force | Out-Null
        $script:LinearUpsertOps = New-Object System.Collections.Generic.List[string]
        try {
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work-dependency.md") -Value @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
"@
            $script:LinearGraphqlInvoker = {
                param($Body, $ApiKey)
                [void]$script:LinearUpsertOps.Add("graphql")
                throw "GraphQL must not run until -ApproveCreate matches"
            }
            { Invoke-LinearUpsert -IntentRecordDir $dir } | Should -Throw "*-ApproveCreate 1*"
            { Invoke-LinearUpsert -IntentRecordDir $dir -ApproveCreate 99 } | Should -Throw "*-ApproveCreate 1*"
            $script:LinearUpsertOps.Count | Should -Be 0
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "refuses create over maxCreate even when approved" {
        Set-Content -LiteralPath $script:LinearProjectionConfigPath -Value @"
teamId: team-123
teamKey: JOU
githubRepoUrl: https://github.com/DerekJeremias-personal/Journeys
maxCreate: 1
states:
  todo: Todo
  inProgress: In Progress
  devComplete: In Review
  done: Done
  cancelled: Canceled
"@
        $dir = Join-Path $env:TEMP ("linear-upsert-max-" + [guid]::NewGuid())
        $ug = Join-Path $dir "inception\units-generation"
        New-Item -ItemType Directory -Path $ug -Force | Out-Null
        try {
            Set-Content -LiteralPath (Join-Path $ug "unit-of-work-dependency.md") -Value @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
  - name: u2-other
    kind: spec
    depends_on: []
"@
            { Invoke-LinearUpsert -IntentRecordDir $dir -ApproveCreate 2 } | Should -Throw "*maxCreate is 1*"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }
}

Describe "Claim comment complete cancel" {
    BeforeEach {
        $env:LINEAR_API_KEY = "test-not-a-real-key"
        $cfgDir = Join-Path $env:TEMP ("linear-cfg-claim-" + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $cfgDir | Out-Null
        $script:LinearProjectionConfigPath = Join-Path $cfgDir "linear-projection.yaml"
        Set-Content -LiteralPath $script:LinearProjectionConfigPath -Value @"
teamId: team-123
teamKey: JOU
githubRepoUrl: https://github.com/DerekJeremias-personal/Journeys
states:
  todo: Todo
  inProgress: In Progress
  devComplete: In Review
  done: Done
  cancelled: Canceled
"@
        $script:ops = New-Object System.Collections.Generic.List[string]
        $script:LinearGraphqlInvoker = {
            param($Body, $ApiKey)
            $json = $Body | ConvertFrom-Json
            if ($json.query -match "workflowStates") {
                [void]$script:ops.Add("workflowStates")
                return @{
                    data = @{
                        workflowStates = @{
                            nodes = @(
                                @{ id = "st-todo"; name = "Todo" }
                                @{ id = "st-prog"; name = "In Progress" }
                                @{ id = "st-rev"; name = "In Review" }
                                @{ id = "st-can"; name = "Canceled" }
                            )
                        }
                    }
                }
            }
            if ($json.query -match "issueUpdate") {
                [void]$script:ops.Add("issueUpdate:" + $json.variables.input.stateId)
                return @{ data = @{ issueUpdate = @{ success = $true; issue = @{ id = "issue-uuid"; identifier = "JOU-1" } } } }
            }
            if ($json.query -match "commentCreate") {
                [void]$script:ops.Add("commentCreate")
                return @{ data = @{ commentCreate = @{ success = $true; comment = @{ id = "c1" } } } }
            }
            if ($json.query -match "attachmentCreate") {
                [void]$script:ops.Add("attachmentCreate:" + $json.variables.input.url)
                return @{ data = @{ attachmentCreate = @{ success = $true; attachment = @{ id = "a1" } } } }
            }
            throw "unexpected GraphQL query"
        }
    }

    AfterEach {
        if ($script:LinearProjectionConfigPath) {
            $parent = Split-Path $script:LinearProjectionConfigPath
            if (Test-Path -LiteralPath $parent) {
                Remove-Item -LiteralPath $parent -Recurse -Force
            }
        }
        $script:LinearProjectionConfigPath = $null
        $script:LinearGraphqlInvoker = $null
        Remove-Item Env:LINEAR_API_KEY -ErrorAction SilentlyContinue
    }

    It "claim freezes the map and moves In Progress" {
        $dir = Join-Path $env:TEMP ("linear-claim-" + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $dir | Out-Null
        try {
            Write-LinearMap -Path (Join-Path $dir "linear-map.yaml") -Map @{
                units = @{ "u1-demo" = @{ identifier = "JOU-1"; issueId = "issue-uuid"; frozen = $false; title = "t" } }
            }
            Invoke-LinearClaim -IntentRecordDir $dir -Unit "u1-demo"
            $map = Read-LinearMap -Path (Join-Path $dir "linear-map.yaml")
            $map.units["u1-demo"].frozen | Should -Be $true
            $script:ops | Should -Contain "issueUpdate:st-prog"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "complete requires CommitSha and attaches commit url" {
        $dir = Join-Path $env:TEMP ("linear-complete-" + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $dir | Out-Null
        try {
            Write-LinearMap -Path (Join-Path $dir "linear-map.yaml") -Map @{
                units = @{ "u1-demo" = @{ identifier = "JOU-1"; issueId = "issue-uuid"; frozen = $true; title = "t" } }
            }
            { Invoke-LinearComplete -IntentRecordDir $dir -Unit "u1-demo" } | Should -Throw "*CommitSha*"
            Invoke-LinearComplete -IntentRecordDir $dir -Unit "u1-demo" -CommitSha "abc"
            $script:ops | Should -Contain "issueUpdate:st-rev"
            $script:ops | Should -Contain "commentCreate"
            $script:ops | Should -Contain "attachmentCreate:https://github.com/DerekJeremias-personal/Journeys/commit/abc"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    It "cancel moves to Canceled" {
        $dir = Join-Path $env:TEMP ("linear-cancel-" + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $dir | Out-Null
        try {
            Write-LinearMap -Path (Join-Path $dir "linear-map.yaml") -Map @{
                units = @{ "u1-demo" = @{ identifier = "JOU-1"; issueId = "issue-uuid"; frozen = $true; title = "t" } }
            }
            Invoke-LinearCancel -IntentRecordDir $dir -Unit "u1-demo"
            $script:ops | Should -Contain "issueUpdate:st-can"
        }
        finally {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }
}

Describe "Create budget" {
    It "counts units that lack issueId" {
        $map = @{
            units = @{
                "u1-demo" = @{ issueId = "x" }
            }
        }
        $pending = Get-LinearPendingCreateNames -Map $map -Names @("u1-demo", "u2-other")
        $pending | Should -Be @("u2-other")
    }
}
