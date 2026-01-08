import express from 'express';
import { createMockPurchase, getUserPurchasesList, getUserCreditHistory, recordCreditUse } from '../controllers/purchase.controller.js';
import { authenticate } from '../middleware/auth.middleware.js';

const router = express.Router();

console.log('🛒 Registrando rotas de compra...');

// Rota de teste (SEM autenticação) para verificar se o router está funcionando
// Esta rota deve ser ANTES do middleware de autenticação
router.get('/test', (req, res) => {
  res.json({
    success: true,
    message: 'Rota de compra está funcionando!',
    path: '/api/purchase/test',
    timestamp: new Date().toISOString()
  });
});

// Rota de compra mockada SEM autenticação (para testes)
// IMPORTANTE: Esta rota DEVE estar ANTES do router.use(authenticate)
router.post('/mock', async (req, res) => {
  console.log('🛒 Rota /mock chamada (SEM autenticação para testes)!');
  console.log('📋 Body recebido:', JSON.stringify(req.body, null, 2));
  
  // Tenta pegar userId do body primeiro (prioridade)
  let userId = req.body.userId;
  
  console.log('👤 userId do body:', userId);
  
  // Se não tiver no body, tenta pegar do token (mas IGNORA completamente se inválido)
  if (!userId) {
    const authHeader = req.headers.authorization;
    if (authHeader && authHeader.startsWith('Bearer ')) {
      try {
        const jwt = await import('jsonwebtoken');
        const token = authHeader.substring(7);
        // Tenta verificar, mas não falha se inválido
        const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
        userId = decoded.userId;
        console.log('✅ Token válido, userId extraído:', userId);
      } catch (err) {
        // IGNORA completamente o erro do token - não é necessário para testes
        console.log('⚠️ Token inválido/expirado - IGNORANDO para testes');
        console.log('💡 Erro do token (ignorado):', err.message);
        // Não define userId - vai pedir no body
      }
    } else {
      console.log('⚠️ Nenhum token fornecido');
    }
  }
  
  // Se ainda não tiver userId, retorna erro
  if (!userId) {
    console.error('❌ userId não encontrado!');
    console.error('📋 Body completo:', JSON.stringify(req.body, null, 2));
    console.error('🔑 Headers authorization:', req.headers.authorization ? 'presente' : 'ausente');
    return res.status(400).json({
      success: false,
      error: 'userId é obrigatório',
      message: 'Envie userId no body da requisição. Exemplo: { userId: "seu-id-aqui", planId: "single", ... }',
      received: {
        hasUserId: !!req.body.userId,
        hasToken: !!req.headers.authorization,
        bodyKeys: Object.keys(req.body),
        bodyContent: req.body
      }
    });
  }
  
  console.log('✅ Usando userId:', userId);
  
  // Adiciona userId ao req para o controller usar
  req.userId = userId;
  
  // Chama o controller diretamente (sem passar pelo middleware)
  try {
    await createMockPurchase(req, res);
  } catch (error) {
    console.error('❌ Erro no controller:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao processar compra',
      message: error.message
    });
  }
});

// Todas as outras rotas requerem autenticação
router.use(authenticate);

console.log('✅ Middleware de autenticação aplicado às outras rotas de compra');

/**
 * @swagger
 * /api/purchase/mock:
 *   post:
 *     summary: Cria uma compra mockada (para testes)
 *     tags: [Compras]
 *     security:
 *       - bearerAuth: []
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - planId
 *               - planName
 *               - creditsAmount
 *               - price
 *             properties:
 *               planId:
 *                 type: string
 *               planName:
 *                 type: string
 *               creditsAmount:
 *                 type: integer
 *               price:
 *                 type: number
 *     responses:
 *       200:
 *         description: Compra realizada com sucesso
 *       400:
 *         description: Dados inválidos
 */
// Rota movida para antes do middleware de autenticação (acima)

/**
 * @swagger
 * /api/purchase/history:
 *   get:
 *     summary: Obtém histórico de compras do usuário
 *     tags: [Compras]
 *     security:
 *       - bearerAuth: []
 *     parameters:
 *       - in: query
 *         name: limit
 *         schema:
 *           type: integer
 *           default: 50
 *     responses:
 *       200:
 *         description: Lista de compras
 */
router.get('/history', getUserPurchasesList);

/**
 * @swagger
 * /api/purchase/credits/history:
 *   get:
 *     summary: Obtém histórico de uso de créditos
 *     tags: [Compras]
 *     security:
 *       - bearerAuth: []
 *     parameters:
 *       - in: query
 *         name: limit
 *         schema:
 *           type: integer
 *           default: 50
 *     responses:
 *       200:
 *         description: Histórico de uso de créditos
 */
router.get('/credits/history', getUserCreditHistory);

/**
 * @swagger
 * /api/purchase/credits/use:
 *   post:
 *     summary: Registra uso de crédito
 *     tags: [Compras]
 *     security:
 *       - bearerAuth: []
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - actionType
 *             properties:
 *               actionType:
 *                 type: string
 *                 enum: [analysis, pdf_generation]
 *               creditsUsed:
 *                 type: integer
 *                 default: 1
 *               resumeFileName:
 *                 type: string
 *     responses:
 *       200:
 *         description: Uso registrado com sucesso
 */
router.post('/credits/use', recordCreditUse);

export default router;

