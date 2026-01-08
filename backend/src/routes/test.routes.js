import express from 'express';
import { supabaseAdmin } from '../services/supabase.service.js';
import { sendVerificationEmail, generateVerificationCode } from '../services/email.service.js';

const router = express.Router();

/**
 * @swagger
 * /api/test/supabase:
 *   get:
 *     summary: Testa conexão com Supabase
 *     tags: [Test]
 *     responses:
 *       200:
 *         description: Conexão OK
 *       500:
 *         description: Erro na conexão
 */
router.get('/supabase', async (req, res) => {
  try {
    // Debug: mostra as variáveis (sem expor valores completos)
    const supabaseUrl = process.env.SUPABASE_URL;
    const supabaseServiceKey = process.env.SUPABASE_SERVICE_ROLE_KEY;
    const supabaseAnonKey = process.env.SUPABASE_ANON_KEY;
    
    const debugInfo = {
      hasUrl: !!supabaseUrl,
      hasServiceKey: !!supabaseServiceKey,
      hasAnonKey: !!supabaseAnonKey,
      urlPreview: supabaseUrl ? `${supabaseUrl.substring(0, 30)}...` : 'não definido',
      serviceKeyPreview: supabaseServiceKey ? `${supabaseServiceKey.substring(0, 20)}...` : 'não definido',
      serviceKeyLength: supabaseServiceKey ? supabaseServiceKey.length : 0,
      serviceKeyStartsWith: supabaseServiceKey ? supabaseServiceKey.substring(0, 10) : 'não definido',
      allEnvKeys: Object.keys(process.env).filter(key => key.includes('SUPABASE'))
    };

    if (!supabaseAdmin) {
      return res.status(500).json({
        success: false,
        error: 'Supabase não configurado',
        message: 'Verifique as variáveis SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY no arquivo .env',
        debug: debugInfo,
        help: [
          '1. Certifique-se de que o arquivo .env está na raiz do diretório backend',
          '2. Verifique se as variáveis estão escritas corretamente (sem espaços, sem aspas extras)',
          '3. Reinicie o servidor após adicionar as variáveis (Ctrl+C e depois npm start)',
          '4. Formato esperado no .env:',
          '   SUPABASE_URL=https://vntoefwsmssbiefpvobp.supabase.co',
          '   SUPABASE_SERVICE_ROLE_KEY=sb_secret_...'
        ]
      });
    }

    // Teste simples: tenta fazer um SELECT na tabela perfis_usuarios
    const { data, error, count } = await supabaseAdmin
      .from('perfis_usuarios')
      .select('*', { count: 'exact' })
      .limit(5);

    if (error) {
      // Se a tabela não existe, isso é esperado
      if (error.code === '42P01' || error.message.includes('does not exist')) {
        return res.status(200).json({
          success: true,
          message: '✅ Conexão com Supabase OK!',
          warning: '⚠️ Tabela perfis_usuarios não encontrada. Execute o SQL de migração.',
          error: error.message,
          connection: 'OK'
        });
      }

      return res.status(500).json({
        success: false,
        error: 'Erro ao consultar Supabase',
        message: error.message,
        code: error.code,
        connection: 'OK (mas erro na query)'
      });
    }

    // Se chegou aqui, a conexão está OK e a tabela existe
    return res.status(200).json({
      success: true,
      message: '✅ Conexão com Supabase OK!',
      table: 'perfis_usuarios existe',
      totalRecords: count || 0,
      sampleRecords: data?.length || 0,
      connection: 'OK',
      data: data || [],
      debug: {
        urlConfigured: !!supabaseUrl,
        serviceKeyConfigured: !!supabaseServiceKey
      }
    });
  } catch (error) {
    console.error('Erro ao testar Supabase:', error);
    return res.status(500).json({
      success: false,
      error: 'Erro ao testar conexão',
      message: error.message,
      connection: 'ERRO'
    });
  }
});

/**
 * @swagger
 * /api/test/health:
 *   get:
 *     summary: Health check da API
 *     tags: [Test]
 *     responses:
 *       200:
 *         description: API funcionando
 */
router.get('/health', (req, res) => {
  res.json({
    success: true,
    message: 'API está funcionando!',
    timestamp: new Date().toISOString()
  });
});

/**
 * @swagger
 * /api/test/email-config:
 *   get:
 *     summary: Verifica configuração de email
 *     tags: [Test]
 *     responses:
 *       200:
 *         description: Status da configuração de email
 */
router.get('/email-config', (req, res) => {
  const config = {
    EMAIL_SERVICE: process.env.EMAIL_SERVICE || 'não definido',
    SMTP_HOST: process.env.SMTP_HOST || process.env.EMAIL_HOST || 'não definido',
    SMTP_PORT: process.env.SMTP_PORT || process.env.EMAIL_PORT || 'não definido',
    SMTP_SECURE: process.env.SMTP_SECURE || process.env.EMAIL_SECURE || 'não definido',
    EMAIL_SENDER: process.env.EMAIL_SENDER || process.env.EMAIL_USER || 'não definido',
    EMAIL_SENDER_PASSWORD: process.env.EMAIL_SENDER_PASSWORD ? '***configurado***' : 'não definido',
    EMAIL_PASSWORD: process.env.EMAIL_PASSWORD ? '***configurado***' : 'não definido',
    EMAIL_SENDER_NAME: process.env.EMAIL_SENDER_NAME || 'não definido',
    EMAIL_COPY_TO: process.env.EMAIL_COPY_TO || 'não definido',
    
    // Status
    emailConfigured: !!(process.env.EMAIL_SENDER || process.env.EMAIL_USER),
    passwordConfigured: !!(process.env.EMAIL_SENDER_PASSWORD || process.env.EMAIL_PASSWORD),
    hostConfigured: !!(process.env.SMTP_HOST || process.env.EMAIL_HOST),
    portConfigured: !!(process.env.SMTP_PORT || process.env.EMAIL_PORT),
    
    // Todas as variáveis de ambiente que contêm EMAIL ou SMTP
    allEmailEnvVars: Object.keys(process.env)
      .filter(key => key.includes('EMAIL') || key.includes('SMTP'))
      .reduce((acc, key) => {
        if (key.includes('PASSWORD')) {
          acc[key] = '***oculto***';
        } else {
          acc[key] = process.env[key];
        }
        return acc;
      }, {})
  };

  res.json({
    success: true,
    config: config,
    message: config.emailConfigured && config.passwordConfigured && config.hostConfigured && config.portConfigured
      ? '✅ Email configurado corretamente!'
      : '⚠️ Email não configurado completamente'
  });
});

/**
 * @swagger
 * /api/test/insert-user:
 *   post:
 *     summary: Insere um usuário de teste no Supabase
 *     tags: [Test]
 *     requestBody:
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             properties:
 *               email:
 *                 type: string
 *               name:
 *                 type: string
 *     responses:
 *       200:
 *         description: Usuário inserido com sucesso
 */
router.post('/insert-user', async (req, res) => {
  try {
    const { email = 'teste@teste.com', name = 'Usuário Teste' } = req.body;

    if (!supabaseAdmin) {
      return res.status(500).json({
        success: false,
        error: 'Supabase não configurado'
      });
    }

    // Primeiro, vamos verificar se a tabela existe
    const { data: checkTable, error: tableError } = await supabaseAdmin
      .from('perfis_usuarios')
      .select('count')
      .limit(1);

    if (tableError) {
      if (tableError.code === '42P01' || tableError.message.includes('does not exist')) {
        return res.status(400).json({
          success: false,
          error: 'Tabela perfis_usuarios não existe',
          message: 'Execute o SQL do arquivo SUPABASE_SETUP.md para criar a tabela',
          sqlError: tableError.message
        });
      }
      return res.status(500).json({
        success: false,
        error: 'Erro ao verificar tabela',
        message: tableError.message
      });
    }

    // Gera um UUID para o usuário (simulando um ID do auth.users)
    const userId = `test-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

    // Tenta inserir o usuário
    const { data, error } = await supabaseAdmin
      .from('perfis_usuarios')
      .insert({
        id: userId,
        email,
        name,
        credits: 0
      })
      .select()
      .single();

    if (error) {
      return res.status(500).json({
        success: false,
        error: 'Erro ao inserir usuário',
        message: error.message,
        code: error.code,
        details: error.details
      });
    }

    return res.status(200).json({
      success: true,
      message: '✅ Usuário inserido com sucesso!',
      data: data
    });
  } catch (error) {
    console.error('Erro ao inserir usuário:', error);
    return res.status(500).json({
      success: false,
      error: 'Erro ao inserir usuário',
      message: error.message
    });
  }
});

/**
 * @swagger
 * /api/test/list-users:
 *   get:
 *     summary: Lista todos os usuários do Supabase
 *     tags: [Test]
 *     responses:
 *       200:
 *         description: Lista de usuários
 */
router.get('/list-users', async (req, res) => {
  try {
    if (!supabaseAdmin) {
      return res.status(500).json({
        success: false,
        error: 'Supabase não configurado'
      });
    }

    const { data, error, count } = await supabaseAdmin
      .from('perfis_usuarios')
      .select('*', { count: 'exact' })
      .order('created_at', { ascending: false });

    if (error) {
      if (error.code === '42P01' || error.message.includes('does not exist')) {
        return res.status(400).json({
          success: false,
          error: 'Tabela perfis_usuarios não existe',
          message: 'Execute o SQL do arquivo SUPABASE_SETUP.md para criar a tabela'
        });
      }
      return res.status(500).json({
        success: false,
        error: 'Erro ao listar usuários',
        message: error.message
      });
    }

    return res.status(200).json({
      success: true,
      total: count || 0,
      users: data || []
    });
  } catch (error) {
    console.error('Erro ao listar usuários:', error);
    return res.status(500).json({
      success: false,
      error: 'Erro ao listar usuários',
      message: error.message
    });
  }
});

/**
 * @swagger
 * /api/test/send-email:
 *   post:
 *     summary: Testa envio de email
 *     tags: [Test]
 *     requestBody:
 *       content:
 *         application/json:
 *           schema:
 *             type: object
 *             properties:
 *               email:
 *                 type: string
 *                 example: teste@exemplo.com
 *               name:
 *                 type: string
 *                 example: Usuário Teste
 *     responses:
 *       200:
 *         description: Email enviado com sucesso
 *       500:
 *         description: Erro ao enviar email
 */
/**
 * @swagger
 * /api/test/check-purchases-table:
 *   get:
 *     summary: Verifica se a tabela purchases existe
 *     tags: [Test]
 *     responses:
 *       200:
 *         description: Status da tabela
 */
router.get('/check-purchases-table', async (req, res) => {
  try {
    if (!supabaseAdmin) {
      return res.status(500).json({
        success: false,
        error: 'Supabase não configurado'
      });
    }

    // Tenta fazer um SELECT na tabela purchases
    const { data, error, count } = await supabaseAdmin
      .from('compras')
      .select('*', { count: 'exact' })
      .limit(1);

    if (error) {
      if (error.code === '42P01' || error.message?.includes('does not exist')) {
        return res.status(200).json({
          success: false,
          tableExists: false,
          message: '⚠️ Tabela compras não existe',
          error: error.message,
          solution: 'Execute o script CREATE_PURCHASES_TABLE.sql no Supabase'
        });
      }
      return res.status(500).json({
        success: false,
        tableExists: false,
        error: 'Erro ao verificar tabela',
        message: error.message
      });
    }

    return res.status(200).json({
      success: true,
      tableExists: true,
      message: '✅ Tabela compras existe',
      totalPurchases: count || 0
    });
  } catch (error) {
    console.error('Erro ao verificar tabela compras:', error);
    return res.status(500).json({
      success: false,
      error: 'Erro ao verificar tabela',
      message: error.message
    });
  }
});

router.post('/send-email', async (req, res) => {
  try {
    const { email = 'teste@exemplo.com', name = 'Usuário Teste' } = req.body;

    // Gera código de teste
    const code = generateVerificationCode();

    // Tenta enviar o email
    const result = await sendVerificationEmail(email, code, name);

    return res.status(200).json({
      success: true,
      message: 'Email de teste enviado com sucesso!',
      code: code, // Retorna o código para facilitar o teste
      email: email,
      messageId: result.messageId
    });
  } catch (error) {
    console.error('Erro ao enviar email de teste:', error);
    
    // Informações de debug
    const emailUser = process.env.EMAIL_SENDER || process.env.EMAIL_USER;
    const emailPassword = process.env.EMAIL_SENDER_PASSWORD || process.env.EMAIL_PASSWORD;
    const emailHost = process.env.SMTP_HOST || process.env.EMAIL_HOST;
    const emailPort = process.env.SMTP_PORT || process.env.EMAIL_PORT;
    
    return res.status(500).json({
      success: false,
      error: 'Erro ao enviar email',
      message: error.message,
      errorCode: error.code,
      errorResponse: error.response,
      details: {
        emailConfigured: !!emailUser,
        passwordConfigured: !!emailPassword,
        passwordLength: emailPassword ? emailPassword.length : 0,
        hostConfigured: !!emailHost,
        portConfigured: !!emailPort,
        emailUser: emailUser ? `${emailUser.substring(0, 5)}...` : 'não definido',
        emailHost: emailHost || 'não definido',
        emailPort: emailPort || 'não definido',
        // Dicas baseadas no erro
        suggestions: error.code === 'EAUTH' ? [
          'Verifique se o email e senha estão corretos',
          'Alguns servidores SMTP precisam do email completo como usuário',
          'Tente usar porta 465 com SMTP_SECURE=true',
          'Verifique se a senha contém caracteres especiais que precisam estar entre aspas no .env'
        ] : []
      }
    });
  }
});

export default router;

