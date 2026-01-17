import { supabaseAdmin } from './supabase.service.js';

/**
 * Serviço para gerenciar sites de vagas
 */

/**
 * Lista todos os sites de vagas ativos
 */
export const getActiveJobSites = async () => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('sites_vagas')
    .select('*')
    .eq('ativo', true)
    .order('nome', { ascending: true });

  if (error) {
    console.error('Erro ao buscar sites de vagas:', error);
    throw error;
  }

  return data || [];
};

/**
 * Obtém um site de vagas por ID
 */
export const getJobSiteById = async (siteId) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('sites_vagas')
    .select('*')
    .eq('id', siteId)
    .single();

  if (error) {
    if (error.code === 'PGRST116') {
      return null;
    }
    console.error('Erro ao buscar site de vagas:', error);
    throw error;
  }

  return data;
};

/**
 * Obtém um site de vagas por nome
 */
export const getJobSiteByName = async (siteName) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('sites_vagas')
    .select('*')
    .eq('nome', siteName)
    .single();

  if (error) {
    if (error.code === 'PGRST116') {
      return null;
    }
    console.error('Erro ao buscar site de vagas:', error);
    throw error;
  }

  return data;
};

/**
 * Obtém palavras-chave padrão de um site
 */
export const getJobSiteKeywords = async (siteId) => {
  const site = await getJobSiteById(siteId);
  if (!site) {
    return [];
  }

  return site.palavras_chave_padrao || [];
};

/**
 * Obtém características específicas de um site
 */
export const getJobSiteCharacteristics = async (siteId) => {
  const site = await getJobSiteById(siteId);
  if (!site) {
    return {};
  }

  return site.caracteristicas || {};
};

/**
 * Valida se um site de vagas existe e está ativo
 */
export const validateJobSite = async (siteId) => {
  const site = await getJobSiteById(siteId);
  if (!site) {
    throw new Error('Site de vagas não encontrado');
  }
  if (!site.ativo) {
    throw new Error('Site de vagas não está ativo');
  }
  return site;
};
