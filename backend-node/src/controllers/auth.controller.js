import '../load-env.js';
import jwt from 'jsonwebtoken';
import bcrypt from 'bcrypt';
import { getOrCreateUser, getUser, saveUser, getUserByEmail } from '../models/user.model.js';
import { verifyUserPassword, getOrCreateUserProfile, verifyEmailCode, verifyLoginCode as verifyLoginCodeService, updateVerificationCode, getUserProfileByEmail, updateVerificationToken, verifyEmailToken, getUserProfile, updateUserProfile, getUserByResetToken } from '../services/supabase.service.js';
import { sendVerificationEmail, generateVerificationCode, sendWelcomeEmail, sendLoginNotificationEmail, sendVerificationLinkEmail, sendPasswordResetEmail, sendPasswordChangeNotificationEmail, sendLoginCodeEmail } from '../services/email.service.js';

const JWT_SECRET = process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao';
const JWT_EXPIRES_IN = '30d'; // Token expira em 30 dias

console.log('🔐 Auth Controller - JWT_SECRET configurado:', JWT_SECRET ? 'sim (tamanho: ' + JWT_SECRET.length + ')' : 'não');

/**
 * Cria uma nova conta (sem fazer login - precisa verificar email)
 */
export const register = async (req, res) => {
  try {
    const { email, password, name, cpf } = req.body;

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

    // Valida CPF (obrigatório: 11 dígitos)
    const cpfNorm = cpf != null ? String(cpf).replace(/\D/g, '') : '';
    if (cpfNorm.length !== 11) {
      return res.status(400).json({
        success: false,
        error: 'CPF é obrigatório e deve conter 11 dígitos'
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
    const user = await getOrCreateUserProfile(userId, email, name || '', passwordHash, false, verificationCode, cpfNorm);

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
      await sendLoginNotificationEmail(profile.email, profile.name || '');
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
        cpf: profile.cpf ?? null,
        date_of_birth: profile.date_of_birth ?? null,
        city: profile.city ?? null,
        country: profile.country ?? null,
        credits: profile.credits || 0,
        user_type: profile.user_type || 'cliente'
      }
    });
  } catch (error) {
    console.error('Erro ao fazer login:', error?.message || error);
    console.error('Stack:', error?.stack);
    res.status(500).json({
      success: false,
      error: 'Erro ao fazer login',
      message: process.env.NODE_ENV === 'production' ? 'Tente novamente ou contate o suporte.' : error.message
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
        cpf: profile.cpf ?? null,
        date_of_birth: profile.date_of_birth ?? null,
        city: profile.city ?? null,
        country: profile.country ?? null,
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
        cpf: profile.cpf ?? null,
        date_of_birth: profile.date_of_birth ?? null,
        city: profile.city ?? null,
        country: profile.country ?? null,
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

/**
 * Troca a senha do usuário
 */
export const changePassword = async (req, res) => {
  try {
    const userId = req.userId; // Do middleware de autenticação
    const { currentPassword, newPassword } = req.body;

    if (!currentPassword || !newPassword) {
      return res.status(400).json({
        success: false,
        error: 'Senha atual e nova senha são obrigatórias'
      });
    }

    // Valida nova senha (mínimo 6 caracteres)
    if (newPassword.length < 6) {
      return res.status(400).json({
        success: false,
        error: 'Nova senha deve ter no mínimo 6 caracteres'
      });
    }

    // Busca o perfil do usuário
    const profile = await getUserProfile(userId);
    
    if (!profile) {
      return res.status(404).json({
        success: false,
        error: 'Usuário não encontrado'
      });
    }

    // Verifica a senha atual
    const isValidPassword = await verifyUserPassword(profile.email, currentPassword);
    
    if (!isValidPassword) {
      return res.status(401).json({
        success: false,
        error: 'Senha atual incorreta'
      });
    }

    // Gera hash da nova senha
    const bcrypt = await import('bcrypt');
    const saltRounds = 10;
    const newPasswordHash = await bcrypt.default.hash(newPassword, saltRounds);

    // Atualiza a senha no banco
    await updateUserProfile(userId, {
      password_hash: newPasswordHash
    });

    res.json({
      success: true,
      message: 'Senha alterada com sucesso'
    });
  } catch (error) {
    console.error('Erro ao trocar senha:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao trocar senha',
      message: error.message
    });
  }
};

/**
 * Atualiza dados do perfil do usuário (nome, email, cpf, data_nascimento, cidade, pais)
 */
export const updateProfile = async (req, res) => {
  try {
    const userId = req.userId;
    const { name, email, cpf, date_of_birth, city, country } = req.body;

    const profile = await getUserProfile(userId);
    if (!profile) {
      return res.status(404).json({
        success: false,
        error: 'Usuário não encontrado'
      });
    }

    const updates = {};
    if (name !== undefined) updates.name = String(name).trim() || '';
    if (email !== undefined) {
      const emailTrim = String(email).trim();
      const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
      if (!emailTrim) {
        return res.status(400).json({ success: false, error: 'Email não pode ser vazio' });
      }
      if (!emailRegex.test(emailTrim)) {
        return res.status(400).json({ success: false, error: 'Email inválido' });
      }
      if (emailTrim !== profile.email) {
        const existing = await getUserProfileByEmail(emailTrim, false);
        if (existing && existing.id !== userId) {
          return res.status(409).json({
            success: false,
            error: 'Este email já está em uso por outra conta'
          });
        }
      }
      updates.email = emailTrim;
    }
    if (cpf !== undefined) {
      const cpfNorm = String(cpf).replace(/\D/g, '');
      if (cpfNorm.length !== 11) {
        return res.status(400).json({ success: false, error: 'CPF deve conter 11 dígitos (apenas números)' });
      }
      updates.cpf = cpfNorm;
    }
    if (date_of_birth !== undefined) {
      updates.date_of_birth = (date_of_birth === '' || date_of_birth === null) ? null : String(date_of_birth).trim().split('T')[0];
    }
    if (city !== undefined) updates.city = city === '' || city === null ? null : String(city).trim();
    if (country !== undefined) updates.country = country === '' || country === null ? null : String(country).trim();

    if (Object.keys(updates).length === 0) {
      return res.status(400).json({
        success: false,
        error: 'Envie pelo menos um campo para atualizar (name, email, cpf, date_of_birth, city, country)'
      });
    }

    const updated = await updateUserProfile(userId, updates);
    res.json({
      success: true,
      message: 'Dados atualizados com sucesso',
      user: updated
    });
  } catch (error) {
    console.error('Erro ao atualizar perfil:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao atualizar perfil',
      message: error.message
    });
  }
};

/**
 * Solicita recuperação de senha (envia email com link)
 */
export const forgotPassword = async (req, res) => {
  try {
    const { email } = req.body;

    if (!email) {
      return res.status(400).json({
        success: false,
        error: 'Email é obrigatório'
      });
    }

    // Busca o usuário por email
    const profile = await getUserProfileByEmail(email, false);
    
    if (!profile) {
      // Por segurança, não revela se o email existe ou não
      return res.json({
        success: true,
        message: 'Se o email estiver cadastrado, você receberá um link de recuperação.'
      });
    }

    // Gera token de reset
    const { randomUUID } = await import('crypto');
    const resetToken = randomUUID();
    
    // Salva o token no banco (expira em 1 hora)
    await updateVerificationToken(profile.id, resetToken, 1);

    // Envia email com link de recuperação
    try {
      await sendPasswordResetEmail(profile.email, resetToken, profile.name || '');
    } catch (emailError) {
      console.error('Erro ao enviar email de recuperação:', emailError);
      // Por segurança, não revela o erro
    }

    // Sempre retorna sucesso (por segurança)
    res.json({
      success: true,
      message: 'Se o email estiver cadastrado, você receberá um link de recuperação.'
    });
  } catch (error) {
    console.error('Erro ao solicitar recuperação de senha:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao processar solicitação',
      message: error.message
    });
  }
};

/**
 * Redefine a senha usando token de recuperação
 */
export const resetPassword = async (req, res) => {
  try {
    const { token, newPassword } = req.body;

    if (!token || !newPassword) {
      return res.status(400).json({
        success: false,
        error: 'Token e nova senha são obrigatórios'
      });
    }

    // Valida nova senha (mínimo 6 caracteres)
    if (newPassword.length < 6) {
      return res.status(400).json({
        success: false,
        error: 'Nova senha deve ter no mínimo 6 caracteres'
      });
    }

    // Busca usuário pelo token de reset
    const profile = await getUserByResetToken(token);

    // Gera hash da nova senha
    const bcrypt = await import('bcrypt');
    const saltRounds = 10;
    const newPasswordHash = await bcrypt.default.hash(newPassword, saltRounds);

    // Atualiza a senha no banco
    await updateUserProfile(profile.id, {
      password_hash: newPasswordHash
    });

    // Log da mudança de senha (auditoria)
    console.log(`🔐 [AUDITORIA] Senha redefinida via recuperação para o usuário: ${profile.email} (ID: ${profile.id}) em ${new Date().toISOString()}`);

    // Envia email de notificação de mudança de senha
    try {
      await sendPasswordChangeNotificationEmail(profile.email, profile.name || '');
    } catch (emailError) {
      console.error('❌ Erro ao enviar email de notificação de mudança de senha:', emailError);
      // Não bloqueia a resposta de sucesso se o email falhar
    }

    // Remove o token de reset
    await updateVerificationToken(profile.id, null);

    res.json({
      success: true,
      message: 'Senha redefinida com sucesso! Faça login com sua nova senha.'
    });
  } catch (error) {
    console.error('Erro ao redefinir senha:', error);
    res.status(400).json({
      success: false,
      error: error.message || 'Token inválido ou expirado',
      message: error.message
    });
  }
};

/**
 * Solicita código de login por email (login sem senha)
 */
export const requestLoginCode = async (req, res) => {
  try {
    const { email } = req.body;

    if (!email) {
      return res.status(400).json({
        success: false,
        error: 'Email é obrigatório'
      });
    }

    // Busca o usuário por email
    const profile = await getUserProfileByEmail(email, false);
    
    if (!profile) {
      // Por segurança, não revela se o email existe ou não
      return res.json({
        success: true,
        message: 'Se o email estiver cadastrado, você receberá um código de login.'
      });
    }

    // Para login com código, não é necessário que o email esteja verificado
    // O código de login serve como verificação adicional

    // Gera código de login (6 dígitos)
    const loginCode = generateVerificationCode();
    
    // Salva o código no banco (expira em 10 minutos)
    await updateVerificationCode(profile.id, loginCode, 10);

    // Envia email com código de login
    try {
      await sendLoginCodeEmail(profile.email, loginCode, profile.name || '');
    } catch (emailError) {
      console.error('Erro ao enviar email com código de login:', emailError);
      // Por segurança, não revela o erro
    }

    // Sempre retorna sucesso (por segurança)
    res.json({
      success: true,
      message: 'Se o email estiver cadastrado, você receberá um código de login.'
    });
  } catch (error) {
    console.error('Erro ao solicitar código de login:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao processar solicitação',
      message: error.message
    });
  }
};

/**
 * Valida código de login e faz login (sem senha)
 */
export const verifyLoginCode = async (req, res) => {
  try {
    const { email, code } = req.body;

    if (!email || !code) {
      return res.status(400).json({
        success: false,
        error: 'Email e código são obrigatórios'
      });
    }

    // Verifica o código de login
    const profile = await verifyLoginCodeService(email, code);

    // Gera token JWT
    const token = jwt.sign(
      { userId: profile.id, email: profile.email },
      JWT_SECRET,
      { expiresIn: JWT_EXPIRES_IN }
    );

    // Envia email de notificação de login
    try {
      await sendLoginNotificationEmail(profile.email, profile.name || '');
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
        cpf: profile.cpf ?? null,
        date_of_birth: profile.date_of_birth ?? null,
        city: profile.city ?? null,
        country: profile.country ?? null,
        credits: profile.credits || 0,
        user_type: profile.user_type || 'cliente'
      }
    });
  } catch (error) {
    console.error('Erro ao verificar código de login:', error);
    res.status(400).json({
      success: false,
      error: error.message || 'Código inválido ou expirado',
      message: error.message
    });
  }
};

/**
 * Callback do OAuth do Google
 */
export const googleCallback = async (req, res) => {
  try {
    const user = req.user;

    if (!user) {
      return res.redirect(`${process.env.FRONTEND_URL || 'http://localhost:4200'}/login?error=google_auth_failed`);
    }

    // Gera token JWT
    const token = jwt.sign(
      { userId: user.id, email: user.email },
      JWT_SECRET,
      { expiresIn: JWT_EXPIRES_IN }
    );

    // Redireciona para o frontend com o token
    const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
    res.redirect(`${frontendUrl}/login?token=${token}&success=true`);
  } catch (error) {
    console.error('Erro no callback do Google:', error);
    const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
    res.redirect(`${frontendUrl}/login?error=google_auth_error`);
  }
};
