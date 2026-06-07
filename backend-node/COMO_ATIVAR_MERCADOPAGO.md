# Mercado Pago — Checkout Pro

## 1. Credenciais

1. Crie uma aplicação em [Mercado Pago Developers](https://www.mercadopago.com.br/developers).
2. No `backend/.env`, configure **teste** e **produção** e escolha o modo ativo:

```env
# test = sandbox | production = pagamentos reais
MERCADOPAGO_MODE=test

MERCADOPAGO_ACCESS_TOKEN_TEST=APP_USR-...
MERCADOPAGO_PUBLIC_KEY_TEST=APP_USR-...

MERCADOPAGO_ACCESS_TOKEN_PRODUCTION=APP_USR-...
MERCADOPAGO_PUBLIC_KEY_PRODUCTION=APP_USR-...

PUBLIC_API_URL=https://sua-api.com.br
```

Para ir ao ar: `MERCADOPAGO_MODE=production` (cobrança real).

## 2. Banco (Supabase)

Execute o SQL em `CRIAR_TABELA_CONFIGURACOES.sql` no editor SQL do Supabase.

## 3. Webhook (IPN)

No painel do Mercado Pago, configure a URL de notificações:

```
https://sua-api.com.br/api/analyze/payment/mercadopago/webhook
```

## 4. Ativar no admin

1. Acesse `/admin` com usuário `tipo_usuario = admin`.
2. Em **Meio de pagamento**, selecione **Mercado Pago** e clique em **Salvar**.

Alternativa via `.env` (fallback se a tabela não existir):

```env
PAYMENT_PROVIDER=mercadopago
```

## 5. Teste local

Use credenciais de **teste** e contas de teste do Mercado Pago. Para webhooks locais, use ngrok apontando para a porta do backend.

### Testar integração (endpoint)

```http
GET http://localhost:3000/api/test/mercadopago
```

Retorna modo (test/production), conta conectada, URL do webhook e se está configurada com HTTPS.
