import express from 'express';
import multer from 'multer';
import { analyzeResume } from '../controllers/analyze.controller.js';
import { generateImprovedResumeAndPDF } from '../controllers/resume-generator.controller.js';
import { generateCoverLetterAndPDF } from '../controllers/cover-letter.controller.js';
import { searchJobs } from '../controllers/job-search.controller.js';
import { startInterview, evaluateInterviewAnswer, finishInterview, getInterview, listUserInterviews, downloadInterview } from '../controllers/interview-simulation.controller.js';
import { getPlans, createPaymentSession, verifyPayment, getCredits } from '../controllers/payment.controller.js';
import { listJobSites } from '../controllers/job-sites.controller.js';
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

/**
 * @swagger
 * /api/analyze/generate-cover-letter:
 *   post:
 *     summary: Gera uma carta de apresentação personalizada baseada no currículo e análise
 *     tags: [Análise]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - resumeText
 *               - analysis
 *             properties:
 *               resumeText:
 *                 type: string
 *                 description: Texto do currículo
 *               analysis:
 *                 type: object
 *                 description: Análise retornada pela IA
 *               siteId:
 *                 type: string
 *                 description: ID do site de vagas para personalização (opcional)
 *     responses:
 *       200:
 *         description: PDF da carta gerado com sucesso
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
router.post('/generate-cover-letter', generateCoverLetterAndPDF);

/**
 * @swagger
 * /api/analyze/search-jobs:
 *   post:
 *     summary: Busca vagas de emprego baseado na análise do currículo e site selecionado
 *     tags: [Análise]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - analysis
 *               - siteId
 *             properties:
 *               analysis:
 *                 type: object
 *                 description: Análise do currículo retornada pela IA
 *               siteId:
 *                 type: string
 *                 description: ID do site de vagas para buscar
 *               location:
 *                 type: string
 *                 description: Localização para busca (opcional, padrão: Brasil)
 *     responses:
 *       200:
 *         description: Vagas encontradas com sucesso
 *         content:
 *           application/json:
 *             schema:
 *               type: object
 *               properties:
 *                 success:
 *                   type: boolean
 *                 site:
 *                   type: string
 *                 url:
 *                   type: string
 *                 jobs:
 *                   type: array
 *                 searchTerms:
 *                   type: array
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
router.post('/search-jobs', searchJobs);

/**
 * @swagger
 * /api/analyze/interview/start:
 *   post:
 *     summary: Inicia uma simulação de entrevista
 *     tags: [Análise]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - resumeText
 *               - analysis
 *             properties:
 *               resumeText:
 *                 type: string
 *               analysis:
 *                 type: object
 *               siteId:
 *                 type: string
 *               resumeId:
 *                 type: string
 *     responses:
 *       200:
 *         description: Simulação iniciada com sucesso
 */
router.post('/interview/start', startInterview);

/**
 * @swagger
 * /api/analyze/interview/evaluate:
 *   post:
 *     summary: Avalia uma resposta do candidato
 *     tags: [Análise]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - question
 *               - answer
 *               - resumeText
 *               - analysis
 *     responses:
 *       200:
 *         description: Resposta avaliada com sucesso
 */
router.post('/interview/evaluate', evaluateInterviewAnswer);

/**
 * @swagger
 * /api/analyze/interview/finish:
 *   post:
 *     summary: Finaliza a simulação de entrevista
 *     tags: [Análise]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - simulationId
 *     responses:
 *       200:
 *         description: Simulação finalizada com sucesso
 */
router.post('/interview/finish', finishInterview);

/**
 * @swagger
 * /api/analyze/interview/{simulationId}:
 *   get:
 *     summary: Busca uma entrevista salva por ID
 *     tags: [Análise]
 *     parameters:
 *       - in: path
 *         name: simulationId
 *         required: true
 *         schema:
 *           type: string
 *     responses:
 *       200:
 *         description: Entrevista encontrada
 */
router.get('/interview/:simulationId', getInterview);

/**
 * @swagger
 * /api/analyze/interview/user/list:
 *   get:
 *     summary: Lista todas as entrevistas do usuário
 *     tags: [Análise]
 *     responses:
 *       200:
 *         description: Lista de entrevistas
 */
router.get('/interview/user/list', listUserInterviews);

/**
 * @swagger
 * /api/analyze/interview/{simulationId}/download:
 *   get:
 *     summary: Faz download de uma entrevista em formato texto
 *     tags: [Análise]
 *     parameters:
 *       - in: path
 *         name: simulationId
 *         required: true
 *         schema:
 *           type: string
 *     responses:
 *       200:
 *         description: Arquivo de texto para download
 *         content:
 *           text/plain:
 *             schema:
 *               type: string
 */
router.get('/interview/:simulationId/download', downloadInterview);

// Payment routes
router.get('/plans', getPlans);
router.post('/payment/create-session', createPaymentSession);
router.get('/payment/verify', verifyPayment);
router.get('/credits', getCredits);

// Job sites routes
router.get('/job-sites', listJobSites);

export default router;


