import { supabaseAdmin } from './supabase.service.js';

/**
 * Obtém estatísticas de uso por site de vagas
 */
export const getJobSiteUsageStats = async (startDate = null, endDate = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  let query = supabaseAdmin
    .from('creditos')
    .select(`
      id_site_vagas,
      sites_vagas!inner(nome, ativo),
      usado_em
    `)
    .eq('usado', true)
    .eq('tipo_acao', 'analysis');

  if (startDate) {
    query = query.gte('usado_em', startDate);
  }
  if (endDate) {
    query = query.lte('usado_em', endDate);
  }

  const { data, error } = await query;

  if (error) {
    throw error;
  }

  // Agrupa por site
  const stats = {};
  (data || []).forEach(credit => {
    const siteId = credit.id_site_vagas || 'generic';
    const siteName = credit.sites_vagas?.nome || 'Não especificado';
    
    if (!stats[siteId]) {
      stats[siteId] = {
        siteId: siteId,
        siteName: siteName,
        totalAnalyses: 0,
        dates: []
      };
    }
    
    stats[siteId].totalAnalyses++;
    if (credit.usado_em) {
      stats[siteId].dates.push(credit.usado_em);
    }
  });

  // Converte para array e ordena por total
  const statsArray = Object.values(stats)
    .map(stat => ({
      ...stat,
      // Calcula estatísticas adicionais
      lastUsed: stat.dates.length > 0 ? new Date(Math.max(...stat.dates.map(d => new Date(d)))) : null,
      firstUsed: stat.dates.length > 0 ? new Date(Math.min(...stat.dates.map(d => new Date(d)))) : null
    }))
    .sort((a, b) => b.totalAnalyses - a.totalAnalyses);

  return statsArray;
};

/**
 * Obtém estatísticas detalhadas de uso por site
 */
export const getJobSiteDetailedStats = async (siteId = null, startDate = null, endDate = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  let query = supabaseAdmin
    .from('creditos')
    .select(`
      *,
      sites_vagas(nome, ativo),
      perfis_usuarios(email, nome)
    `)
    .eq('usado', true)
    .eq('tipo_acao', 'analysis');

  if (siteId) {
    query = query.eq('id_site_vagas', siteId);
  }

  if (startDate) {
    query = query.gte('usado_em', startDate);
  }
  if (endDate) {
    query = query.lte('usado_em', endDate);
  }

  const { data, error } = await query.order('usado_em', { ascending: false });

  if (error) {
    throw error;
  }

  return data || [];
};

/**
 * Obtém ranking de sites mais usados
 */
export const getJobSiteRanking = async (limit = 10, startDate = null, endDate = null) => {
  const stats = await getJobSiteUsageStats(startDate, endDate);
  return stats.slice(0, limit);
};
