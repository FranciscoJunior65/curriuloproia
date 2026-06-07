import '../load-env.js';
import Stripe from 'stripe';

/**
 * Testa conexão com a API do Stripe
 */
export const testStripeConnection = async () => {
  const secretKey = process.env.STRIPE_SECRET_KEY;
  if (!secretKey || !String(secretKey).trim()) {
    return {
      connected: false,
      provider: 'stripe',
      message: 'STRIPE_SECRET_KEY não configurada no .env'
    };
  }

  if (secretKey.includes('sua-chave') || secretKey === 'sk_test_sua-chave-stripe') {
    return {
      connected: false,
      provider: 'stripe',
      message: 'STRIPE_SECRET_KEY ainda está com valor de exemplo no .env'
    };
  }

  try {
    const stripe = new Stripe(secretKey, { apiVersion: '2024-11-20.acacia' });
    const balance = await stripe.balance.retrieve();
    const mode = secretKey.startsWith('sk_live') ? 'live' : 'test';

    return {
      connected: true,
      provider: 'stripe',
      message: `Conexão com Stripe OK (modo ${mode}).`,
      details: {
        mode,
        currencies: (balance.available || []).map((b) => b.currency)
      }
    };
  } catch (error) {
    return {
      connected: false,
      provider: 'stripe',
      message: error.message || 'Falha ao conectar com Stripe',
      details: { code: error.code }
    };
  }
};

/**
 * Testa conexão com a API do Mercado Pago
 */
export const testMercadoPagoConnection = async () => {
  const { testMercadoPagoIntegration } = await import('./mercadopago.service.js');
  return testMercadoPagoIntegration();
};

/**
 * Testa o provedor informado ou ativo
 */
export const testPaymentProviderConnection = async (provider) => {
  if (provider === 'mercadopago') {
    return testMercadoPagoConnection();
  }
  return testStripeConnection();
};
