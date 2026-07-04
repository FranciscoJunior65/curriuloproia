param(
    [string]$ApiBaseUrl = "http://localhost:3000",
    [string]$AdminEmail,
    [string]$AdminPassword,
    [string]$AdminToken,
    [string]$CustomerEmail,
    [string]$UserId,
    [string]$PlanId,
    [Nullable[int]]$Credits,
    [Nullable[decimal]]$Price,
    [string]$PaymentMethod = "kiwify_manual",
    [string]$OrderId,
    [string]$PaymentId,
    [string]$Reason,
    [switch]$SendEmail
)

$ErrorActionPreference = "Stop"

function Join-ApiUrl {
    param(
        [string]$BaseUrl,
        [string]$Path
    )

    return ("{0}/{1}" -f $BaseUrl.TrimEnd('/'), $Path.TrimStart('/'))
}

function Get-HttpErrorDetails {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    $message = $ErrorRecord.Exception.Message
    $response = $ErrorRecord.Exception.Response

    if ($null -eq $response) {
        return $message
    }

    try {
        $stream = $response.GetResponseStream()
        if ($null -eq $stream) {
            return $message
        }

        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        if ([string]::IsNullOrWhiteSpace($body)) {
            return $message
        }

        return $body
    }
    catch {
        return $message
    }
}

function Get-AdminToken {
    param(
        [string]$BaseUrl,
        [string]$Email,
        [string]$Password,
        [string]$Token
    )

    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        return $Token.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($Email) -or [string]::IsNullOrWhiteSpace($Password)) {
        throw "Informe -AdminToken ou entao -AdminEmail e -AdminPassword."
    }

    $loginBody = @{
        email = $Email.Trim()
        password = $Password
    } | ConvertTo-Json

    try {
        $loginResponse = Invoke-RestMethod `
            -Method Post `
            -Uri (Join-ApiUrl $BaseUrl "api/auth/login") `
            -ContentType "application/json; charset=utf-8" `
            -Body $loginBody
    }
    catch {
        throw "Falha no login admin: $(Get-HttpErrorDetails $_)"
    }

    if (-not $loginResponse.success -or [string]::IsNullOrWhiteSpace($loginResponse.token)) {
        throw "Login admin nao retornou token valido."
    }

    return [string]$loginResponse.token
}

if ([string]::IsNullOrWhiteSpace($CustomerEmail) -and [string]::IsNullOrWhiteSpace($UserId)) {
    throw "Informe -CustomerEmail ou -UserId para localizar o cliente."
}

if ([string]::IsNullOrWhiteSpace($PlanId) -and (-not $Credits -or $Credits.Value -le 0)) {
    throw "Informe -PlanId (ex.: single, pack3, pack5) ou -Credits maior que zero."
}

if ([string]::IsNullOrWhiteSpace($PaymentId)) {
    if (-not [string]::IsNullOrWhiteSpace($OrderId)) {
        $PaymentId = "manual_payment_fix_{0}" -f $OrderId.Trim()
    }
    else {
        $PaymentId = "manual_payment_fix_{0}_{1}" -f (Get-Date -Format "yyyyMMddHHmmss"), ([guid]::NewGuid().ToString("N").Substring(0, 8))
        Write-Warning "Nenhum -OrderId informado. Foi gerado um paymentId unico; se rodar de novo, podera duplicar os creditos."
    }
}

if ([string]::IsNullOrWhiteSpace($Reason)) {
    if (-not [string]::IsNullOrWhiteSpace($OrderId)) {
        $Reason = "Credito manual por pagamento aprovado sem retorno automatico. orderId=$($OrderId.Trim())"
    }
    else {
        $Reason = "Credito manual por pagamento aprovado sem retorno automatico."
    }
}

$token = Get-AdminToken -BaseUrl $ApiBaseUrl -Email $AdminEmail -Password $AdminPassword -Token $AdminToken

$body = [ordered]@{
    paymentMethod = $PaymentMethod
    paymentId = $PaymentId
    reason = $Reason
    sendEmail = [bool]$SendEmail
}

if (-not [string]::IsNullOrWhiteSpace($CustomerEmail)) {
    $body.email = $CustomerEmail.Trim()
}

if (-not [string]::IsNullOrWhiteSpace($UserId)) {
    $body.userId = $UserId.Trim()
}

if (-not [string]::IsNullOrWhiteSpace($PlanId)) {
    $body.planId = $PlanId.Trim()
}

if ($Credits -and $Credits.Value -gt 0) {
    $body.credits = $Credits.Value
}

if ($Price) {
    $body.price = $Price.Value
}

$jsonBody = $body | ConvertTo-Json -Depth 6
$headers = @{
    Authorization = "Bearer $token"
}

try {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri (Join-ApiUrl $ApiBaseUrl "api/admin/credits/grant") `
        -Headers $headers `
        -ContentType "application/json; charset=utf-8" `
        -Body $jsonBody
}
catch {
    throw "Falha ao incluir credito manual: $(Get-HttpErrorDetails $_)"
}

Write-Host ""
Write-Host "Resultado:" -ForegroundColor Cyan
Write-Host ("  success: {0}" -f $response.success)
Write-Host ("  alreadyFulfilled: {0}" -f $response.alreadyFulfilled)
Write-Host ("  userId: {0}" -f $response.userId)
Write-Host ("  userEmail: {0}" -f $response.userEmail)
Write-Host ("  credits: {0}" -f $response.credits)
Write-Host ("  message: {0}" -f $response.message)
Write-Host ("  paymentId usado: {0}" -f $PaymentId)

Write-Host ""
Write-Host "Resposta completa:" -ForegroundColor DarkGray
$response | ConvertTo-Json -Depth 6
