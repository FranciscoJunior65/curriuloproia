import '../load-env.js';
import { MercadoPagoConfig, Preference, Payment, User } from 'mercadopago';
import { buildCheckoutContext } from './payment-checkout.service.js';
import { fulfillPaidOrder } from './payment-fulfillment.service.js';
import {
  getMercadoPagoAccessToken,
  getMercadoPagoDebugInfo,
  isMercadoPagoProductionMode,
  maskMercadoPagoToken,
  buildCheckoutPaymentMethods
} from './mercadopago-config.js';

let mpClient = null;
let cachedLiveMode = null;

const getAccessToken = () => getMercadoPagoAccessToken() || '';

const maskToken = maskMercadoPagoToken;

const resolveIsProductionMode = async () => {
  if (cachedLiveMode !== null) return cachedLiveMode;
  cachedLiveMode = isMercadoPagoProductionMode();
  return cachedLiveMode;
};

const resolveCheckoutInitPoint = (result, isProduction) => {
  const production = result?.init_point || null;
  const sandbox = result?.sandbox_init_point || null;
  return isProduction ? (production || sandbox) : (sandbox || production);
};

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

/** MP exige HTTPS e proíbe localhost para auto_return e notification_url. */
const supportsMercadoPagoHttpsCallback = (url) => {
  if (!url) return false;
  const normalized = String(url).toLowerCase();
  return normalized.startsWith('https://')
    && !normalized.includes('localhost')
    && !normalized.includes('127.0.0.1');
};

const buildPayer = (email, cpfNormalized) => {
  const hasEmail = email && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
  const hasCpf = cpfNormalized && String(cpfNormalized).length === 11;
  if (!hasEmail && !hasCpf) return undefined;

  const payer = {};
  if (hasEmail) payer.email = email.trim();
  if (hasCpf) {
    payer.identification = { type: 'CPF', number: String(cpfNormalized) };
  }
  return payer;
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

  const backUrls = {
    success: `${baseUrl}/compra/sucesso?provider=mercadopago&userId=${encodeURIComponent(userId)}`,
    failure: `${baseUrl}/compra/falha?provider=mercadopago&userId=${encodeURIComponent(userId)}`,
    pending: `${baseUrl}/compra/pendente?provider=mercadopago&userId=${encodeURIComponent(userId)}`
  };

  const isProduction = await resolveIsProductionMode();

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
    payer: buildPayer(email, ctx.cpfNormalized || ctx.metadata.cpfNormalized),
    external_reference: externalReference,
    metadata: ctx.metadata,
    back_urls: backUrls,
    payment_methods: buildCheckoutPaymentMethods(isProduction)
  };

  if (supportsMercadoPagoHttpsCallback(backUrls.success)) {
    body.auto_return = 'approved';
  }

  const notificationUrl = `${getApiBaseUrl()}/api/analyze/payment/mercadopago/webhook`;
  if (supportsMercadoPagoHttpsCallback(notificationUrl)) {
    body.notification_url = notificationUrl;
  }

  const result = await preferenceClient.create({ body });
  const initPoint = resolveCheckoutInitPoint(result, isProduction);

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

/** Testa credenciais e conta Mercado Pago (GET /api/test/mercadopago). */
export const testMercadoPagoIntegration = async () => {
  const token = getAccessToken();
  if (!token) {
    return {
      connected: false,
      provider: 'mercadopago',
      message: 'MERCADOPAGO_ACCESS_TOKEN não configurado no .env'
    };
  }

  if (token.includes('seu-access-token')) {
    return {
      connected: false,
      provider: 'mercadopago',
      message: 'MERCADOPAGO_ACCESS_TOKEN ainda está com valor de exemplo no .env'
    };
  }

  const webhookUrl = `${getApiBaseUrl()}/api/analyze/payment/mercadopago/webhook`;
  const frontendUrl = (process.env.FRONTEND_URL || 'http://localhost:4200').replace(/\/$/, '');

  try {
    const client = new MercadoPagoConfig({ accessToken: token });
    const userClient = new User(client);
    const account = await userClient.get();
    cachedLiveMode = account?.live_mode === true;
    const mode = cachedLiveMode ? 'production' : 'test';

    return {
      connected: true,
      provider: 'mercadopago',
      message: `Conexão com Mercado Pago OK (modo ${mode}).`,
      details: {
        mode,
        liveMode: cachedLiveMode,
        checkoutTarget: cachedLiveMode ? 'init_point' : 'sandbox_init_point',
        userId: account?.id,
        email: account?.email,
        country: account?.country_id,
        siteId: account?.site_id,
        tokenPreview: maskToken(token),
        webhookUrl,
        webhookConfigured: supportsMercadoPagoHttpsCallback(webhookUrl),
        frontendUrl,
        paymentProvider: process.env.PAYMENT_PROVIDER || 'stripe',
        sandboxOverride: process.env.MERCADOPAGO_SANDBOX || null,
        config: getMercadoPagoDebugInfo()
      }
    };
  } catch (error) {
    return {
      connected: false,
      provider: 'mercadopago',
      message: error.message || 'Falha ao conectar com Mercado Pago',
      details: { webhookUrl, tokenPreview: maskToken(token), status: error.status, config: getMercadoPagoDebugInfo() }
    };
  }
};
