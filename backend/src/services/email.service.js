import nodemailer from 'nodemailer';

// Configuração do transporter de email
const createTransporter = () => {
  const emailService = process.env.EMAIL_SERVICE;
  const emailHost = process.env.EMAIL_HOST || process.env.SMTP_HOST;
  const emailPort = process.env.EMAIL_PORT || process.env.SMTP_PORT;
  const emailSecure = process.env.EMAIL_SECURE === 'true' || process.env.SMTP_SECURE === 'true';
  const emailUser = process.env.EMAIL_USER || process.env.EMAIL_SENDER;
  const emailPassword = process.env.EMAIL_PASSWORD || process.env.EMAIL_SENDER_PASSWORD;
  const emailSenderName = process.env.EMAIL_SENDER_NAME || 'CurriculoPro IA';

  if (!emailUser || !emailPassword) {
    console.warn('⚠️  Email não configurado. Variáveis EMAIL_USER e EMAIL_PASSWORD são necessárias.');
    return null;
  }

  // Se usar Gmail (EMAIL_SERVICE=gmail)
  if (emailService === 'gmail') {
    return nodemailer.createTransport({
      service: 'gmail',
      auth: {
        user: emailUser,
        pass: emailPassword
      }
    });
  }

  // Se usar SMTP genérico
  if (emailHost && emailPort) {
    const port = parseInt(emailPort);
    const secure = emailSecure || port === 465; // 465 geralmente usa SSL
    
    return nodemailer.createTransport({
      host: emailHost,
      port: port,
      secure: secure, // true para 465 (SSL), false para 587 (TLS)
      auth: {
        user: emailUser,
        pass: emailPassword
      },
      tls: {
        rejectUnauthorized: false, // Para servidores com certificado auto-assinado
        ciphers: 'SSLv3' // Alguns servidores precisam disso
      },
      // Para porta 587, requer STARTTLS
      requireTLS: port === 587,
      debug: false, // Desativa logs de debug (mude para true se precisar debugar)
      logger: false // Desativa logs no console
    });
  }

  console.warn('⚠️  Configuração de email incompleta.');
  return null;
};

let transporter = createTransporter();

// Função para recriar o transporter (útil se as variáveis de ambiente mudarem)
export const recreateTransporter = () => {
  transporter = createTransporter();
  return transporter;
};

/**
 * Envia email de verificação com código
 */
export const sendVerificationEmail = async (email, code, name = '') => {
  // Tenta recriar o transporter se não existir (caso as variáveis foram carregadas depois)
  if (!transporter) {
    transporter = createTransporter();
  }
  
  if (!transporter) {
    const emailUser = process.env.EMAIL_USER || process.env.EMAIL_SENDER;
    const emailPassword = process.env.EMAIL_PASSWORD || process.env.EMAIL_SENDER_PASSWORD;
    const emailHost = process.env.EMAIL_HOST || process.env.SMTP_HOST;
    const emailPort = process.env.EMAIL_PORT || process.env.SMTP_PORT;
    
    console.error('❌ Email não configurado. Variáveis necessárias:');
    console.error('   EMAIL_SENDER ou EMAIL_USER:', emailUser ? '✅' : '❌');
    console.error('   EMAIL_SENDER_PASSWORD ou EMAIL_PASSWORD:', emailPassword ? '✅' : '❌');
    console.error('   SMTP_HOST ou EMAIL_HOST:', emailHost ? '✅' : '❌');
    console.error('   SMTP_PORT ou EMAIL_PORT:', emailPort ? '✅' : '❌');
    
    throw new Error('Serviço de email não configurado. Verifique as variáveis de ambiente no arquivo .env');
  }

  const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
  const appName = 'CurriculoPro IA'; // Sempre usa CurriculoPro IA para emails do serviço
  const emailSender = process.env.EMAIL_USER || process.env.EMAIL_SENDER;
  const emailCopy = process.env.EMAIL_COPY || process.env.EMAIL_COPY_TO; // Cópia para outro email

  const mailOptions = {
    from: `"${appName}" <${emailSender}>`,
    to: email,
    cc: emailCopy ? [emailCopy] : undefined, // Adiciona cópia se configurado
    subject: `🔐 Código de Verificação - ${appName}`,
    html: `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <style>
          body {
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
          }
          .container {
            background: #f9f9f9;
            border-radius: 8px;
            padding: 30px;
            margin: 20px 0;
          }
          .header {
            text-align: center;
            margin-bottom: 30px;
          }
          .code-box {
            background: #fff;
            border: 2px dashed #4CAF50;
            border-radius: 8px;
            padding: 20px;
            text-align: center;
            margin: 30px 0;
          }
          .code {
            font-size: 32px;
            font-weight: bold;
            color: #4CAF50;
            letter-spacing: 8px;
            font-family: 'Courier New', monospace;
          }
          .footer {
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #ddd;
            font-size: 12px;
            color: #666;
            text-align: center;
          }
        </style>
      </head>
      <body>
        <div class="container">
          <div class="header">
            <h1 style="color: #4CAF50; margin: 0;">${appName}</h1>
          </div>
          
          <p>Olá${name ? `, ${name}` : ''}!</p>
          
          <p>Obrigado por se cadastrar no ${appName}. Para completar seu cadastro, use o código de verificação abaixo:</p>
          
          <div class="code-box">
            <div class="code">${code}</div>
          </div>
          
          <p><strong>Este código expira em 15 minutos.</strong></p>
          
          <p>Se você não solicitou este código, ignore este email.</p>
          
          <div class="footer">
            <p>Este é um email automático, por favor não responda.</p>
            <p>&copy; ${new Date().getFullYear()} GetPush Tecnologia. Todos os direitos reservados.</p>
          </div>
        </div>
      </body>
      </html>
    `,
    text: `
      Olá${name ? `, ${name}` : ''}!
      
      Obrigado por se cadastrar no ${appName}. 
      
      Seu código de verificação é: ${code}
      
      Este código expira em 15 minutos.
      
      Se você não solicitou este código, ignore este email.
    `
  };

  try {
    const info = await transporter.sendMail(mailOptions);
    console.log('✅ Email de verificação enviado:', info.messageId);
    return { success: true, messageId: info.messageId };
  } catch (error) {
    console.error('❌ Erro ao enviar email:', error);
    throw error;
  }
};

/**
 * Gera código de verificação aleatório (6 dígitos)
 */
export const generateVerificationCode = () => {
  return Math.floor(100000 + Math.random() * 900000).toString();
};

/**
 * Envia email de boas-vindas após cadastro
 */
export const sendWelcomeEmail = async (email, name = '') => {
  if (!transporter) {
    transporter = createTransporter();
  }
  
  if (!transporter) {
    throw new Error('Serviço de email não configurado');
  }

  const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
  const appName = 'CurriculoPro IA'; // Sempre usa CurriculoPro IA para emails do serviço
  const emailSender = process.env.EMAIL_USER || process.env.EMAIL_SENDER;
  const emailCopy = process.env.EMAIL_COPY || process.env.EMAIL_COPY_TO;

  const mailOptions = {
    from: `"${appName}" <${emailSender}>`,
    to: email,
    cc: emailCopy ? [emailCopy] : undefined,
    subject: `🎉 Bem-vindo ao ${appName}!`,
    html: `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <style>
          body {
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
          }
          .container {
            background: #f9f9f9;
            border-radius: 8px;
            padding: 30px;
            margin: 20px 0;
          }
          .header {
            text-align: center;
            margin-bottom: 30px;
          }
          .button {
            display: inline-block;
            padding: 12px 30px;
            background: #4CAF50;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            margin: 20px 0;
          }
        </style>
      </head>
      <body>
        <div class="container">
          <div class="header">
            <h1 style="color: #4CAF50; margin: 0;">${appName}</h1>
          </div>
          
          <p>Olá${name ? `, ${name}` : ''}!</p>
          
          <p>Bem-vindo ao ${appName}! Sua conta foi criada com sucesso.</p>
          
          <p>Estamos felizes em tê-lo conosco. Agora você pode aproveitar todos os recursos da nossa plataforma.</p>
          
          <p style="text-align: center;">
            <a href="${frontendUrl}" class="button">Acessar Plataforma</a>
          </p>
          
          <p>Se você tiver alguma dúvida, nossa equipe está pronta para ajudar!</p>
          
          <div style="margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; text-align: center;">
            <p>Este é um email automático, por favor não responda.</p>
            <p>&copy; ${new Date().getFullYear()} GetPush Tecnologia. Todos os direitos reservados.</p>
          </div>
        </div>
      </body>
      </html>
    `,
    text: `
      Olá${name ? `, ${name}` : ''}!
      
      Bem-vindo ao ${appName}! Sua conta foi criada com sucesso.
      
      Estamos felizes em tê-lo conosco. Agora você pode aproveitar todos os recursos da nossa plataforma.
      
      Acesse: ${frontendUrl}
      
      Se você tiver alguma dúvida, nossa equipe está pronta para ajudar!
    `
  };

  try {
    const info = await transporter.sendMail(mailOptions);
    console.log('✅ Email de boas-vindas enviado:', info.messageId);
    return { success: true, messageId: info.messageId };
  } catch (error) {
    console.error('❌ Erro ao enviar email de boas-vindas:', error);
    throw error;
  }
};

/**
 * Envia email de notificação de login
 */
export const sendLoginNotificationEmail = async (email, name = '') => {
  if (!transporter) {
    transporter = createTransporter();
  }
  
  if (!transporter) {
    throw new Error('Serviço de email não configurado');
  }

  const appName = 'CurriculoPro IA'; // Sempre usa CurriculoPro IA para emails do serviço
  const emailSender = process.env.EMAIL_USER || process.env.EMAIL_SENDER;
  const emailCopy = process.env.EMAIL_COPY || process.env.EMAIL_COPY_TO;
  const now = new Date().toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' });

  const mailOptions = {
    from: `"${appName}" <${emailSender}>`,
    to: email,
    cc: emailCopy ? [emailCopy] : undefined,
    subject: `🔐 Login Realizado - ${appName}`,
    html: `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <style>
          body {
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
          }
          .container {
            background: #f9f9f9;
            border-radius: 8px;
            padding: 30px;
            margin: 20px 0;
          }
          .alert {
            background: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px;
            margin: 20px 0;
          }
        </style>
      </head>
      <body>
        <div class="container">
          <h2 style="color: #4CAF50;">Login Realizado</h2>
          
          <p>Olá${name ? `, ${name}` : ''}!</p>
          
          <p>Identificamos um novo login na sua conta do ${appName}.</p>
          
          <div class="alert">
            <strong>Detalhes do acesso:</strong><br>
            Data e hora: ${now}
          </div>
          
          <p><strong>Não foi você?</strong></p>
          <p>Se você não realizou este login, altere sua senha imediatamente e entre em contato conosco.</p>
          
          <p>Se foi você, pode ignorar este email.</p>
          
          <div style="margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; text-align: center;">
            <p>Este é um email automático de segurança.</p>
            <p>&copy; ${new Date().getFullYear()} GetPush Tecnologia. Todos os direitos reservados.</p>
          </div>
        </div>
      </body>
      </html>
    `,
    text: `
      Olá${name ? `, ${name}` : ''}!
      
      Identificamos um novo login na sua conta do ${appName}.
      
      Detalhes do acesso:
      Data e hora: ${now}
      
      Não foi você? Se você não realizou este login, altere sua senha imediatamente.
      
      Se foi você, pode ignorar este email.
    `
  };

  try {
    const info = await transporter.sendMail(mailOptions);
    console.log('✅ Email de notificação de login enviado:', info.messageId);
    return { success: true, messageId: info.messageId };
  } catch (error) {
    console.error('❌ Erro ao enviar email de notificação de login:', error);
    throw error;
  }
};

/**
 * Envia email com link de verificação
 */
export const sendVerificationLinkEmail = async (email, token, name = '') => {
  if (!transporter) {
    transporter = createTransporter();
  }
  
  if (!transporter) {
    throw new Error('Serviço de email não configurado');
  }

  const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
  const appName = 'CurriculoPro IA'; // Sempre usa CurriculoPro IA para emails do serviço
  const emailSender = process.env.EMAIL_USER || process.env.EMAIL_SENDER;
  const emailCopy = process.env.EMAIL_COPY || process.env.EMAIL_COPY_TO;
  const verificationLink = `${frontendUrl}/verify-email?token=${token}&email=${encodeURIComponent(email)}`;

  const mailOptions = {
    from: `"${appName}" <${emailSender}>`,
    to: email,
    cc: emailCopy ? [emailCopy] : undefined,
    subject: `🔗 Verifique seu email - ${appName}`,
    html: `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <style>
          body {
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
          }
          .container {
            background: #f9f9f9;
            border-radius: 8px;
            padding: 30px;
            margin: 20px 0;
          }
          .button {
            display: inline-block;
            padding: 12px 30px;
            background: #4CAF50;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            margin: 20px 0;
          }
        </style>
      </head>
      <body>
        <div class="container">
          <h2 style="color: #4CAF50;">Verifique seu email</h2>
          
          <p>Olá${name ? `, ${name}` : ''}!</p>
          
          <p>Você já possui uma conta no ${appName}, mas seu email ainda não foi verificado.</p>
          
          <p>Clique no botão abaixo para verificar seu email e ativar sua conta:</p>
          
          <p style="text-align: center;">
            <a href="${verificationLink}" class="button">Verificar Email</a>
          </p>
          
          <p>Ou copie e cole este link no seu navegador:</p>
          <p style="word-break: break-all; color: #666; font-size: 12px;">${verificationLink}</p>
          
          <p><strong>Este link expira em 24 horas.</strong></p>
          
          <p>Se você não solicitou este email, ignore esta mensagem.</p>
          
          <div style="margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; text-align: center;">
            <p>Este é um email automático, por favor não responda.</p>
            <p>&copy; ${new Date().getFullYear()} GetPush Tecnologia. Todos os direitos reservados.</p>
          </div>
        </div>
      </body>
      </html>
    `,
    text: `
      Olá${name ? `, ${name}` : ''}!
      
      Você já possui uma conta no ${appName}, mas seu email ainda não foi verificado.
      
      Clique no link abaixo para verificar seu email:
      ${verificationLink}
      
      Este link expira em 24 horas.
      
      Se você não solicitou este email, ignore esta mensagem.
    `
  };

  try {
    const info = await transporter.sendMail(mailOptions);
    console.log('✅ Email com link de verificação enviado:', info.messageId);
    return { success: true, messageId: info.messageId };
  } catch (error) {
    console.error('❌ Erro ao enviar email com link de verificação:', error);
    throw error;
  }
};

/**
 * Envia email de notificação de mudança de senha
 */
export const sendPasswordChangeNotificationEmail = async (email, name = '') => {
  if (!transporter) {
    transporter = createTransporter();
  }
  
  if (!transporter) {
    throw new Error('Serviço de email não configurado');
  }

  const appName = 'CurriculosPro IA';
  const emailSender = process.env.EMAIL_USER || process.env.EMAIL_SENDER;
  const emailCopy = process.env.EMAIL_COPY || process.env.EMAIL_COPY_TO;
  const now = new Date().toLocaleString('pt-BR', { 
    timeZone: 'America/Sao_Paulo',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });

  const mailOptions = {
    from: `"${appName}" <${emailSender}>`,
    to: email,
    cc: emailCopy ? [emailCopy] : undefined,
    subject: `🔐 Senha alterada - ${appName}`,
    html: `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <style>
          body {
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
          }
          .container {
            background: #f9f9f9;
            border-radius: 8px;
            padding: 30px;
            margin: 20px 0;
          }
          .alert {
            background: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
          }
          .info-box {
            background: #e7f3ff;
            border-left: 4px solid #2196F3;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
          }
        </style>
      </head>
      <body>
        <div class="container">
          <h2 style="color: #4CAF50;">✅ Senha Alterada com Sucesso</h2>
          
          <p>Olá${name ? `, ${name}` : ''}!</p>
          
          <p>Sua senha foi alterada com sucesso na sua conta do <strong>${appName}</strong>.</p>
          
          <div class="info-box">
            <p><strong>Detalhes da alteração:</strong></p>
            <p>Data e hora: <strong>${now}</strong></p>
          </div>
          
          <div class="alert">
            <p><strong>⚠️ Importante:</strong></p>
            <p>Se você não realizou esta alteração, entre em contato conosco imediatamente através do nosso suporte.</p>
          </div>
          
          <p>Para sua segurança, recomendamos:</p>
          <ul>
            <li>Usar uma senha forte e única</li>
            <li>Não compartilhar sua senha com ninguém</li>
            <li>Alterar sua senha periodicamente</li>
          </ul>
          
          <p>Se foi você quem alterou a senha, pode ignorar este email.</p>
          
          
          <div style="margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; text-align: center;">
            <p>Este é um email automático, por favor não responda.</p>
            <p>&copy; ${new Date().getFullYear()} CurriculosPro IA. Todos os direitos reservados.</p>
          </div>
        </div>
      </body>
      </html>
    `,
    text: `
      Olá${name ? `, ${name}` : ''}!
      
      Sua senha foi alterada com sucesso na sua conta do ${appName}.
      
      Detalhes da alteração:
      Data e hora: ${now}
      
      ⚠️ IMPORTANTE: Se você não realizou esta alteração, entre em contato conosco imediatamente.
      
      Para sua segurança, recomendamos usar uma senha forte e única, não compartilhar sua senha com ninguém e alterar sua senha periodicamente.
      
      Se foi você quem alterou a senha, pode ignorar este email.
    `
  };

  try {
    const info = await transporter.sendMail(mailOptions);
    console.log('✅ Email de notificação de mudança de senha enviado:', info.messageId);
    return { success: true, messageId: info.messageId };
  } catch (error) {
    console.error('❌ Erro ao enviar email de notificação de mudança de senha:', error);
    throw error;
  }
};

/**
 * Envia email com link de recuperação de senha
 */
export const sendPasswordResetEmail = async (email, token, name = '') => {
  if (!transporter) {
    transporter = createTransporter();
  }
  
  if (!transporter) {
    throw new Error('Serviço de email não configurado');
  }

  const appName = 'CurriculosPro IA';
  const emailSender = process.env.EMAIL_USER || process.env.EMAIL_SENDER;
  const emailCopy = process.env.EMAIL_COPY || process.env.EMAIL_COPY_TO;
  const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';
  const resetLink = `${frontendUrl}/login?token=${token}`;

  const mailOptions = {
    from: `"${appName}" <${emailSender}>`,
    to: email,
    cc: emailCopy ? [emailCopy] : undefined,
    subject: `🔐 Recuperação de Senha - ${appName}`,
    html: `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <style>
          body {
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
          }
          .container {
            background: #f9f9f9;
            border-radius: 8px;
            padding: 30px;
            margin: 20px 0;
          }
          .button {
            display: inline-block;
            padding: 12px 30px;
            background: linear-gradient(to right, #6366f1, #8b5cf6);
            color: white;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            margin: 20px 0;
          }
          .alert {
            background: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
          }
        </style>
      </head>
      <body>
        <div class="container">
          <h2 style="color: #6366f1;">Recuperação de Senha</h2>
          
          <p>Olá${name ? `, ${name}` : ''}!</p>
          
          <p>Recebemos uma solicitação para redefinir a senha da sua conta no <strong>${appName}</strong>.</p>
          
          <p>Clique no botão abaixo para redefinir sua senha:</p>
          
          <div style="text-align: center;">
            <a href="${resetLink}" class="button">Redefinir Senha</a>
          </div>
          
          <p>Ou copie e cole este link no seu navegador:</p>
          <p style="word-break: break-all; color: #6366f1;">${resetLink}</p>
          
          <div class="alert">
            <p><strong>⚠️ Importante:</strong></p>
            <ul>
              <li>Este link expira em 1 hora</li>
              <li>Se você não solicitou esta recuperação, ignore este email</li>
              <li>Não compartilhe este link com ninguém</li>
            </ul>
          </div>
          
          <p>Se você não solicitou esta recuperação, pode ignorar este email. Sua senha permanecerá a mesma.</p>
          
          <div style="margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; text-align: center;">
            <p>Este é um email automático, por favor não responda.</p>
            <p>&copy; ${new Date().getFullYear()} CurriculosPro IA. Todos os direitos reservados.</p>
          </div>
        </div>
      </body>
      </html>
    `,
    text: `
      Olá${name ? `, ${name}` : ''}!
      
      Recebemos uma solicitação para redefinir a senha da sua conta no ${appName}.
      
      Clique no link abaixo para redefinir sua senha:
      ${resetLink}
      
      ⚠️ IMPORTANTE:
      - Este link expira em 1 hora
      - Se você não solicitou esta recuperação, ignore este email
      - Não compartilhe este link com ninguém
      
      Se você não solicitou esta recuperação, pode ignorar este email. Sua senha permanecerá a mesma.
    `
  };

  try {
    const info = await transporter.sendMail(mailOptions);
    console.log('✅ Email de recuperação de senha enviado:', info.messageId);
    return { success: true, messageId: info.messageId };
  } catch (error) {
    console.error('❌ Erro ao enviar email de recuperação de senha:', error);
    throw error;
  }
};
