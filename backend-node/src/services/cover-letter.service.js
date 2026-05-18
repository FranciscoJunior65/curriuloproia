import '../load-env.js';
import OpenAI from 'openai';
import PDFDocument from 'pdfkit';
import { getJobSiteById } from './job-sites.service.js';

// OpenAI DESATIVADO temporariamente para não consumir créditos
// const openai = new OpenAI({
//   apiKey: process.env.OPENAI_API_KEY
// });
const openai = null; // Forçado para null para desativar OpenAI

const DEFAULT_MODEL = process.env.OPENAI_MODEL || 'gpt-4';

/**
 * Gera uma carta de apresentação personalizada baseada no currículo e no site de vagas
 */
export const generateCoverLetter = async (resumeText, analysis, siteId = null) => {
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
Esta carta será usada no site: ${site.nome}
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

    const systemPrompt = `Você é um especialista em redação de cartas de apresentação profissionais otimizadas para análise por sistemas ATS (Applicant Tracking Systems) e IAs de validação de currículo.
Sua função é criar cartas de apresentação personalizadas, persuasivas e profissionais que destaquem as qualificações do candidato de forma estratégica e otimizada para passar por sistemas automatizados de triagem.

IMPORTANTE:
- A carta deve ser profissional, concisa e impactante
- Destaque os pontos fortes identificados na análise
- Use linguagem adequada ao contexto do site de vagas (se fornecido)
- A carta deve ter entre 3-4 parágrafos
- Seja específico e evite clichês genéricos
- Mencione conquistas e resultados quando possível
- Adapte o tom e estilo conforme o site de vagas selecionado
- Otimize a carta para passar por sistemas ATS e análise de IA de recrutadores
${siteKeywords.length > 0 ? `- Use naturalmente e estrategicamente as seguintes palavras-chave CRÍTICAS para o site: ${siteKeywords.join(', ')} - estas palavras são essenciais para passar por sistemas ATS` : ''}
${siteId ? `- A carta DEVE ser especificamente adaptada para o site de vagas selecionado` : ''}`;

    const userPrompt = `Com base no currículo e na análise fornecidos, crie uma carta de apresentação profissional e personalizada.

${siteInfo}

CURRÍCULO DO CANDIDATO:
${resumeText}

ANÁLISE DO CURRÍCULO:
- Pontos Fortes: ${Array.isArray(analysis.pontosFortes) ? analysis.pontosFortes.join(', ') : (analysis.pontosFortes || 'Não especificado')}
- Experiência: ${analysis.experiencia || 'Não especificado'}
- Formação: ${analysis.formacao || 'Não especificado'}
- Habilidades: ${Array.isArray(analysis.habilidades) ? analysis.habilidades.join(', ') : (analysis.habilidades || 'Não especificado')}
- Score: ${analysis.score || 'N/A'}/100

${siteId ? `IMPORTANTE: Esta carta será usada no site ${siteInfo.includes('Nome:') ? siteInfo.split('Nome:')[1].split('\n')[0].trim() : 'selecionado'}. Adapte o conteúdo, tom e palavras-chave para este contexto específico.` : ''}

Crie uma carta de apresentação que:
1. Apresenta o candidato de forma profissional
2. Destaca os principais pontos fortes e experiências relevantes
3. Demonstra interesse e adequação para oportunidades
4. Usa linguagem persuasiva mas profissional
5. É específica e evita generalidades
${siteKeywords.length > 0 ? `6. Incorpora NATURALMENTE e ESTRATEGICAMENTE as palavras-chave CRÍTICAS: ${siteKeywords.join(', ')} - estas são ESSENCIAIS para passar por sistemas ATS e análise de IA` : ''}
7. Está otimizada para passar por sistemas ATS e análise de IA de recrutadores
${siteId ? `8. É especificamente adaptada para o site ${siteInfo.includes('Nome:') ? siteInfo.split('Nome:')[1].split('\n')[0].trim() : 'selecionado'} - use o contexto e características deste site` : ''}

Formato da carta:
- Saudação profissional (Ex: "Prezados Senhores," ou "Caro(a) Recrutador(a),")
- Parágrafo introdutório: Apresentação e objetivo
- Parágrafo(s) do meio: Destaque de qualificações e experiências relevantes
- Parágrafo final: Encerramento profissional e disponibilidade para contato

Retorne APENAS o texto da carta de apresentação, sem explicações adicionais.`;

    console.log('🤖 Gerando carta de apresentação com IA...');

    if (!openai) {
      throw new Error('OpenAI está desativado temporariamente. Use Gemini ou ative o modo mock.');
    }

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
      temperature: 0.8,
      max_tokens: 1500
    });

    const coverLetter = completion.choices[0].message.content.trim();
    
    // Remove markdown se existir
    let cleanedLetter = coverLetter;
    if (cleanedLetter.startsWith('```')) {
      cleanedLetter = cleanedLetter.replace(/^```[a-z]*\s*/, '').replace(/\s*```$/, '');
    }

    console.log('✅ Carta de apresentação gerada com sucesso');
    return cleanedLetter;

  } catch (error) {
    console.error('❌ Erro ao gerar carta de apresentação:', error);
    throw new Error(`Erro ao gerar carta de apresentação: ${error.message}`);
  }
};

/**
 * Converte texto da carta de apresentação em PDF
 */
export const generateCoverLetterPDF = (coverLetterText) => {
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

      // Título
      doc.fontSize(18).font('Helvetica-Bold').text('CARTA DE APRESENTAÇÃO', { align: 'center' });
      doc.moveDown(2);

      // Data (opcional, pode ser adicionada dinamicamente)
      const today = new Date();
      const dateStr = today.toLocaleDateString('pt-BR', { 
        day: '2-digit', 
        month: 'long', 
        year: 'numeric' 
      });
      doc.fontSize(10).font('Helvetica').text(dateStr, { align: 'right' });
      doc.moveDown(1.5);

      // Processa o texto da carta
      const paragraphs = coverLetterText.split(/\n\s*\n/).filter(p => p.trim());
      
      paragraphs.forEach((paragraph, index) => {
        const trimmedParagraph = paragraph.trim();
        
        if (!trimmedParagraph) {
          doc.moveDown(0.5);
          return;
        }

        // Primeiro parágrafo pode ter saudação
        if (index === 0 && trimmedParagraph.length < 100) {
          doc.fontSize(11).font('Helvetica').text(trimmedParagraph);
          doc.moveDown(1);
        } else {
          // Parágrafos normais
          doc.fontSize(11).font('Helvetica').text(trimmedParagraph, {
            align: 'justify',
            width: 500,
            lineGap: 3
          });
          doc.moveDown(1);
        }
      });

      // Espaço para assinatura
      doc.moveDown(2);
      doc.fontSize(11).font('Helvetica').text('Atenciosamente,', { align: 'left' });
      doc.moveDown(1.5);
      doc.fontSize(11).font('Helvetica').text('___________________________', { align: 'left' });

      doc.end();
    } catch (error) {
      reject(new Error(`Erro ao gerar PDF da carta: ${error.message}`));
    }
  });
};
