param(
    [string]$Configuration = "Debug",
    [decimal]$Threshold = 80
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

function Should-IncludeCoverageFile {
    param([string]$FullPath)

    $normalized = $FullPath.Replace("/", "\")

    $targets = @(
        "\src\BalanceControl.Api\Controllers\Balances\",
        "\src\BalanceControl.Application\Business\Balances\",
        "\src\BalanceControl.Infrastructure\Database\Repositories\Balances\"
    )

    foreach ($target in $targets) {
        if ($normalized.Contains($target, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

Push-Location $root

try {
    Get-ChildItem -Path .\tests -Directory -Recurse -Filter TestResults |
        Remove-Item -Recurse -Force

    dotnet test .\BalanceControlApi.slnx `
        -c $Configuration `
        --settings .\tests\coverage.runsettings `
        --collect "XPlat Code Coverage"

    $coverageFiles = Get-ChildItem -Path .\tests -Recurse -Filter coverage.opencover.xml
    if ($coverageFiles.Count -eq 0) {
        throw "No coverage.opencover.xml files were generated."
    }

    $coverageByLine = @{}
    $includedFiles = New-Object System.Collections.Generic.HashSet[string]

    foreach ($file in $coverageFiles) {
        [xml]$coverage = Get-Content -Path $file.FullName -Raw
        $filesById = @{}

        foreach ($sourceFile in $coverage.SelectNodes("//File")) {
            $fullPath = [string]$sourceFile.fullPath
            if (Should-IncludeCoverageFile $fullPath) {
                $filesById[[string]$sourceFile.uid] = $fullPath
                [void]$includedFiles.Add($fullPath)
            }
        }

        foreach ($point in $coverage.SelectNodes("//SequencePoint")) {
            $fileId = [string]$point.fileid
            if (-not $filesById.ContainsKey($fileId)) {
                continue
            }

            $key = "{0}:{1}" -f $filesById[$fileId], $point.sl
            $wasVisited = [int]$point.vc -gt 0

            if (-not $coverageByLine.ContainsKey($key)) {
                $coverageByLine[$key] = $wasVisited
                continue
            }

            $coverageByLine[$key] = $coverageByLine[$key] -or $wasVisited
        }
    }

    if ($includedFiles.Count -eq 0) {
        throw "No files matched the coverage target."
    }

    $total = $coverageByLine.Count
    if ($total -eq 0) {
        throw "Coverage files did not contain sequence points for the target files."
    }

    $visited = @($coverageByLine.GetEnumerator() | Where-Object Value).Count
    $coveragePercent = [Math]::Round(($visited / $total) * 100, 2)

    Write-Host "Coverage target files:"
    $includedFiles | Sort-Object | ForEach-Object {
        Write-Host " - $_"
    }

    $result = [pscustomobject]@{
        lineCoverage = $coveragePercent
        visitedLines = $visited
        totalLines = $total
        targetFiles = $includedFiles.Count
        threshold = $Threshold
    }

    $result

    if ($coveragePercent -lt $Threshold) {
        throw "Line coverage $coveragePercent% is below threshold $Threshold%."
    }
}
finally {
    Pop-Location
}
