import { generateImprovedResume, generatePDF } from '../services/resume-generator.service.js';

export const generateImprovedResumeAndPDF = async (req, res) => {
  const startTime = Date.now();
  
  try {
    const { originalText, analysis } = req.body;

    // Validação
    if (!originalText || !analysis) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer originalText e analysis'
      });
    }

    if (!analysis.pontosFortes || !analysis.recomendacoes) {
      return res.status(400).json({
        success: false,
        error: 'Análise inválida',
        message: 'A análise deve conter pontosFortes e recomendacoes'
      });
    }

    console.log('📝 Gerando currículo melhorado...');

    // Gera o currículo melhorado
    const improvedResume = await generateImprovedResume(originalText, analysis);

    console.log('📄 Gerando PDF...');

    // Gera o PDF
    const pdfBuffer = await generatePDF(improvedResume);

    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.log(`✨ Currículo melhorado gerado em ${processingTime}s`);

    // Define headers para download
    res.setHeader('Content-Type', 'application/pdf');
    res.setHeader('Content-Disposition', 'attachment; filename="curriculo-melhorado.pdf"');
    res.setHeader('Content-Length', pdfBuffer.length);

    res.send(pdfBuffer);

  } catch (error) {
    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.error(`❌ Erro ao gerar currículo melhorado (${processingTime}s):`, error);
    
    res.status(500).json({
      success: false,
      error: 'Erro ao gerar currículo melhorado',
      message: error.message || 'Ocorreu um erro inesperado ao gerar o currículo melhorado'
    });
  }
};

