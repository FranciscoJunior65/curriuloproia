# Como ativar a Kiwify (PIX + cartão — checkout hospedado)

Integração paralela à Cakto/Stripe/Mercado Pago. O cliente paga em `pay.kiwify.com.br` (**janela separada** — a Kiwify não permite iframe no seu site). Os créditos são liberados via **webhook** `compra_aprovada`.

Documentação oficial: [docs.kiwify.com.br](https://docs.kiwify.com.br/api-reference/general)

## 1. Painel Kiwify — produto e ofertas

1. Crie um produto (ex.: **Créditos CurriculosPro IA**).
2. Para cada plano do app, crie uma **oferta** com o preço final (o mesmo `displayAmountBRL` da vitrine, se repassar taxa ao cliente):
   - Análise única (`single`)
   - Pacote 3 (`pack3`)
   - Pacote 5 (`pack5`)
   - Inglês avulso (`english`)
3. Opcional — pacote + inglês no checkout: ofertas extras e variáveis `KIWIFY_CHECKOUT_SINGLE_ENGLISH`, etc.
4. Em cada oferta, copie o código do link `https://pay.kiwify.com.br/XXXXXXX` (só o `XXXXXXX`).

## 2. API Key (você já criou)

1. **Apps → API → Criar API Key**
2. Copie:
   - **API Key** → `KIWIFY_API_KEY`
   - **client_secret** → `KIWIFY_CLIENT_SECRET`
   - **account_id** (mesma tela) → `KIWIFY_ACCOUNT_ID`

## 3. Variáveis no `backend/.env`

```env
PAYMENT_PROVIDER=kiwify

KIWIFY_API_KEY=sua_api_key
KIWIFY_CLIENT_SECRET=seu_client_secret
KIWIFY_ACCOUNT_ID=seu_account_id

# Código após pay.kiwify.com.br/ (um por plano)
KIWIFY_CHECKOUT_SINGLE=8oQrd43
KIWIFY_CHECKOUT_PACK3=...
KIWIFY_CHECKOUT_PACK5=...
KIWIFY_CHECKOUT_ENGLISH=...

# Opcional: plano + currículo em inglês no mesmo checkout
# KIWIFY_CHECKOUT_SINGLE_ENGLISH=...
# KIWIFY_CHECKOUT_PACK3_ENGLISH=...
# KIWIFY_CHECKOUT_PACK5_ENGLISH=...

# Token do webhook (Apps → Webhooks → seu webhook → token)
KIWIFY_WEBHOOK_TOKEN=rxue90njjv1
```

## 4. Webhook

1. **Apps → Webhooks → Criar Webhook**
2. **URL:** `https://api.curriculoproia.com.br/api/analyze/payment/kiwify/webhook`
3. **Evento:** `compra_aprovada`
4. Copie o **token** do webhook para `KIWIFY_WEBHOOK_TOKEN`
5. Teste com **Testar Webhook** no painel

O backend valida o `token` no JSON (se `KIWIFY_WEBHOOK_TOKEN` estiver definido), consulta a venda na API e libera créditos usando o parâmetro `sck` da URL de checkout.

## 5. Página de obrigado (opcional — fecha o modal)

No produto Kiwify, configure a página de obrigado para:

```
https://curriculoproia.com.br/compra/kiwify-popup-retorno
```

(ou reutilize `/compra/cakto-popup-retorno` — o componente é o mesmo.)

## 6. Admin do app

**Admin → Configurações → Meio de pagamento → Kiwify → Salvar → Validar conexão**

## 7. Cupons

- Cupom do app: enviado na URL como `coupon=CODIGO` (o cupom precisa existir na Kiwify).
- Cupom 100% no app: checkout gratuito interno (sem Kiwify).

## 8. Deploy

Publique **backend** e **frontend**. Sem deploy, o admin não mostra Kiwify e o checkout não abre no modal.

## Fluxo resumido

```
Usuário escolhe plano → API monta pay.kiwify.com.br/...?email&cpf&sck={metadata}
→ Modal iframe → PIX ou cartão na Kiwify
→ Webhook compra_aprovada → API libera créditos
→ Página de obrigado (opcional) notifica o modal
```

## Diferença da Cakto

| | Cakto | Kiwify |
|---|--------|--------|
| Preço | Sincroniza oferta via API antes da compra | Preço fixo por oferta no painel |
| Links | Um checkout + sync de preço | Um link por plano (`KIWIFY_CHECKOUT_*`) |
