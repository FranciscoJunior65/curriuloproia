import express from 'express';
import { getDashboardStats, getDailyUsage, getMonthlyUsage, getSales, getSalesStatistics, getAIUsageStatistics, getJobSiteStats, getJobSiteDetailedStatsController } from '../controllers/admin.controller.js';
import { authenticate, requireAdmin } from '../middleware/auth.middleware.js';

const router = express.Router();

// Todas as rotas admin requerem autenticação e permissão de admin
router.use(authenticate);
router.use(requireAdmin);

/**
 * @swagger
 * /api/admin/stats:
 *   get:
 *     summary: Obtém estatísticas gerais do dashboard
 *     tags: [Admin]
 *     responses:
 *       200:
 *         description: Estatísticas obtidas com sucesso
 */
router.get('/stats', getDashboardStats);

/**
 * @swagger
 * /api/admin/usage/daily:
 *   get:
 *     summary: Obtém uso diário
 *     tags: [Admin]
 *     parameters:
 *       - in: query
 *         name: days
 *         schema:
 *           type: integer
 *           default: 30
 *         description: Número de dias para retornar
 *     responses:
 *       200:
 *         description: Dados de uso diário
 */
router.get('/usage/daily', getDailyUsage);

/**
 * @swagger
 * /api/admin/usage/monthly:
 *   get:
 *     summary: Obtém uso mensal
 *     tags: [Admin]
 *     parameters:
 *       - in: query
 *         name: months
 *         schema:
 *           type: integer
 *           default: 12
 *         description: Número de meses para retornar
 *     responses:
 *       200:
 *         description: Dados de uso mensal
 */
router.get('/usage/monthly', getMonthlyUsage);

/**
 * @swagger
 * /api/admin/sales:
 *   get:
 *     summary: Obtém lista de vendas/compras
 *     tags: [Admin]
 *     parameters:
 *       - in: query
 *         name: limit
 *         schema:
 *           type: integer
 *           default: 100
 *       - in: query
 *         name: offset
 *         schema:
 *           type: integer
 *           default: 0
 *     responses:
 *       200:
 *         description: Lista de vendas
 */
router.get('/sales', getSales);

/**
 * @swagger
 * /api/admin/sales/statistics:
 *   get:
 *     summary: Obtém estatísticas de vendas
 *     tags: [Admin]
 *     parameters:
 *       - in: query
 *         name: startDate
 *         schema:
 *           type: string
 *       - in: query
 *         name: endDate
 *         schema:
 *           type: string
 *     responses:
 *       200:
 *         description: Estatísticas de vendas
 */
router.get('/sales/statistics', getSalesStatistics);

/**
 * @swagger
 * /api/admin/ai-usage:
 *   get:
 *     summary: Obtém estatísticas de uso de IA
 *     tags: [Admin]
 *     parameters:
 *       - in: query
 *         name: period
 *         schema:
 *           type: string
 *           enum: [hour, day, week, month]
 *           default: day
 *         description: Período para análise
 *     responses:
 *       200:
 *         description: Estatísticas de uso de IA
 */
router.get('/ai-usage', getAIUsageStatistics);

/**
 * @swagger
 * /api/admin/job-sites/stats:
 *   get:
 *     summary: Obtém estatísticas de uso por site de vagas
 *     tags: [Admin]
 *     parameters:
 *       - in: query
 *         name: startDate
 *         schema:
 *           type: string
 *       - in: query
 *         name: endDate
 *         schema:
 *           type: string
 *       - in: query
 *         name: limit
 *         schema:
 *           type: integer
 *           default: 10
 *     responses:
 *       200:
 *         description: Estatísticas de sites de vagas
 */
router.get('/job-sites/stats', getJobSiteStats);

/**
 * @swagger
 * /api/admin/job-sites/:siteId/stats:
 *   get:
 *     summary: Obtém estatísticas detalhadas de um site específico
 *     tags: [Admin]
 *     parameters:
 *       - in: path
 *         name: siteId
 *         required: true
 *         schema:
 *           type: string
 *       - in: query
 *         name: startDate
 *         schema:
 *           type: string
 *       - in: query
 *         name: endDate
 *         schema:
 *           type: string
 *     responses:
 *       200:
 *         description: Estatísticas detalhadas do site
 */
router.get('/job-sites/:siteId/stats', getJobSiteDetailedStatsController);

export default router;

