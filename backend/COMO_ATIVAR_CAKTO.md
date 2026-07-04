# Como ativar a Cakto (checkout hospedado)

Integração via **checkout hospedado** (`pay.cakto.com.br`): PIX e cartão na página da Cakto. O site sincroniza o **preço base** da oferta; a Cakto exibe a **taxa de serviço** no resumo antes do total.

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

## 2. Chave SDK (opcional — cartão não usa mais SDK no site)

Integrações → SDK → criar chave (só necessária se voltar ao checkout transparente de cartão).

**Nunca** coloque o `client_secret` no frontend.

## 3. Produto e oferta no painel

1. Crie um produto genérico (ex.: "Créditos CurriculosPro").
2. Crie **uma oferta ativa** (preço inicial qualquer — o backend sincroniza antes de cada cobrança).
3. Anote `CAKTO_PRODUCT_ID` e `CAKTO_OFFER_ID` (short_id ou UUID).
4. Na aba **Links** do produto, copie o código do link `https://pay.cakto.com.br/XXXX` → `CAKTO_CHECKOUT_CODE` (se diferente do offer id).

### Página de retorno após cartão (recomendado)

Produtos → seu produto → **Upsell/Downsell** → habilite **Página de obrigado** e configure **esta URL exata**:

`https://curriculoproia.com.br/compra/cakto-popup-retorno?provider=cakto`

Essa página abre **dentro da popup de pagamento** (não na home). Ela confirma o pagamento, avisa o modal na home e fecha a popup sozinha.

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
# Opcional: código do link pay.cakto.com.br (aba Links). Padrão: CAKTO_OFFER_ID
CAKTO_CHECKOUT_CODE=
CAKTO_WEBHOOK_SECRET=secret_configurado_no_webhook

PUBLIC_API_URL=https://api.curriculoproia.com.br
```

## 6. Ativar no admin

Painel admin → **Meio de pagamento** → selecione **Cakto** → Salvar → **Testar conexão**.

## 7. Fluxo resumido

1. Usuário escolhe plano → abre popup `pay.cakto.com.br` (PIX ou cartão na Cakto).
2. Backend calcula valor base (cupom/descontos) e sincroniza **só a base** na oferta Cakto.
3. No checkout Cakto: produto (ex. R$ 18,71) + **Taxa de serviço** (ex. R$ 0,99) = total (ex. R$ 19,70).
4. Após pagamento, redirect para `/compra/cakto-popup-retorno` (popup) ou webhook libera créditos.
5. Ao fechar a popup, o site atualiza o saldo de créditos.

## 8. Checklist deploy produção

1. Publicar backend com fixes Cakto (payload PIX sem `productId`/`antifraudProfilingAttemptReference`).
2. `.env` no servidor: `CAKTO_*`, `CAKTO_WEBHOOK_SECRET`, `PUBLIC_API_URL=https://api.curriculoproia.com.br`.
3. Painel Cakto: webhook URL + evento `purchase_approved` + secret igual ao `.env`.
4. Admin: provedor **Cakto** salvo no Supabase.
5. Teste: comprar via PIX → popup fecha em ~4–8s → créditos no header → Menu Financeiro mostra a compra.

## 9. Endpoints

- `POST /api/analyze/payment/create-session` — sincroniza oferta e retorna `checkoutUrl` Cakto
- `POST /api/analyze/payment/cakto/card-checkout` — URL hospedada (legado; create-session já retorna URL)
- `POST /api/analyze/payment/cakto/pix` — PIX via API (legado; preferir checkout hospedado)
