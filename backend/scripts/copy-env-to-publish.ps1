# Copia o .env local para a pasta de publish (deploy IIS/Plesk).
# Uso: .\scripts\copy-env-to-publish.ps1 -PublishDir "C:\caminho\publish"

param(
    [string]$PublishDir = (Join-Path $PSScriptRoot "..\publish")
)

$source = Join-Path $PSScriptRoot "..\.env"
if (-not (Test-Path $source)) {
    $source = Join-Path $PSScriptRoot "..\..\backend-node\.env"
}

if (-not (Test-Path $source)) {
    Write-Error "Nenhum .env encontrado. Crie backend/.env antes do deploy."
    exit 1
}

if (-not (Test-Path $PublishDir)) {
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
}

$destEnv = Join-Path $PublishDir ".env"
$destApp = Join-Path $PublishDir "app.env"
Copy-Item $source $destEnv -Force
Copy-Item $source $destApp -Force
Write-Host "Copiado para:"
Write-Host "  $destEnv"
Write-Host "  $destApp"
