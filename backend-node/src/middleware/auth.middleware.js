import '../load-env.js';
import jwt from 'jsonwebtoken';
import { getUserProfile } from '../services/supabase.service.js';

const JWT_SECRET = process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao';

console.log('🔐 Middleware - JWT_SECRET configurado:', JWT_SECRET ? 'sim (tamanho: ' + JWT_SECRET.length + ')' : 'não');

/**
 * Middleware para verificar autenticação
 */
export const authenticate = async (req, res, next) => {
  try {
    const authHeader = req.headers.authorization;
    
    console.log('🔐 Middleware authenticate - authHeader:', authHeader ? 'presente' : 'ausente');
    
    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      console.log('❌ Token não fornecido ou formato inválido');
      return res.status(401).json({
        success: false,
        error: 'Token não fornecido'
      });
    }

    const token = authHeader.substring(7); // Remove 'Bearer '
    console.log('🔑 Token extraído:', token.substring(0, 20) + '...');

    try {
      const decoded = jwt.verify(token, JWT_SECRET);
      console.log('✅ Token válido - userId:', decoded.userId);
      req.userId = decoded.userId;
      req.userEmail = decoded.email;
      next();
    } catch (error) {
      console.error('❌ Erro ao verificar token:', error.message);
      console.error('JWT_SECRET configurado:', JWT_SECRET ? 'sim' : 'não');
      return res.status(401).json({
        success: false,
        error: 'Token inválido ou expirado',
        details: error.message
      });
    }
  } catch (error) {
    console.error('❌ Erro no middleware authenticate:', error);
    return res.status(500).json({
      success: false,
      error: 'Erro ao verificar autenticação',
      message: error.message
    });
  }
};

/**
 * Middleware para verificar se o usuário é admin
 */
export const requireAdmin = async (req, res, next) => {
  try {
    // Primeiro verifica autenticação
    if (!req.userId) {
      return res.status(401).json({
        success: false,
        error: 'Usuário não autenticado'
      });
    }

    // Busca o perfil do usuário
    const profile = await getUserProfile(req.userId);

    if (!profile) {
      return res.status(404).json({
        success: false,
        error: 'Usuário não encontrado'
      });
    }

    // Verifica se é admin
    if (profile.user_type !== 'admin') {
      return res.status(403).json({
        success: false,
        error: 'Acesso negado',
        message: 'Apenas administradores podem acessar esta rota'
      });
    }

    // Adiciona informações do usuário na requisição
    req.user = profile;
    next();
  } catch (error) {
    return res.status(500).json({
      success: false,
      error: 'Erro ao verificar permissões',
      message: error.message
    });
  }
};

