import { supabaseAdmin } from './supabase.service.js';

/**
 * Salva vagas encontradas no banco de dados
 */
export const saveFoundJobs = async (userId, resumeId, siteId, jobs) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  if (!jobs || jobs.length === 0) {
    return [];
  }

  const jobsToSave = jobs.map(job => ({
    id_curriculo: resumeId,
    id_usuario: userId,
    id_site_vagas: siteId,
    titulo_vaga: job.title || 'Sem título',
    empresa: job.company || 'Não informado',
    localizacao: job.location || 'Não informado',
    url_vaga: job.url || '',
    descricao_vaga: job.description || '',
    requisitos: Array.isArray(job.requirements) ? job.requirements : [],
    score_compatibilidade: job.compatibilityScore || 0,
    palavras_chave_match: Array.isArray(job.matchedKeywords) ? job.matchedKeywords : [],
    dados_completos: {
      salary: job.salary || '',
      contractType: job.contractType || '',
      experienceLevel: job.experienceLevel || '',
      site: job.site || ''
    },
    status: 'ativa'
  }));

  // Insere em lote (limita a 50 por vez para evitar problemas)
  const batchSize = 50;
  const savedJobs = [];

  for (let i = 0; i < jobsToSave.length; i += batchSize) {
    const batch = jobsToSave.slice(i, i + batchSize);
    
    const { data, error } = await supabaseAdmin
      .from('vagas_encontradas')
      .insert(batch)
      .select();

    if (error) {
      console.error(`❌ Erro ao salvar lote ${i / batchSize + 1}:`, error);
      // Continua mesmo se houver erro em um lote
    } else {
      savedJobs.push(...(data || []));
    }
  }

  console.log(`✅ ${savedJobs.length} vagas salvas no banco de dados`);
  return savedJobs;
};

/**
 * Busca vagas salvas de um usuário
 */
export const getSavedJobs = async (userId, siteId = null, limit = 50) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  let query = supabaseAdmin
    .from('vagas_encontradas')
    .select('*')
    .eq('id_usuario', userId)
    .eq('status', 'ativa')
    .order('score_compatibilidade', { ascending: false })
    .order('criado_em', { ascending: false })
    .limit(limit);

  if (siteId) {
    query = query.eq('id_site_vagas', siteId);
  }

  const { data, error } = await query;

  if (error) {
    console.error('Erro ao buscar vagas salvas:', error);
    throw error;
  }

  return data || [];
};
