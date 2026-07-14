param(
    [switch]$Volumes
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root

try {
    Write-Host "Docker stack: BalanceControl-local"

    if ($Volumes) {
        docker compose down -v
    }
    else {
        docker compose down
    }
}
finally {
    Pop-Location
}
