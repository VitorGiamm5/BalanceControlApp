param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root

try {
    dotnet build .\BalanceControlApi.slnx -c $Configuration
}
finally {
    Pop-Location
}
