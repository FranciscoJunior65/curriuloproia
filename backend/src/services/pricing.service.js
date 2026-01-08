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
    description: '1 análise completa otimizada para sites de vagas',
    analyses: 1,
    priceBRL: 10.90,
    priceUSD: 1.98,
    features: [
      '1 análise completa com IA',
      'Otimização para sites de vagas (Gupy, LinkedIn, Vagas.com, InfoJobs, Catho, Indeed)',
      'Score detalhado e recomendações personalizadas',
      'Currículo melhorado em PDF ou WORD',
      'Palavras-chave estratégicas',
      'Análise única para um site específico'
    ]
  },
  pack3: {
    id: 'pack3',
    name: 'Pacote 3 Análises',
    description: '3 análises completas otimizadas para diferentes sites',
    analyses: 3,
    priceBRL: 27.90, // Economia de R$ 5,80 comparado a 3x single
    priceUSD: 5.58,
    savings: 'Economize R$ 5,80',
    features: [
      '3 análises completas com IA',
      'Otimização para diferentes sites de vagas',
      'Score detalhado para cada análise',
      'Recomendações personalizadas',
      '3 currículos melhorados em PDF ou WORD',
      'Melhor custo-benefício'
    ]
  },
  pack5: {
    id: 'pack5',
    name: 'Pacote 5 Análises',
    description: '5 análises completas otimizadas para diferentes sites',
    analyses: 5,
    priceBRL: 37.90, // Economia de R$ 16,60 comparado a 5x single
    priceUSD: 7.58,
    savings: 'Economize R$ 16,60',
    features: [
      '5 análises completas com IA',
      'Otimização para diferentes sites de vagas',
      'Score detalhado para cada análise',
      'Recomendações personalizadas',
      '5 currículos melhorados em PDF ou WORD',
      'Máxima economia'
    ]
  },
  english: {
    id: 'english',
    name: 'Currículo em Inglês',
    description: 'Geração de currículo profissional em inglês (apenas PDF e WORD, sem análise)',
    analyses: 0, // Não adiciona créditos, é apenas serviço
    priceBRL: 9.90, // Preço normal quando comprado separadamente
    priceBRLBundle: 5.90, // Preço promocional quando comprado junto com análise
    priceUSD: 1.98,
    features: [
      'Currículo traduzido e adaptado para padrões internacionais',
      'Formatação ATS-friendly',
      'Download em PDF ou WORD',
      'Otimizado para vagas globais',
      'Adaptação cultural profissional'
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

