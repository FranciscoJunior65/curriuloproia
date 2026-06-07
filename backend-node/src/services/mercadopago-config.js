import '../load-env.js';

const MODE_TEST = 'test';
const MODE_PRODUCTION = 'production';

export const getMercadoPagoMode = () => {
  const mode = String(process.env.MERCADOPAGO_MODE || '').trim().toLowerCase();
  if (mode === MODE_TEST || mode === MODE_PRODUCTION) {
    return mode;
  }

  const sandbox = String(process.env.MERCADOPAGO_SANDBOX || '').trim();
  if (sandbox === 'true' || sandbox === '1') return MODE_TEST;
  if (sandbox === 'false' || sandbox === '0') return MODE_PRODUCTION;

  return MODE_TEST;
};

export const isMercadoPagoProductionMode = () => getMercadoPagoMode() === MODE_PRODUCTION;

export const getMercadoPagoAccessToken = () => {
  const direct = process.env.MERCADOPAGO_ACCESS_TOKEN?.trim();
  if (direct && !direct.includes('seu-access-token')) {
    return direct;
  }

  const mode = getMercadoPagoMode();
  return mode === MODE_PRODUCTION
    ? process.env.MERCADOPAGO_ACCESS_TOKEN_PRODUCTION?.trim()
    : process.env.MERCADOPAGO_ACCESS_TOKEN_TEST?.trim();
};

export const getMercadoPagoPublicKey = () => {
  const direct = process.env.MERCADOPAGO_PUBLIC_KEY?.trim();
  if (direct) return direct;

  const mode = getMercadoPagoMode();
  return mode === MODE_PRODUCTION
    ? process.env.MERCADOPAGO_PUBLIC_KEY_PRODUCTION?.trim()
    : process.env.MERCADOPAGO_PUBLIC_KEY_TEST?.trim();
};

export const maskMercadoPagoToken = (token) => {
  if (!token || token.length <= 12) return '***';
  return `${token.slice(0, 8)}...${token.slice(-4)}`;
};

export const getMercadoPagoDebugInfo = () => {
  const mode = getMercadoPagoMode();
  const token = getMercadoPagoAccessToken();

  return {
    mode,
    isProduction: mode === MODE_PRODUCTION,
    checkoutTarget: mode === MODE_PRODUCTION ? 'init_point' : 'sandbox_init_point',
    hasAccessToken: !!token,
    tokenPreview: token ? maskMercadoPagoToken(token) : null,
    hasPublicKey: !!getMercadoPagoPublicKey(),
    legacySandboxFlag: process.env.MERCADOPAGO_SANDBOX || null
  };
};

/** Sandbox: cartão e conta MP. Produção: cartão, conta MP e PIX. Sempre sem boleto/débito. */
export const buildCheckoutPaymentMethods = (isProduction) => {
  const excludedPaymentTypes = [
    { id: 'ticket' },
    { id: 'debit_card' }
  ];

  if (!isProduction) {
    excludedPaymentTypes.push({ id: 'bank_transfer' });
  }

  return {
    excluded_payment_types: excludedPaymentTypes,
    excluded_payment_methods: [{ id: 'bolbradesco' }]
  };
};
