import passport from 'passport';
import { Strategy as GoogleStrategy } from 'passport-google-oauth20';
import { getOrCreateUserProfile } from '../services/supabase.service.js';
import jwt from 'jsonwebtoken';

const JWT_SECRET = process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao';
const JWT_EXPIRES_IN = process.env.JWT_EXPIRES_IN || '7d';

export const setupGoogleStrategy = () => {
  const GOOGLE_CLIENT_ID = process.env.GOOGLE_CLIENT_ID;
  const GOOGLE_CLIENT_SECRET = process.env.GOOGLE_CLIENT_SECRET;
  const FRONTEND_URL = process.env.FRONTEND_URL || 'http://localhost:4200';

  if (!GOOGLE_CLIENT_ID || !GOOGLE_CLIENT_SECRET) {
    console.warn('⚠️  Google OAuth não configurado. Defina GOOGLE_CLIENT_ID e GOOGLE_CLIENT_SECRET no .env');
    // Registra uma estratégia dummy usando GoogleStrategy com credenciais inválidas
    // Isso evita o erro "Unknown authentication strategy" mas sempre falhará na autenticação
    passport.use('google', new GoogleStrategy(
      {
        clientID: 'dummy',
        clientSecret: 'dummy',
        callbackURL: '/api/auth/google/callback'
      },
      async (accessToken, refreshToken, profile, done) => {
        return done(new Error('Google OAuth não configurado. Configure GOOGLE_CLIENT_ID e GOOGLE_CLIENT_SECRET no .env'), null);
      }
    ));
    return;
  }

  passport.use('google',
    new GoogleStrategy(
      {
        clientID: GOOGLE_CLIENT_ID,
        clientSecret: GOOGLE_CLIENT_SECRET,
        callbackURL: '/api/auth/google/callback'
      },
      async (accessToken, refreshToken, profile, done) => {
        try {
          const email = profile.emails?.[0]?.value;
          const name = profile.displayName || profile.name?.givenName || '';
          const googleId = profile.id;

          if (!email) {
            return done(new Error('Email não encontrado no perfil do Google'), null);
          }

          // Busca usuário existente por email
          const { getUserProfileByEmail, updateUserProfile } = await import('../services/supabase.service.js');
          let userProfile = await getUserProfileByEmail(email, false);

          if (!userProfile) {
            // Cria novo usuário se não existir
            const { randomUUID } = await import('crypto');
            const userId = randomUUID();
            userProfile = await getOrCreateUserProfile(
              userId,
              email,
              name,
              null, // Sem senha para login OAuth
              true, // Email já verificado pelo Google
              null  // Sem código de verificação
            );
          } else {
            // Atualiza email_verificado se necessário
            if (!userProfile.emailVerified) {
              await updateUserProfile(userProfile.id, {
                email_verified: true
              });
              userProfile.emailVerified = true;
            }
          }

          return done(null, {
            id: userProfile.id,
            email: userProfile.email,
            name: userProfile.name,
            googleId: googleId
          });
        } catch (error) {
          console.error('Erro ao processar login do Google:', error);
          return done(error, null);
        }
      }
    )
  );

  passport.serializeUser((user, done) => {
    done(null, user);
  });

  passport.deserializeUser((user, done) => {
    done(null, user);
  });
};

export const generateGoogleAuthToken = (user) => {
  return jwt.sign(
    { userId: user.id, email: user.email },
    JWT_SECRET,
    { expiresIn: JWT_EXPIRES_IN }
  );
};
