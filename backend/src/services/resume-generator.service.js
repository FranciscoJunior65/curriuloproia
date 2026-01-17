import OpenAI from 'openai';
import dotenv from 'dotenv';
import PDFDocument from 'pdfkit';
import { getJobSiteById } from './job-sites.service.js';

dotenv.config();

const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY
});

const DEFAULT_MODEL = process.env.OPENAI_MODEL || 'gpt-4';

/**
 * Gera um currículo melhorado baseado na análise e no currículo original
 * @param {string} originalText - Texto original do currículo
 * @param {object} analysis - Análise do currículo
 * @param {string|null} siteId - ID do site de vagas para personalização
 */
export const generateImprovedResume = async (originalText, analysis, siteId = null) => {
  try {
    let siteInfo = '';
    let siteKeywords = [];
    let siteCharacteristics = {};

    // Busca informações do site de vagas se fornecido
    if (siteId) {
      console.log(`📍 Buscando informações do site de vagas: ${siteId}`);
      try {
        const site = await getJobSiteById(siteId);
        if (site) {
          console.log(`✅ Site encontrado: ${site.nome}`);
          console.log(`🔑 Palavras-chave: ${Array.isArray(site.palavras_chave_padrao) ? site.palavras_chave_padrao.join(', ') : 'Nenhuma'}`);
          siteKeywords = Array.isArray(site.palavras_chave_padrao) ? site.palavras_chave_padrao : [];
          siteCharacteristics = site.caracteristicas && typeof site.caracteristicas === 'object' ? site.caracteristicas : {};
          
          siteInfo = `
═══════════════════════════════════════════════════════════════
CONTEXTO CRÍTICO - SITE DE VAGAS SELECIONADO:
Este currículo será usado no site: ${site.nome}
${site.descricao ? `Descrição do site: ${site.descricao}` : ''}
${site.caracteristicas ? `Características específicas do site: ${JSON.stringify(site.caracteristicas, null, 2)}` : ''}
${siteKeywords.length > 0 ? `PALAVRAS-CHAVE PRIORITÁRIAS PARA ESTE SITE (ESSENCIAIS PARA ATS): ${siteKeywords.join(', ')}` : ''}
═══════════════════════════════════════════════════════════════

IMPORTANTE: Toda a geração DEVE ser adaptada especificamente para o site ${site.nome}.
As palavras-chave acima são CRÍTICAS e devem ser incorporadas naturalmente no texto.
`;
        }
      } catch (error) {
        console.warn('⚠️ Erro ao buscar informações do site:', error.message);
      }
    }

    const systemPrompt = `Você é um especialista em redação de currículos profissionais otimizados para ATS (Applicant Tracking Systems) e análise por IA de recrutadores.
Sua função é reescrever e melhorar currículos aplicando as recomendações fornecidas, mantendo todas as informações verdadeiras e relevantes do currículo original.

IMPORTANTE:
- Mantenha TODAS as informações verdadeiras do currículo original
- Aplique as melhorias sugeridas na análise
- Melhore a formatação e organização
- Use linguagem profissional e clara
- Mantenha a estrutura padrão de currículo (Dados Pessoais, Objetivo, Experiência, Formação, Habilidades)
- Não invente informações que não estavam no original
- Otimize o currículo para passar por sistemas ATS e análise de IA
${siteKeywords.length > 0 ? `- Use naturalmente as seguintes palavras-chave estratégicas relevantes para o site: ${siteKeywords.join(', ')}` : ''}
${siteId ? `- Adapte o currículo especificamente para o site ${siteInfo.includes('site:') ? siteInfo.split('site:')[1].split('\n')[0].trim() : 'selecionado'}` : ''}`;

    const pontosFortes = Array.isArray(analysis.pontosFortes) ? analysis.pontosFortes.join(', ') : (analysis.pontosFortes || 'Não especificado');
    const pontosMelhorar = Array.isArray(analysis.pontosMelhorar) ? analysis.pontosMelhorar.join(', ') : (analysis.pontosMelhorar || 'Não especificado');
    const recomendacoes = Array.isArray(analysis.recomendacoes) ? analysis.recomendacoes.join('; ') : (analysis.recomendacoes || 'Não especificado');

    const userPrompt = `Com base no currículo original e na análise fornecida, gere uma versão melhorada do currículo.

${siteInfo}

CURRÍCULO ORIGINAL:
${originalText}

ANÁLISE E RECOMENDAÇÕES:
- Pontos Fortes: ${pontosFortes}
- Pontos a Melhorar: ${pontosMelhorar}
- Recomendações: ${recomendacoes}

${siteId ? `IMPORTANTE: Este currículo será usado no site ${siteInfo.includes('site:') ? siteInfo.split('site:')[1].split('\n')[0].trim() : 'selecionado'}. Adapte o conteúdo, palavras-chave e formatação para este contexto específico.` : ''}

Gere um currículo melhorado que:
1. Mantém todas as informações verdadeiras do original
2. Aplica as recomendações da análise
3. Melhora a organização e clareza
4. Destaque os pontos fortes identificados
5. Corrige ou melhora os pontos fracos mencionados
${siteKeywords.length > 0 ? `6. Incorpora naturalmente as palavras-chave estratégicas: ${siteKeywords.join(', ')} - estas são CRÍTICAS para passar por sistemas ATS e análise de IA` : ''}
${siteId ? `7. Está otimizado especificamente para o site ${siteInfo.includes('site:') ? siteInfo.split('site:')[1].split('\n')[0].trim() : 'selecionado'}` : ''}
8. É otimizado para passar por sistemas ATS e análise de IA de recrutadores

Retorne APENAS o texto do currículo melhorado, sem explicações adicionais.`;

    console.log('🤖 Gerando currículo melhorado com IA...');

    const completion = await openai.chat.completions.create({
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
      temperature: 0.7,
      max_tokens: 3000
    });

    const improvedResume = completion.choices[0].message.content.trim();
    
    // Remove markdown se existir
    let cleanedResume = improvedResume;
    if (cleanedResume.startsWith('```')) {
      cleanedResume = cleanedResume.replace(/^```[a-z]*\s*/, '').replace(/\s*```$/, '');
    }

    console.log('✅ Currículo melhorado gerado com sucesso');
    return cleanedResume;

  } catch (error) {
    console.error('❌ Erro ao gerar currículo melhorado:', error);
    throw new Error(`Erro ao gerar currículo melhorado: ${error.message}`);
  }
};

/**
 * Converte texto do currículo em PDF
 */
export const generatePDF = (resumeText) => {
  return new Promise((resolve, reject) => {
    try {
      const doc = new PDFDocument({
        size: 'A4',
        margins: { top: 50, bottom: 50, left: 50, right: 50 }
      });

      const chunks = [];
      doc.on('data', chunk => chunks.push(chunk));
      doc.on('end', () => resolve(Buffer.concat(chunks)));
      doc.on('error', reject);

      // Configuração de fonte
      doc.fontSize(20).font('Helvetica-Bold').text('CURRÍCULO', { align: 'center' });
      doc.moveDown(1);

      // Processa o texto do currículo
      const lines = resumeText.split('\n');
      let isHeader = false;

      lines.forEach((line, index) => {
        const trimmedLine = line.trim();
        
        if (!trimmedLine) {
          doc.moveDown(0.5);
          return;
        }

        // Detecta seções principais (títulos em maiúsculas ou com formatação especial)
        if (trimmedLine.length < 50 && (
          trimmedLine === trimmedLine.toUpperCase() ||
          trimmedLine.match(/^[A-ZÁÉÍÓÚÇ][A-ZÁÉÍÓÚÇ\s]+$/) ||
          trimmedLine.includes('---') ||
          trimmedLine.includes('===')
        )) {
          doc.fontSize(14).font('Helvetica-Bold').text(trimmedLine.replace(/[-=]/g, ''), { align: 'left' });
          doc.moveDown(0.3);
          isHeader = true;
        } else {
          // Texto normal
          if (isHeader) {
            doc.fontSize(11).font('Helvetica');
            isHeader = false;
          } else {
            doc.fontSize(11).font('Helvetica');
          }
          
          // Quebra linhas longas
          doc.text(trimmedLine, {
            align: 'left',
            width: 500,
            lineGap: 2
          });
          doc.moveDown(0.2);
        }
      });

      doc.end();
    } catch (error) {
      reject(new Error(`Erro ao gerar PDF: ${error.message}`));
    }
  });
};

