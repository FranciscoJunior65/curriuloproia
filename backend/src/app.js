import express from 'express';
import cors from 'cors';
import dotenv from 'dotenv';
import analyzeRoutes from './routes/analyze.routes.js';
import authRoutes from './routes/auth.routes.js';
import testRoutes from './routes/test.routes.js';
import adminRoutes from './routes/admin.routes.js';
import purchaseRoutes from './routes/purchase.routes.js';
import { setupSwagger } from './config/swagger.js';

dotenv.config();

const app = express();
const PORT = process.env.PORT || 3000;

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// Swagger Documentation
setupSwagger(app);

// Stripe webhook (precisa do body raw, antes das outras rotas)
app.post('/api/analyze/payment/webhook', express.raw({ type: 'application/json' }), async (req, res) => {
  const { handleWebhook } = await import('./services/stripe.service.js');
  return handleWebhook(req, res);
});

// Routes
app.use('/api/auth', authRoutes);
app.use('/api/analyze', analyzeRoutes);
app.use('/api/test', testRoutes);
app.use('/api/admin', adminRoutes);
app.use('/api/purchase', purchaseRoutes);

// Log para debug - verificar se rotas foram registradas
console.log('📋 Rotas registradas:');
console.log('  - /api/auth');
console.log('  - /api/analyze');
console.log('  - /api/test');
console.log('  - /api/admin');
console.log('  - /api/purchase');

/**
 * @swagger
 * /api/health:
 *   get:
 *     summary: Verifica o status da API
 *     tags: [Health]
 *     responses:
 *       200:
 *         description: API está funcionando
 *         content:
 *           application/json:
 *             schema:
 *               type: object
 *               properties:
 *                 status:
 *                   type: string
 *                   example: ok
 *                 message:
 *                   type: string
 *                   example: API funcionando
 *                 openaiConfigured:
 *                   type: boolean
 *                   example: true
 *                 model:
 *                   type: string
 *                   example: gpt-4
 */
app.get('/api/health', (req, res) => {
  const hasApiKey = !!process.env.OPENAI_API_KEY;
  res.json({ 
    status: 'ok', 
    message: 'API funcionando',
    openaiConfigured: hasApiKey,
    model: process.env.OPENAI_MODEL || 'gpt-4'
  });
});

const server = app.listen(PORT, () => {
  console.log(`✅ Servidor rodando na porta ${PORT}`);
  console.log(`📚 Swagger UI: http://localhost:${PORT}/api-docs`);
  console.log(`🏥 Health Check: http://localhost:${PORT}/api/health`);
  console.log(`🧪 Test Supabase: http://localhost:${PORT}/api/test/supabase`);
  console.log(`📊 Admin Dashboard: http://localhost:4200/admin`);
});

server.on('error', (error) => {
  if (error.code === 'EADDRINUSE') {
    console.error(`❌ Erro: A porta ${PORT} já está em uso.`);
    console.error(`💡 Solução: Pare o processo que está usando a porta ${PORT} ou altere a porta no arquivo .env`);
    process.exit(1);
  } else {
    console.error('❌ Erro ao iniciar o servidor:', error);
    process.exit(1);
  }
});


