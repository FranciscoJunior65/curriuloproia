import express from 'express';
import { getDashboardStats, getDailyUsage, getMonthlyUsage, getSales, getSalesStatistics } from '../controllers/admin.controller.js';
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

export default router;

