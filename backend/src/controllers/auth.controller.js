import jwt from 'jsonwebtoken';
import bcrypt from 'bcrypt';
import dotenv from 'dotenv';
import { getOrCreateUser, getUser, saveUser, getUserByEmail } from '../models/user.model.js';
import { verifyUserPassword, getOrCreateUserProfile, verifyEmailCode, updateVerificationCode, getUserProfileByEmail, updateVerificationToken, verifyEmailToken, getUserProfile } from '../services/supabase.service.js';
import { sendVerificationEmail, generateVerificationCode, sendWelcomeEmail, sendLoginNotificationEmail, sendVerificationLinkEmail } from '../services/email.service.js';

dotenv.config();

const JWT_SECRET = process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao';
const JWT_EXPIRES_IN = '30d'; // Token expira em 30 dias

console.log('🔐 Auth Controller - JWT_SECRET configurado:', JWT_SECRET ? 'sim (tamanho: ' + JWT_SECRET.length + ')' : 'não');

/**
 * Cria uma nova conta (sem fazer login - precisa verificar email)
 */
export const register = async (req, res) => {
  try {
    const { email, password, name } = req.body;

    // Validações básicas
    if (!email || !password) {
      return res.status(400).json({
        success: false,
        error: 'Email e senha são obrigatórios'
      });
    }

    // Valida formato de email
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
      return res.status(400).json({
        success: false,
        error: 'Email inválido'
      });
    }

    // Valida senha (mínimo 6 caracteres)
    if (password.length < 6) {
      return res.status(400).json({
        success: false,
        error: 'Senha deve ter no mínimo 6 caracteres'
      });
    }

    // Verifica se usuário já existe
    const existingProfile = await getUserProfileByEmail(email, false);
    
    if (existingProfile) {
      // Se email já está verificado, informa para fazer login
      if (existingProfile.email_verified) {
        return res.status(409).json({
          success: false,
          error: 'Email já cadastrado',
          message: 'Este email já está cadastrado e verificado. Faça login para continuar.',
          action: 'login'
        });
      }
      
      // Se email não está verificado, envia link de verificação
      const { randomUUID } = await import('crypto');
      const verificationToken = randomUUID();
      await updateVerificationToken(existingProfile.id, verificationToken);
      
      try {
        await sendVerificationLinkEmail(email, verificationToken, existingProfile.name || '');
      } catch (emailError) {
        console.error('Erro ao enviar email:', emailError);
      }
      
      return res.status(409).json({
        success: false,
        error: 'Email já cadastrado',
        message: 'Este email já está cadastrado mas não foi verificado. Enviamos um novo link de verificação para seu email.',
        requiresVerification: true,
        action: 'verify'
      });
    }

    // Cria hash da senha
    const saltRounds = 10;
    const passwordHash = await bcrypt.hash(password, saltRounds);

    // Gera código de verificação
    const verificationCode = generateVerificationCode();

    // Gera UUID válido para o usuário (Supabase requer UUID)
    const { randomUUID } = await import('crypto');
    const userId = randomUUID();

    // Cria novo usuário (email não verificado)
    const user = await getOrCreateUserProfile(userId, email, name || '', passwordHash, false, verificationCode);

    // Envia email com código de verificação
    try {
      await sendVerificationEmail(email, verificationCode, name || '');
    } catch (emailError) {
      console.error('Erro ao enviar email:', emailError);
      // Continua mesmo se o email falhar (para desenvolvimento)
    }

    res.json({
      success: true,
      message: 'Conta criada! Verifique seu email para o código de verificação.',
      requiresVerification: true,
      userId: user.id,
      email: user.email
    });
  } catch (error) {
    console.error('Erro ao criar conta:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao criar conta',
      message: error.message
    });
  }
};

/**
 * Faz login (só permite se email estiver verificado)
 */
export const login = async (req, res) => {
  try {
    const { email, password } = req.body;

    if (!email || !password) {
      return res.status(400).json({
        success: false,
        error: 'Email e senha são obrigatórios'
      });
    }

    // Busca usuário por email (incluindo email_verified)
    const profile = await getUserProfileByEmail(email, true);

    if (!profile) {
      return res.status(401).json({
        success: false,
        error: 'Email ou senha incorretos'
      });
    }

    // Verifica senha primeiro
    const isValidPassword = await verifyUserPassword(email, password);
    
    if (!isValidPassword) {
      return res.status(401).json({
        success: false,
        error: 'Email ou senha incorretos'
      });
    }

    // Se senha está correta mas email não está verificado, envia código
    if (!profile.email_verified) {
      const verificationCode = generateVerificationCode();
      await updateVerificationCode(profile.id, verificationCode);
      
      try {
        await sendVerificationEmail(email, verificationCode, profile.name || '');
      } catch (emailError) {
        console.error('Erro ao enviar email:', emailError);
      }
      
      return res.status(403).json({
        success: false,
        error: 'Email não verificado',
        requiresVerification: true,
        message: 'Sua senha está correta, mas o email ainda não foi verificado. Enviamos um novo código de verificação para seu email.',
        codeSent: true
      });
    }

    // Gera token JWT
    const token = jwt.sign(
      { userId: profile.id, email: profile.email },
      JWT_SECRET,
      { expiresIn: JWT_EXPIRES_IN }
    );

    // Envia email de notificação de login
    try {
      const clientIp = req.ip || req.connection.remoteAddress || 'desconhecido';
      await sendLoginNotificationEmail(profile.email, profile.name || '', clientIp);
    } catch (emailError) {
      console.error('Erro ao enviar email de notificação de login:', emailError);
      // Não bloqueia o login se o email falhar
    }

    res.json({
      success: true,
      message: 'Login realizado com sucesso',
      token,
      user: {
        id: profile.id,
        email: profile.email,
        name: profile.name,
        credits: profile.credits || 0,
        user_type: profile.user_type || 'cliente'
      }
    });
  } catch (error) {
    console.error('Erro ao fazer login:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao fazer login',
      message: error.message
    });
  }
};

/**
 * Verifica código de verificação e faz login
 */
export const verifyEmail = async (req, res) => {
  try {
    const { email, code } = req.body;

    if (!email || !code) {
      return res.status(400).json({
        success: false,
        error: 'Email e código são obrigatórios'
      });
    }

    // Verifica o código
    const profile = await verifyEmailCode(email, code);

    // Gera token JWT após verificação bem-sucedida
    const token = jwt.sign(
      { userId: profile.id, email: profile.email },
      JWT_SECRET,
      { expiresIn: JWT_EXPIRES_IN }
    );

    // Envia email de boas-vindas após verificação
    try {
      await sendWelcomeEmail(profile.email, profile.name || '');
    } catch (emailError) {
      console.error('Erro ao enviar email de boas-vindas:', emailError);
      // Não bloqueia a verificação se o email falhar
    }

    res.json({
      success: true,
      message: 'Email verificado com sucesso!',
      token,
      user: {
        id: profile.id,
        email: profile.email,
        name: profile.name,
        credits: profile.credits || 0,
        user_type: profile.user_type || 'cliente'
      }
    });
  } catch (error) {
    console.error('Erro ao verificar email:', error);
    res.status(400).json({
      success: false,
      error: error.message || 'Erro ao verificar código',
      message: error.message
    });
  }
};

/**
 * Reenvia código de verificação
 */
export const resendVerificationCode = async (req, res) => {
  try {
    const { email } = req.body;

    if (!email) {
      return res.status(400).json({
        success: false,
        error: 'Email é obrigatório'
      });
    }

    // Busca usuário
    const profile = await getUserProfileByEmail(email, false);

    if (!profile) {
      return res.status(404).json({
        success: false,
        error: 'Usuário não encontrado'
      });
    }

    // Se já está verificado, não precisa reenviar
    if (profile.email_verified) {
      return res.status(400).json({
        success: false,
        error: 'Email já está verificado'
      });
    }

    // Gera novo código
    const verificationCode = generateVerificationCode();
    await updateVerificationCode(profile.id, verificationCode);

    // Envia email
    try {
      await sendVerificationEmail(email, verificationCode, profile.name || '');
      res.json({
        success: true,
        message: 'Código de verificação reenviado com sucesso!'
      });
    } catch (emailError) {
      console.error('Erro ao enviar email:', emailError);
      res.status(500).json({
        success: false,
        error: 'Erro ao enviar email de verificação',
        message: emailError.message
      });
    }
  } catch (error) {
    console.error('Erro ao reenviar código:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao reenviar código',
      message: error.message
    });
  }
};

/**
 * Verifica token e retorna dados do usuário
 */
export const verifyToken = async (req, res) => {
  try {
    const token = req.headers.authorization?.replace('Bearer ', '');

    if (!token) {
      return res.status(401).json({
        success: false,
        error: 'Token não fornecido'
      });
    }

    const decoded = jwt.verify(token, JWT_SECRET);
    const profile = await getUserProfile(decoded.userId);

    if (!profile) {
      return res.status(401).json({
        success: false,
        error: 'Usuário não encontrado'
      });
    }

    res.json({
      success: true,
      user: {
        id: profile.id,
        email: profile.email,
        name: profile.name,
        credits: profile.credits || 0,
        plan: profile.plan,
        user_type: profile.user_type || 'cliente'
      }
    });
  } catch (error) {
    if (error.name === 'JsonWebTokenError' || error.name === 'TokenExpiredError') {
      return res.status(401).json({
        success: false,
        error: 'Token inválido ou expirado'
      });
    }

    res.status(500).json({
      success: false,
      error: 'Erro ao verificar token',
      message: error.message
    });
  }
};

/**
 * Verifica email via token (link de verificação)
 */
export const verifyEmailByToken = async (req, res) => {
  try {
    const { email, token } = req.query;

    if (!email || !token) {
      return res.status(400).json({
        success: false,
        error: 'Email e token são obrigatórios'
      });
    }

    // Verifica o token
    const profile = await verifyEmailToken(email, token);

    // Gera token JWT após verificação bem-sucedida
    const jwtToken = jwt.sign(
      { userId: profile.id, email: profile.email },
      JWT_SECRET,
      { expiresIn: JWT_EXPIRES_IN }
    );

    // Envia email de boas-vindas após verificação
    try {
      await sendWelcomeEmail(profile.email, profile.name || '');
    } catch (emailError) {
      console.error('Erro ao enviar email de boas-vindas:', emailError);
    }

    // Redireciona para o frontend com o token
    const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
    res.redirect(`${frontendUrl}/verify-email-success?token=${jwtToken}`);
  } catch (error) {
    console.error('Erro ao verificar email por token:', error);
    const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
    res.redirect(`${frontendUrl}/verify-email-error?error=${encodeURIComponent(error.message)}`);
  }
};

