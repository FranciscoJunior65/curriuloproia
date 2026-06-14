# Build + publish — o backend/.env do localhost vai junto como .env.
# Uso: .\scripts\publish-production.ps1

param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\publish")
)

$ErrorActionPreference = "Stop"
$backendRoot = Join-Path $PSScriptRoot ".."
$project = Join-Path $backendRoot "src\CurriculosProIA.Api\CurriculosProIA.Api.csproj"

Write-Host "=== CurriculosPro IA — publish ===" -ForegroundColor Cyan
Write-Host ""

& (Join-Path $PSScriptRoot "validate-env.ps1")

Write-Host ""
Write-Host "Publicando Release..." -ForegroundColor Cyan
dotnet publish $project -c Release -o $OutputDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Pronto. Suba a pasta para o servidor:" -ForegroundColor Green
Write-Host "  $OutputDir"
Write-Host ""
Write-Host "O .env e o mesmo backend/.env que voce usa no localhost."
Write-Host "Depois: reinicie o app pool e teste GET /api/test/mercadopago" -ForegroundColor DarkGray
