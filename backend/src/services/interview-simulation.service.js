import { GoogleGenerativeAI } from '@google/generative-ai';
import OpenAI from 'openai';
import dotenv from 'dotenv';
import { getJobSiteById } from './job-sites.service.js';

dotenv.config();

const genAI = process.env.GEMINI_API_KEY ? new GoogleGenerativeAI(process.env.GEMINI_API_KEY) : null;
// OpenAI DESATIVADO temporariamente para não consumir créditos
// const openai = process.env.OPENAI_API_KEY ? new OpenAI({ apiKey: process.env.OPENAI_API_KEY }) : null;
const openai = null; // Forçado para null para desativar OpenAI
const DEFAULT_GEMINI_MODEL = process.env.GEMINI_MODEL || 'gemini-3-flash-preview';
const DEFAULT_OPENAI_MODEL = process.env.OPENAI_MODEL || 'gpt-4';
const DEFAULT_PROVIDER = process.env.AI_PROVIDER || 'gemini';

/**
 * Gera perguntas de entrevista baseadas no currículo e tecnologia
 */
export const generateInterviewQuestions = async (resumeText, analysis, siteId = null) => {
  try {
    let siteInfo = '';
    if (siteId) {
      const site = await getJobSiteById(siteId);
      if (site) {
        siteInfo = `\n\nCONTEXTO DO SITE DE VAGAS: ${site.nome}\n${site.descricao || ''}`;
      }
    }

    // Identifica tecnologias principais do currículo
    const technologies = extractTechnologies(resumeText, analysis);
    
    const prompt = `Você é um recrutador técnico experiente. Com base no currículo e análise fornecidos, gere uma lista de 8-10 perguntas de entrevista técnica e comportamental relevantes.

CURRÍCULO:
${resumeText.substring(0, 2000)}

ANÁLISE DO CURRÍCULO:
- Habilidades: ${Array.isArray(analysis.habilidades) ? analysis.habilidades.join(', ') : 'Não especificado'}
- Experiência: ${analysis.experiencia || 'Não especificado'}
- Pontos Fortes: ${Array.isArray(analysis.pontosFortes) ? analysis.pontosFortes.slice(0, 5).join(', ') : 'Não especificado'}
- Área de Atuação: ${analysis.areaAtuacao || 'Não especificado'}
${technologies.length > 0 ? `- Tecnologias Identificadas: ${technologies.join(', ')}` : ''}
${siteInfo}

INSTRUÇÕES:
1. Gere perguntas técnicas específicas sobre as tecnologias mencionadas no currículo
2. Inclua perguntas comportamentais (ex: "Conte-me sobre um projeto desafiador")
3. Adapte as perguntas ao nível de experiência indicado
4. Faça perguntas práticas e relevantes para a área
5. Retorne APENAS um array JSON de strings, sem explicações

FORMATO DE RESPOSTA (JSON array):
["Pergunta 1", "Pergunta 2", "Pergunta 3", ...]`;

    console.log('🤖 Gerando perguntas de entrevista com IA...');
    
    let questions = [];
    try {
      if (DEFAULT_PROVIDER === 'gemini' && genAI) {
        const model = genAI.getGenerativeModel({ model: DEFAULT_GEMINI_MODEL });
        const result = await model.generateContent(prompt);
        const response = await result.response;
        const responseText = response.text();
        questions = parseQuestionsFromResponse(responseText);
      } else if (openai) {
        // OpenAI desativado - não usar
        throw new Error('OpenAI está desativado temporariamente');
      } else {
        throw new Error('Nenhuma IA configurada');
      }
    } catch (aiError) {
      console.error('❌ Erro ao gerar perguntas com IA:', aiError);
      // Fallback para perguntas padrão
      questions = generateDefaultQuestions(technologies);
    }

    if (!Array.isArray(questions) || questions.length === 0) {
      questions = generateDefaultQuestions(technologies);
    }

    console.log(`✅ ${questions.length} perguntas geradas`);
    return questions;

  } catch (error) {
    console.error('❌ Erro ao gerar perguntas:', error);
    // Retorna perguntas padrão em caso de erro
    const technologies = extractTechnologies(resumeText, analysis);
    return generateDefaultQuestions(technologies);
  }
};

/**
 * Extrai tecnologias do currículo
 */
const extractTechnologies = (resumeText, analysis) => {
  const techList = [];
  const text = resumeText.toLowerCase();
  
  // Adiciona habilidades da análise
  if (Array.isArray(analysis.habilidades)) {
    techList.push(...analysis.habilidades);
  }
  
  // Padrões de tecnologias comuns
  const techPatterns = [
    'javascript', 'typescript', 'python', 'java', 'c#', 'php', 'ruby', 'go', 'rust',
    'react', 'angular', 'vue', 'node.js', 'express', 'django', 'flask', 'spring',
    'sql', 'mysql', 'postgresql', 'mongodb', 'redis',
    'aws', 'azure', 'gcp', 'docker', 'kubernetes', 'git'
  ];
  
  techPatterns.forEach(tech => {
    if (text.includes(tech)) {
      techList.push(tech);
    }
  });
  
  return [...new Set(techList)].slice(0, 10);
};

/**
 * Parse das perguntas da resposta da IA
 */
const parseQuestionsFromResponse = (response) => {
  try {
    // Remove markdown code blocks
    let cleaned = response.replace(/```json\n?/g, '').replace(/```\n?/g, '').trim();
    
    // Tenta encontrar array JSON
    const match = cleaned.match(/\[.*?\]/s);
    if (match) {
      return JSON.parse(match[0]);
    }
    
    // Tenta parse direto
    return JSON.parse(cleaned);
  } catch (error) {
    console.warn('⚠️ Erro ao fazer parse das perguntas:', error);
    return [];
  }
};

/**
 * Gera perguntas padrão baseadas nas tecnologias
 */
const generateDefaultQuestions = (technologies) => {
  const baseQuestions = [
    'Conte-me sobre você e sua experiência profissional.',
    'Qual foi o projeto mais desafiador que você já trabalhou?',
    'Como você lida com prazos apertados e pressão no trabalho?',
    'Descreva uma situação onde você teve que trabalhar em equipe para resolver um problema.',
    'O que você sabe sobre nossa empresa?',
    'Por que você quer trabalhar conosco?',
    'Quais são suas principais conquistas profissionais?',
    'Como você se mantém atualizado com as novas tecnologias?'
  ];

  const techQuestions = [];
  if (technologies.length > 0) {
    const mainTech = technologies[0];
    techQuestions.push(
      `Explique como você usa ${mainTech} em seus projetos.`,
      `Quais são os principais desafios ao trabalhar com ${mainTech}?`,
      `Conte-me sobre um projeto onde você usou ${mainTech}.`
    );
  }

  return [...techQuestions, ...baseQuestions].slice(0, 10);
};

/**
 * Avalia uma resposta do candidato
 */
export const evaluateAnswer = async (question, answer, resumeText, analysis) => {
  try {
    const prompt = `Você é um recrutador técnico avaliando uma resposta de entrevista.

PERGUNTA:
${question}

RESPOSTA DO CANDIDATO:
${answer}

CONTEXTO DO CURRÍCULO:
${resumeText.substring(0, 1000)}

ANÁLISE DO CURRÍCULO:
- Habilidades: ${Array.isArray(analysis.habilidades) ? analysis.habilidades.join(', ') : 'Não especificado'}
- Experiência: ${analysis.experiencia || 'Não especificado'}

INSTRUÇÕES:
1. Avalie a qualidade da resposta (0-100)
2. Forneça feedback construtivo
3. Identifique pontos fortes e fracos
4. Retorne APENAS um objeto JSON no formato:
{
  "score": 85,
  "feedback": "Feedback detalhado aqui",
  "strengths": ["Ponto forte 1", "Ponto forte 2"],
  "improvements": ["Ponto a melhorar 1", "Ponto a melhorar 2"]
}`;

    let evaluation = null;
    
    if (DEFAULT_PROVIDER === 'gemini' && genAI) {
      const model = genAI.getGenerativeModel({ model: DEFAULT_GEMINI_MODEL });
      const result = await model.generateContent(prompt);
      const response = await result.response;
      const responseText = response.text();
      evaluation = parseEvaluationFromResponse(responseText);
    } else if (openai) {
      const completion = await openai.chat.completions.create({
        model: DEFAULT_OPENAI_MODEL,
        messages: [
          { role: 'system', content: 'Você é um recrutador técnico. Retorne apenas JSON.' },
          { role: 'user', content: prompt }
        ],
        temperature: 0.7,
        max_tokens: 500
      });
      evaluation = parseEvaluationFromResponse(completion.choices[0].message.content);
    }

    // Fallback se não conseguir avaliar
    if (!evaluation) {
      evaluation = {
        score: 70,
        feedback: 'Resposta recebida. Continue com a próxima pergunta.',
        strengths: ['Respondeu à pergunta'],
        improvements: ['Pode ser mais específico']
      };
    }

    return evaluation;

  } catch (error) {
    console.error('❌ Erro ao avaliar resposta:', error);
    
    // OpenAI desativado - não usar como fallback
    // Se for erro de quota, apenas retorna fallback básico
    
    // Fallback básico se ambos falharem
    return {
      score: 70,
      feedback: 'Resposta recebida. Continue com a próxima pergunta.',
      strengths: ['Resposta fornecida'],
      improvements: ['Tente ser mais específico e detalhado']
    };
  }
};

/**
 * Parse da avaliação da resposta da IA
 */
const parseEvaluationFromResponse = (response) => {
  try {
    let cleaned = response.replace(/```json\n?/g, '').replace(/```\n?/g, '').trim();
    const match = cleaned.match(/\{.*\}/s);
    if (match) {
      return JSON.parse(match[0]);
    }
    return JSON.parse(cleaned);
  } catch (error) {
    console.warn('⚠️ Erro ao fazer parse da avaliação:', error);
    return null;
  }
};
