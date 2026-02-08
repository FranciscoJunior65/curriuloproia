import { supabaseAdmin } from './supabase.service.js';

/**
 * Salva uma análise completa de currículo no banco de dados
 */
export const saveAnalysis = async (resumeId, userId, siteId, analysis) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  if (!resumeId || !userId || !siteId || !analysis) {
    console.warn('⚠️ Dados incompletos para salvar análise');
    return null;
  }

  try {
    const analysisData = {
      id_curriculo: resumeId,
      id_usuario: userId,
      id_site_vagas: siteId,
      score_geral: analysis.score || null,
      pontos_fortes: Array.isArray(analysis.pontosFortes) ? analysis.pontosFortes : [],
      pontos_melhorar: Array.isArray(analysis.pontosMelhorar) ? analysis.pontosMelhorar : [],
      palavras_chave_sugeridas: Array.isArray(analysis.habilidades) ? analysis.habilidades : [],
      recomendacoes: Array.isArray(analysis.recomendacoes) ? analysis.recomendacoes : [],
      resultado_completo: {
        experiencia: analysis.experiencia || '',
        formacao: analysis.formacao || '',
        habilidades: Array.isArray(analysis.habilidades) ? analysis.habilidades : [],
        score: analysis.score || 0,
        pontosFortes: Array.isArray(analysis.pontosFortes) ? analysis.pontosFortes : [],
        pontosMelhorar: Array.isArray(analysis.pontosMelhorar) ? analysis.pontosMelhorar : [],
        recomendacoes: Array.isArray(analysis.recomendacoes) ? analysis.recomendacoes : []
      }
    };

    const { data, error } = await supabaseAdmin
      .from('analises_curriculo')
      .insert(analysisData)
      .select()
      .single();

    if (error) {
      console.error('❌ Erro ao salvar análise:', error);
      return null;
    }

    console.log(`✅ Análise salva no banco: ${data.id}`);
    return data.id;
  } catch (error) {
    console.error('❌ Erro ao salvar análise:', error);
    return null;
  }
};

/**
 * Busca todas as análises de um usuário
 */
export const getUserAnalyses = async (userId, limit = 50, offset = 0) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    const { data, error } = await supabaseAdmin
      .from('analises_curriculo')
      .select(`
        *,
        curriculos_importados (
          id,
          nome_arquivo_original,
          tipo_arquivo,
          criado_em
        ),
        sites_vagas (
          id,
          nome,
          url_base
        )
      `)
      .eq('id_usuario', userId)
      .order('criado_em', { ascending: false })
      .range(offset, offset + limit - 1);

    if (error) {
      throw error;
    }

    return data || [];
  } catch (error) {
    console.error('Erro ao buscar análises:', error);
    throw error;
  }
};

/**
 * Busca uma análise específica por ID
 */
export const getAnalysisById = async (analysisId) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    const { data, error } = await supabaseAdmin
      .from('analises_curriculo')
      .select(`
        *,
        curriculos_importados (
          id,
          nome_arquivo_original,
          tipo_arquivo,
          conteudo_extraido,
          dados_estruturados,
          criado_em
        ),
        sites_vagas (
          id,
          nome,
          url_base
        )
      `)
      .eq('id', analysisId)
      .single();

    if (error) {
      throw error;
    }

    return data;
  } catch (error) {
    console.error('Erro ao buscar análise:', error);
    throw error;
  }
};
