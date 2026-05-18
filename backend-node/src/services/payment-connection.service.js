import '../load-env.js';
import Stripe from 'stripe';
import { MercadoPagoConfig, User } from 'mercadopago';

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
  const token = process.env.MERCADOPAGO_ACCESS_TOKEN;
  if (!token || !String(token).trim()) {
    return {
      connected: false,
      provider: 'mercadopago',
      message: 'MERCADOPAGO_ACCESS_TOKEN não configurado no .env'
    };
  }

  if (token.includes('seu-access-token') || token === 'APP_USR-seu-access-token') {
    return {
      connected: false,
      provider: 'mercadopago',
      message: 'MERCADOPAGO_ACCESS_TOKEN ainda está com valor de exemplo no .env'
    };
  }

  try {
    const client = new MercadoPagoConfig({ accessToken: token });
    const userClient = new User(client);
    const account = await userClient.get();

    const isTest = token.includes('TEST') || account?.live_mode === false;
    const mode = isTest ? 'test' : 'production';

    return {
      connected: true,
      provider: 'mercadopago',
      message: `Conexão com Mercado Pago OK (modo ${mode}).`,
      details: {
        mode,
        userId: account?.id,
        email: account?.email,
        country: account?.country_id
      }
    };
  } catch (error) {
    return {
      connected: false,
      provider: 'mercadopago',
      message: error.message || 'Falha ao conectar com Mercado Pago',
      details: { status: error.status }
    };
  }
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
