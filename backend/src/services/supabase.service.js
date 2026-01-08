import { createClient } from '@supabase/supabase-js';

const supabaseUrl = process.env.SUPABASE_URL;
const supabaseServiceKey = process.env.SUPABASE_SERVICE_ROLE_KEY;

if (!supabaseUrl || !supabaseServiceKey) {
  console.warn('⚠️  Supabase não configurado. Variáveis SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY são necessárias.');
}

export const supabaseAdmin = supabaseUrl && supabaseServiceKey
  ? createClient(supabaseUrl, supabaseServiceKey, {
      auth: {
        autoRefreshToken: false,
        persistSession: false
      }
    })
  : null;

/**
 * Obtém créditos disponíveis do usuário (usado = false)
 */
export const getAvailableCredits = async (userId) => {
  if (!supabaseAdmin || !userId) {
    return 0;
  }

  try {
    const { count, error } = await supabaseAdmin
      .from('creditos')
      .select('*', { count: 'exact', head: true })
      .eq('id_usuario', userId)
      .eq('usado', false);

    if (error) {
      console.error('Erro ao contar créditos disponíveis:', error);
      return 0;
    }

    return count || 0;
  } catch (error) {
    console.error('Erro ao calcular créditos disponíveis:', error);
    return 0;
  }
};

/**
 * Mapeia perfil do português (banco) para inglês (código)
 */
export const mapProfileToEnglish = async (profile) => {
  if (!profile) return null;

  const credits = await getAvailableCredits(profile.id);

  return {
    id: profile.id,
    email: profile.email,
    name: profile.nome || profile.name || '',
    credits: credits,
    plan: profile.plano || profile.plan || null,
    created_at: profile.criado_em || profile.created_at,
    last_analysis: profile.ultima_analise || profile.last_analysis,
    updated_at: profile.atualizado_em || profile.updated_at,
    email_verified: profile.email_verificado || profile.email_verified || false,
    verification_code: profile.codigo_verificacao || profile.verification_code,
    verification_code_expires_at: profile.codigo_verificacao_expira_em || profile.verification_code_expires_at,
    user_type: profile.tipo_usuario || profile.user_type || 'cliente',
    password_hash: profile.hash_senha || profile.password_hash
  };
};

/**
 * Mapeia compra do português (banco) para inglês (código)
 */
export const mapPurchaseToEnglish = (purchase) => {
  if (!purchase) return null;

  return {
    id: purchase.id,
    user_id: purchase.id_usuario || purchase.user_id,
    plan_id: purchase.id_plano || purchase.plan_id,
    plan_name: purchase.nome_plano || purchase.plan_name,
    credits_amount: purchase.quantidade_creditos || purchase.credits_amount || 0,
    price: purchase.preco || purchase.price,
    currency: purchase.moeda || purchase.currency || 'BRL',
    status: purchase.status || 'concluida',
    payment_method: purchase.metodo_pagamento || purchase.payment_method,
    payment_id: purchase.id_pagamento || purchase.payment_id,
    created_at: purchase.criado_em || purchase.created_at,
    updated_at: purchase.atualizado_em || purchase.updated_at,
    parentPurchaseId: purchase.id_compra_pai || purchase.parentPurchaseId || null,
    serviceType: purchase.tipo_servico || purchase.serviceType || 'analysis_plan'
  };
};

/**
 * Mapeia crédito do português (banco) para inglês (código)
 */
export const mapCreditToEnglish = (credit) => {
  if (!credit) return null;

  return {
    id: credit.id,
    purchaseId: credit.id_compra || credit.purchaseId,
    userId: credit.id_usuario || credit.userId,
    used: credit.usado || credit.used || false,
    usedAt: credit.usado_em || credit.usedAt,
    actionType: credit.tipo_acao || credit.actionType,
    resumeFileName: credit.nome_arquivo_curriculo || credit.resumeFileName,
    createdAt: credit.criado_em || credit.created_at
  };
};

/**
 * Obtém ou cria perfil do usuário
 */
export const getOrCreateUserProfile = async (userId, email, name = '', passwordHash = null, emailVerified = false, verificationCode = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    const { data: existing, error: fetchError } = await supabaseAdmin
      .from('perfis_usuarios')
      .select('*')
      .eq('id', userId)
      .single();

    if (existing) {
      return await mapProfileToEnglish(existing);
    }

    const now = new Date().toISOString();
    const insertData = {
      id: userId,
      email: email,
      nome: name,
      email_verificado: emailVerified,
      tipo_usuario: 'cliente',
      criado_em: now,
      atualizado_em: now
    };

    if (passwordHash) {
      insertData.hash_senha = passwordHash;
    }

    if (verificationCode) {
      insertData.codigo_verificacao = verificationCode;
      const expiresAt = new Date();
      expiresAt.setMinutes(expiresAt.getMinutes() + 15);
      insertData.codigo_verificacao_expira_em = expiresAt.toISOString();
    }

    const { data: newProfile, error: createError } = await supabaseAdmin
      .from('perfis_usuarios')
      .insert(insertData)
      .select()
      .single();

    if (createError) {
      throw createError;
    }

    return await mapProfileToEnglish(newProfile);
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
    .from('perfis_usuarios')
    .select('*')
    .eq('id', userId)
    .single();

  if (error && error.code !== 'PGRST116') {
    throw error;
  }

  if (!data) return null;

  return await mapProfileToEnglish(data);
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
    selectFields = 'id, email, nome, plano, criado_em, ultima_analise, atualizado_em, email_verificado, codigo_verificacao, codigo_verificacao_expira_em, tipo_usuario';
  }

  const { data, error } = await supabaseAdmin
    .from('perfis_usuarios')
    .select(selectFields)
    .eq('email', email)
    .single();

  if (error && error.code !== 'PGRST116') {
    throw error;
  }

  if (!data) return null;

  return await mapProfileToEnglish(data);
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

  const updatesPt = {};
  for (const [key, value] of Object.entries(updates)) {
    const keyMap = {
      'name': 'nome',
      'plan': 'plano',
      'last_analysis': 'ultima_analise',
      'updated_at': 'atualizado_em',
      'email_verified': 'email_verificado',
      'verification_code': 'codigo_verificacao',
      'verification_code_expires_at': 'codigo_verificacao_expira_em',
      'user_type': 'tipo_usuario',
      'password_hash': 'hash_senha'
    };
    updatesPt[keyMap[key] || key] = value;
  }

  const { data, error } = await supabaseAdmin
    .from('perfis_usuarios')
    .update({
      ...updatesPt,
      atualizado_em: new Date().toISOString()
    })
    .eq('id', userId)
    .select()
    .single();

  if (error) {
    throw error;
  }

  return await mapProfileToEnglish(data);
};

/**
 * Atualiza código de verificação do usuário
 */
export const updateVerificationCode = async (userId, code, expiresInMinutes = 15) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const expiresAt = new Date();
  expiresAt.setMinutes(expiresAt.getMinutes() + expiresInMinutes);

  const { data, error } = await supabaseAdmin
    .from('perfis_usuarios')
    .update({
      codigo_verificacao: code,
      codigo_verificacao_expira_em: expiresAt.toISOString(),
      atualizado_em: new Date().toISOString()
    })
    .eq('id', userId)
    .select()
    .single();

  if (error) {
    throw error;
  }

  return await mapProfileToEnglish(data);
};

/**
 * Verifica código de verificação e marca email como verificado
 */
export const verifyEmailCode = async (email, code) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data: profile, error: fetchError } = await supabaseAdmin
    .from('perfis_usuarios')
    .select('*')
    .eq('email', email)
    .single();

  if (fetchError || !profile) {
    throw new Error('Usuário não encontrado');
  }

  if (profile.codigo_verificacao !== code) {
    throw new Error('Código de verificação inválido');
  }

  const now = new Date();
  const expiresAt = profile.codigo_verificacao_expira_em ? new Date(profile.codigo_verificacao_expira_em) : null;
  
  if (expiresAt && now > expiresAt) {
    throw new Error('Código de verificação expirado');
  }

  const { data: updated, error: updateError } = await supabaseAdmin
    .from('perfis_usuarios')
    .update({
      email_verificado: true,
      codigo_verificacao: null,
      codigo_verificacao_expira_em: null,
      atualizado_em: new Date().toISOString()
    })
    .eq('id', profile.id)
    .select()
    .single();

  if (updateError) {
    throw updateError;
  }

  return await mapProfileToEnglish(updated);
};

/**
 * Verifica código de login (não marca email como verificado, apenas valida o código)
 */
export const verifyLoginCode = async (email, code) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data: profile, error: fetchError } = await supabaseAdmin
    .from('perfis_usuarios')
    .select('*')
    .eq('email', email)
    .single();

  if (fetchError || !profile) {
    throw new Error('Usuário não encontrado');
  }

  if (profile.codigo_verificacao !== code) {
    throw new Error('Código de login inválido');
  }

  const now = new Date();
  const expiresAt = profile.codigo_verificacao_expira_em ? new Date(profile.codigo_verificacao_expira_em) : null;
  
  if (expiresAt && now > expiresAt) {
    throw new Error('Código de login expirado');
  }

  // Limpa o código após validação (mas não marca email como verificado)
  const { data: updated, error: updateError } = await supabaseAdmin
    .from('perfis_usuarios')
    .update({
      codigo_verificacao: null,
      codigo_verificacao_expira_em: null,
      atualizado_em: new Date().toISOString()
    })
    .eq('id', profile.id)
    .select()
    .single();

  if (updateError) {
    throw updateError;
  }

  return await mapProfileToEnglish(updated);
};

/**
 * Adiciona créditos ao usuário (cria registros na tabela creditos)
 */
export const addCreditsToUser = async (userId, amount) => {
  // Esta função não é mais usada diretamente
  // Os créditos são criados via createPurchase
  console.warn('addCreditsToUser está deprecated. Use createPurchase para adicionar créditos.');
  return { success: true };
};

/**
 * Remove créditos do usuário (marca como usado na tabela creditos)
 */
export const deductCreditsFromUser = async (userId, amount = 1) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  // Busca créditos disponíveis
  const { data: credits, error: fetchError } = await supabaseAdmin
    .from('creditos')
    .select('*')
    .eq('id_usuario', userId)
    .eq('usado', false)
    .limit(amount);

  if (fetchError || !credits || credits.length < amount) {
    throw new Error('Créditos insuficientes');
  }

  // Marca como usado
  const creditIds = credits.map(c => c.id);
  const { error: updateError } = await supabaseAdmin
    .from('creditos')
    .update({ usado: true, usado_em: new Date().toISOString() })
    .in('id', creditIds);

  if (updateError) {
    throw updateError;
  }

  // Atualiza última análise
  await supabaseAdmin
    .from('perfis_usuarios')
    .update({ ultima_analise: new Date().toISOString(), atualizado_em: new Date().toISOString() })
    .eq('id', userId);

  return { success: true };
};

/**
 * Verifica se usuário tem créditos suficientes
 */
export const userHasCredits = async (userId, amount = 1) => {
  const available = await getAvailableCredits(userId);
  return available >= amount;
};

/**
 * Obtém créditos do usuário
 */
export const getUserCredits = async (userId) => {
  return await getAvailableCredits(userId);
};

/**
 * Obtém créditos de uma compra específica
 */
export const getCreditsByPurchase = async (purchaseId) => {
  if (!supabaseAdmin) {
    return [];
  }

  try {
    const { data, error } = await supabaseAdmin
      .from('creditos')
      .select('*')
      .eq('id_compra', purchaseId);

    if (error) {
      console.error('Erro ao buscar créditos da compra:', error);
      return [];
    }

    return (data || []).map(mapCreditToEnglish);
  } catch (error) {
    console.error('Erro ao buscar créditos da compra:', error);
    return [];
  }
};

/**
 * Cria uma compra/transação e os créditos correspondentes
 */
export const createPurchase = async (userId, planId, planName, creditsAmount, price, currency = 'BRL', paymentMethod = 'mock', paymentId = null, parentPurchaseId = null, serviceType = 'analysis_plan') => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { randomUUID } = await import('crypto');
  const purchaseId = randomUUID();
  const now = new Date().toISOString();

  // 1. Cria a compra
  const { data: purchase, error: purchaseError } = await supabaseAdmin
    .from('compras')
    .insert({
      id: purchaseId,
      id_usuario: userId,
      id_plano: planId,
      nome_plano: planName,
      quantidade_creditos: creditsAmount,
      preco: price,
      moeda: currency,
      status: 'concluida',
      metodo_pagamento: paymentMethod,
      id_pagamento: paymentId || `mock_${Date.now()}`,
      criado_em: now,
      atualizado_em: now,
      id_compra_pai: parentPurchaseId,
      tipo_servico: serviceType
    })
    .select()
    .single();

  if (purchaseError) {
    throw purchaseError;
  }

  // 2. Cria N créditos (1 linha por crédito)
  const creditos = [];
  for (let i = 0; i < creditsAmount; i++) {
    creditos.push({
      id_compra: purchaseId,
      id_usuario: userId,
      usado: false,
      criado_em: now
    });
  }

  // Only insert credits if creditsAmount > 0
  if (creditsAmount > 0) {
    const { error: creditsError } = await supabaseAdmin
      .from('creditos')
      .insert(creditos);

    if (creditsError) {
      console.error('Erro ao criar créditos:', creditsError);
      // Tenta remover a compra se falhar
      await supabaseAdmin.from('compras').delete().eq('id', purchaseId);
      throw new Error('Erro ao criar créditos: ' + creditsError.message);
    }
  }

  return mapPurchaseToEnglish(purchase);
};

/**
 * Obtém compras de um usuário
 */
export const getUserPurchases = async (userId, limit = 50) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data: purchases, error } = await supabaseAdmin
    .from('compras')
    .select('*')
    .eq('id_usuario', userId)
    .order('criado_em', { ascending: false })
    .limit(limit);

  if (error) {
    throw error;
  }

  // Para cada compra, busca informações dos créditos
  const purchasesWithCredits = await Promise.all(
    (purchases || []).map(async (purchase) => {
      const credits = await getCreditsByPurchase(purchase.id);
      const total = credits.length;
      const used = credits.filter(c => c.used).length;
      const available = total - used;

      return {
        ...mapPurchaseToEnglish(purchase),
        creditsInfo: {
          total,
          used,
          available,
          credits
        }
      };
    })
  );

  return purchasesWithCredits;
};

/**
 * Registra uso de crédito
 */
export const recordCreditUsage = async (userId, actionType, creditsUsed = 1, resumeFileName = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  // Busca créditos disponíveis
  const { data: credits, error: fetchError } = await supabaseAdmin
    .from('creditos')
    .select('*')
    .eq('id_usuario', userId)
    .eq('usado', false)
    .limit(creditsUsed);

  if (fetchError || !credits || credits.length < creditsUsed) {
    throw new Error('Créditos insuficientes');
  }

  // Marca como usado
  const creditIds = credits.map(c => c.id);
  const now = new Date().toISOString();
  const { error: updateError } = await supabaseAdmin
    .from('creditos')
    .update({
      usado: true,
      usado_em: now,
      tipo_acao: actionType,
      nome_arquivo_curriculo: resumeFileName
    })
    .in('id', creditIds);

  if (updateError) {
    throw updateError;
  }

  // Atualiza última análise
  await supabaseAdmin
    .from('perfis_usuarios')
    .update({ ultima_analise: now, atualizado_em: now })
    .eq('id', userId);

  return { success: true, creditsUsed };
};

/**
 * Obtém histórico de uso de créditos do usuário
 */
export const getUserCreditUsage = async (userId, limit = 50) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('creditos')
    .select('*')
    .eq('id_usuario', userId)
    .eq('usado', true)
    .order('usado_em', { ascending: false })
    .limit(limit);

  if (error) {
    throw error;
  }

  return (data || []).map(mapCreditToEnglish);
};

/**
 * Obtém todas as compras (para admin)
 */
export const getAllPurchases = async (limit = 100, offset = 0) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const { data, error } = await supabaseAdmin
    .from('compras')
    .select('*')
    .order('criado_em', { ascending: false })
    .range(offset, offset + limit - 1);

  if (error) {
    throw error;
  }

  return (data || []).map(mapPurchaseToEnglish);
};

/**
 * Obtém estatísticas de vendas (para admin)
 */
export const getSalesStats = async (startDate = null, endDate = null) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  let query = supabaseAdmin
    .from('compras')
    .select('*');

  if (startDate) {
    query = query.gte('criado_em', startDate);
  }
  if (endDate) {
    query = query.lte('criado_em', endDate);
  }

  const { data, error } = await query;

  if (error) {
    throw error;
  }

  const purchases = (data || []).map(mapPurchaseToEnglish);
  
  const stats = {
    totalPurchases: purchases.length,
    totalRevenue: purchases.reduce((sum, p) => sum + parseFloat(p.price || 0), 0),
    totalCreditsSold: purchases.reduce((sum, p) => sum + (p.credits_amount || 0), 0),
    completedPurchases: purchases.filter(p => p.status === 'concluida' || p.status === 'completed').length,
    pendingPurchases: purchases.filter(p => p.status === 'pendente' || p.status === 'pending').length,
    cancelledPurchases: purchases.filter(p => p.status === 'cancelada' || p.status === 'cancelled').length
  };

  return stats;
};

/**
 * Atualiza token de verificação (para link de verificação ou reset de senha)
 */
export const updateVerificationToken = async (userId, token, expiresInHours = 1) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  const expiresAt = new Date();
  expiresAt.setHours(expiresAt.getHours() + expiresInHours); // Padrão: 1 hora (para reset de senha)

  const { data, error } = await supabaseAdmin
    .from('perfis_usuarios')
    .update({
      codigo_verificacao: token,
      codigo_verificacao_expira_em: expiresAt.toISOString(),
      atualizado_em: new Date().toISOString()
    })
    .eq('id', userId)
    .select()
    .single();

  if (error) {
    throw error;
  }

  return await mapProfileToEnglish(data);
};

/**
 * Verifica token de verificação e marca email como verificado
 */
export const verifyEmailToken = async (email, token) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  // Se email for null, busca apenas por token (para reset de senha)
  let query = supabaseAdmin.from('perfis_usuarios').select('*');
  
  if (email) {
    query = query.eq('email', email);
  }
  
  query = query.eq('codigo_verificacao', token);

  const { data: profiles, error: fetchError } = await query;

  if (fetchError || !profiles || profiles.length === 0) {
    throw new Error('Token inválido');
  }

  const profile = profiles[0];

  // Verifica se o token não expirou
  const now = new Date();
  const expiresAt = profile.codigo_verificacao_expira_em ? new Date(profile.codigo_verificacao_expira_em) : null;
  
  if (expiresAt && now > expiresAt) {
    throw new Error('Token expirado');
  }

  // Se email foi fornecido, marca como verificado
  if (email) {
    const { data: updated, error: updateError } = await supabaseAdmin
      .from('perfis_usuarios')
      .update({
        email_verificado: true,
        codigo_verificacao: null,
        codigo_verificacao_expira_em: null,
        atualizado_em: new Date().toISOString()
      })
      .eq('id', profile.id)
      .select()
      .single();

    if (updateError) {
      throw updateError;
    }

    return await mapProfileToEnglish(updated);
  }

  // Se não tem email, apenas retorna o perfil (para reset de senha)
  return await mapProfileToEnglish(profile);
};

/**
 * Busca usuário por token de reset de senha
 */
export const getUserByResetToken = async (token) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  // Busca usuário pelo token
  const { data: profiles, error } = await supabaseAdmin
    .from('perfis_usuarios')
    .select('*')
    .eq('codigo_verificacao', token);

  if (error || !profiles || profiles.length === 0) {
    throw new Error('Token inválido');
  }

  const profile = profiles[0];

  // Verifica se o token não expirou
  const now = new Date();
  const expiresAt = profile.codigo_verificacao_expira_em ? new Date(profile.codigo_verificacao_expira_em) : null;
  
  if (expiresAt && now > expiresAt) {
    throw new Error('Token expirado');
  }

  return await mapProfileToEnglish(profile);
};
