import '../load-env.js';
import Stripe from 'stripe';
import { buildCheckoutContext } from './payment-checkout.service.js';
import { fulfillPaidOrder } from './payment-fulfillment.service.js';

const stripe = new Stripe(process.env.STRIPE_SECRET_KEY, {
  apiVersion: '2024-11-20.acacia'
});

/**
 * Cria uma sessão de checkout do Stripe
 */
export const createCheckoutSession = async (planId, userId, email, frontendUrl = null, couponCode = null, cpf = null) => {
  try {
    const ctx = await buildCheckoutContext(planId, userId, couponCode, cpf);

    if (ctx.freeCheckout) {
      return {
        freeCheckout: true,
        userId: ctx.userId,
        planId: ctx.planId,
        planName: ctx.planName,
        analyses: ctx.analyses,
        couponId: ctx.couponInfo?.couponId,
        couponName: ctx.couponInfo?.couponName,
        discountPercent: ctx.couponInfo?.discountPercent,
        originalPrice: ctx.couponInfo?.originalPrice,
        cpfNormalized: ctx.cpfNormalized
      };
    }

    const statementDescriptor = process.env.STRIPE_STATEMENT_DESCRIPTOR || 'CurriculosPro IA';
    const isValidEmail = email && email.trim() !== '' && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
    const baseUrl = (frontendUrl || process.env.FRONTEND_URL || 'http://localhost:4200').replace(/\/$/, '');

    const sessionConfig = {
      payment_method_types: ['card'],
      line_items: [
        {
          price_data: {
            currency: 'brl',
            product_data: {
              name: ctx.planName,
              description: ctx.plan.description + (ctx.couponInfo ? ` (${ctx.couponInfo.couponName}: ${ctx.couponInfo.discountPercent}% off)` : '')
            },
            unit_amount: ctx.amountInCents
          },
          quantity: 1
        }
      ],
      mode: 'payment',
      success_url: `${baseUrl}?session_id={CHECKOUT_SESSION_ID}&userId=${userId}&provider=stripe`,
      cancel_url: `${baseUrl}/payment/cancel`,
      payment_intent_data: {
        statement_descriptor: statementDescriptor.substring(0, 22)
      },
      metadata: ctx.metadata
    };

    if (isValidEmail) {
      sessionConfig.customer_email = email.trim();
    }

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

export const getCheckoutSession = async (sessionId) => {
  try {
    return await stripe.checkout.sessions.retrieve(sessionId);
  } catch (error) {
    console.error('Erro ao buscar sessão Stripe:', error);
    throw new Error(`Erro ao verificar sessão: ${error.message}`);
  }
};

/**
 * Webhook Stripe — usa fulfillment idempotente
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

  if (event.type === 'checkout.session.completed') {
    const session = event.data.object;
    if (session.payment_status === 'paid' && session.metadata?.userId) {
      try {
        await fulfillPaidOrder({
          userId: session.metadata.userId,
          planId: session.metadata.planId,
          planName: session.metadata.planName || `Plano ${session.metadata.planId}`,
          analyses: parseInt(session.metadata.analyses, 10),
          price: parseFloat(session.amount_total) / 100,
          paymentMethod: 'stripe',
          paymentId: session.id,
          customerEmail: session.customer_details?.email || session.customer_email || '',
          couponId: session.metadata.couponId || null,
          couponName: session.metadata.couponName || null,
          discountPercent: session.metadata.discountPercent != null ? parseFloat(session.metadata.discountPercent) : null,
          originalPrice: session.metadata.originalPrice != null ? parseFloat(session.metadata.originalPrice) : null,
          cpfNormalized: session.metadata.cpfNormalized || null
        });
      } catch (err) {
        console.error('Erro ao processar webhook Stripe:', err);
      }
    }
  }

  res.json({ received: true });
};
