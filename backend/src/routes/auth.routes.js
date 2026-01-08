import express from 'express';
import { register, login, verifyToken, verifyEmail, resendVerificationCode, verifyEmailByToken, changePassword, forgotPassword, resetPassword, requestLoginCode, verifyLoginCode, googleCallback } from '../controllers/auth.controller.js';
import { authenticate } from '../middleware/auth.middleware.js';
import passport from 'passport';

const router = express.Router();

/**
 * @swagger
 * /api/auth/register:
 *   post:
 *     summary: Cria uma nova conta
 *     tags: [Autenticação]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - email
 *               - password
 *             properties:
 *               email:
 *                 type: string
 *               password:
 *                 type: string
 *               name:
 *                 type: string
 *     responses:
 *       200:
 *         description: Conta criada com sucesso
 *       400:
 *         description: Dados inválidos
 *       409:
 *         description: Email já cadastrado
 */
router.post('/register', register);

/**
 * @swagger
 * /api/auth/login:
 *   post:
 *     summary: Faz login
 *     tags: [Autenticação]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - email
 *               - password
 *             properties:
 *               email:
 *                 type: string
 *               password:
 *                 type: string
 *     responses:
 *       200:
 *         description: Login realizado com sucesso
 *       401:
 *         description: Credenciais inválidas
 */
router.post('/login', login);

/**
 * @swagger
 * /api/auth/verify:
 *   get:
 *     summary: Verifica token e retorna dados do usuário
 *     tags: [Autenticação]
 *     security:
 *       - bearerAuth: []
 *     responses:
 *       200:
 *         description: Token válido
 *       401:
 *         description: Token inválido ou expirado
 */
router.get('/verify', verifyToken);

/**
 * @swagger
 * /api/auth/verify-email:
 *   post:
 *     summary: Verifica código de verificação de email
 *     tags: [Autenticação]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - email
 *               - code
 *             properties:
 *               email:
 *                 type: string
 *               code:
 *                 type: string
 *     responses:
 *       200:
 *         description: Email verificado com sucesso
 *       400:
 *         description: Código inválido ou expirado
 */
router.post('/verify-email', verifyEmail);

/**
 * @swagger
 * /api/auth/resend-verification:
 *   post:
 *     summary: Reenvia código de verificação
 *     tags: [Autenticação]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - email
 *             properties:
 *               email:
 *                 type: string
 *     responses:
 *       200:
 *         description: Código reenviado com sucesso
 *       404:
 *         description: Usuário não encontrado
 */
router.post('/resend-verification', resendVerificationCode);

/**
 * @swagger
 * /api/auth/verify-email-link:
 *   get:
 *     summary: Verifica email via link/token
 *     tags: [Autenticação]
 *     parameters:
 *       - in: query
 *         name: email
 *         required: true
 *         schema:
 *           type: string
 *       - in: query
 *         name: token
 *         required: true
 *         schema:
 *           type: string
 *     responses:
 *       302:
 *         description: Redireciona para frontend com token ou erro
 */
router.get('/verify-email-link', verifyEmailByToken);

/**
 * @swagger
 * /api/auth/change-password:
 *   post:
 *     summary: Troca a senha do usuário autenticado
 *     tags: [Autenticação]
 *     security:
 *       - bearerAuth: []
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - currentPassword
 *               - newPassword
 *             properties:
 *               currentPassword:
 *                 type: string
 *               newPassword:
 *                 type: string
 *     responses:
 *       200:
 *         description: Senha alterada com sucesso
 *       401:
 *         description: Senha atual incorreta ou não autorizado
 *       400:
 *         description: Dados inválidos
 */
router.post('/change-password', authenticate, changePassword);

/**
 * @swagger
 * /api/auth/forgot-password:
 *   post:
 *     summary: Solicita recuperação de senha
 *     tags: [Autenticação]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - email
 *             properties:
 *               email:
 *                 type: string
 *     responses:
 *       200:
 *         description: Email enviado (se o email estiver cadastrado)
 */
router.post('/forgot-password', forgotPassword);

/**
 * @swagger
 * /api/auth/reset-password:
 *   post:
 *     summary: Redefine senha usando token de recuperação
 *     tags: [Autenticação]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - token
 *               - newPassword
 *             properties:
 *               token:
 *                 type: string
 *               newPassword:
 *                 type: string
 *     responses:
 *       200:
 *         description: Senha redefinida com sucesso
 *       400:
 *         description: Token inválido ou expirado
 */
router.post('/reset-password', resetPassword);

/**
 * @swagger
 * /api/auth/request-login-code:
 *   post:
 *     summary: Solicita código de login por email (login sem senha)
 *     tags: [Autenticação]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - email
 *             properties:
 *               email:
 *                 type: string
 *     responses:
 *       200:
 *         description: Código enviado (se o email estiver cadastrado)
 */
router.post('/request-login-code', requestLoginCode);

/**
 * @swagger
 * /api/auth/verify-login-code:
 *   post:
 *     summary: Valida código de login e faz login
 *     tags: [Autenticação]
 *     requestBody:
 *       required: true
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             required:
 *               - email
 *               - code
 *             properties:
 *               email:
 *                 type: string
 *               code:
 *                 type: string
 *     responses:
 *       200:
 *         description: Login realizado com sucesso
 *       400:
 *         description: Código inválido ou expirado
 */
router.post('/verify-login-code', verifyLoginCode);

/**
 * @swagger
 * /api/auth/google:
 *   get:
 *     summary: Inicia autenticação com Google OAuth
 *     tags: [Autenticação]
 *     responses:
 *       302:
 *         description: Redireciona para o Google
 */
router.get('/google', (req, res, next) => {
  // Verifica se as credenciais do Google estão configuradas
  if (!process.env.GOOGLE_CLIENT_ID || !process.env.GOOGLE_CLIENT_SECRET) {
    const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
    return res.redirect(`${frontendUrl}/login?error=google_not_configured`);
  }
  passport.authenticate('google', { scope: ['profile', 'email'] })(req, res, next);
});

/**
 * @swagger
 * /api/auth/google/callback:
 *   get:
 *     summary: Callback do Google OAuth
 *     tags: [Autenticação]
 *     responses:
 *       302:
 *         description: Redireciona para o frontend com token
 */
router.get('/google/callback', (req, res, next) => {
  // Verifica se as credenciais do Google estão configuradas
  if (!process.env.GOOGLE_CLIENT_ID || !process.env.GOOGLE_CLIENT_SECRET) {
    const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
    return res.redirect(`${frontendUrl}/login?error=google_not_configured`);
  }
  passport.authenticate('google', { 
    failureRedirect: `${process.env.FRONTEND_URL || 'http://localhost:4200'}/login?error=google_auth_failed` 
  })(req, res, next);
}, googleCallback);

export default router;

