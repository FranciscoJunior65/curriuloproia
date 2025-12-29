import { createClient } from '@supabase/supabase-js';

const supabaseUrl = process.env.SUPABASE_URL;
const supabaseServiceKey = process.env.SUPABASE_SERVICE_ROLE_KEY; // Usa service_role para operações no backend

if (!supabaseUrl || !supabaseServiceKey) {
  console.warn('⚠️  Supabase não configurado. Variáveis SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY são necessárias.');
}

// Cliente do Supabase para operações no backend (com service_role key)
export const supabaseAdmin = supabaseUrl && supabaseServiceKey
  ? createClient(supabaseUrl, supabaseServiceKey, {
      auth: {
        autoRefreshToken: false,
        persistSession: false
      }
    })
  : null;

/**
 * Obtém ou cria perfil do usuário
 */
export const getOrCreateUserProfile = async (userId, email, name = '', passwordHash = null, emailVerified = false, verificationCode = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    // Tenta buscar perfil existente
    const { data: profile, error: fetchError } = await supabaseAdmin
      .from('user_profiles')
      .select('*')
      .eq('id', userId)
      .single();

    if (profile) {
      // Se tem passwordHash e não está no perfil, atualiza
      if (passwordHash && !profile.password_hash) {
        await supabaseAdmin
          .from('user_profiles')
          .update({ password_hash: passwordHash })
          .eq('id', userId);
      }
      return profile;
    }

    // Se não existe, cria novo perfil
    const insertData = {
      id: userId,
      email,
      name,
      credits: 0,
      email_verified: emailVerified,
      user_type: 'cliente' // Todos os novos usuários são clientes por padrão
    };
    
    if (passwordHash) {
      insertData.password_hash = passwordHash;
    }

    if (verificationCode) {
      insertData.verification_code = verificationCode;
      // Código expira em 15 minutos
      const expiresAt = new Date();
      expiresAt.setMinutes(expiresAt.getMinutes() + 15);
      insertData.verification_code_expires_at = expiresAt.toISOString();
    }

    const { data: newProfile, error: createError } = await supabaseAdmin
      .from('user_profiles')
      .insert(insertData)
      .select()
      .single();

    if (createError) {
      throw createError;
    }

    return newProfile;
  } catch (error) {
    console.error('Erro ao obter/criar perfil:', error);
    throw error;
  }
};

/**
 * Obtém perfil do usuário por ID
 */
export const getUserProfile = async (userId) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('user_profiles')
    .select('*')
    .eq('id', userId)
    .single();

  if (error && error.code !== 'PGRST116') { // PGRST116 = não encontrado
    throw error;
  }

  return data;
};

/**
 * Obtém perfil do usuário por email
 */
export const getUserProfileByEmail = async (email, includePassword = false) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  let selectFields = '*';
  if (!includePassword) {
    // Não retorna password_hash por padrão
    selectFields = 'id, email, name, credits, plan, created_at, last_analysis, updated_at';
  }

  const { data, error } = await supabaseAdmin
    .from('user_profiles')
    .select(selectFields)
    .eq('email', email)
    .single();

  if (error && error.code !== 'PGRST116') {
    throw error;
  }

  return data;
};

/**
 * Verifica senha do usuário
 */
export const verifyUserPassword = async (email, password) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const bcrypt = await import('bcrypt');
  const profile = await getUserProfileByEmail(email, true);
  
  if (!profile || !profile.password_hash) {
    return false;
  }

  return await bcrypt.default.compare(password, profile.password_hash);
};

/**
 * Atualiza perfil do usuário
 */
export const updateUserProfile = async (userId, updates) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('user_profiles')
    .update({
      ...updates,
      updated_at: new Date().toISOString()
    })
    .eq('id', userId)
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
};

/**
 * Atualiza código de verificação do usuário
 */
export const updateVerificationCode = async (userId, code) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const expiresAt = new Date();
  expiresAt.setMinutes(expiresAt.getMinutes() + 15);

  const { data, error } = await supabaseAdmin
    .from('user_profiles')
    .update({
      verification_code: code,
      verification_code_expires_at: expiresAt.toISOString(),
      updated_at: new Date().toISOString()
    })
    .eq('id', userId)
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
};

/**
 * Verifica código de verificação e marca email como verificado
 */
export const verifyEmailCode = async (email, code) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  // Busca usuário por email
  const { data: profile, error: fetchError } = await supabaseAdmin
    .from('user_profiles')
    .select('*')
    .eq('email', email)
    .single();

  if (fetchError || !profile) {
    throw new Error('Usuário não encontrado');
  }

  // Verifica se o código está correto
  if (profile.verification_code !== code) {
    throw new Error('Código de verificação inválido');
  }

  // Verifica se o código não expirou
  const now = new Date();
  const expiresAt = profile.verification_code_expires_at ? new Date(profile.verification_code_expires_at) : null;
  
  if (expiresAt && now > expiresAt) {
    throw new Error('Código de verificação expirado');
  }

  // Marca email como verificado e remove o código
  const { data: updated, error: updateError } = await supabaseAdmin
    .from('user_profiles')
    .update({
      email_verified: true,
      verification_code: null,
      verification_code_expires_at: null,
      updated_at: new Date().toISOString()
    })
    .eq('id', profile.id)
    .select()
    .single();

  if (updateError) {
    throw updateError;
  }

  return updated;
};

/**
 * Adiciona créditos ao usuário
 */
export const addCreditsToUser = async (userId, amount) => {
  const profile = await getUserProfile(userId);
  if (!profile) {
    throw new Error('Usuário não encontrado');
  }

  return await updateUserProfile(userId, {
    credits: (profile.credits || 0) + amount
  });
};

/**
 * Remove créditos do usuário
 */
export const deductCreditsFromUser = async (userId, amount = 1) => {
  const profile = await getUserProfile(userId);
  if (!profile) {
    throw new Error('Usuário não encontrado');
  }

  if ((profile.credits || 0) < amount) {
    throw new Error('Créditos insuficientes');
  }

  return await updateUserProfile(userId, {
    credits: (profile.credits || 0) - amount,
    last_analysis: new Date().toISOString()
  });
};

/**
 * Verifica se usuário tem créditos suficientes
 */
export const userHasCredits = async (userId, amount = 1) => {
  const profile = await getUserProfile(userId);
  return profile && (profile.credits || 0) >= amount;
};

/**
 * Cria uma compra/transação
 */
export const createPurchase = async (userId, planId, planName, creditsAmount, price, currency = 'BRL', paymentMethod = 'mock', paymentId = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { randomUUID } = await import('crypto');
  const purchaseId = randomUUID();

  const { data, error } = await supabaseAdmin
    .from('purchases')
    .insert({
      id: purchaseId,
      user_id: userId,
      plan_id: planId,
      plan_name: planName,
      credits_amount: creditsAmount,
      price: price,
      currency: currency,
      status: 'completed',
      payment_method: paymentMethod,
      payment_id: paymentId || `mock_${Date.now()}`,
      created_at: new Date().toISOString(),
      updated_at: new Date().toISOString()
    })
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
};

/**
 * Obtém compras de um usuário
 */
export const getUserPurchases = async (userId, limit = 50) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('purchases')
    .select('*')
    .eq('user_id', userId)
    .order('created_at', { ascending: false })
    .limit(limit);

  if (error) {
    throw error;
  }

  return data || [];
};

/**
 * Registra uso de crédito
 */
export const recordCreditUsage = async (userId, actionType, creditsUsed = 1, resumeFileName = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { randomUUID } = await import('crypto');
  const usageId = randomUUID();

  const { data, error } = await supabaseAdmin
    .from('credit_usage')
    .insert({
      id: usageId,
      user_id: userId,
      credits_used: creditsUsed,
      action_type: actionType,
      resume_file_name: resumeFileName,
      created_at: new Date().toISOString()
    })
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
};

/**
 * Obtém histórico de uso de créditos do usuário
 */
export const getUserCreditUsage = async (userId, limit = 50) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('credit_usage')
    .select('*')
    .eq('user_id', userId)
    .order('created_at', { ascending: false })
    .limit(limit);

  if (error) {
    throw error;
  }

  return data || [];
};

/**
 * Obtém todas as compras (para admin)
 */
export const getAllPurchases = async (limit = 100, offset = 0) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('purchases')
    .select('*')
    .order('created_at', { ascending: false })
    .range(offset, offset + limit - 1);

  if (error) {
    throw error;
  }

  return data || [];
};

/**
 * Obtém estatísticas de vendas (para admin)
 */
export const getSalesStats = async (startDate = null, endDate = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  let query = supabaseAdmin
    .from('purchases')
    .select('*');

  if (startDate) {
    query = query.gte('created_at', startDate);
  }
  if (endDate) {
    query = query.lte('created_at', endDate);
  }

  const { data, error } = await query;

  if (error) {
    throw error;
  }

  const purchases = data || [];
  
  const stats = {
    totalPurchases: purchases.length,
    totalRevenue: purchases.reduce((sum, p) => sum + parseFloat(p.price || 0), 0),
    totalCreditsSold: purchases.reduce((sum, p) => sum + (p.credits_amount || 0), 0),
    completedPurchases: purchases.filter(p => p.status === 'completed').length,
    pendingPurchases: purchases.filter(p => p.status === 'pending').length,
    cancelledPurchases: purchases.filter(p => p.status === 'cancelled').length
  };

  return stats;
};

/**
 * Atualiza token de verificação (para link de verificação)
 */
export const updateVerificationToken = async (userId, token) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const expiresAt = new Date();
  expiresAt.setHours(expiresAt.getHours() + 24); // Token expira em 24 horas

  const { data, error } = await supabaseAdmin
    .from('user_profiles')
    .update({
      verification_code: token,
      verification_code_expires_at: expiresAt.toISOString(),
      updated_at: new Date().toISOString()
    })
    .eq('id', userId)
    .select()
    .single();

  if (error) {
    throw error;
  }

  return data;
};

/**
 * Verifica token de verificação e marca email como verificado
 */
export const verifyEmailToken = async (email, token) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  // Busca usuário por email
  const { data: profile, error: fetchError } = await supabaseAdmin
    .from('user_profiles')
    .select('*')
    .eq('email', email)
    .single();

  if (fetchError || !profile) {
    throw new Error('Usuário não encontrado');
  }

  // Verifica se o token está correto
  if (profile.verification_code !== token) {
    throw new Error('Token de verificação inválido');
  }

  // Verifica se o token não expirou
  const now = new Date();
  const expiresAt = profile.verification_code_expires_at ? new Date(profile.verification_code_expires_at) : null;
  
  if (expiresAt && now > expiresAt) {
    throw new Error('Token de verificação expirado');
  }

  // Marca email como verificado e remove o token
  const { data: updated, error: updateError } = await supabaseAdmin
    .from('user_profiles')
    .update({
      email_verified: true,
      verification_code: null,
      verification_code_expires_at: null,
      updated_at: new Date().toISOString()
    })
    .eq('id', profile.id)
    .select()
    .single();

  if (updateError) {
    throw updateError;
  }

  return updated;
};

