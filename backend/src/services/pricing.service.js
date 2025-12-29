import dotenv from 'dotenv';

dotenv.config();

/**
 * Preços por token da OpenAI (em USD)
 * Fonte: https://openai.com/pricing (valores aproximados)
 */
const OPENAI_PRICING = {
  'gpt-4': {
    input: 0.03 / 1000,   // $0.03 por 1K tokens de entrada
    output: 0.06 / 1000   // $0.06 por 1K tokens de saída
  },
  'gpt-4-turbo': {
    input: 0.01 / 1000,   // $0.01 por 1K tokens de entrada
    output: 0.03 / 1000   // $0.03 por 1K tokens de saída
  },
  'gpt-4o': {
    input: 0.005 / 1000,  // $0.005 por 1K tokens de entrada
    output: 0.015 / 1000 // $0.015 por 1K tokens de saída
  },
  'gpt-3.5-turbo': {
    input: 0.0005 / 1000, // $0.0005 por 1K tokens de entrada
    output: 0.0015 / 1000  // $0.0015 por 1K tokens de saída
  }
};

// Taxa de câmbio USD para BRL (pode ser atualizada ou usar API)
const USD_TO_BRL = 5.0; // Ajuste conforme necessário

/**
 * Estima tokens baseado em caracteres (aproximação: 1 token ≈ 4 caracteres)
 */
const estimateTokens = (text) => {
  return Math.ceil(text.length / 4);
};

/**
 * Calcula o custo de uma análise
 */
export const calculateAnalysisCost = (resumeText, analysisText, model = 'gpt-4') => {
  const modelPricing = OPENAI_PRICING[model] || OPENAI_PRICING['gpt-4'];
  
  // Tokens de entrada: prompt do sistema + prompt do usuário + texto do currículo
  const systemPromptTokens = 200; // Aproximação
  const userPromptTokens = 300; // Aproximação
  const resumeTokens = estimateTokens(resumeText);
  const inputTokens = systemPromptTokens + userPromptTokens + resumeTokens;
  
  // Tokens de saída: resposta da análise
  const outputTokens = estimateTokens(analysisText);
  
  // Custo em USD
  const costUSD = (inputTokens * modelPricing.input) + (outputTokens * modelPricing.output);
  
  // Custo em BRL
  const costBRL = costUSD * USD_TO_BRL;
  
  return {
    inputTokens,
    outputTokens,
    totalTokens: inputTokens + outputTokens,
    costUSD: parseFloat(costUSD.toFixed(4)),
    costBRL: parseFloat(costBRL.toFixed(2))
  };
};

/**
 * Calcula o custo de geração de currículo melhorado
 */
export const calculateGenerationCost = (originalText, improvedText, model = 'gpt-4') => {
  const modelPricing = OPENAI_PRICING[model] || OPENAI_PRICING['gpt-4'];
  
  const systemPromptTokens = 150;
  const userPromptTokens = 200;
  const originalTokens = estimateTokens(originalText);
  const inputTokens = systemPromptTokens + userPromptTokens + originalTokens;
  
  const outputTokens = estimateTokens(improvedText);
  
  const costUSD = (inputTokens * modelPricing.input) + (outputTokens * modelPricing.output);
  const costBRL = costUSD * USD_TO_BRL;
  
  return {
    inputTokens,
    outputTokens,
    totalTokens: inputTokens + outputTokens,
    costUSD: parseFloat(costUSD.toFixed(4)),
    costBRL: parseFloat(costBRL.toFixed(2))
  };
};

/**
 * Custo total de uma operação completa (análise + geração)
 */
export const calculateTotalCost = (resumeText, analysisText, improvedText, model = 'gpt-4') => {
  const analysisCost = calculateAnalysisCost(resumeText, analysisText, model);
  const generationCost = calculateGenerationCost(resumeText, improvedText, model);
  
  return {
    analysis: analysisCost,
    generation: generationCost,
    totalUSD: parseFloat((analysisCost.costUSD + generationCost.costUSD).toFixed(4)),
    totalBRL: parseFloat((analysisCost.costBRL + generationCost.costBRL).toFixed(2))
  };
};

/**
 * Planos de preço com margem de lucro
 */
export const PRICING_PLANS = {
  single: {
    id: 'single',
    name: 'Análise Única',
    description: '1 análise completa + 1 currículo melhorado em PDF',
    analyses: 1,
    priceBRL: 9.90, // Preço acessível
    priceUSD: 1.98,
    features: [
      '1 análise completa com IA',
      'Score detalhado',
      'Recomendações personalizadas',
      '1 currículo melhorado em PDF'
    ]
  },
  pack3: {
    id: 'pack3',
    name: 'Pacote 3 Análises',
    description: '3 análises completas + 3 currículos melhorados',
    analyses: 3,
    priceBRL: 24.90, // Economia de ~16% comparado a 3x single
    priceUSD: 4.98,
    savings: 'Economize R$ 4,80',
    features: [
      '3 análises completas com IA',
      'Score detalhado para cada',
      'Recomendações personalizadas',
      '3 currículos melhorados em PDF',
      'Melhor custo-benefício'
    ]
  }
};

/**
 * Calcula margem de lucro estimada
 * Assumindo custo médio de ~R$ 0.50 por análise completa (análise + geração)
 */
export const calculateProfitMargin = (planId) => {
  const plan = PRICING_PLANS[planId];
  const estimatedCostPerAnalysis = 0.50; // Custo estimado por análise completa
  const totalCost = estimatedCostPerAnalysis * plan.analyses;
  const profit = plan.priceBRL - totalCost;
  const margin = (profit / plan.priceBRL) * 100;
  
  return {
    totalCost: parseFloat(totalCost.toFixed(2)),
    profit: parseFloat(profit.toFixed(2)),
    margin: parseFloat(margin.toFixed(1))
  };
};

