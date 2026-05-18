# 🔐 Como Ativar o Stripe para Pagamentos Reais

## ✅ Alterações Já Realizadas

1. **Frontend atualizado** para usar `createPaymentSession()` em vez de `createMockPurchase()`
2. **Backend já está pronto** para processar pagamentos via Stripe
3. **Chave pública do Stripe já está no frontend** (`analyzer.service.ts`)

## 📋 Passos para Ativar o Stripe

### 1. Obter Chaves do Stripe

1. Acesse https://dashboard.stripe.com/
2. Faça login ou crie uma conta
3. Vá em **Developers > API keys**
4. Copie as seguintes chaves:
   - **Chave Pública** (Publishable key): começa com `pk_live_...` ou `pk_test_...`
   - **Chave Secreta** (Secret key): começa com `sk_live_...` ou `sk_test_...`

### 2. Configurar Chaves no Backend

Abra o arquivo `backend/.env` e adicione/atualize as chaves do Stripe:

```env
# Stripe - Pagamentos Reais
STRIPE_SECRET_KEY=sk_live_sua_chave_secreta_aqui
STRIPE_WEBHOOK_SECRET=whsec_sua_webhook_secret_aqui
STRIPE_STATEMENT_DESCRIPTOR=CurriculosPro IA
```

**⚠️ IMPORTANTE:**
- Para **testes**, use chaves `sk_test_...` e `pk_test_...`
- Para **produção**, use chaves `sk_live_...` e `pk_live_...`

### 3. Configurar Chave Pública no Frontend

A chave pública já está configurada em `frontend/src/app/services/analyzer.service.ts` (linha 33):

```typescript
public readonly stripePublishableKey = 'pk_live_51RyHWo...';
```

**Se você quiser usar uma chave diferente**, edite essa linha com sua chave pública.

### 4. Configurar Webhook (Opcional mas Recomendado)

O webhook permite que o Stripe notifique seu backend quando um pagamento for confirmado.

#### Desenvolvimento Local (Teste)

1. Instale o Stripe CLI:
   ```bash
   # Windows (com Chocolatey)
   choco install stripe
   
   # Ou baixe em: https://stripe.com/docs/stripe-cli
   ```

2. Faça login:
   ```bash
   stripe login
   ```

3. Inicie o webhook forwarding:
   ```bash
   stripe listen --forward-to localhost:3000/api/stripe/webhook
   ```

4. Copie o webhook secret (`whsec_...`) e adicione ao `.env`:
   ```env
   STRIPE_WEBHOOK_SECRET=whsec_...
   ```

#### Produção

1. No dashboard do Stripe, vá em **Developers > Webhooks**
2. Clique em **Add endpoint**
3. Configure:
   - **URL**: `https://seu-dominio.com/api/stripe/webhook`
   - **Events**: Selecione `checkout.session.completed`
4. Copie o webhook secret e adicione ao `.env` de produção

### 5. Testar o Pagamento

1. **Reinicie o backend**:
   ```bash
   cd backend
   npm start
   ```

2. **Acesse o frontend** e tente comprar créditos

3. **Use cartões de teste do Stripe**:
   - **Sucesso**: `4242 4242 4242 4242`
   - **Recusado**: `4000 0000 0000 0002`
   - **Autenticação 3D Secure**: `4000 0025 0000 3155`
   - **CVV**: qualquer 3 dígitos
   - **Validade**: qualquer data futura
   - **CEP**: qualquer valor

## 🎯 Como Funciona

### Fluxo de Pagamento

1. **Usuário clica em "Comprar"** no frontend
2. **Frontend chama** `createPaymentSession(planId, userId, email)`
3. **Backend cria** uma sessão de checkout no Stripe
4. **Usuário é redirecionado** para a página de pagamento do Stripe
5. **Após pagamento**, Stripe redireciona de volta com `session_id` na URL
6. **Frontend verifica** o pagamento chamando `verifyPayment(sessionId)`
7. **Backend confirma** o pagamento e adiciona créditos ao usuário

### Endpoints Envolvidos

- **POST** `/api/analyze/payment/create-session` - Cria sessão de checkout
- **GET** `/api/analyze/payment/verify?sessionId=...` - Verifica pagamento
- **POST** `/api/stripe/webhook` - Recebe notificações do Stripe (webhook)

## 🔄 Reverter para Mock (Testes)

Se você quiser voltar a usar compras mockadas (sem Stripe):

1. No `analyzer.component.ts`, troque:
   ```typescript
   // DE:
   this.analyzerService.createPaymentSession(...)
   
   // PARA:
   this.analyzerService.createMockPurchase(...)
   ```

2. Remova a chave do Stripe do `.env` ou use chaves de teste

## 🆘 Problemas Comuns

### "Erro ao criar sessão de pagamento"
- Verifique se `STRIPE_SECRET_KEY` está correto no `.env`
- Verifique se reiniciou o backend após alterar o `.env`
- Verifique os logs do terminal do backend para detalhes

### "Pagamento não foi confirmado"
- Verifique se o webhook está configurado corretamente
- Verifique os logs do Stripe Dashboard > Developers > Webhooks
- Teste manualmente chamando `/api/analyze/payment/verify?sessionId=...`

### "Chave pública inválida"
- Certifique-se de que está usando a chave pública (`pk_...`) no frontend
- Certifique-se de que está usando a chave secreta (`sk_...`) no backend

## 📊 Monitoramento

Após ativar, você pode:

1. Ver todas as transações no **Stripe Dashboard > Payments**
2. Ver logs de webhook em **Developers > Webhooks > [seu endpoint]**
3. Ver compras no banco de dados (tabela `compras`)

## 💰 Preços Atuais

Os preços estão definidos em `backend/src/services/pricing.service.js`:

- **Starter**: R$ 9,90 (1 análise)
- **Basic**: R$ 19,90 (3 análises)
- **Pro**: R$ 39,90 (10 análises)
- **Premium**: R$ 69,90 (25 análises)
- **Currículo em Inglês**: R$ 9,90 (1 tradução) ou R$ 5,90 (quando comprado junto)

Para alterar os preços, edite o arquivo `pricing.service.js`.

## ✅ Checklist Final

- [ ] Chaves do Stripe configuradas no `.env`
- [ ] Chave pública atualizada no frontend (se necessário)
- [ ] Backend reiniciado
- [ ] Webhook configurado (opcional)
- [ ] Testado com cartão de teste
- [ ] Verificado que créditos foram adicionados após pagamento

---

**🎉 Pronto! O Stripe está ativado!**

Para mais informações, consulte a documentação oficial: https://stripe.com/docs
