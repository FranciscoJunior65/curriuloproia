import { PRICING_PLANS, calculateProfitMargin } from '../services/pricing.service.js';
import { getOrCreateUser, saveUser, getUser } from '../models/user.model.js';
import { createCheckoutSession, getCheckoutSession } from '../services/stripe.service.js';
import { sendPurchaseConfirmationEmail } from '../services/email.service.js';
import { createPurchase, getAvailableCredits } from '../services/supabase.service.js';

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

    const couponCode = req.body.couponCode && String(req.body.couponCode).trim() ? String(req.body.couponCode).trim() : null;
    const cpf = req.body.cpf != null ? String(req.body.cpf).trim() : null;
    
    const result = await createCheckoutSession(planId, finalUserId, email || '', frontendUrl, couponCode, cpf);

    // Compra 100% grátis (cupom): concluir sem Stripe
    if (result.freeCheckout) {
      const { createPurchase, registerCouponUse } = await import('../services/supabase.service.js');
      const user = await getOrCreateUser(finalUserId, email || '');
      await createPurchase(
        finalUserId,
        result.planId,
        result.planName,
        result.analyses,
        0,
        'BRL',
        'stripe',
        `free_${Date.now()}`,
        null,
        'analysis_plan',
        result.couponId,
        result.couponName,
        result.discountPercent,
        result.originalPrice
      );
      if (result.couponId && result.cpfNormalized) {
        await registerCouponUse(result.couponId, result.cpfNormalized);
      }
      await user.addCredits(result.analyses);
      const updatedUser = await saveUser({ ...user, plan: result.planId });
      const customerEmail = email || updatedUser.email || '';
      if (customerEmail) {
        try {
          await sendPurchaseConfirmationEmail(customerEmail, {
            planName: result.planName,
            analyses: result.analyses,
            price: 0,
            customerName: updatedUser.name || '',
            extraInfo: 'Compra 100% grátis com cupom.',
            couponName: result.couponName || undefined,
            discountPercent: result.discountPercent ?? undefined,
            originalPrice: result.originalPrice ?? undefined
          });
        } catch (emailErr) {
          console.error('Erro ao enviar confirmação de compra (grátis):', emailErr);
        }
      }
      const baseUrl = (req.headers.origin || req.headers.referer?.split('/').slice(0, 3).join('/') || process.env.FRONTEND_URL || '').replace(/\/$/, '');
      const redirectUrl = `${baseUrl}?free=1&userId=${finalUserId}`;
      return res.json({
        success: true,
        freeCheckout: true,
        redirectUrl,
        user: { id: updatedUser.id, credits: updatedUser.credits, plan: updatedUser.plan }
      });
    }

    res.json({
      success: true,
      sessionId: result.sessionId,
      checkoutUrl: result.url
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
      const userId = session.metadata.userId;
      const planId = session.metadata.planId;
      const analyses = parseInt(session.metadata.analyses);
      const planName = session.metadata.planName || `Plano ${planId}`;
      const price = parseFloat(session.amount_total) / 100; // Stripe retorna em centavos (já com desconto se houve cupom)

      const couponId = session.metadata.couponId || null;
      const couponName = session.metadata.couponName || null;
      const discountPercent = session.metadata.discountPercent != null ? parseFloat(session.metadata.discountPercent) : null;
      const originalPrice = session.metadata.originalPrice != null ? parseFloat(session.metadata.originalPrice) : null;
      const cpfNormalized = session.metadata.cpfNormalized || null;

      const user = await getOrCreateUser(userId, session.customer_email || session.customer_details?.email || '');
      
      const { createPurchase, registerCouponUse } = await import('../services/supabase.service.js');
      await createPurchase(
        userId,
        planId,
        planName,
        analyses,
        price,
        'BRL',
        'stripe',
        session.id,
        null,
        'analysis_plan',
        couponId,
        couponName,
        discountPercent,
        originalPrice
      );

      if (couponId && cpfNormalized) {
        await registerCouponUse(couponId, cpfNormalized);
      }
      
      await user.addCredits(analyses);
      const updatedUser = await saveUser({
        ...user,
        plan: planId
      });

      const customerEmail = session.customer_email || session.customer_details?.email || updatedUser.email || '';
      if (customerEmail) {
        try {
          await sendPurchaseConfirmationEmail(customerEmail, {
            planName,
            analyses,
            price,
            customerName: updatedUser.name || '',
            extraInfo: '',
            couponName: couponName || undefined,
            discountPercent: discountPercent ?? undefined,
            originalPrice: originalPrice ?? undefined
          });
        } catch (emailErr) {
          console.error('Erro ao enviar confirmação de compra:', emailErr);
        }
      }

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
 * Valida um cupom por código e CPF (1 uso por cupom por CPF)
 * GET /api/analyze/coupon/validate?code=XXX&cpf=XXX  ou  POST { code, cpf }
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

/**
 * Admin: adiciona créditos grátis para testes (sem pagamento).
 * POST /api/analyze/payment/admin-free-credits
 * Body: { planId } (single, pack3, pack5; english não adiciona créditos)
 * Requer: authenticate + requireAdmin
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

