import { searchJobsBySite } from '../services/job-search.service.js';

export const searchJobs = async (req, res) => {
  const startTime = Date.now();
  
  try {
    const { analysis, siteId, location, resumeText, resumeId } = req.body;

    // Obtém userId do token JWT
    let userId = null;
    const token = req.headers.authorization?.replace('Bearer ', '');
    if (token) {
      try {
        const jwt = await import('jsonwebtoken');
        const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
        userId = decoded.userId;
        console.log(`👤 Usuário autenticado: ${userId}`);
        console.log(`📄 ResumeId fornecido: ${resumeId || 'NÃO FORNECIDO'}`);
        console.log(`📝 ResumeText fornecido: ${resumeText ? 'SIM (' + resumeText.length + ' chars)' : 'NÃO'}`);
        console.log(`🔍 SiteId: ${siteId || 'NÃO FORNECIDO'}`);
        console.log(`📍 Location: ${location || 'Brasil'}`);
      } catch (err) {
        console.warn('⚠️ Token inválido ou não fornecido, continuando sem userId');
      }
    }

    // Validação
    if (!analysis || !siteId) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer analysis e siteId'
      });
    }

    if (!analysis.habilidades && !analysis.experiencia) {
      return res.status(400).json({
        success: false,
        error: 'Análise inválida',
        message: 'A análise deve conter habilidades ou experiencia para buscar vagas'
      });
    }

    console.log('🔍 Iniciando busca avançada de vagas...');
    console.log('📋 Dados recebidos:', {
      hasAnalysis: !!analysis,
      hasResumeText: !!resumeText,
      resumeTextLength: resumeText?.length || 0,
      siteId: siteId || 'NÃO FORNECIDO',
      location: location || 'Brasil',
      userId: userId || 'NÃO AUTENTICADO',
      resumeId: resumeId || 'NÃO FORNECIDO'
    });

    // Busca vagas no site selecionado (usa busca avançada se resumeText foi fornecido)
    const results = await searchJobsBySite(
      siteId, 
      analysis, 
      location || 'Brasil',
      resumeText || null,
      userId || null,
      resumeId || null
    );

    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.log(`✨ Busca de vagas concluída em ${processingTime}s`);
    console.log(`📊 Resultados: ${results.totalFound || results.jobs?.length || 0} vagas encontradas`);

    res.json({
      success: true,
      ...results,
      processingTime: `${processingTime}s`
    });

  } catch (error) {
    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.error(`❌ Erro ao buscar vagas (${processingTime}s):`, error);
    
    res.status(500).json({
      success: false,
      error: 'Erro ao buscar vagas',
      message: error.message || 'Ocorreu um erro inesperado ao buscar vagas'
    });
  }
};
