# Legado — o publish ja inclui backend/.env. Prefira: .\scripts\publish-production.ps1

param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\publish")
)

$source = Join-Path $PSScriptRoot "..\.env"
if (-not (Test-Path $source)) {
    Write-Error "Crie backend/.env (mesmo arquivo do localhost)."
    exit 1
}

if (-not (Test-Path $PublishDir)) {
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
}

Copy-Item $source (Join-Path $PublishDir ".env") -Force
Write-Host "backend/.env -> $PublishDir\.env"
