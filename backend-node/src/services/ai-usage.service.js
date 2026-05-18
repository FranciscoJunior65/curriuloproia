import { supabaseAdmin } from './supabase.service.js';

/**
 * Serviço para rastrear uso de APIs de IA
 */

/**
 * Estima tokens baseado no tamanho do texto
 * Aproximação: 1 token ≈ 4 caracteres (português)
 */
const estimateTokens = (text) => {
  if (!text) return 0;
  return Math.ceil(text.length / 4);
};

/**
 * Calcula custo estimado baseado no provedor
 */
const calculateCost = (provider, tokensInput, tokensOutput) => {
  // Preços por 1K tokens (aproximados)
  const pricing = {
    gemini: {
      input: 0.00025, // $0.25 por 1M tokens
      output: 0.0005  // $0.50 por 1M tokens
    },
    openai: {
      'gpt-4': { input: 0.03, output: 0.06 },
      'gpt-4-turbo': { input: 0.01, output: 0.03 },
      'gpt-3.5-turbo': { input: 0.0015, output: 0.002 }
    },
    groq: {
      input: 0.0002,
      output: 0.0002
    }
  };

  if (provider === 'gemini') {
    return ((tokensInput / 1000) * pricing.gemini.input) + 
           ((tokensOutput / 1000) * pricing.gemini.output);
  }

  if (provider === 'groq') {
    return ((tokensInput / 1000) * pricing.groq.input) + 
           ((tokensOutput / 1000) * pricing.groq.output);
  }

  // OpenAI - precisa do modelo específico
  if (provider.startsWith('openai-')) {
    const model = provider.replace('openai-', '');
    const modelPricing = pricing.openai[model] || pricing.openai['gpt-3.5-turbo'];
    return ((tokensInput / 1000) * modelPricing.input) + 
           ((tokensOutput / 1000) * modelPricing.output);
  }

  return 0; // Desconhecido
};

/**
 * Registra uso de IA no banco de dados
 */
export const logAIUsage = async ({
  provider,
  serviceType,
  tokensInput = null,
  tokensOutput = null,
  responseTimeMs = null,
  success = true,
  errorMessage = null,
  userId = null,
  curriculoId = null,
  model = null
}) => {
  if (!supabaseAdmin) {
    console.warn('⚠️  Supabase não configurado, não é possível registrar uso de IA');
    return null;
  }

  try {
    // Estima tokens se não fornecidos
    const estimatedInput = tokensInput || 0;
    const estimatedOutput = tokensOutput || 0;

    // Calcula custo estimado
    const costEstimate = calculateCost(provider, estimatedInput, estimatedOutput);

    // Prepara provider com modelo se necessário
    const providerKey = model && provider === 'openai' ? `openai-${model}` : provider;

    const { data, error } = await supabaseAdmin
      .from('ai_usage_log')
      .insert({
        provider: providerKey,
        service_type: serviceType,
        tokens_input: estimatedInput,
        tokens_output: estimatedOutput,
        cost_estimate: costEstimate,
        response_time_ms: responseTimeMs,
        success: success,
        error_message: errorMessage,
        id_usuario: userId,
        id_curriculo: curriculoId
      })
      .select()
      .single();

    if (error) {
      console.error('Erro ao registrar uso de IA:', error);
      return null;
    }

    return data;
  } catch (error) {
    console.error('Erro ao registrar uso de IA:', error);
    return null;
  }
};

/**
 * Obtém estatísticas de uso de IA
 */
export const getAIUsageStats = async (period = 'day') => {
  if (!supabaseAdmin) {
    return null;
  }

  try {
    const now = new Date();
    let startDate;

    switch (period) {
      case 'hour':
        startDate = new Date(now.getTime() - 60 * 60 * 1000); // Última hora
        break;
      case 'day':
        startDate = new Date(now.setHours(0, 0, 0, 0)); // Hoje
        break;
      case 'week':
        startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
        break;
      case 'month':
        startDate = new Date(now.getFullYear(), now.getMonth(), 1);
        break;
      default:
        startDate = new Date(now.setHours(0, 0, 0, 0));
    }

    // Total de requisições hoje
    const { count: todayCount } = await supabaseAdmin
      .from('ai_usage_log')
      .select('*', { count: 'exact', head: true })
      .eq('provider', 'gemini')
      .gte('criado_em', startDate.toISOString())
      .eq('success', true);

    // Requisições por serviço
    const { data: byService } = await supabaseAdmin
      .from('ai_usage_log')
      .select('service_type')
      .eq('provider', 'gemini')
      .gte('criado_em', startDate.toISOString())
      .eq('success', true);

    const serviceCounts = {};
    byService?.forEach(item => {
      serviceCounts[item.service_type] = (serviceCounts[item.service_type] || 0) + 1;
    });

    // Requisições por hora (últimas 24h)
    const { data: hourlyData } = await supabaseAdmin
      .from('ai_usage_log')
      .select('criado_em')
      .eq('provider', 'gemini')
      .gte('criado_em', new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString())
      .eq('success', true);

    const hourlyStats = {};
    hourlyData?.forEach(item => {
      const date = new Date(item.criado_em);
      const hourKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')} ${String(date.getHours()).padStart(2, '0')}:00`;
      hourlyStats[hourKey] = (hourlyStats[hourKey] || 0) + 1;
    });

    // Limites do Gemini
    const dailyLimit = 1500;
    const hourlyLimit = 900; // 15 req/min * 60 min

    const usagePercentage = (todayCount / dailyLimit) * 100;
    const isNearLimit = usagePercentage > 80;

    return {
      today: {
        used: todayCount || 0,
        limit: dailyLimit,
        remaining: dailyLimit - (todayCount || 0),
        percentage: usagePercentage,
        isNearLimit
      },
      byService: serviceCounts,
      hourly: hourlyStats
    };
  } catch (error) {
    console.error('Erro ao obter estatísticas de uso de IA:', error);
    return null;
  }
};
