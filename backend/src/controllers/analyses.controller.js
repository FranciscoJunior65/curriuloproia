import { getUserAnalyses, getAnalysisById } from '../services/analise-db.service.js';

/**
 * Lista todas as análises do usuário autenticado
 */
export const listUserAnalyses = async (req, res) => {
  try {
    // Obtém userId do token
    let userId = null;
    const token = req.headers.authorization?.replace('Bearer ', '');
    if (token) {
      try {
        const jwt = await import('jsonwebtoken');
        const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
        userId = decoded.userId;
      } catch (err) {
        // Token inválido
      }
    }

    if (!userId) {
      return res.status(401).json({
        success: false,
        error: 'Não autenticado',
        message: 'É necessário estar autenticado para listar análises'
      });
    }

    const limit = parseInt(req.query.limit) || 50;
    const offset = parseInt(req.query.offset) || 0;

    console.log(`📋 Listando análises do usuário ${userId}...`);

    const analyses = await getUserAnalyses(userId, limit, offset);

    res.json({
      success: true,
      analyses,
      total: analyses.length,
      limit,
      offset
    });

  } catch (error) {
    console.error('❌ Erro ao listar análises:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao listar análises',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};

/**
 * Busca uma análise específica por ID
 */
export const getAnalysis = async (req, res) => {
  try {
    // Obtém userId do token
    let userId = null;
    const token = req.headers.authorization?.replace('Bearer ', '');
    if (token) {
      try {
        const jwt = await import('jsonwebtoken');
        const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
        userId = decoded.userId;
      } catch (err) {
        // Token inválido
      }
    }

    if (!userId) {
      return res.status(401).json({
        success: false,
        error: 'Não autenticado',
        message: 'É necessário estar autenticado para buscar análise'
      });
    }

    const { analysisId } = req.params;

    if (!analysisId) {
      return res.status(400).json({
        success: false,
        error: 'ID não fornecido',
        message: 'É necessário fornecer o ID da análise'
      });
    }

    console.log(`🔍 Buscando análise ${analysisId}...`);

    const analysis = await getAnalysisById(analysisId);

    // Verifica se a análise pertence ao usuário
    if (analysis && analysis.id_usuario !== userId) {
      return res.status(403).json({
        success: false,
        error: 'Acesso negado',
        message: 'Você não tem permissão para acessar esta análise'
      });
    }

    res.json({
      success: true,
      analysis
    });

  } catch (error) {
    console.error('❌ Erro ao buscar análise:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao buscar análise',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};
