import '../load-env.js';
import { supabaseAdmin } from './supabase.service.js';

const PAYMENT_PROVIDER_KEY = 'payment_provider';
const VALID_PROVIDERS = ['stripe', 'mercadopago'];

let memoryCache = null;

const normalizeProvider = (value) => {
  const v = String(value || '').trim().toLowerCase();
  return VALID_PROVIDERS.includes(v) ? v : 'stripe';
};

const getEnvDefault = () => normalizeProvider(process.env.PAYMENT_PROVIDER || 'stripe');

/**
 * Obtém o provedor de pagamento ativo (stripe | mercadopago)
 */
export const getPaymentProvider = async () => {
  if (memoryCache) return memoryCache;

  if (supabaseAdmin) {
    try {
      const { data, error } = await supabaseAdmin
        .from('app_configuracoes')
        .select('valor')
        .eq('chave', PAYMENT_PROVIDER_KEY)
        .maybeSingle();

      if (!error && data?.valor) {
        memoryCache = normalizeProvider(data.valor);
        return memoryCache;
      }
    } catch (err) {
      console.warn('⚠️ Tabela app_configuracoes indisponível, usando PAYMENT_PROVIDER do .env:', err.message);
    }
  }

  memoryCache = getEnvDefault();
  return memoryCache;
};

/**
 * Define o provedor de pagamento (admin)
 */
export const setPaymentProvider = async (provider) => {
  const normalized = normalizeProvider(provider);
  memoryCache = normalized;

  if (!supabaseAdmin) {
    console.warn('⚠️ Supabase não configurado — provedor salvo apenas em memória até reiniciar o servidor.');
    return normalized;
  }

  const now = new Date().toISOString();
  const { error } = await supabaseAdmin
    .from('app_configuracoes')
    .upsert(
      { chave: PAYMENT_PROVIDER_KEY, valor: normalized, atualizado_em: now },
      { onConflict: 'chave' }
    );

  if (error) {
    memoryCache = null;
    throw new Error(`Erro ao salvar provedor de pagamento: ${error.message}`);
  }

  return normalized;
};

export const getValidPaymentProviders = () => [...VALID_PROVIDERS];

export const clearPaymentProviderCache = () => {
  memoryCache = null;
};
