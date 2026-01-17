import { searchJobsBySite } from '../services/job-search.service.js';

export const searchJobs = async (req, res) => {
  const startTime = Date.now();
  
  try {
    const { analysis, siteId, location } = req.body;

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

    console.log('🔍 Buscando vagas...');
    console.log('📋 Dados recebidos:', {
      hasAnalysis: !!analysis,
      siteId: siteId || 'NÃO FORNECIDO',
      location: location || 'Brasil'
    });

    // Busca vagas no site selecionado
    const results = await searchJobsBySite(siteId, analysis, location || 'Brasil');

    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.log(`✨ Busca de vagas concluída em ${processingTime}s`);

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
