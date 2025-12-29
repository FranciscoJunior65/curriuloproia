import OpenAI from 'openai';
import dotenv from 'dotenv';

dotenv.config();

// Valida se a chave da API está configurada
if (!process.env.OPENAI_API_KEY) {
  console.warn('⚠️  OPENAI_API_KEY não configurada no .env');
}

const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY
});

// Modelo padrão, pode ser alterado via variável de ambiente
const DEFAULT_MODEL = process.env.OPENAI_MODEL || 'gpt-4';

// Modelos que suportam response_format json_object
const MODELS_WITH_JSON_SUPPORT = [
  'gpt-4-turbo',
  'gpt-4-turbo-preview',
  'gpt-4-0125-preview',
  'gpt-4-1106-preview',
  'gpt-4o',
  'gpt-4o-mini',
  'gpt-3.5-turbo-1106',
  'gpt-3.5-turbo-0125'
];

/**
 * Verifica se o modelo suporta response_format json_object
 */
const supportsJsonFormat = (model) => {
  return MODELS_WITH_JSON_SUPPORT.some(supportedModel => 
    model.includes(supportedModel) || model.startsWith('gpt-4-turbo') || model.startsWith('gpt-4o')
  );
};

/**
 * Valida e limita o tamanho do texto do currículo
 * GPT-4 tem limite de ~8192 tokens de entrada
 */
const validateAndTruncateText = (text, maxLength = 15000) => {
  if (!text || text.trim().length === 0) {
    throw new Error('Texto do currículo está vazio');
  }

  if (text.length > maxLength) {
    console.warn(`Texto truncado de ${text.length} para ${maxLength} caracteres`);
    return text.substring(0, maxLength) + '... [texto truncado]';
  }

  return text;
};

/**
 * Analisa um currículo usando dados mockados (para testes)
 */
export const analyzeResumeWithAIMock = async (resumeText) => {
  console.log('🎭 Usando análise MOCKADA (não consome créditos OpenAI)');
  
  // Simula um delay de processamento
  await new Promise(resolve => setTimeout(resolve, 1000));
  
  // Retorna análise mockada baseada no texto
  const textLength = resumeText.length;
  const textLower = resumeText.toLowerCase();
  const hasEmail = textLower.includes('@') || /\b[\w\.-]+@[\w\.-]+\.\w+\b/i.test(resumeText);
  const hasPhone = /\d{10,}/.test(resumeText) || /\(\d{2}\)\s?\d{4,5}-?\d{4}/.test(resumeText);
  const hasExperience = /experiência|experience|trabalho|work|empresa|company|profissional|professional/i.test(resumeText);
  const hasEducation = /formação|education|graduação|graduation|curso|course|universidade|university|faculdade|college/i.test(resumeText);
  const hasSkills = /habilidade|skill|competência|competency|conhecimento|knowledge/i.test(resumeText);
  
  // Calcula score baseado em indicadores
  let score = 50;
  if (hasEmail) score += 10;
  if (hasPhone) score += 10;
  if (hasExperience) score += 15;
  if (hasEducation) score += 15;
  if (hasSkills) score += 10;
  if (textLength > 500) score += 5;
  if (textLength > 1000) score += 5;
  score = Math.min(100, Math.max(0, score));
  
  // Gera pontos fortes baseados no conteúdo
  const pontosFortes = [];
  if (hasEmail) pontosFortes.push('Email de contato presente');
  if (hasPhone) pontosFortes.push('Telefone de contato presente');
  if (hasExperience) pontosFortes.push('Experiência profissional mencionada');
  if (hasEducation) pontosFortes.push('Formação acadêmica mencionada');
  if (hasSkills) pontosFortes.push('Habilidades e competências destacadas');
  if (textLength > 500) pontosFortes.push('Currículo com conteúdo detalhado');
  if (pontosFortes.length === 0) pontosFortes.push('Estrutura básica do currículo presente');
  
  // Gera pontos a melhorar
  const pontosMelhorar = [];
  if (!hasEmail) pontosMelhorar.push('Adicione um email de contato profissional');
  if (!hasPhone) pontosMelhorar.push('Adicione um telefone de contato');
  if (!hasExperience) pontosMelhorar.push('Destaque sua experiência profissional com períodos e responsabilidades');
  if (!hasEducation) pontosMelhorar.push('Mencione sua formação acadêmica com instituições e períodos');
  if (!hasSkills) pontosMelhorar.push('Liste suas principais habilidades técnicas e comportamentais');
  if (textLength < 500) pontosMelhorar.push('Adicione mais detalhes e informações relevantes');
  if (pontosMelhorar.length === 0) pontosMelhorar.push('Revise a formatação e organização do currículo');
  
  // Gera habilidades baseadas em palavras-chave comuns
  const habilidades = [];
  if (/javascript|js|node|react|angular|vue/i.test(resumeText)) habilidades.push('JavaScript');
  if (/python|django|flask/i.test(resumeText)) habilidades.push('Python');
  if (/java|spring/i.test(resumeText)) habilidades.push('Java');
  if (/sql|database|banco de dados/i.test(resumeText)) habilidades.push('Banco de Dados');
  if (/git|github|versionamento/i.test(resumeText)) habilidades.push('Controle de Versão');
  if (/html|css|web/i.test(resumeText)) habilidades.push('Desenvolvimento Web');
  if (/gerenciamento|management|gestão/i.test(resumeText)) habilidades.push('Gestão');
  if (/comunicação|communication/i.test(resumeText)) habilidades.push('Comunicação');
  if (/trabalho em equipe|team work|colaboração/i.test(resumeText)) habilidades.push('Trabalho em Equipe');
  if (habilidades.length === 0) {
    habilidades.push('Comunicação', 'Trabalho em Equipe', 'Organização', 'Proatividade');
  }
  
  return {
    pontosFortes: pontosFortes.slice(0, 5),
    pontosMelhorar: pontosMelhorar.slice(0, 5),
    experiencia: hasExperience 
      ? 'Experiência profissional identificada no currículo. Recomenda-se detalhar períodos, empresas, cargos e principais responsabilidades e conquistas em cada posição.'
      : 'Experiência profissional não encontrada ou não detalhada. É importante destacar seu histórico profissional com datas, empresas, cargos e responsabilidades.',
    formacao: hasEducation
      ? 'Formação acadêmica identificada. Recomenda-se incluir instituições, cursos, períodos de conclusão e qualquer certificação ou curso complementar relevante.'
      : 'Formação acadêmica não encontrada ou não detalhada. É importante destacar sua educação formal, cursos técnicos, graduações e especializações.',
    habilidades: habilidades.slice(0, 10),
    recomendacoes: [
      'Revise e atualize suas informações de contato (email e telefone)',
      'Destaque suas principais conquistas e resultados quantificáveis',
      'Organize as informações de forma clara e cronológica',
      'Inclua palavras-chave relevantes para sua área de atuação',
      'Mantenha o currículo atualizado e adaptado para cada oportunidade'
    ],
    score: score
  };
};

/**
 * Analisa um currículo usando OpenAI GPT ou Mock (baseado em variável de ambiente)
 */
export const analyzeResumeWithAI = async (resumeText) => {
  // Verifica se deve usar mock
  const useMock = process.env.USE_MOCK_AI === 'true' || process.env.USE_MOCK_AI === '1';
  
  if (useMock) {
    return await analyzeResumeWithAIMock(resumeText);
  }
  
  try {
    // Valida e prepara o texto
    const validatedText = validateAndTruncateText(resumeText);

    // Prompt do sistema - define o papel do assistente
    const systemPrompt = `Você é um especialista em Recursos Humanos e análise de currículos com mais de 10 anos de experiência. 
Sua função é analisar currículos de forma objetiva, construtiva e detalhada, identificando:
- Pontos fortes e áreas de destaque
- Pontos que precisam de melhoria
- Experiência profissional relevante
- Formação acadêmica
- Habilidades técnicas e comportamentais
- Recomendações práticas para melhorar o currículo

Seja sempre construtivo e específico em suas análises.`;

    // Prompt do usuário - instruções detalhadas
    const userPrompt = `Analise o seguinte currículo e forneça uma análise completa e detalhada em formato JSON.

INSTRUÇÕES:
1. Analise cuidadosamente todo o conteúdo do currículo
2. Identifique pelo menos 3-5 pontos fortes relevantes
3. Identifique 3-5 pontos que podem ser melhorados (seja construtivo)
4. Faça um resumo objetivo da experiência profissional
5. Faça um resumo da formação acadêmica
6. Liste todas as habilidades técnicas e comportamentais identificadas
7. Forneça 3-5 recomendações práticas e específicas para melhorar o currículo
8. Atribua um score de 0 a 100 baseado em: clareza, organização, relevância das informações, completude e impacto

FORMATO DE RESPOSTA (JSON obrigatório):
{
  "pontosFortes": ["ponto 1", "ponto 2", ...],
  "pontosMelhorar": ["ponto 1", "ponto 2", ...],
  "experiencia": "resumo detalhado da experiência profissional",
  "formacao": "resumo da formação acadêmica",
  "habilidades": ["habilidade 1", "habilidade 2", ...],
  "recomendacoes": ["recomendação 1", "recomendação 2", ...],
  "score": 85
}

CURRÍCULO PARA ANÁLISE:
${validatedText}

IMPORTANTE: Responda APENAS com o JSON válido, sem texto adicional antes ou depois.`;

    console.log(`🤖 Iniciando análise com modelo: ${DEFAULT_MODEL}`);
    
    // Configuração base da requisição
    const requestConfig = {
      model: DEFAULT_MODEL,
      messages: [
        {
          role: "system",
          content: systemPrompt
        },
        {
          role: "user",
          content: userPrompt
        }
      ],
      temperature: 0.7, // Balance entre criatividade e consistência
      max_tokens: 2000 // Limite de tokens na resposta
    };

    // Adiciona response_format apenas se o modelo suportar
    if (supportsJsonFormat(DEFAULT_MODEL)) {
      requestConfig.response_format = { type: "json_object" };
      console.log('✅ Usando response_format json_object (suportado pelo modelo)');
    } else {
      console.log('⚠️  Modelo não suporta response_format json_object, usando parsing manual');
      // Melhora o prompt para garantir resposta JSON
      requestConfig.messages[1].content = userPrompt + '\n\nCRÍTICO: Sua resposta DEVE ser APENAS um objeto JSON válido, sem markdown, sem código, sem explicações. Apenas o JSON puro.';
    }

    const completion = await openai.chat.completions.create(requestConfig);

    let responseContent = completion.choices[0].message.content;
    
    if (!responseContent) {
      throw new Error('Resposta vazia da API OpenAI');
    }

    // Limpa a resposta caso contenha markdown ou código
    responseContent = responseContent.trim();
    
    // Remove markdown code blocks se existirem
    if (responseContent.startsWith('```json')) {
      responseContent = responseContent.replace(/^```json\s*/, '').replace(/\s*```$/, '');
    } else if (responseContent.startsWith('```')) {
      responseContent = responseContent.replace(/^```\s*/, '').replace(/\s*```$/, '');
    }

    // Parse do JSON
    let analysis;
    try {
      analysis = JSON.parse(responseContent);
    } catch (parseError) {
      console.error('Erro ao fazer parse do JSON. Resposta recebida:', responseContent.substring(0, 200));
      throw new Error(`Resposta da IA não está em formato JSON válido: ${parseError.message}`);
    }

    // Validação da estrutura da resposta
    validateAnalysisStructure(analysis);

    console.log('✅ Análise concluída com sucesso');
    return analysis;

  } catch (error) {
    console.error('❌ Erro ao chamar OpenAI:', error);
    
    // Tratamento de erros específicos da API
    if (error instanceof OpenAI.APIError) {
      if (error.status === 401) {
        throw new Error('Chave de API inválida. Verifique OPENAI_API_KEY no .env');
      } else if (error.status === 400) {
        // Erro 400 geralmente é parâmetro inválido
        if (error.message && error.message.includes('response_format')) {
          throw new Error(`O modelo ${DEFAULT_MODEL} não suporta response_format json_object. Use um modelo mais recente como gpt-4-turbo ou gpt-4o.`);
        }
        throw new Error(`Parâmetro inválido: ${error.message}`);
      } else if (error.status === 429) {
        throw new Error('Limite de requisições excedido. Tente novamente em alguns instantes.');
      } else if (error.status === 500) {
        throw new Error('Erro interno da OpenAI. Tente novamente mais tarde.');
      }
    }

    throw new Error(`Erro na análise com IA: ${error.message}`);
  }
};

/**
 * Valida se a estrutura da análise está correta
 */
const validateAnalysisStructure = (analysis) => {
  const requiredFields = [
    'pontosFortes',
    'pontosMelhorar',
    'experiencia',
    'formacao',
    'habilidades',
    'recomendacoes',
    'score'
  ];

  for (const field of requiredFields) {
    if (!(field in analysis)) {
      throw new Error(`Campo obrigatório ausente na análise: ${field}`);
    }
  }

  // Validações de tipo
  if (!Array.isArray(analysis.pontosFortes)) {
    throw new Error('pontosFortes deve ser um array');
  }
  if (!Array.isArray(analysis.pontosMelhorar)) {
    throw new Error('pontosMelhorar deve ser um array');
  }
  if (!Array.isArray(analysis.habilidades)) {
    throw new Error('habilidades deve ser um array');
  }
  if (!Array.isArray(analysis.recomendacoes)) {
    throw new Error('recomendacoes deve ser um array');
  }
  if (typeof analysis.score !== 'number' || analysis.score < 0 || analysis.score > 100) {
    throw new Error('score deve ser um número entre 0 e 100');
  }
  if (typeof analysis.experiencia !== 'string') {
    throw new Error('experiencia deve ser uma string');
  }
  if (typeof analysis.formacao !== 'string') {
    throw new Error('formacao deve ser uma string');
  }
};


