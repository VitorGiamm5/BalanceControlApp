param(
    [string]$BaseUrl = "http://localhost:9005",
    [int]$Adjustments = 900,
    [int]$Concurrency = 60,
    [int]$BalanceQueryRatioPercent = 20,
    [int]$ReplayRatioPercent = 10,
    [decimal]$InitialBalance = 10000.00,
    [string]$ClientId = "balance-client",
    [string]$ClientSecret = "balance-secret",
    [string]$OutputDirectory = "artifacts/spike"
)

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "This script requires PowerShell 7 or newer because it uses ForEach-Object -Parallel."
}

if ($Adjustments -lt 3 -or ($Adjustments % 3) -ne 0) {
    throw "Adjustments must be divisible by 3 and greater than or equal to 3."
}

if ($Concurrency -lt 1) {
    throw "Concurrency must be greater than zero."
}

if ($BalanceQueryRatioPercent -lt 0) {
    throw "BalanceQueryRatioPercent must be greater than or equal to zero."
}

if ($ReplayRatioPercent -lt 0) {
    throw "ReplayRatioPercent must be greater than or equal to zero."
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$outputPath = Join-Path $root $OutputDirectory
$batchId = "spike-" + [Guid]::NewGuid().ToString("N")
$startedAt = Get-Date
$accounts = @(
    "$batchId-account-a",
    "$batchId-account-b",
    "$batchId-account-c"
)
$adjustUri = "$BaseUrl/api/v1/balances/adjustments"
$authHeader = $null

function Invoke-ApiHealth {
    param([string]$Url)

    $health = Invoke-WebRequest -Method Get -Uri "$Url/health" -SkipHttpErrorCheck
    if ($health.StatusCode -lt 200 -or $health.StatusCode -gt 299) {
        throw "Health endpoint returned HTTP $($health.StatusCode)."
    }
}

function New-AdjustmentBody {
    param(
        [string]$Account,
        [Guid]$OperationId,
        [decimal]$Amount,
        [string]$Description
    )

    [pscustomobject]@{
        userId = $Account
        operationId = $OperationId
        amount = $Amount
        description = $Description
    }
}

function Invoke-Adjustment {
    param(
        [string]$Uri,
        [object]$Body
    )

    $json = $Body | ConvertTo-Json -Depth 8
    Invoke-RestMethod -Method Post -Uri $Uri -Body $json -ContentType "application/json" -Headers $script:authHeader
}

function Get-SpikeAmount {
    param([int]$Sequence)

    switch ($Sequence % 6) {
        0 { return 100.00 }
        1 { return -20.00 }
        2 { return 25.00 }
        3 { return -5.00 }
        4 { return 10.00 }
        default { return -75.00 }
    }
}

function Get-Percentile {
    param(
        [long[]]$Values,
        [double]$Percentile
    )

    if ($Values.Count -eq 0) {
        return 0
    }

    $ordered = $Values | Sort-Object
    $index = [Math]::Min($ordered.Count - 1, [Math]::Floor($ordered.Count * $Percentile))
    return $ordered[$index]
}

function Invoke-PlannedRequests {
    param(
        [object[]]$Requests,
        [int]$ThrottleLimit
    )

    $Requests | ForEach-Object -Parallel {
        $request = $_
        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            if ($request.method -eq "POST") {
                $json = $request.body | ConvertTo-Json -Depth 8
                $response = Invoke-WebRequest `
                    -Method Post `
                    -Uri $request.uri `
                    -Body $json `
                    -ContentType "application/json" `
                    -SkipHttpErrorCheck `
                    -Headers $using:authHeader
            }
            else {
                $response = Invoke-WebRequest `
                    -Method Get `
                    -Uri $request.uri `
                    -SkipHttpErrorCheck `
                    -Headers $using:authHeader
            }

            $sw.Stop()
            $parsed = $null
            if (-not [string]::IsNullOrWhiteSpace($response.Content)) {
                $parsed = $response.Content | ConvertFrom-Json
            }

            $data = $null
            $applied = $null
            $balance = $null

            if ($parsed -and $parsed.PSObject.Properties.Name -contains "data") {
                $data = $parsed.data

                if ($data -and $data.PSObject.Properties.Name -contains "applied") {
                    $applied = [bool]$data.applied
                }

                if ($data -and $data.PSObject.Properties.Name -contains "balance") {
                    $balance = [decimal]$data.balance
                }
            }

            [pscustomobject]@{
                phase = $request.phase
                kind = $request.kind
                account = $request.account
                sequence = $request.sequence
                operationId = $request.operationId
                amount = $request.amount
                expectedStatus = $request.expectedStatus
                expectedApplied = $request.expectedApplied
                status = [int]$response.StatusCode
                applied = $applied
                balance = $balance
                elapsedMs = $sw.ElapsedMilliseconds
                ok = $true
                error = $null
            }
        }
        catch {
            $sw.Stop()

            [pscustomobject]@{
                phase = $request.phase
                kind = $request.kind
                account = $request.account
                sequence = $request.sequence
                operationId = $request.operationId
                amount = $request.amount
                expectedStatus = $request.expectedStatus
                expectedApplied = $request.expectedApplied
                status = 0
                applied = $null
                balance = $null
                elapsedMs = $sw.ElapsedMilliseconds
                ok = $false
                error = $_.Exception.Message
            }
        }
    } -ThrottleLimit $ThrottleLimit
}

function Assert-NoItems {
    param(
        [object[]]$Items,
        [string]$Message
    )

    if ($Items.Count -gt 0) {
        $sample = $Items | Select-Object -First 5 | ConvertTo-Json -Depth 8
        throw "$Message Sample: $sample"
    }
}

Push-Location $root

try {
    Invoke-ApiHealth $BaseUrl
    $tokenResponse = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/v1/auth/token" `
        -Body (@{ clientId = $ClientId; clientSecret = $ClientSecret } | ConvertTo-Json) `
        -ContentType "application/json"
    $authHeader = @{ Authorization = "Bearer $($tokenResponse.data.accessToken)" }
    $script:authHeader = $authHeader

    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

    $expectedBalances = @{}
    $plannedAdjustments = New-Object System.Collections.Generic.List[object]
    $seedResults = New-Object System.Collections.Generic.List[object]

    foreach ($account in $accounts) {
        $operationId = [Guid]::NewGuid()
        $description = "spike|batch=$batchId|account=$account|seq=seed|type=seed"
        $body = New-AdjustmentBody $account $operationId $InitialBalance $description
        $response = Invoke-Adjustment $adjustUri $body

        if ($response.data.applied -ne $true) {
            throw "Seed operation was not applied for $account."
        }

        $expectedBalances[$account] = $InitialBalance
        $seedResults.Add([pscustomobject]@{
            account = $account
            operationId = $operationId
            amount = $InitialBalance
            balance = [decimal]$response.data.balance
            applied = [bool]$response.data.applied
        })
    }

    $perAccount = [int]($Adjustments / $accounts.Count)

    foreach ($account in $accounts) {
        for ($sequence = 1; $sequence -le $perAccount; $sequence++) {
            $amount = [decimal](Get-SpikeAmount $sequence)
            $type = if ($amount -ge 0) { "credit" } else { "debit" }
            $operationId = [Guid]::NewGuid()
            $description = "spike|batch=$batchId|account=$account|seq=$sequence|type=$type"
            $body = New-AdjustmentBody $account $operationId $amount $description

            $expectedBalances[$account] = [decimal]$expectedBalances[$account] + $amount
            $plannedAdjustments.Add([pscustomobject]@{
                phase = "main"
                kind = "adjustment"
                method = "POST"
                uri = $adjustUri
                account = $account
                sequence = $sequence
                operationId = $operationId
                amount = $amount
                expectedStatus = 200
                expectedApplied = $true
                body = $body
            })
        }
    }

    $queryCount = [int][Math]::Floor($Adjustments * ($BalanceQueryRatioPercent / 100))
    $plannedQueries = New-Object System.Collections.Generic.List[object]
    for ($i = 1; $i -le $queryCount; $i++) {
        $account = $accounts[($i - 1) % $accounts.Count]
        $plannedQueries.Add([pscustomobject]@{
            phase = "main"
            kind = "balance-query"
            method = "GET"
            uri = "$BaseUrl/api/v1/balances/$account"
            account = $account
            sequence = $i
            operationId = $null
            amount = $null
            expectedStatus = 200
            expectedApplied = $null
            body = $null
        })
    }

    $mainPlan = @($plannedAdjustments.ToArray() + $plannedQueries.ToArray()) | Sort-Object { Get-Random }
    $mainResults = @(Invoke-PlannedRequests $mainPlan $Concurrency)

    Assert-NoItems @($mainResults | Where-Object { -not $_.ok }) "Unexpected transport failures during main spike."
    Assert-NoItems @($mainResults | Where-Object { $_.status -ne $_.expectedStatus }) "Unexpected HTTP status during main spike."
    Assert-NoItems @($mainResults | Where-Object { $_.kind -eq "adjustment" -and $_.applied -ne $true }) "A main adjustment was not applied exactly once."

    $replayCount = [int][Math]::Floor($Adjustments * ($ReplayRatioPercent / 100))
    $replayPlan = New-Object System.Collections.Generic.List[object]
    $replaySources = $plannedAdjustments | Sort-Object account, sequence | Select-Object -First $replayCount
    $replaySequence = 0

    foreach ($source in $replaySources) {
        $replaySequence++
        $replayPlan.Add([pscustomobject]@{
            phase = "replay"
            kind = "adjustment-replay"
            method = "POST"
            uri = $adjustUri
            account = $source.account
            sequence = $replaySequence
            operationId = $source.operationId
            amount = $source.amount
            expectedStatus = 200
            expectedApplied = $false
            body = $source.body
        })
    }

    $replayQueryCount = [Math]::Max(1, [int][Math]::Floor($replayCount / 3))
    for ($i = 1; $i -le $replayQueryCount; $i++) {
        $account = $accounts[($i - 1) % $accounts.Count]
        $replayPlan.Add([pscustomobject]@{
            phase = "replay"
            kind = "balance-query"
            method = "GET"
            uri = "$BaseUrl/api/v1/balances/$account"
            account = $account
            sequence = $i
            operationId = $null
            amount = $null
            expectedStatus = 200
            expectedApplied = $null
            body = $null
        })
    }

    $replayResults = @(Invoke-PlannedRequests (@($replayPlan.ToArray()) | Sort-Object { Get-Random }) $Concurrency)

    Assert-NoItems @($replayResults | Where-Object { -not $_.ok }) "Unexpected transport failures during replay spike."
    Assert-NoItems @($replayResults | Where-Object { $_.status -ne $_.expectedStatus }) "Unexpected HTTP status during replay spike."
    Assert-NoItems @($replayResults | Where-Object { $_.kind -eq "adjustment-replay" -and $_.applied -ne $false }) "An intentional replay was applied again."

    $finalAssertions = New-Object System.Collections.Generic.List[object]
    foreach ($account in $accounts) {
        $balanceResponse = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/balances/$account" -Headers $script:authHeader
        $statementResponse = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/balances/$account/statement?page=1&pageSize=1" -Headers $script:authHeader

        $actualBalance = [decimal]$balanceResponse.data.balance
        $expectedBalance = [decimal]$expectedBalances[$account]
        $expectedMovements = $perAccount + 1
        $actualMovements = [int64]$statementResponse.data.totalItems

        if ($actualBalance -ne $expectedBalance) {
            throw "Unexpected final balance for $account. Expected $expectedBalance, got $actualBalance."
        }

        if ($actualMovements -ne $expectedMovements) {
            throw "Unexpected movement count for $account. Expected $expectedMovements, got $actualMovements."
        }

        $finalAssertions.Add([pscustomobject]@{
            account = $account
            expectedBalance = $expectedBalance
            actualBalance = $actualBalance
            expectedMovements = $expectedMovements
            actualMovements = $actualMovements
        })
    }

    $allResults = @($mainResults + $replayResults)
    $elapsedValues = [long[]]@($allResults | ForEach-Object { [long]$_.elapsedMs })
    $finishedAt = Get-Date
    $durationSeconds = [Math]::Max(0.001, ($finishedAt - $startedAt).TotalSeconds)

    $summary = [pscustomobject]@{
        batchId = $batchId
        baseUrl = $BaseUrl
        accounts = $accounts
        uniqueAdjustments = $Adjustments
        seedAdjustments = $accounts.Count
        balanceQueries = $queryCount + $replayQueryCount
        intentionalReplays = $replayCount
        concurrency = $Concurrency
        requests = $allResults.Count + $seedResults.Count
        durationSeconds = [Math]::Round($durationSeconds, 2)
        throughput = [Math]::Round((($allResults.Count + $seedResults.Count) / $durationSeconds), 2)
        unexpectedFailures = @($allResults | Where-Object { -not $_.ok -or $_.status -ne $_.expectedStatus }).Count
        p95Ms = Get-Percentile $elapsedValues 0.95
        p99Ms = Get-Percentile $elapsedValues 0.99
        assertions = "passed"
        finalAccounts = $finalAssertions
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $summaryPath = Join-Path $outputPath "$timestamp-$batchId-summary.json"
    $requestsPath = Join-Path $outputPath "$timestamp-$batchId-requests.json"
    $apiLogPath = Join-Path $outputPath "$timestamp-$batchId-api.log"
    $postgresLogPath = Join-Path $outputPath "$timestamp-$batchId-postgres.log"

    $summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath -Encoding UTF8
    @($seedResults + $allResults) | ConvertTo-Json -Depth 8 | Set-Content -Path $requestsPath -Encoding UTF8

    $since = $startedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    $apiLogs = @(docker logs BalanceControl-local-api --since $since 2>&1)
    $postgresLogs = @(docker logs BalanceControl-local-postgres --since $since 2>&1)

    Set-Content -Path $apiLogPath -Value $apiLogs -Encoding UTF8
    Set-Content -Path $postgresLogPath -Value $postgresLogs -Encoding UTF8

    $apiLog = (Get-Content -Path $apiLogPath -Raw) ?? ""
    $postgresLog = (Get-Content -Path $postgresLogPath -Raw) ?? ""
    $apiBadPatterns = [regex]::Matches(
        $apiLog,
        "(?i)\b(Exception|Unhandled|deadlock|timeout)\b|""http\.response\.status_code"":500|""StatusCode"":500|responded 500")
    $postgresBadPatterns = [regex]::Matches($postgresLog, "(?i)\b(ERROR|FATAL|PANIC|deadlock)\b")

    if ($apiBadPatterns.Count -gt 0) {
        throw "API logs contain $($apiBadPatterns.Count) suspicious entries. See $apiLogPath."
    }

    if ($postgresBadPatterns.Count -gt 0) {
        throw "PostgreSQL logs contain $($postgresBadPatterns.Count) suspicious entries. See $postgresLogPath."
    }

    $summary | Add-Member -NotePropertyName summaryPath -NotePropertyValue $summaryPath
    $summary | Add-Member -NotePropertyName requestsPath -NotePropertyValue $requestsPath
    $summary | Add-Member -NotePropertyName apiLogPath -NotePropertyValue $apiLogPath
    $summary | Add-Member -NotePropertyName postgresLogPath -NotePropertyValue $postgresLogPath
    $summary | Add-Member -NotePropertyName apiSuspiciousLogEntries -NotePropertyValue $apiBadPatterns.Count
    $summary | Add-Member -NotePropertyName postgresSuspiciousLogEntries -NotePropertyValue $postgresBadPatterns.Count

    $summary
}
finally {
    Pop-Location
}
