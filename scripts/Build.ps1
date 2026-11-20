$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location "$repoRoot/src/frontend"
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
} finally { Pop-Location }
$webRoot = Join-Path $repoRoot 'src/backend/GitCosmos.API/wwwroot'
New-Item -ItemType Directory -Force $webRoot | Out-Null
Copy-Item -Path "$repoRoot/src/frontend/dist/*" -Destination $webRoot -Recurse -Force
Push-Location $repoRoot
try {
    dotnet build GitCosmos.slnx --no-restore -m:1 /p:UseAppHost=false
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
} finally { Pop-Location }
