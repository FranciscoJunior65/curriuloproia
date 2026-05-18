import { getPaymentProvider } from './settings.service.js';
import { buildCheckoutContext } from './payment-checkout.service.js';
import { createCheckoutSession, getCheckoutSession } from './stripe.service.js';
import { createMercadoPagoCheckout, verifyMercadoPagoPayment } from './mercadopago.service.js';
import { fulfillPaidOrder } from './payment-fulfillment.service.js';

/**
 * Cria sessão de checkout no provedor ativo
 */
export const createProviderCheckout = async (planId, userId, email, frontendUrl, couponCode, cpf) => {
  const provider = await getPaymentProvider();
  const ctx = await buildCheckoutContext(planId, userId, couponCode, cpf);

  if (ctx.freeCheckout) {
    return {
      provider,
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

  if (provider === 'mercadopago') {
    const mp = await createMercadoPagoCheckout(planId, userId, email, frontendUrl, couponCode, cpf);
    return { provider: 'mercadopago', ...mp };
  }

  const stripe = await createCheckoutSession(planId, userId, email, frontendUrl, couponCode, cpf);
  return { provider: 'stripe', ...stripe };
};

/**
 * Verifica pagamento conforme provedor
 */
export const verifyProviderPayment = async (sessionId, providerHint = null) => {
  const provider = providerHint || await getPaymentProvider();

  if (provider === 'mercadopago') {
    return verifyMercadoPagoPayment(sessionId);
  }

  const session = await getCheckoutSession(sessionId);

  if (session.payment_status !== 'paid') {
    return {
      paid: false,
      paymentStatus: session.payment_status
    };
  }

  const userId = session.metadata.userId;
  const planId = session.metadata.planId;
  const analyses = parseInt(session.metadata.analyses, 10);
  const planName = session.metadata.planName || `Plano ${planId}`;
  const price = parseFloat(session.amount_total) / 100;

  const result = await fulfillPaidOrder({
    userId,
    planId,
    planName,
    analyses,
    price,
    paymentMethod: 'stripe',
    paymentId: session.id,
    customerEmail: session.customer_email || session.customer_details?.email || '',
    couponId: session.metadata.couponId || null,
    couponName: session.metadata.couponName || null,
    discountPercent: session.metadata.discountPercent != null ? parseFloat(session.metadata.discountPercent) : null,
    originalPrice: session.metadata.originalPrice != null ? parseFloat(session.metadata.originalPrice) : null,
    cpfNormalized: session.metadata.cpfNormalized || null
  });

  return {
    paid: true,
    user: result.user,
    alreadyFulfilled: result.alreadyFulfilled
  };
};
