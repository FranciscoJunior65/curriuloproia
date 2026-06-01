import '../load-env.js';
import { MercadoPagoConfig, Preference, Payment } from 'mercadopago';
import { buildCheckoutContext } from './payment-checkout.service.js';
import { fulfillPaidOrder } from './payment-fulfillment.service.js';

let mpClient = null;

const getClient = () => {
  const token = process.env.MERCADOPAGO_ACCESS_TOKEN;
  if (!token) {
    throw new Error('MERCADOPAGO_ACCESS_TOKEN não configurado');
  }
  if (!mpClient) {
    mpClient = new MercadoPagoConfig({ accessToken: token });
  }
  return mpClient;
};

const getApiBaseUrl = () => {
  const url = process.env.PUBLIC_API_URL || process.env.API_URL || `http://localhost:${process.env.PORT || 3000}`;
  return String(url).replace(/\/$/, '');
};

const parseExternalReference = (externalReference) => {
  if (!externalReference) return null;
  try {
    return JSON.parse(externalReference);
  } catch {
    return null;
  }
};

/**
 * Cria preferência Checkout Pro do Mercado Pago
 */
export const createMercadoPagoCheckout = async (planId, userId, email, frontendUrl = null, couponCode = null, cpf = null) => {
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

  const baseUrl = (frontendUrl || process.env.FRONTEND_URL || 'http://localhost:4200').replace(/\/$/, '');
  const preferenceClient = new Preference(getClient());

  const externalReference = JSON.stringify({
    userId: ctx.userId,
    planId: ctx.planId,
    planName: ctx.planName,
    analyses: ctx.analyses,
    couponId: ctx.metadata.couponId || null,
    couponName: ctx.metadata.couponName || null,
    discountPercent: ctx.metadata.discountPercent != null ? parseFloat(ctx.metadata.discountPercent) : null,
    originalPrice: ctx.metadata.originalPrice != null ? parseFloat(ctx.metadata.originalPrice) : null,
    cpfNormalized: ctx.metadata.cpfNormalized || null,
    amountBRL: ctx.amountBRL
  });

  const body = {
    items: [
      {
        id: ctx.planId,
        title: ctx.planName,
        description: ctx.plan.description + (ctx.couponInfo ? ` (${ctx.couponInfo.couponName}: ${ctx.couponInfo.discountPercent}% off)` : ''),
        quantity: 1,
        unit_price: ctx.amountBRL,
        currency_id: 'BRL'
      }
    ],
    payer: email && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()) ? { email: email.trim() } : undefined,
    external_reference: externalReference,
    metadata: ctx.metadata,
    back_urls: {
      success: `${baseUrl}/compra/sucesso?provider=mercadopago&userId=${encodeURIComponent(userId)}`,
      failure: `${baseUrl}/compra/falha?provider=mercadopago&userId=${encodeURIComponent(userId)}`,
      pending: `${baseUrl}/compra/pendente?provider=mercadopago&userId=${encodeURIComponent(userId)}`
    },
    auto_return: 'approved',
    notification_url: `${getApiBaseUrl()}/api/analyze/payment/mercadopago/webhook`
  };

  const result = await preferenceClient.create({ body });
  const initPoint = result.init_point || result.sandbox_init_point;

  if (!initPoint) {
    throw new Error('Mercado Pago não retornou URL de checkout');
  }

  return {
    sessionId: result.id,
    url: initPoint,
    preferenceId: result.id
  };
};

/**
 * Busca pagamento no Mercado Pago
 */
export const getMercadoPagoPayment = async (paymentId) => {
  const paymentClient = new Payment(getClient());
  return paymentClient.get({ id: paymentId });
};

/**
 * Verifica pagamento aprovado e libera créditos
 */
export const verifyMercadoPagoPayment = async (paymentId) => {
  const payment = await getMercadoPagoPayment(paymentId);
  const meta = parseExternalReference(payment.external_reference) || payment.metadata || {};

  if (payment.status !== 'approved') {
    return {
      paid: false,
      paymentStatus: payment.status,
      statusDetail: payment.status_detail
    };
  }

  const userId = meta.userId || payment.metadata?.userId;
  const planId = meta.planId || payment.metadata?.planId;
  const analyses = parseInt(meta.analyses ?? payment.metadata?.analyses ?? '0', 10);
  const planName = meta.planName || payment.metadata?.planName || `Plano ${planId}`;
  const price = meta.amountBRL != null ? parseFloat(meta.amountBRL) : parseFloat(payment.transaction_amount || 0);

  const result = await fulfillPaidOrder({
    userId,
    planId,
    planName,
    analyses,
    price,
    paymentMethod: 'mercadopago',
    paymentId: String(payment.id),
    customerEmail: payment.payer?.email || '',
    couponId: meta.couponId || null,
    couponName: meta.couponName || null,
    discountPercent: meta.discountPercent ?? null,
    originalPrice: meta.originalPrice ?? null,
    cpfNormalized: meta.cpfNormalized || null
  });

  return {
    paid: true,
    user: result.user,
    alreadyFulfilled: result.alreadyFulfilled
  };
};

/**
 * Webhook IPN Mercado Pago
 */
export const handleMercadoPagoWebhook = async (req, res) => {
  try {
    const topic = req.query.topic || req.query.type || req.body?.type;
    const id = req.query.id || req.query['data.id'] || req.body?.data?.id;

    if ((topic === 'payment' || topic === 'merchant_order') && id) {
      if (topic === 'payment') {
        await verifyMercadoPagoPayment(id);
      }
    }

    res.status(200).send('OK');
  } catch (error) {
    console.error('Erro no webhook Mercado Pago:', error);
    res.status(200).send('OK');
  }
};
