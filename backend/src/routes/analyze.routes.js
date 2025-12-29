import express from 'express';
import multer from 'multer';
import { analyzeResume } from '../controllers/analyze.controller.js';
import { generateImprovedResumeAndPDF } from '../controllers/resume-generator.controller.js';
import { getPlans, createPaymentSession, verifyPayment, getCredits } from '../controllers/payment.controller.js';
import { handleWebhook } from '../services/stripe.service.js';

const router = express.Router();
const upload = multer({ 
  storage: multer.memoryStorage(),
  limits: { fileSize: 10 * 1024 * 1024 } // 10MB
});

/**
 * @swagger
 * /api/analyze/upload:
 *   post:
 *     summary: Analisa um currículo usando IA
 *     tags: [Análise]
 *     requestBody:
 *       required: true
 *       content:
 *         multipart/form-data:
 *           schema:
 *             type: object
 *             required:
 *               - file
 *             properties:
 *               file:
 *                 type: string
 *                 format: binary
 *                 description: Arquivo do currículo (PDF, DOC, DOCX ou TXT)
 *     responses:
 *       200:
 *         description: Análise realizada com sucesso
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/AnalysisResult'
 *       400:
 *         description: Erro na validação do arquivo
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/Error'
 *       429:
 *         description: Limite de requisições excedido
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/Error'
 *       500:
 *         description: Erro interno do servidor
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/Error'
 */
router.post('/upload', upload.single('file'), analyzeResume);

/**
 * @swagger
 * /api/analyze/generate-improved:
 *   post:
 *     summary: Gera um currículo melhorado baseado na análise e retorna em PDF
 *     tags: [Análise]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - originalText
 *               - analysis
 *             properties:
 *               originalText:
 *                 type: string
 *                 description: Texto original do currículo
 *               analysis:
 *                 type: object
 *                 description: Análise retornada pela IA
 *     responses:
 *       200:
 *         description: PDF gerado com sucesso
 *         content:
 *           application/pdf:
 *             schema:
 *               type: string
 *               format: binary
 *       400:
 *         description: Erro na validação dos dados
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/Error'
 *       500:
 *         description: Erro interno do servidor
 *         content:
 *           application/json:
 *             schema:
 *               $ref: '#/components/schemas/Error'
 */
router.post('/generate-improved', generateImprovedResumeAndPDF);

// Payment routes
router.get('/plans', getPlans);
router.post('/payment/create-session', createPaymentSession);
router.get('/payment/verify', verifyPayment);
router.get('/credits', getCredits);

export default router;


