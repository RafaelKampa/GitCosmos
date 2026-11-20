$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    dotnet test tests/GitCosmos.Application.Tests/GitCosmos.Application.Tests.csproj --no-restore -m:1 /p:UseAppHost=false --collect:'XPlat Code Coverage' --settings tests/GitCosmos.Application.Tests/coverage.runsettings --results-directory artifacts/coverage/backend
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    & "$PSScriptRoot/Check-Coverage.ps1"
    Push-Location src/frontend
    try {
        npm run test:coverage
        if ($LASTEXITCODE -ne 0) { throw 'Frontend tests or coverage failed.' }
    } finally { Pop-Location }
} finally { Pop-Location }
