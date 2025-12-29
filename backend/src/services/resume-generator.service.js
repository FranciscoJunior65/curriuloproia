import OpenAI from 'openai';
import dotenv from 'dotenv';
import PDFDocument from 'pdfkit';

dotenv.config();

const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY
});

const DEFAULT_MODEL = process.env.OPENAI_MODEL || 'gpt-4';

/**
 * Gera um currículo melhorado baseado na análise e no currículo original
 */
export const generateImprovedResume = async (originalText, analysis) => {
  try {
    const systemPrompt = `Você é um especialista em redação de currículos profissionais. 
Sua função é reescrever e melhorar currículos aplicando as recomendações fornecidas, mantendo todas as informações verdadeiras e relevantes do currículo original.

IMPORTANTE:
- Mantenha TODAS as informações verdadeiras do currículo original
- Aplique as melhorias sugeridas na análise
- Melhore a formatação e organização
- Use linguagem profissional e clara
- Mantenha a estrutura padrão de currículo (Dados Pessoais, Objetivo, Experiência, Formação, Habilidades)
- Não invente informações que não estavam no original`;

    const userPrompt = `Com base no currículo original e na análise fornecida, gere uma versão melhorada do currículo.

CURRÍCULO ORIGINAL:
${originalText}

ANÁLISE E RECOMENDAÇÕES:
- Pontos Fortes: ${analysis.pontosFortes.join(', ')}
- Pontos a Melhorar: ${analysis.pontosMelhorar.join(', ')}
- Recomendações: ${analysis.recomendacoes.join('; ')}

Gere um currículo melhorado que:
1. Mantém todas as informações verdadeiras do original
2. Aplica as recomendações da análise
3. Melhora a organização e clareza
4. Destaque os pontos fortes identificados
5. Corrige ou melhora os pontos fracos mencionados

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

