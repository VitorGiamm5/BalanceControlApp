param(
    [string]$BaseUrl = "http://localhost:9005",
    [int]$Users = 10,
    [int]$Operations = 1000,
    [int]$Concurrency = 50,
    [decimal]$Amount = 1.00,
    [double]$ReplayRatio = 0.10,
    [string]$ClientId = "balance-client",
    [string]$ClientSecret = "balance-secret"
)

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "This script requires PowerShell 7 or newer because it uses ForEach-Object -Parallel."
}

if ($Users -lt 1) { throw "Users must be greater than zero." }
if ($Operations -lt 1) { throw "Operations must be greater than zero." }
if ($Concurrency -lt 1) { throw "Concurrency must be greater than zero." }
if ($ReplayRatio -lt 0 -or $ReplayRatio -gt 1) { throw "ReplayRatio must be between 0 and 1." }

$adjustUri = "$BaseUrl/api/v1/balances/adjustments"
$tokenResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/api/v1/auth/token" `
    -Body (@{ clientId = $ClientId; clientSecret = $ClientSecret } | ConvertTo-Json) `
    -ContentType "application/json"
$authHeader = @{ Authorization = "Bearer $($tokenResponse.data.accessToken)" }
$prefix = "stress-" + [Guid]::NewGuid().ToString("N")
$planned = New-Object System.Collections.Generic.List[object]

for ($i = 0; $i -lt $Operations; $i++) {
    $planned.Add([pscustomobject]@{
        userId = "$prefix-user-" + ($i % $Users)
        operationId = [Guid]::NewGuid().ToString()
        amount = $Amount
        description = "stress operation"
    })
}

$replayCount = [int][Math]::Floor($Operations * $ReplayRatio)
for ($i = 0; $i -lt $replayCount; $i++) {
    $planned.Add($planned[$i])
}

$started = Get-Date
$results = $planned | ForEach-Object -Parallel {
    $body = $_ | ConvertTo-Json -Depth 8
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-RestMethod -Method Post -Uri $using:adjustUri -Body $body -ContentType "application/json" -Headers $using:authHeader
        $sw.Stop()
        [pscustomobject]@{
            ok = $true
            status = 200
            elapsedMs = $sw.ElapsedMilliseconds
            applied = [bool]$response.data.applied
            userId = $_.userId
        }
    }
    catch {
        $sw.Stop()
        $statusCode = 0
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        [pscustomobject]@{
            ok = $false
            status = $statusCode
            elapsedMs = $sw.ElapsedMilliseconds
            applied = $false
            userId = $_.userId
        }
    }
} -ThrottleLimit $Concurrency

$finished = Get-Date
$ordered = $results | Sort-Object elapsedMs
$count = @($ordered).Count
$p95Index = [Math]::Min($count - 1, [Math]::Floor($count * 0.95))
$p99Index = [Math]::Min($count - 1, [Math]::Floor($count * 0.99))
$durationSeconds = [Math]::Max(0.001, ($finished - $started).TotalSeconds)

[pscustomobject]@{
    requests = $count
    users = $Users
    concurrency = $Concurrency
    durationSeconds = [Math]::Round($durationSeconds, 2)
    throughput = [Math]::Round($count / $durationSeconds, 2)
    success = @($results | Where-Object ok).Count
    failed = @($results | Where-Object { -not $_.ok }).Count
    applied = @($results | Where-Object applied).Count
    replayed = @($results | Where-Object { $_.ok -and -not $_.applied }).Count
    p95Ms = $ordered[$p95Index].elapsedMs
    p99Ms = $ordered[$p99Index].elapsedMs
}
