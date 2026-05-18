import { PRICING_PLANS, calculateProfitMargin } from '../services/pricing.service.js';
import { getUser } from '../models/user.model.js';
import { createProviderCheckout, verifyProviderPayment } from '../services/payment-provider.service.js';
import { fulfillFreeCheckout } from '../services/payment-fulfillment.service.js';
import { getPaymentProvider, getValidPaymentProviders } from '../services/settings.service.js';
import { createPurchase, getAvailableCredits } from '../services/supabase.service.js';
import { sendPurchaseConfirmationEmail } from '../services/email.service.js';

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
 * Retorna provedor de pagamento ativo (público)
 */
export const getActivePaymentProvider = async (req, res) => {
  try {
    const provider = await getPaymentProvider();
    res.json({
      success: true,
      provider,
      providers: getValidPaymentProviders()
    });
  } catch (error) {
    res.status(500).json({
      success: false,
      error: 'Erro ao obter provedor de pagamento',
      message: error.message
    });
  }
};

/**
 * Cria sessão de checkout (Stripe ou Mercado Pago conforme configuração)
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

    let finalUserId = userId;
    if (!finalUserId) {
      const token = req.headers.authorization?.replace('Bearer ', '');
      if (token) {
        try {
          const jwt = await import('jsonwebtoken');
          const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
          finalUserId = decoded.userId;
        } catch {
          // Token inválido
        }
      }
    }

    if (!finalUserId) {
      return res.status(401).json({
        success: false,
        error: 'É necessário estar autenticado para realizar a compra'
      });
    }

    const frontendUrl = req.headers.origin || req.headers.referer?.split('/').slice(0, 3).join('/') || process.env.FRONTEND_URL;
    const couponCode = req.body.couponCode && String(req.body.couponCode).trim() ? String(req.body.couponCode).trim() : null;
    const cpf = req.body.cpf != null ? String(req.body.cpf).trim() : null;

    const result = await createProviderCheckout(planId, finalUserId, email || '', frontendUrl, couponCode, cpf);

    if (result.freeCheckout) {
      const fulfillment = await fulfillFreeCheckout(
        {
          userId: finalUserId,
          planId: result.planId,
          planName: result.planName,
          analyses: result.analyses,
          couponId: result.couponId,
          couponName: result.couponName,
          discountPercent: result.discountPercent,
          originalPrice: result.originalPrice,
          cpfNormalized: result.cpfNormalized
        },
        email || ''
      );

      const baseUrl = (frontendUrl || process.env.FRONTEND_URL || '').replace(/\/$/, '');
      const redirectUrl = `${baseUrl}?free=1&userId=${finalUserId}`;

      return res.json({
        success: true,
        freeCheckout: true,
        provider: result.provider,
        redirectUrl,
        user: fulfillment.user
      });
    }

    res.json({
      success: true,
      provider: result.provider,
      sessionId: result.sessionId,
      checkoutUrl: result.url,
      preferenceId: result.preferenceId
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
 * Verifica status do pagamento (Stripe session_id ou Mercado Pago payment_id)
 */
export const verifyPayment = async (req, res) => {
  try {
    const sessionId = req.query.sessionId || req.query.payment_id || req.query.paymentId;
    const providerHint = req.query.provider || null;

    if (!sessionId) {
      return res.status(400).json({
        success: false,
        error: 'sessionId ou payment_id é obrigatório'
      });
    }

    const result = await verifyProviderPayment(sessionId, providerHint);

    if (result.paid) {
      return res.json({
        success: true,
        paid: true,
        provider: providerHint || await getPaymentProvider(),
        user: result.user,
        alreadyFulfilled: result.alreadyFulfilled || false
      });
    }

    res.json({
      success: true,
      paid: false,
      paymentStatus: result.paymentStatus,
      statusDetail: result.statusDetail
    });
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
 * Valida cupom
 */
export const validateCoupon = async (req, res) => {
  try {
    const code = req.query.code || (req.body && req.body.code);
    const cpf = req.query.cpf || (req.body && req.body.cpf);
    if (!code || !String(code).trim()) {
      return res.status(400).json({
        success: false,
        valid: false,
        error: 'Código do cupom é obrigatório'
      });
    }
    const { validateCoupon: validate } = await import('../services/supabase.service.js');
    const result = await validate(String(code).trim(), cpf != null ? String(cpf).trim() : null);
    if (!result.valid) {
      return res.json({ success: true, valid: false, message: result.message || 'Cupom inválido ou inativo.' });
    }
    if (cpf == null || !String(cpf).trim()) {
      return res.json({
        success: true,
        valid: true,
        coupon: { nome: result.coupon.nome, porcentagem_desconto: result.coupon.porcentagem_desconto },
        message: 'Informe seu CPF antes de finalizar a compra para usar este cupom.'
      });
    }
    return res.json({
      success: true,
      valid: true,
      coupon: {
        nome: result.coupon.nome,
        porcentagem_desconto: result.coupon.porcentagem_desconto
      }
    });
  } catch (error) {
    console.error('Erro ao validar cupom:', error);
    return res.status(500).json({
      success: false,
      valid: false,
      error: 'Erro ao validar cupom',
      message: error.message
    });
  }
};

/**
 * Créditos do usuário
 */
export const getCredits = async (req, res) => {
  try {
    let userId = req.query.userId;

    if (!userId) {
      const token = req.headers.authorization?.replace('Bearer ', '');
      if (token) {
        try {
          const jwt = await import('jsonwebtoken');
          const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
          userId = decoded.userId;
        } catch {
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

/**
 * Admin: créditos grátis para testes
 */
export const adminFreeCredits = async (req, res) => {
  try {
    const userId = req.userId;
    const { planId } = req.body;

    if (!planId || !PRICING_PLANS[planId]) {
      return res.status(400).json({
        success: false,
        error: 'planId inválido. Use: single, pack3 ou pack5.'
      });
    }

    const plan = PRICING_PLANS[planId];
    const creditsAmount = plan.analyses ?? 0;

    await createPurchase(
      userId,
      planId,
      plan.name,
      creditsAmount,
      0,
      'BRL',
      'admin_test',
      `admin_free_${Date.now()}_${userId}`,
      null,
      'analysis_plan',
      null,
      null,
      null,
      null
    );

    const customerEmail = req.user?.email || '';
    if (customerEmail) {
      try {
        await sendPurchaseConfirmationEmail(customerEmail, {
          planName: plan.name,
          creditsAmount,
          price: 0,
          customerName: req.user?.name || '',
          extraInfo: 'Créditos de teste (admin).'
        });
      } catch (emailErr) {
        console.error('Erro ao enviar confirmação de compra (admin free):', emailErr);
      }
    }

    const creditsAvailable = await getAvailableCredits(userId);

    res.json({
      success: true,
      message: `${creditsAmount} crédito(s) adicionado(s) para testes.`,
      credits: creditsAvailable
    });
  } catch (error) {
    console.error('Erro ao adicionar créditos grátis (admin):', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao adicionar créditos',
      message: error.message
    });
  }
};
