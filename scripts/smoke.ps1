param(
    [string]$BaseUrl = "http://localhost:9005",
    [string]$ClientId = "balance-client",
    [string]$ClientSecret = "balance-secret"
)

$ErrorActionPreference = "Stop"

$userId = "smoke-" + [Guid]::NewGuid().ToString("N")
$operationId = [Guid]::NewGuid().ToString()

function Invoke-JsonPost {
    param(
        [string]$Uri,
        [object]$Body
    )

    $json = $Body | ConvertTo-Json -Depth 8
    Invoke-RestMethod -Method Post -Uri $Uri -Body $json -ContentType "application/json" -Headers $script:AuthHeader
}

$tokenResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/api/v1/auth/token" `
    -Body (@{ clientId = $ClientId; clientSecret = $ClientSecret } | ConvertTo-Json) `
    -ContentType "application/json"
$script:AuthHeader = @{ Authorization = "Bearer $($tokenResponse.data.accessToken)" }

$adjustUri = "$BaseUrl/api/v1/balances/adjustments"
$balanceUri = "$BaseUrl/api/v1/balances/$userId"
$statementUri = "$BaseUrl/api/v1/balances/$userId/statement?page=1&pageSize=10"

$first = Invoke-JsonPost $adjustUri @{
    userId = $userId
    operationId = $operationId
    amount = 100.50
    description = "smoke credit"
}

$replay = Invoke-JsonPost $adjustUri @{
    userId = $userId
    operationId = $operationId
    amount = 100.50
    description = "smoke credit"
}

$second = Invoke-JsonPost $adjustUri @{
    userId = $userId
    operationId = [Guid]::NewGuid().ToString()
    amount = -40.50
    description = "smoke debit"
}

$balance = Invoke-RestMethod -Method Get -Uri $balanceUri -Headers $script:AuthHeader
$statement = Invoke-RestMethod -Method Get -Uri $statementUri -Headers $script:AuthHeader

if ($first.data.applied -ne $true) {
    throw "First adjustment was not applied."
}

if ($replay.data.applied -ne $false) {
    throw "Replay was applied again."
}

if ([decimal]$balance.data.balance -ne 60.00) {
    throw "Unexpected balance: $($balance.data.balance)"
}

if ([int]$statement.data.totalItems -ne 2) {
    throw "Unexpected statement totalItems: $($statement.data.totalItems)"
}

[pscustomobject]@{
    userId = $userId
    finalBalance = $balance.data.balance
    statementItems = $statement.data.totalItems
    replayApplied = $replay.data.applied
}
