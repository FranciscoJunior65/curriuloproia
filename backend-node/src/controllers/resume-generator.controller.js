import {
  generateImprovedResume,
  generatePDF,
  generateEnglishResume,
  buildResumeExcelBuffer
} from '../services/resume-generator.service.js';

export const generateImprovedResumeAndPDF = async (req, res) => {
  const startTime = Date.now();
  
  try {
    const { originalText, analysis, siteId } = req.body;

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
    console.log('📋 Dados recebidos:', {
      hasOriginalText: !!originalText,
      hasAnalysis: !!analysis,
      siteId: siteId || 'NÃO FORNECIDO'
    });
    if (siteId) {
      console.log(`📍 Site de vagas selecionado: ${siteId}`);
    } else {
      console.warn('⚠️ ATENÇÃO: Nenhum site de vagas foi fornecido! O currículo será genérico.');
    }

    // Gera o currículo melhorado (com siteId para personalização)
    const improvedResume = await generateImprovedResume(originalText, analysis, siteId || null);

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

export const generateEnglishExcelResume = async (req, res) => {
  const startTime = Date.now();

  try {
    const { originalText, analysis, siteId } = req.body;

    if (!originalText) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer originalText'
      });
    }

    console.log('🌐 Gerando currículo em inglês para Excel...');
    const englishResume = await generateEnglishResume(originalText, analysis || null, siteId || null);
    const excelBuffer = buildResumeExcelBuffer(englishResume);

    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.log(`✨ Excel em inglês gerado em ${processingTime}s`);

    res.setHeader('Content-Type', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    res.setHeader('Content-Disposition', 'attachment; filename="curriculo-ingles.xlsx"');
    res.setHeader('Content-Length', excelBuffer.length);
    res.send(excelBuffer);
  } catch (error) {
    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.error(`❌ Erro ao gerar Excel em inglês (${processingTime}s):`, error);

    res.status(500).json({
      success: false,
      error: 'Erro ao gerar Excel em inglês',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};

