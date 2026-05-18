# Mercado Pago — Checkout Pro

## 1. Credenciais

1. Crie uma aplicação em [Mercado Pago Developers](https://www.mercadopago.com.br/developers).
2. Copie o **Access Token** (teste ou produção) para o `backend/.env`:

```env
MERCADOPAGO_ACCESS_TOKEN=APP_USR-...
PUBLIC_API_URL=https://sua-api.com.br
```

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
