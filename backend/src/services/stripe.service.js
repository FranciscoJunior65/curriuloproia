import Stripe from 'stripe';
import dotenv from 'dotenv';

dotenv.config();

// Inicializa Stripe
const stripe = new Stripe(process.env.STRIPE_SECRET_KEY, {
  apiVersion: '2024-11-20.acacia'
});

/**
 * Cria uma sessão de checkout do Stripe
 * @param {string} [couponCode] - Código do cupom (opcional)
 * @param {string} [cpf] - CPF do cliente (obrigatório quando usar cupom; 1 uso por cupom por CPF)
 */
export const createCheckoutSession = async (planId, userId, email, frontendUrl = null, couponCode = null, cpf = null) => {
  try {
    const { PRICING_PLANS } = await import('./pricing.service.js');
    const plan = PRICING_PLANS[planId];

    if (!plan) {
      throw new Error('Plano não encontrado');
    }

    let amountBRL = plan.priceBRL;
    let metadata = {
      userId,
      planId,
      planName: plan.name,
      analyses: plan.analyses.toString()
    };
    let couponInfo = null;

    if (couponCode && couponCode.trim()) {
      const { validateCoupon, normalizeCpf } = await import('./supabase.service.js');
      const cpfNorm = cpf ? normalizeCpf(cpf) : '';
      if (cpfNorm.length !== 11) {
        throw new Error('Para usar cupom, informe seu CPF (11 dígitos).');
      }
      const result = await validateCoupon(couponCode, cpf);
      if (!result.valid || !result.coupon) {
        throw new Error(result.message || 'Cupom inválido ou já utilizado por este CPF.');
      }
      const pct = result.coupon.porcentagem_desconto;
      const original = plan.priceBRL;
      amountBRL = Math.max(0, original * (1 - pct / 100));
      couponInfo = {
        couponId: result.coupon.id,
        couponName: result.coupon.nome,
        discountPercent: pct,
        originalPrice: original
      };
      metadata.couponId = result.coupon.id;
      metadata.couponName = result.coupon.nome;
      metadata.discountPercent = String(pct);
      metadata.originalPrice = String(original);
      metadata.cpfNormalized = cpfNorm;
    }

    const amountInCents = Math.round(amountBRL * 100);

    // Checkout 100% grátis: Stripe não aceita valor zero; concluir no backend
    if (amountInCents <= 0 && couponInfo) {
      return {
        freeCheckout: true,
        userId,
        planId,
        planName: plan.name,
        analyses: plan.analyses,
        couponId: couponInfo.couponId,
        couponName: couponInfo.couponName,
        discountPercent: couponInfo.discountPercent,
        originalPrice: couponInfo.originalPrice,
        cpfNormalized: metadata.cpfNormalized || null
      };
    }

    // Descrição no extrato bancário (statement descriptor)
    const statementDescriptor = process.env.STRIPE_STATEMENT_DESCRIPTOR || 'CurriculosPro IA';

    // Valida email (opcional - só inclui se for válido)
    const isValidEmail = email && email.trim() !== '' && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());

    // Determina a URL do frontend
    const baseUrl = frontendUrl || process.env.FRONTEND_URL || 'http://localhost:4200';
    const cleanBaseUrl = baseUrl.replace(/\/$/, '');

    const sessionConfig = {
      payment_method_types: ['card'],
      line_items: [
        {
          price_data: {
            currency: 'brl',
            product_data: {
              name: plan.name,
              description: plan.description + (couponInfo ? ` (${couponInfo.couponName}: ${couponInfo.discountPercent}% off)` : ''),
            },
            unit_amount: amountInCents,
          },
          quantity: 1,
        },
      ],
      mode: 'payment',
      success_url: `${cleanBaseUrl}?session_id={CHECKOUT_SESSION_ID}&userId=${userId}`,
      cancel_url: `${cleanBaseUrl}/payment/cancel`,
      payment_intent_data: {
        statement_descriptor: statementDescriptor.substring(0, 22)
      },
      metadata
    };

    // Adiciona email apenas se for válido
    if (isValidEmail) {
      sessionConfig.customer_email = email.trim();
    }

    // Cria sessão de checkout
    const session = await stripe.checkout.sessions.create(sessionConfig);

    return {
      sessionId: session.id,
      url: session.url
    };
  } catch (error) {
    console.error('Erro ao criar sessão Stripe:', error);
    throw new Error(`Erro ao criar sessão de pagamento: ${error.message}`);
  }
};

/**
 * Verifica o status de uma sessão de checkout
 */
export const getCheckoutSession = async (sessionId) => {
  try {
    const session = await stripe.checkout.sessions.retrieve(sessionId);
    return session;
  } catch (error) {
    console.error('Erro ao buscar sessão Stripe:', error);
    throw new Error(`Erro ao verificar sessão: ${error.message}`);
  }
};

/**
 * Webhook handler para processar eventos do Stripe
 */
export const handleWebhook = async (req, res) => {
  const sig = req.headers['stripe-signature'];
  const webhookSecret = process.env.STRIPE_WEBHOOK_SECRET;

  let event;

  try {
    event = stripe.webhooks.constructEvent(req.body, sig, webhookSecret);
  } catch (err) {
    console.error('Erro ao verificar webhook:', err.message);
    return res.status(400).send(`Webhook Error: ${err.message}`);
  }

  // Processa o evento
  if (event.type === 'checkout.session.completed') {
    const session = event.data.object;
    
    // Aqui você processaria o pagamento bem-sucedido
    // Adicionaria créditos ao usuário, etc.
    console.log('Pagamento confirmado:', {
      sessionId: session.id,
      userId: session.metadata.userId,
      planId: session.metadata.planId,
      analyses: session.metadata.analyses
    });

    // Importa e atualiza créditos do usuário
    const { getOrCreateUser, saveUser } = await import('../models/user.model.js');
    const user = await getOrCreateUser(
      session.metadata.userId, 
      session.customer_details?.email || session.customer_email || ''
    );
    await user.addCredits(parseInt(session.metadata.analyses));
    await saveUser({
      ...user,
      plan: session.metadata.planId
    });
  }

  res.json({ received: true });
};

