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

$paymentProvider = Read-EnvValue $content "PAYMENT_PROVIDER"
if ([string]::IsNullOrWhiteSpace($paymentProvider)) { $paymentProvider = "stripe" }

$caktoClientId = Read-EnvValue $content "CAKTO_CLIENT_ID"
$caktoSecret = Read-EnvValue $content "CAKTO_CLIENT_SECRET"
$caktoProduct = Read-EnvValue $content "CAKTO_PRODUCT_ID"
$caktoOffer = Read-EnvValue $content "CAKTO_OFFER_ID"
$caktoWebhookSecret = Read-EnvValue $content "CAKTO_WEBHOOK_SECRET"

if ($paymentProvider -eq "cakto") {
    if ([string]::IsNullOrWhiteSpace($caktoClientId) -or [string]::IsNullOrWhiteSpace($caktoSecret)) {
        $errors += "PAYMENT_PROVIDER=cakto exige CAKTO_CLIENT_ID e CAKTO_CLIENT_SECRET."
    }
    if ([string]::IsNullOrWhiteSpace($caktoProduct) -or [string]::IsNullOrWhiteSpace($caktoOffer)) {
        $errors += "PAYMENT_PROVIDER=cakto exige CAKTO_PRODUCT_ID e CAKTO_OFFER_ID."
    }
    if ([string]::IsNullOrWhiteSpace($caktoWebhookSecret)) {
        $warnings += "CAKTO_WEBHOOK_SECRET vazio — webhook purchase_approved será aceito sem validar secret."
    }
    if ([string]::IsNullOrWhiteSpace($publicApi)) {
        $errors += "PAYMENT_PROVIDER=cakto em produção exige PUBLIC_API_URL (ex.: https://api.curriculoproia.com.br)."
    }
    if ($publicApi) {
        $expectedWebhook = "$($publicApi.TrimEnd('/'))/api/analyze/payment/cakto/webhook"
        Write-Host "  Cakto webhook (painel): $expectedWebhook" -ForegroundColor DarkGray
    }
}

$kiwifyApiKey = Read-EnvValue $content "KIWIFY_API_KEY"
$kiwifySecret = Read-EnvValue $content "KIWIFY_CLIENT_SECRET"
$kiwifyAccount = Read-EnvValue $content "KIWIFY_ACCOUNT_ID"
$kiwifyCheckoutSingle = Read-EnvValue $content "KIWIFY_CHECKOUT_SINGLE"
$kiwifyWebhookToken = Read-EnvValue $content "KIWIFY_WEBHOOK_TOKEN"

if ($paymentProvider -eq "kiwify") {
    if ([string]::IsNullOrWhiteSpace($kiwifyApiKey) -or [string]::IsNullOrWhiteSpace($kiwifySecret) -or [string]::IsNullOrWhiteSpace($kiwifyAccount)) {
        $errors += "PAYMENT_PROVIDER=kiwify exige KIWIFY_API_KEY, KIWIFY_CLIENT_SECRET e KIWIFY_ACCOUNT_ID."
    }
    if ([string]::IsNullOrWhiteSpace($kiwifyCheckoutSingle)) {
        $warnings += "KIWIFY_CHECKOUT_SINGLE vazio — configure links pay.kiwify.com.br para cada plano."
    }
    if ([string]::IsNullOrWhiteSpace($kiwifyWebhookToken)) {
        $warnings += "KIWIFY_WEBHOOK_TOKEN vazio — webhook compra_aprovada será aceito sem validar token."
    }
    if ([string]::IsNullOrWhiteSpace($publicApi)) {
        $errors += "PAYMENT_PROVIDER=kiwify em produção exige PUBLIC_API_URL (ex.: https://api.curriculoproia.com.br)."
    }
    if ($publicApi) {
        $expectedWebhook = "$($publicApi.TrimEnd('/'))/api/analyze/payment/kiwify/webhook"
        Write-Host "  Kiwify webhook (painel): $expectedWebhook" -ForegroundColor DarkGray
    }
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

Write-Host "OK: backend/.env validado (PAYMENT_PROVIDER=$paymentProvider, MERCADOPAGO_MODE=$mpMode)." -ForegroundColor Green
if ($mpMode -eq "test") {
    Write-Host "  Sandbox: use cartão 5031 4332 1540 6351, titular APRO. PIX desabilitado em teste." -ForegroundColor DarkGray
}
