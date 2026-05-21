$backendRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $backendRoot ".env"
$examplePath = Join-Path $backendRoot "ENV_EXAMPLE.env"

if (Test-Path $envPath) {
    Write-Host "backend/.env ja existe."
    exit 0
}

if (-not (Test-Path $examplePath)) {
    Write-Error "ENV_EXAMPLE.env nao encontrado em $backendRoot"
    exit 1
}

$content = Get-Content $examplePath -Raw
$content = $content -replace '(?m)^USE_MOCK_AI=false', 'USE_MOCK_AI=true'
Set-Content -Path $envPath -Value $content -NoNewline

Write-Host "Criado backend/.env a partir de ENV_EXAMPLE.env"
Write-Host "Edite SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY com os valores do painel Supabase."
Write-Host "Se o backend-node ja tiver .env funcionando, copie as mesmas variaveis para backend/.env"
