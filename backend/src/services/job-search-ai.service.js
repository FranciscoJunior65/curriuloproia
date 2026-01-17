// Importa funções necessárias do ai.service
import { GoogleGenerativeAI } from '@google/generative-ai';
import OpenAI from 'openai';
import dotenv from 'dotenv';

dotenv.config();

const genAI = process.env.GEMINI_API_KEY ? new GoogleGenerativeAI(process.env.GEMINI_API_KEY) : null;
const openai = process.env.OPENAI_API_KEY ? new OpenAI({ apiKey: process.env.OPENAI_API_KEY }) : null;
const DEFAULT_GEMINI_MODEL = process.env.GEMINI_MODEL || 'gemini-3-flash-preview';
const DEFAULT_OPENAI_MODEL = process.env.OPENAI_MODEL || 'gpt-4';

/**
 * Chama Gemini para gerar texto
 */
const callGemini = async (prompt, options = {}) => {
  if (!genAI) {
    throw new Error('Gemini não configurado');
  }
  
  const model = genAI.getGenerativeModel({ model: DEFAULT_GEMINI_MODEL });
  const result = await model.generateContent(prompt);
  const response = await result.response;
  return response.text();
};

/**
 * Chama OpenAI para gerar texto
 */
const callOpenAI = async (prompt, options = {}) => {
  if (!openai) {
    throw new Error('OpenAI não configurado');
  }
  
  const completion = await openai.chat.completions.create({
    model: DEFAULT_OPENAI_MODEL,
    messages: [
      { role: 'system', content: 'Você é um assistente especializado em recrutamento e busca de vagas.' },
      { role: 'user', content: prompt }
    ],
    temperature: options.temperature || 0.7,
    max_tokens: options.max_tokens || 1000
  });
  
  return completion.choices[0].message.content;
};

/**
 * Gera palavras-chave otimizadas usando IA baseado no currículo e análise
 */
export const generateSearchKeywordsWithAI = async (resumeText, analysis, siteInfo) => {
  try {
    const prompt = `Você é um especialista em recrutamento e busca de vagas. Analise o currículo e a análise fornecida para gerar palavras-chave otimizadas para busca de vagas no site ${siteInfo.nome}.

CURRÍCULO:
${resumeText.substring(0, 2000)}

ANÁLISE DO CURRÍCULO:
- Habilidades: ${Array.isArray(analysis.habilidades) ? analysis.habilidades.join(', ') : 'Não especificado'}
- Experiência: ${analysis.experiencia || 'Não especificado'}
- Pontos Fortes: ${Array.isArray(analysis.pontosFortes) ? analysis.pontosFortes.slice(0, 5).join(', ') : 'Não especificado'}
- Área de Atuação: ${analysis.areaAtuacao || 'Não especificado'}

CARACTERÍSTICAS DO SITE ${siteInfo.nome}:
${JSON.stringify(siteInfo.caracteristicas, null, 2)}

PALAVRAS-CHAVE PADRÃO DO SITE:
${Array.isArray(siteInfo.palavras_chave_padrao) ? siteInfo.palavras_chave_padrao.join(', ') : 'Nenhuma'}

INSTRUÇÕES:
1. Gere 15-20 palavras-chave relevantes para busca de vagas
2. Inclua tecnologias, habilidades técnicas, soft skills e termos do mercado
3. Priorize palavras-chave que combinem com o perfil do candidato
4. Considere sinônimos e variações de termos técnicos
5. Inclua termos específicos do site ${siteInfo.nome}
6. Retorne APENAS um array JSON de strings, sem explicações adicionais

FORMATO DE RESPOSTA (JSON array):
["palavra-chave 1", "palavra-chave 2", "palavra-chave 3", ...]`;

    console.log('🤖 Gerando palavras-chave com IA...');
    
    const response = await callGemini(prompt, { temperature: 0.7 });
    
    // Tenta extrair o array JSON da resposta
    let keywords = [];
    try {
      // Remove markdown code blocks se houver
      const cleanedResponse = response.replace(/```json\n?/g, '').replace(/```\n?/g, '').trim();
      keywords = JSON.parse(cleanedResponse);
    } catch (parseError) {
      // Se não conseguir fazer parse, tenta extrair manualmente
      const match = response.match(/\[.*?\]/s);
      if (match) {
        keywords = JSON.parse(match[0]);
      } else {
        // Fallback: usa palavras-chave básicas
        keywords = extractFallbackKeywords(analysis);
      }
    }
    
    if (!Array.isArray(keywords) || keywords.length === 0) {
      keywords = extractFallbackKeywords(analysis);
    }
    
    console.log(`✅ ${keywords.length} palavras-chave geradas: ${keywords.slice(0, 5).join(', ')}...`);
    return keywords;
    
  } catch (error) {
    console.error('❌ Erro ao gerar palavras-chave com IA:', error);
    // Fallback para palavras-chave básicas
    return extractFallbackKeywords(analysis);
  }
};

/**
 * Extrai palavras-chave básicas como fallback
 */
const extractFallbackKeywords = (analysis) => {
  const keywords = [];
  
  if (Array.isArray(analysis.habilidades)) {
    keywords.push(...analysis.habilidades);
  }
  
  if (analysis.areaAtuacao) {
    keywords.push(analysis.areaAtuacao);
  }
  
  // Adiciona termos técnicos comuns
  const techTerms = ['desenvolvedor', 'programador', 'analista', 'engenheiro', 'tecnologia', 'software'];
  keywords.push(...techTerms);
  
  return [...new Set(keywords)].slice(0, 15);
};

/**
 * Gera combinações de palavras-chave para múltiplas buscas
 */
export const generateSearchCombinations = (keywords, maxCombinations = 10) => {
  const combinations = [];
  
  // Combinações individuais (palavras-chave mais importantes)
  const topKeywords = keywords.slice(0, 8);
  topKeywords.forEach(keyword => {
    combinations.push([keyword]);
  });
  
  // Combinações de 2 palavras (mais relevantes)
  for (let i = 0; i < Math.min(5, topKeywords.length - 1); i++) {
    for (let j = i + 1; j < Math.min(i + 3, topKeywords.length); j++) {
      combinations.push([topKeywords[i], topKeywords[j]]);
    }
  }
  
  // Combinações de 3 palavras (se ainda houver espaço)
  if (combinations.length < maxCombinations && topKeywords.length >= 3) {
    for (let i = 0; i < Math.min(3, topKeywords.length - 2); i++) {
      combinations.push([
        topKeywords[i],
        topKeywords[i + 1],
        topKeywords[i + 2]
      ]);
    }
  }
  
  // Limita o número de combinações
  return combinations.slice(0, maxCombinations);
};

/**
 * Calcula score de compatibilidade entre vaga e currículo
 */
export const calculateCompatibilityScore = (jobData, analysis, keywords) => {
  let score = 0;
  const matchedKeywords = [];
  
  const jobText = `${jobData.title} ${jobData.company} ${jobData.description || ''} ${jobData.requirements || ''}`.toLowerCase();
  
  // Verifica correspondência de palavras-chave
  keywords.forEach(keyword => {
    if (jobText.includes(keyword.toLowerCase())) {
      score += 10;
      matchedKeywords.push(keyword);
    }
  });
  
  // Verifica habilidades do currículo
  if (Array.isArray(analysis.habilidades)) {
    analysis.habilidades.forEach(habilidade => {
      if (jobText.includes(habilidade.toLowerCase())) {
        score += 15;
      }
    });
  }
  
  // Verifica área de atuação
  if (analysis.areaAtuacao && jobText.includes(analysis.areaAtuacao.toLowerCase())) {
    score += 20;
  }
  
  // Limita o score entre 0 e 100
  score = Math.min(100, score);
  
  return {
    score,
    matchedKeywords: [...new Set(matchedKeywords)]
  };
};
