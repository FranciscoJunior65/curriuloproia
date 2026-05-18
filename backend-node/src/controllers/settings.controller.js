import {
  getPaymentProvider,
  setPaymentProvider,
  getValidPaymentProviders
} from '../services/settings.service.js';
import { testPaymentProviderConnection } from '../services/payment-connection.service.js';

/**
 * GET /api/admin/settings/payment-provider
 */
export const getPaymentProviderSetting = async (req, res) => {
  try {
    const provider = await getPaymentProvider();
    res.json({
      success: true,
      provider,
      providers: getValidPaymentProviders(),
      labels: {
        stripe: 'Stripe',
        mercadopago: 'Mercado Pago'
      }
    });
  } catch (error) {
    res.status(500).json({
      success: false,
      error: 'Erro ao obter configuração',
      message: error.message
    });
  }
};

/**
 * PUT /api/admin/settings/payment-provider
 * Body: { provider: 'stripe' | 'mercadopago' }
 */
export const updatePaymentProviderSetting = async (req, res) => {
  try {
    const { provider } = req.body;

    if (!provider) {
      return res.status(400).json({
        success: false,
        error: 'Campo provider é obrigatório (stripe ou mercadopago)'
      });
    }

    const normalized = await setPaymentProvider(provider);
    const confirmed = await getPaymentProvider();

    res.json({
      success: true,
      message: `Meio de pagamento alterado para ${normalized === 'stripe' ? 'Stripe' : 'Mercado Pago'}.`,
      provider: confirmed
    });
  } catch (error) {
    res.status(500).json({
      success: false,
      error: 'Erro ao salvar configuração',
      message: error.message
    });
  }
};

/**
 * POST /api/admin/settings/payment-provider/test
 * Body opcional: { provider: 'stripe' | 'mercadopago' } — usa o selecionado ou o ativo
 */
export const testPaymentProviderConnectionHandler = async (req, res) => {
  try {
    const provider = req.body?.provider || await getPaymentProvider();
    const result = await testPaymentProviderConnection(provider);

    res.json({
      success: true,
      connected: result.connected,
      provider: result.provider,
      message: result.message,
      details: result.details || null
    });
  } catch (error) {
    res.status(500).json({
      success: false,
      connected: false,
      error: 'Erro ao testar conexão',
      message: error.message
    });
  }
};
