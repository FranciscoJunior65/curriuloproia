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
    return {
      id: profile.id,
      email: profile.email,
      name: profile.name || '',
      credits: profile.credits || 0,
      plan: profile.plan || null,
      createdAt: profile.created_at ? new Date(profile.created_at) : new Date(),
      lastAnalysis: profile.last_analysis ? new Date(profile.last_analysis) : null,
      hasCredits: (amount = 1) => (profile.credits || 0) >= amount,
      useCredits: async (amount = 1) => {
        if ((profile.credits || 0) < amount) {
          throw new Error('Créditos insuficientes');
        }
        await deductCreditsFromUser(userId, amount);
        return (profile.credits || 0) - amount;
      },
      addCredits: async (amount) => {
        await addCreditsToUser(userId, amount);
        return (profile.credits || 0) + amount;
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
      name: profile.name || '',
      credits: profile.credits || 0,
      plan: profile.plan || null,
      createdAt: profile.created_at ? new Date(profile.created_at) : new Date(),
      lastAnalysis: profile.last_analysis ? new Date(profile.last_analysis) : null,
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
    
    return {
      id: profile.id,
      email: profile.email,
      name: profile.name || '',
      credits: profile.credits || 0,
      plan: profile.plan || null,
      createdAt: profile.created_at ? new Date(profile.created_at) : new Date(),
      lastAnalysis: profile.last_analysis ? new Date(profile.last_analysis) : null,
      hasCredits: (amount = 1) => (profile.credits || 0) >= amount,
      useCredits: async (amount = 1) => {
        if ((profile.credits || 0) < amount) {
          throw new Error('Créditos insuficientes');
        }
        await deductCreditsFromUser(userId, amount);
        return (profile.credits || 0) - amount;
      },
      addCredits: async (amount) => {
        await addCreditsToUser(userId, amount);
        return (profile.credits || 0) + amount;
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
      .from('user_profiles')
      .select('*')
      .order('created_at', { ascending: false });
    
    if (error) {
      console.error('Erro ao listar usuários:', error);
      return [];
    }
    
    return (data || []).map(profile => ({
      id: profile.id,
      email: profile.email,
      name: profile.name || '',
      credits: profile.credits || 0,
      plan: profile.plan || null,
      createdAt: profile.created_at ? new Date(profile.created_at) : new Date(),
      lastAnalysis: profile.last_analysis ? new Date(profile.last_analysis) : null
    }));
  } catch (error) {
    console.error('Erro ao listar usuários:', error);
    return [];
  }
};

