# Valida backend/.env antes do publish (Mercado Pago, URLs, Supabase).
# Uso: .\scripts\validate-env.ps1

param(
    [string]$EnvFile = (Join-Path $PSScriptRoot "..\.env")
)

function Read-EnvValue {
    param([string]$Content, [string]$Key)
    if ($Content -match "(?m)^\s*$([regex]::Escape($Key))\s*=\s*(.+)\s*$") {
        $v = $Matches[1].Trim()
        if ($v.Length -ge 2 -and $v.StartsWith('"') -and $v.EndsWith('"')) { $v = $v.Substring(1, $v.Length - 2) }
        return $v
    }
    return $null
}

if (-not (Test-Path $EnvFile)) {
    Write-Error @"
Arquivo backend/.env não encontrado.

  1. Copie:  copy ENV_EXAMPLE.env .env
  2. Edite backend/.env com suas chaves reais
  3. Rode o publish de novo
"@
    exit 1
}

$content = Get-Content $EnvFile -Raw -Encoding UTF8
$errors = @()
$warnings = @()

$mpMode = Read-EnvValue $content "MERCADOPAGO_MODE"
if ([string]::IsNullOrWhiteSpace($mpMode)) { $mpMode = "test" }
$mpTest = Read-EnvValue $content "MERCADOPAGO_ACCESS_TOKEN_TEST"
$mpProd = Read-EnvValue $content "MERCADOPAGO_ACCESS_TOKEN_PRODUCTION"
$publicApi = Read-EnvValue $content "PUBLIC_API_URL"
$frontend = Read-EnvValue $content "FRONTEND_URL"
$supabaseUrl = Read-EnvValue $content "SUPABASE_URL"
$supabaseKey = Read-EnvValue $content "SUPABASE_SERVICE_ROLE_KEY"

if ([string]::IsNullOrWhiteSpace($supabaseUrl) -or $supabaseUrl -match "seu-projeto") {
    $errors += "SUPABASE_URL inválido ou placeholder em backend/.env"
}
if ([string]::IsNullOrWhiteSpace($supabaseKey) -or $supabaseKey -match "sua_service_role") {
    $errors += "SUPABASE_SERVICE_ROLE_KEY inválido ou placeholder em backend/.env"
}

if ($mpMode -eq "production") {
    if ([string]::IsNullOrWhiteSpace($mpProd) -or $mpProd -match "seu-access-token|APP_USR-seu") {
        $errors += "MERCADOPAGO_MODE=production exige MERCADOPAGO_ACCESS_TOKEN_PRODUCTION com token REAL de produção (Developers → Credenciais de produção)."
    }
    if ([string]::IsNullOrWhiteSpace($publicApi)) {
        $errors += "MERCADOPAGO_MODE=production exige PUBLIC_API_URL=https://api.seudominio.com.br"
    }
    if ($frontend -match "localhost") {
        $warnings += "FRONTEND_URL ainda aponta para localhost — links de retorno do checkout podem falhar."
    }
    if ($mpTest -and $mpProd -and ($mpTest -eq $mpProd)) {
        $errors += "MERCADOPAGO_ACCESS_TOKEN_TEST e MERCADOPAGO_ACCESS_TOKEN_PRODUCTION são iguais — o de produção deve ser outro token."
    }
}
else {
    if ([string]::IsNullOrWhiteSpace($mpTest) -and [string]::IsNullOrWhiteSpace($mpProd)) {
        $warnings += "Nenhum token Mercado Pago definido. Defina MERCADOPAGO_ACCESS_TOKEN_TEST para sandbox."
    }
}

foreach ($w in $warnings) { Write-Warning $w }
foreach ($e in $errors) { Write-Error $e }

if ($errors.Count -gt 0) { exit 1 }

Write-Host "OK: backend/.env validado (MERCADOPAGO_MODE=$mpMode)." -ForegroundColor Green
if ($mpMode -eq "test") {
    Write-Host "  Sandbox: use cartão 5031 4332 1540 6351, titular APRO. PIX desabilitado em teste." -ForegroundColor DarkGray
}
