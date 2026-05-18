import { createPurchase, getUserPurchases, recordCreditUsage, getUserCreditUsage, validateCoupon, registerCouponUse, normalizeCpf } from '../services/supabase.service.js';
import { getUserProfile } from '../services/supabase.service.js';
import { sendPurchaseConfirmationEmail } from '../services/email.service.js';

/**
 * Cria uma compra mockada (para testes)
 */
export const createMockPurchase = async (req, res) => {
  try {
    console.log('🛒 Iniciando compra mockada...');
    console.log('📋 Body recebido:', req.body);
    console.log('👤 UserId do token:', req.userId);
    
    const userId = req.userId || req.body.userId; // Do middleware ou do body (para testes)
    const { planId, planName, creditsAmount, price, includeEnglish, englishPrice, couponCode, cpf } = req.body;

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

    let finalPrice = parseFloat(price);
    let couponId = null;
    let couponName = null;
    let discountPercent = null;
    let originalPrice = null;

    if (couponCode && String(couponCode).trim()) {
      const cpfTrim = cpf != null ? String(cpf).trim() : '';
      if (normalizeCpf(cpfTrim).length !== 11) {
        return res.status(400).json({
          success: false,
          error: 'Para usar cupom, informe o CPF (11 dígitos).',
          code: 'CPF_REQUIRED'
        });
      }
      const validation = await validateCoupon(String(couponCode).trim(), cpfTrim);
      if (!validation.valid) {
        return res.status(400).json({
          success: false,
          error: validation.message || 'Cupom inválido ou já utilizado por este CPF.',
          code: 'COUPON_INVALID'
        });
      }
      if (validation.coupon) {
        const pct = validation.coupon.porcentagem_desconto;
        originalPrice = finalPrice;
        finalPrice = Math.max(0, finalPrice * (1 - pct / 100));
        couponId = validation.coupon.id;
        couponName = validation.coupon.nome;
        discountPercent = pct;
        console.log('🎟️ Cupom aplicado:', couponName, pct + '%', '- Preço final:', finalPrice);
      }
    }

    console.log('📦 Criando compra...');

    // Cria a compra principal
    let purchase;
    try {
      purchase = await createPurchase(
        userId,
        planId,
        planName,
        parseInt(creditsAmount),
        finalPrice,
        'BRL',
        'mock',
        `mock_${Date.now()}_${userId}`,
        null,
        'analysis_plan',
        couponId,
        couponName,
        discountPercent,
        originalPrice
      );
      console.log('✅ Compra criada:', purchase.id);
      
      // Se incluir currículo em inglês (venda casada)
      if (includeEnglish && planId !== 'english') {
        const englishPriceFinal = englishPrice || 5.90;
        console.log('📄 Adicionando currículo em inglês (venda casada) por R$', englishPriceFinal);
        
        const englishPurchase = await createPurchase(
          userId,
          'english',
          'Currículo em Inglês (Venda Casada)',
          0, // Não adiciona créditos, é apenas serviço
          parseFloat(englishPriceFinal),
          'BRL',
          'mock',
          `mock_english_${Date.now()}_${userId}`
        );
        console.log('✅ Compra de currículo em inglês criada:', englishPurchase.id);
      }
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

    if (couponId && cpf != null && String(cpf).trim()) {
      await registerCouponUse(couponId, cpf);
    }

    // Os créditos já foram criados pelo createPurchase na tabela creditos
    // Não precisa mais chamar addCreditsToUser

    // Envia confirmação ao cliente (BCC para juniorbx@gmail.com / EMAIL_COPY_TO)
    if (user.email) {
      try {
        await sendPurchaseConfirmationEmail(user.email, {
          planName,
          creditsAmount: parseInt(creditsAmount),
          price: finalPrice,
          customerName: user.name || '',
          extraInfo: includeEnglish ? 'Incluiu currículo em inglês (venda casada).' : '',
          couponName: couponName || undefined,
          discountPercent: discountPercent ?? undefined,
          originalPrice: originalPrice ?? undefined
        });
      } catch (emailErr) {
        console.error('Erro ao enviar confirmação de compra:', emailErr);
      }
    }
    
    // Verifica créditos disponíveis após a compra
    const { getAvailableCredits } = await import('../services/supabase.service.js');
    const creditsAvailable = await getAvailableCredits(userId);
    console.log('✅ Créditos disponíveis após compra:', creditsAvailable);

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
        id: userId,
        credits: creditsAvailable
      },
      creditsAvailable: creditsAvailable
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
      purchases: purchases.map(p => {
        const price = parseFloat(p.price);
        const creditsAmount = parseInt(p.credits_amount, 10);
        const creditsInfo = p.creditsInfo || {
          total: Number.isFinite(creditsAmount) ? creditsAmount : 0,
          used: 0,
          available: Number.isFinite(creditsAmount) ? creditsAmount : 0,
          credits: []
        };
        return {
          id: p.id,
          planId: p.plan_id,
          planName: p.plan_name,
          creditsAmount: Number.isFinite(creditsAmount) ? creditsAmount : 0,
          price: Number.isFinite(price) ? price : 0,
          currency: p.currency || 'BRL',
          status: p.status,
          paymentMethod: p.payment_method,
          createdAt: p.created_at,
          serviceType: p.serviceType || 'analise',
          parentPurchaseId: p.parentPurchaseId || null,
          creditsInfo: {
            total: creditsInfo.total ?? 0,
            used: creditsInfo.used ?? 0,
            available: creditsInfo.available ?? 0,
            credits: creditsInfo.credits ?? []
          }
        };
      })
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
        purchaseId: u.purchase_id,
        used: u.used,
        usedAt: u.used_at,
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
        purchaseId: usage.purchase_id,
        used: usage.used,
        usedAt: usage.used_at,
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

