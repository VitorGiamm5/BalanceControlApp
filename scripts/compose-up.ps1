param(
    [switch]$Build
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root

try {
    if ($Build) {
        docker compose up --build -d
    }
    else {
        docker compose up -d
    }

    Write-Host "Docker stack: BalanceControl-local"
    Write-Host "Swagger: http://localhost:9005/swagger"
    Write-Host "Health:  http://localhost:9005/health"
}
finally {
    Pop-Location
}
