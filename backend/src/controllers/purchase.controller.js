import { createPurchase, getUserPurchases, recordCreditUsage, getUserCreditUsage, addCreditsToUser } from '../services/supabase.service.js';
import { getUserProfile } from '../services/supabase.service.js';

/**
 * Cria uma compra mockada (para testes)
 */
export const createMockPurchase = async (req, res) => {
  try {
    console.log('🛒 Iniciando compra mockada...');
    console.log('📋 Body recebido:', req.body);
    console.log('👤 UserId do token:', req.userId);
    
    const userId = req.userId; // Do middleware de autenticação
    const { planId, planName, creditsAmount, price } = req.body;

    if (!userId) {
      console.error('❌ Usuário não autenticado');
      return res.status(401).json({
        success: false,
        error: 'Usuário não autenticado',
        message: 'É necessário estar logado para realizar compras'
      });
    }

    if (!planId || !planName || creditsAmount === undefined || price === undefined) {
      console.error('❌ Dados incompletos:', { planId, planName, creditsAmount, price });
      return res.status(400).json({
        success: false,
        error: 'Dados do plano são obrigatórios',
        received: { planId, planName, creditsAmount, price }
      });
    }

    console.log('✅ Validações passadas, verificando usuário...');

    // Verifica se o usuário existe
    const user = await getUserProfile(userId);
    if (!user) {
      console.error('❌ Usuário não encontrado:', userId);
      return res.status(404).json({
        success: false,
        error: 'Usuário não encontrado'
      });
    }

    console.log('✅ Usuário encontrado:', user.email);
    console.log('💰 Créditos atuais:', user.credits || 0);
    console.log('📦 Criando compra...');

    // Cria a compra
    let purchase;
    try {
      purchase = await createPurchase(
        userId,
        planId,
        planName,
        parseInt(creditsAmount),
        parseFloat(price),
        'BRL',
        'mock',
        `mock_${Date.now()}_${userId}`
      );
      console.log('✅ Compra criada:', purchase.id);
    } catch (purchaseError) {
      console.error('❌ Erro ao criar compra:', purchaseError);
      // Se o erro for de tabela não encontrada, informa o usuário
      if (purchaseError.code === '42P01' || purchaseError.message?.includes('does not exist')) {
        return res.status(500).json({
          success: false,
          error: 'Tabela de compras não encontrada',
          message: 'Execute o script SQL CREATE_PURCHASES_TABLE.sql no Supabase',
          details: purchaseError.message
        });
      }
      throw purchaseError;
    }

    console.log('💳 Adicionando créditos ao usuário...');
    
    // Adiciona créditos ao usuário
    try {
      await addCreditsToUser(userId, parseInt(creditsAmount));
      console.log('✅ Créditos adicionados:', creditsAmount);
    } catch (creditError) {
      console.error('❌ Erro ao adicionar créditos:', creditError);
      throw creditError;
    }

    // Atualiza o perfil do usuário
    const updatedUser = await getUserProfile(userId);
    console.log('✅ Usuário atualizado. Créditos finais:', updatedUser.credits || 0);

    res.json({
      success: true,
      message: 'Compra realizada com sucesso!',
      purchase: {
        id: purchase.id,
        planName: purchase.plan_name,
        creditsAmount: purchase.credits_amount,
        price: purchase.price,
        status: purchase.status,
        createdAt: purchase.created_at
      },
      user: {
        id: updatedUser.id,
        credits: updatedUser.credits || 0
      }
    });
  } catch (error) {
    console.error('❌ Erro completo ao criar compra mockada:', error);
    console.error('Stack:', error.stack);
    
    // Determina status code apropriado
    let statusCode = 500;
    if (error.message?.includes('não encontrado')) {
      statusCode = 404;
    } else if (error.message?.includes('não autenticado')) {
      statusCode = 401;
    }
    
    res.status(statusCode).json({
      success: false,
      error: 'Erro ao processar compra',
      message: error.message || 'Ocorreu um erro inesperado',
      details: process.env.NODE_ENV === 'development' ? error.stack : undefined
    });
  }
};

/**
 * Obtém compras do usuário
 */
export const getUserPurchasesList = async (req, res) => {
  try {
    const userId = req.userId; // Do middleware de autenticação
    const limit = parseInt(req.query.limit) || 50;

    const purchases = await getUserPurchases(userId, limit);

    res.json({
      success: true,
      purchases: purchases.map(p => ({
        id: p.id,
        planName: p.plan_name,
        creditsAmount: p.credits_amount,
        price: p.price,
        currency: p.currency,
        status: p.status,
        paymentMethod: p.payment_method,
        createdAt: p.created_at
      }))
    });
  } catch (error) {
    console.error('Erro ao obter compras do usuário:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter compras',
      message: error.message
    });
  }
};

/**
 * Obtém histórico de uso de créditos do usuário
 */
export const getUserCreditHistory = async (req, res) => {
  try {
    const userId = req.userId; // Do middleware de autenticação
    const limit = parseInt(req.query.limit) || 50;

    const usage = await getUserCreditUsage(userId, limit);

    res.json({
      success: true,
      usage: usage.map(u => ({
        id: u.id,
        creditsUsed: u.credits_used,
        actionType: u.action_type,
        resumeFileName: u.resume_file_name,
        createdAt: u.created_at
      }))
    });
  } catch (error) {
    console.error('Erro ao obter histórico de créditos:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao obter histórico',
      message: error.message
    });
  }
};

/**
 * Registra uso de crédito (chamado quando usuário faz análise ou gera PDF)
 */
export const recordCreditUse = async (req, res) => {
  try {
    const userId = req.userId; // Do middleware de autenticação
    const { actionType, creditsUsed = 1, resumeFileName } = req.body;

    if (!actionType) {
      return res.status(400).json({
        success: false,
        error: 'Tipo de ação é obrigatório'
      });
    }

    const usage = await recordCreditUsage(userId, actionType, creditsUsed, resumeFileName);

    res.json({
      success: true,
      usage: {
        id: usage.id,
        creditsUsed: usage.credits_used,
        actionType: usage.action_type,
        createdAt: usage.created_at
      }
    });
  } catch (error) {
    console.error('Erro ao registrar uso de crédito:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao registrar uso',
      message: error.message
    });
  }
};

