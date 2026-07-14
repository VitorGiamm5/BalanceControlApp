param(
    [string]$BaseUrl = "http://localhost:9005",
    [string]$ClientId = "balance-client",
    [string]$ClientSecret = "balance-secret"
)

$ErrorActionPreference = "Stop"

$userId = "idempotency-" + [Guid]::NewGuid().ToString("N")
$operationId = [Guid]::NewGuid().ToString()
$adjustUri = "$BaseUrl/api/v1/balances/adjustments"
$balanceUri = "$BaseUrl/api/v1/balances/$userId"
$statementUri = "$BaseUrl/api/v1/balances/$userId/statement?page=1&pageSize=10"

function Invoke-JsonPost {
    param(
        [string]$Uri,
        [object]$Body,
        [switch]$AllowHttpError
    )

    $json = $Body | ConvertTo-Json -Depth 8

    if ($AllowHttpError) {
        return Invoke-WebRequest `
            -Method Post `
            -Uri $Uri `
            -Body $json `
            -ContentType "application/json" `
            -Headers $script:AuthHeader `
            -SkipHttpErrorCheck
    }

    Invoke-RestMethod `
        -Method Post `
        -Uri $Uri `
        -Body $json `
        -ContentType "application/json" `
        -Headers $script:AuthHeader
}

$tokenResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/api/v1/auth/token" `
    -Body (@{ clientId = $ClientId; clientSecret = $ClientSecret } | ConvertTo-Json) `
    -ContentType "application/json"

$script:AuthHeader = @{ Authorization = "Bearer $($tokenResponse.data.accessToken)" }

$originalPayload = @{
    userId = $userId
    operationId = $operationId
    amount = 100.50
    description = "idempotency validation credit"
}

$first = Invoke-JsonPost $adjustUri $originalPayload
$replay = Invoke-JsonPost $adjustUri $originalPayload

$conflictResponse = Invoke-JsonPost $adjustUri @{
    userId = $userId
    operationId = $operationId
    amount = 101.00
    description = "changed idempotency validation credit"
} -AllowHttpError

$balance = Invoke-RestMethod -Method Get -Uri $balanceUri -Headers $script:AuthHeader
$statement = Invoke-RestMethod -Method Get -Uri $statementUri -Headers $script:AuthHeader

if ($first.data.applied -ne $true) {
    throw "First adjustment was not applied."
}

if ($replay.data.applied -ne $false) {
    throw "Replay was applied again."
}

if ($first.data.movementId -ne $replay.data.movementId) {
    throw "Replay did not return the original movementId."
}

if ($conflictResponse.StatusCode -ne 409) {
    throw "Expected conflict 409, got HTTP $($conflictResponse.StatusCode)."
}

if ([decimal]$balance.data.balance -ne 100.50) {
    throw "Unexpected balance after replay and conflict: $($balance.data.balance)."
}

if ([int]$statement.data.totalItems -ne 1) {
    throw "Unexpected statement totalItems: $($statement.data.totalItems)."
}

[pscustomobject]@{
    userId = $userId
    operationId = $operationId
    firstApplied = $first.data.applied
    replayApplied = $replay.data.applied
    sameMovementId = ($first.data.movementId -eq $replay.data.movementId)
    conflictStatusCode = $conflictResponse.StatusCode
    finalBalance = $balance.data.balance
    statementItems = $statement.data.totalItems
}
