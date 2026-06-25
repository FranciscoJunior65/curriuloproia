# Como ativar a Cakto (PIX + cartão)

Integração transparente: o CurriculosPro orquestra checkout, preço e liberação de créditos; a Cakto processa o pagamento.

Documentação oficial: https://docs.cakto.com.br/introduction

## 1. Escopos da chave API (backend)

No painel Cakto → Integrações → Cakto API → Criar Chave API, marque:

| Checkbox | Obrigatório |
|----------|-------------|
| Leitura | Sim |
| Escrita | Sim |
| Pagamentos | Sim |
| Pedidos | Sim |
| Ofertas | Sim (preço dinâmico antes de cada cobrança) |
| Webhooks | Sim (se registrar webhook via API; opcional se usar só o painel) |

## 2. Chave SDK (frontend — só `client_id`)

Integrações → SDK → criar chave com:

- Leitura
- Escrita
- Tokenização de Cartão

**Nunca** coloque o `client_secret` no frontend.

## 3. Produto e oferta no painel

1. Crie um produto genérico (ex.: "Créditos CurriculosPro").
2. Crie **uma oferta ativa** (preço inicial qualquer — o backend sincroniza antes de cada cobrança).
3. Anote `CAKTO_PRODUCT_ID` e `CAKTO_OFFER_ID` (short_id ou UUID).

## 4. Webhook (servidor — não é a página do site)

O webhook aponta para a **API backend**, nunca para o frontend ou popup de pagamento.

Integrações → Webhooks → Adicionar:

- **URL (produção):** `https://api.curriculoproia.com.br/api/analyze/payment/cakto/webhook`
- **Evento:** Compra aprovada (`purchase_approved`)
- **Produto:** o produto criado acima
- Defina um **secret** forte → `CAKTO_WEBHOOK_SECRET` no `.env` (mesmo valor nos dois lados)

Em localhost use ngrok apontando para a API (`https://xxxx.ngrok.io/api/analyze/payment/cakto/webhook`).

A popup PIX na página principal **não recebe** o webhook. Ela confirma o pagamento por **polling** (`GET /api/analyze/payment/verify`) a cada 4s. Webhook e polling liberam créditos de forma idempotente (sem duplicar).

## 5. Variáveis no `backend/.env`

```env
PAYMENT_PROVIDER=cakto

CAKTO_CLIENT_ID=seu_client_id_api
CAKTO_CLIENT_SECRET=seu_client_secret_api
CAKTO_SDK_CLIENT_ID=seu_client_id_sdk
CAKTO_PRODUCT_ID=short_id_ou_uuid_do_produto
CAKTO_OFFER_ID=short_id_da_oferta
CAKTO_WEBHOOK_SECRET=secret_configurado_no_webhook

PUBLIC_API_URL=https://api.curriculoproia.com.br
```

## 6. Ativar no admin

Painel admin → **Meio de pagamento** → selecione **Cakto** → Salvar → **Testar conexão**.

## 7. Fluxo resumido

1. Usuário escolhe plano → modal Cakto (PIX ou cartão).
2. Backend calcula valor (cupom/descontos), atualiza preço da oferta na Cakto, cria cobrança.
3. PIX: exibe QR + polling na popup; cartão: SDK tokeniza + 3DS → backend cobra.
4. Popup fecha sozinha quando `paid=true`; header atualiza créditos; `/financeiro` lista a compra ao abrir o menu.
5. Webhook `purchase_approved` (backup) → libera créditos via `PaymentFulfillmentService` se o polling não rodar.

## 8. Checklist deploy produção

1. Publicar backend com fixes Cakto (payload PIX sem `productId`/`antifraudProfilingAttemptReference`).
2. `.env` no servidor: `CAKTO_*`, `CAKTO_WEBHOOK_SECRET`, `PUBLIC_API_URL=https://api.curriculoproia.com.br`.
3. Painel Cakto: webhook URL + evento `purchase_approved` + secret igual ao `.env`.
4. Admin: provedor **Cakto** salvo no Supabase.
5. Teste: comprar via PIX → popup fecha em ~4–8s → créditos no header → Menu Financeiro mostra a compra.

## 9. Endpoints

- `POST /api/analyze/payment/create-session` — inicia checkout (provider `cakto`)
- `POST /api/analyze/payment/cakto/pix` — gera QR PIX
- `POST /api/analyze/payment/cakto/card` — cobrança cartão 3DS
- `GET /api/analyze/payment/verify?sessionId={orderId}&provider=cakto` — polling
- `POST /api/analyze/payment/cakto/webhook` — confirmação assíncrona
