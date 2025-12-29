import { PRICING_PLANS, calculateProfitMargin } from '../services/pricing.service.js';
import { getOrCreateUser, saveUser, getUser } from '../models/user.model.js';
import { createCheckoutSession, getCheckoutSession } from '../services/stripe.service.js';

/**
 * Lista planos disponíveis
 */
export const getPlans = (req, res) => {
  const plans = Object.values(PRICING_PLANS).map(plan => ({
    ...plan,
    profitMargin: calculateProfitMargin(plan.id)
  }));

  res.json({
    success: true,
    plans
  });
};

/**
 * Cria sessão de checkout do Stripe
 */
export const createPaymentSession = async (req, res) => {
  try {
    const { planId, userId, email } = req.body;

    if (!planId || !PRICING_PLANS[planId]) {
      return res.status(400).json({
        success: false,
        error: 'Plano inválido'
      });
    }

    // Se não tiver userId no body, tenta pegar do token JWT
    let finalUserId = userId;
    if (!finalUserId) {
      const token = req.headers.authorization?.replace('Bearer ', '');
      if (token) {
        try {
          const jwt = await import('jsonwebtoken');
          const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
          finalUserId = decoded.userId;
        } catch (err) {
          // Token inválido, continua sem userId
        }
      }
    }

    if (!finalUserId) {
      return res.status(401).json({
        success: false,
        error: 'É necessário estar autenticado para realizar a compra'
      });
    }

    // Obtém a URL do frontend da requisição ou da variável de ambiente
    const frontendUrl = req.headers.origin || req.headers.referer?.split('/').slice(0, 3).join('/') || process.env.FRONTEND_URL;
    
    // Cria sessão de checkout no Stripe
    const { sessionId, url } = await createCheckoutSession(planId, finalUserId, email || '', frontendUrl);

    res.json({
      success: true,
      sessionId,
      checkoutUrl: url
    });
  } catch (error) {
    console.error('Erro ao criar sessão de pagamento:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao criar sessão de pagamento',
      message: error.message
    });
  }
};

/**
 * Verifica status do pagamento
 */
export const verifyPayment = async (req, res) => {
  try {
    const { sessionId } = req.query;

    if (!sessionId) {
      return res.status(400).json({
        success: false,
        error: 'sessionId é obrigatório'
      });
    }

    const session = await getCheckoutSession(sessionId);

    if (session.payment_status === 'paid') {
      // Busca usuário e atualiza créditos
      const userId = session.metadata.userId;
      const planId = session.metadata.planId;
      const analyses = parseInt(session.metadata.analyses);
      const planName = session.metadata.planName || `Plano ${planId}`;
      const price = parseFloat(session.amount_total) / 100; // Stripe retorna em centavos

      const user = await getOrCreateUser(userId, session.customer_email || session.customer_details?.email || '');
      
      // Cria registro de compra
      const { createPurchase } = await import('../services/supabase.service.js');
      await createPurchase(
        userId,
        planId,
        planName,
        analyses,
        price,
        'BRL',
        'stripe',
        session.id
      );
      
      // Adiciona créditos
      await user.addCredits(analyses);
      const updatedUser = await saveUser({
        ...user,
        plan: planId
      });

      res.json({
        success: true,
        paid: true,
        user: {
          id: updatedUser.id,
          credits: updatedUser.credits,
          plan: updatedUser.plan
        }
      });
    } else {
      res.json({
        success: true,
        paid: false,
        paymentStatus: session.payment_status
      });
    }
  } catch (error) {
    console.error('Erro ao verificar pagamento:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao verificar pagamento',
      message: error.message
    });
  }
};

/**
 * Verifica status de créditos do usuário
 */
export const getCredits = async (req, res) => {
  try {
    // Tenta obter userId do token JWT primeiro
    let userId = req.query.userId;
    
    if (!userId) {
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
    }

    if (!userId) {
      return res.status(401).json({
        success: false,
        error: 'Não autenticado'
      });
    }

    const user = await getUser(userId);

    if (!user) {
      return res.status(404).json({
        success: false,
        error: 'Usuário não encontrado'
      });
    }

    res.json({
      success: true,
      credits: user.credits,
      plan: user.plan,
      lastAnalysis: user.lastAnalysis
    });
  } catch (error) {
    res.status(500).json({
      success: false,
      error: 'Erro ao obter créditos',
      message: error.message
    });
  }
};

