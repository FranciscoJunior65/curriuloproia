/**
 * Modelo de usuário usando Supabase
 */

import {
  getOrCreateUserProfile,
  getUserProfile,
  getUserProfileByEmail,
  updateUserProfile,
  addCreditsToUser,
  deductCreditsFromUser,
  userHasCredits
} from '../services/supabase.service.js';

/**
 * Cria ou obtém usuário (compatibilidade com código existente)
 */
export const getOrCreateUser = async (userId, email, name = '', passwordHash = null) => {
  try {
    const profile = await getOrCreateUserProfile(userId, email, name, passwordHash);
    
    // Obtém créditos disponíveis dinamicamente
    const { getAvailableCredits } = await import('../services/supabase.service.js');
    const availableCredits = await getAvailableCredits(userId);
    
    return {
      id: profile.id,
      email: profile.email,
      name: profile.name || profile.nome || '',
      credits: availableCredits, // Créditos calculados dinamicamente da tabela creditos
      plan: profile.plan || profile.plano || null,
      createdAt: profile.created_at ? new Date(profile.created_at) : (profile.criado_em ? new Date(profile.criado_em) : new Date()),
      lastAnalysis: profile.last_analysis ? new Date(profile.last_analysis) : (profile.ultima_analise ? new Date(profile.ultima_analise) : null),
      hasCredits: async (amount = 1) => {
        // Usa a função userHasCredits que verifica na tabela creditos
        return await userHasCredits(userId, amount);
      },
      useCredits: async (amount = 1) => {
        // Verifica créditos antes de usar
        const hasCredits = await userHasCredits(userId, amount);
        if (!hasCredits) {
          throw new Error('Créditos insuficientes');
        }
        await deductCreditsFromUser(userId, amount);
        // Retorna créditos restantes
        return await getAvailableCredits(userId);
      },
      addCredits: async (amount) => {
        await addCreditsToUser(userId, amount);
        // Retorna créditos atualizados
        return await getAvailableCredits(userId);
      }
    };
  } catch (error) {
    console.error('Erro ao obter/criar usuário:', error);
    throw error;
  }
};

/**
 * Busca usuário por email
 */
export const getUserByEmail = async (email) => {
  try {
    const profile = await getUserProfileByEmail(email);
    if (!profile) return null;
    
    return {
      id: profile.id,
      email: profile.email,
      name: profile.name || profile.nome || '',
      credits: profile.credits || profile.creditos || 0,
      plan: profile.plan || profile.plano || null,
      createdAt: profile.created_at ? new Date(profile.created_at) : (profile.criado_em ? new Date(profile.criado_em) : new Date()),
      lastAnalysis: profile.last_analysis ? new Date(profile.last_analysis) : (profile.ultima_analise ? new Date(profile.ultima_analise) : null),
      password: '' // Não retornamos senha do Supabase
    };
  } catch (error) {
    console.error('Erro ao buscar usuário por email:', error);
    return null;
  }
};

/**
 * Obtém usuário por ID
 */
export const getUser = async (userId) => {
  try {
    const profile = await getUserProfile(userId);
    if (!profile) return null;
    
    // Obtém créditos disponíveis dinamicamente
    const { getAvailableCredits } = await import('../services/supabase.service.js');
    const availableCredits = await getAvailableCredits(userId);
    
    return {
      id: profile.id,
      email: profile.email,
      name: profile.name || profile.nome || '',
      credits: availableCredits, // Créditos calculados dinamicamente da tabela creditos
      plan: profile.plan || profile.plano || null,
      createdAt: profile.created_at ? new Date(profile.created_at) : (profile.criado_em ? new Date(profile.criado_em) : new Date()),
      lastAnalysis: profile.last_analysis ? new Date(profile.last_analysis) : (profile.ultima_analise ? new Date(profile.ultima_analise) : null),
      hasCredits: async (amount = 1) => {
        // Usa a função userHasCredits que verifica na tabela creditos
        return await userHasCredits(userId, amount);
      },
      useCredits: async (amount = 1) => {
        // Verifica créditos antes de usar
        const hasCredits = await userHasCredits(userId, amount);
        if (!hasCredits) {
          throw new Error('Créditos insuficientes');
        }
        await deductCreditsFromUser(userId, amount);
        // Retorna créditos restantes
        return await getAvailableCredits(userId);
      },
      addCredits: async (amount) => {
        await addCreditsToUser(userId, amount);
        // Retorna créditos atualizados
        return await getAvailableCredits(userId);
      }
    };
  } catch (error) {
    console.error('Erro ao obter usuário:', error);
    return null;
  }
};

/**
 * Salva usuário (atualiza no Supabase)
 */
export const saveUser = async (user) => {
  try {
    const updates = {
      email: user.email,
      name: user.name || '',
      credits: user.credits || 0,
      plan: user.plan || null
    };
    
    if (user.lastAnalysis) {
      updates.last_analysis = user.lastAnalysis instanceof Date 
        ? user.lastAnalysis.toISOString() 
        : user.lastAnalysis;
    }
    
    const updated = await updateUserProfile(user.id, updates);
    return {
      id: updated.id,
      email: updated.email,
      name: updated.name || '',
      credits: updated.credits || 0,
      plan: updated.plan || null,
      createdAt: updated.created_at ? new Date(updated.created_at) : new Date(),
      lastAnalysis: updated.last_analysis ? new Date(updated.last_analysis) : null
    };
  } catch (error) {
    console.error('Erro ao salvar usuário:', error);
    throw error;
  }
};

/**
 * Lista todos os usuários (para debug/admin)
 */
export const getAllUsers = async () => {
  try {
    const { supabaseAdmin } = await import('../services/supabase.service.js');
    if (!supabaseAdmin) {
      return [];
    }
    
    const { data, error } = await supabaseAdmin
      .from('perfis_usuarios')
      .select('*')
      .order('criado_em', { ascending: false });
    
    if (error) {
      console.error('Erro ao listar usuários:', error);
      return [];
    }
    
    return (data || []).map(profile => ({
      id: profile.id,
      email: profile.email,
      name: profile.nome || '',
      credits: profile.creditos || 0,
      plan: profile.plano || null,
      createdAt: profile.criado_em ? new Date(profile.criado_em) : new Date(),
      lastAnalysis: profile.ultima_analise ? new Date(profile.ultima_analise) : null
    }));
  } catch (error) {
    console.error('Erro ao listar usuários:', error);
    return [];
  }
};

