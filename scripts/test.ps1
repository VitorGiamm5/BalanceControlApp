param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root

try {
    if (-not $SkipBuild) {
        dotnet build .\BalanceControlApi.slnx -c $Configuration
    }

    dotnet test .\tests\BalanceControl.UnitTests\BalanceControl.UnitTests.csproj -c $Configuration --no-build --filter "FullyQualifiedName~Balances"
    dotnet test .\tests\BalanceControl.IntegrationTests\BalanceControl.IntegrationTests.csproj -c $Configuration --no-build --filter "FullyQualifiedName~Balances"
    dotnet test .\tests\BalanceControl.FunctionalTests\BalanceControl.FunctionalTests.csproj -c $Configuration --no-build --filter "FullyQualifiedName~BalanceSmokeTests"
}
finally {
    Pop-Location
}
