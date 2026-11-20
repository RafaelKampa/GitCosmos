param([string]$ReportDirectory = "$PSScriptRoot/../artifacts/coverage/backend")
$ErrorActionPreference = 'Stop'
$report = Get-ChildItem -LiteralPath $ReportDirectory -Filter coverage.cobertura.xml -Recurse |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $report) { throw 'Backend coverage report is missing.' }
[xml]$coverage = Get-Content -Raw -LiteralPath $report.FullName
$lineRate = [double]::Parse($coverage.coverage.'line-rate', [Globalization.CultureInfo]::InvariantCulture)
$branchRate = [double]::Parse($coverage.coverage.'branch-rate', [Globalization.CultureInfo]::InvariantCulture)
Write-Output ('Backend coverage: lines {0:P2}; branches {1:P2}' -f $lineRate, $branchRate)
if ($lineRate -lt 0.95 -or $branchRate -lt 0.95) { throw 'Backend unit coverage must be at least 95% for lines and branches.' }
