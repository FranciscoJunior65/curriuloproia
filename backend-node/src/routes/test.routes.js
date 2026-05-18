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
 * /api/test/env-check:
 *   get:
 *     summary: Verifica variáveis de ambiente (sem expor valores completos)
 *     tags: [Test]
 *     responses:
 *       200:
 *         description: Status das variáveis
 */
router.get('/env-check', (req, res) => {
  const envCheck = {
    supabase: {
      hasUrl: !!process.env.SUPABASE_URL,
      hasServiceKey: !!process.env.SUPABASE_SERVICE_ROLE_KEY,
      urlPreview: process.env.SUPABASE_URL ? `${process.env.SUPABASE_URL.substring(0, 30)}...` : 'não definido',
      serviceKeyLength: process.env.SUPABASE_SERVICE_ROLE_KEY ? process.env.SUPABASE_SERVICE_ROLE_KEY.length : 0,
      configured: !!(process.env.SUPABASE_URL && process.env.SUPABASE_SERVICE_ROLE_KEY)
    },
    gemini: {
      hasApiKey: !!process.env.GEMINI_API_KEY,
      apiKeyLength: process.env.GEMINI_API_KEY ? process.env.GEMINI_API_KEY.length : 0,
      model: process.env.GEMINI_MODEL || 'gemini-1.5-flash-preview (padrão)',
      provider: process.env.AI_PROVIDER || 'gemini (padrão)',
      configured: !!process.env.GEMINI_API_KEY
    },
    openai: {
      hasApiKey: !!process.env.OPENAI_API_KEY,
      model: process.env.OPENAI_MODEL || 'gpt-4 (padrão)',
      configured: !!process.env.OPENAI_API_KEY
    },
    jwt: {
      hasSecret: !!process.env.JWT_SECRET,
      configured: !!process.env.JWT_SECRET
    }
  };

  res.json({
    success: true,
    env: envCheck,
    issues: [
      !envCheck.supabase.configured && '⚠️ Supabase não configurado (SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY)',
      !envCheck.gemini.configured && '⚠️ Gemini não configurado (GEMINI_API_KEY)',
      !envCheck.jwt.configured && '⚠️ JWT Secret não configurado (JWT_SECRET)'
    ].filter(Boolean),
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

/**
 * @swagger
 * /api/test/gemini:
 *   get:
 *     summary: Testa conexão com Gemini
 *     tags: [Test]
 *     responses:
 *       200:
 *         description: Conexão OK
 *         content:
 *           application/json:
 *             schema:
 *               type: object
 *               properties:
 *                 success:
 *                   type: boolean
 *                   example: true
 *                 message:
 *                   type: string
 *                   example: "Conexão com Gemini OK!"
 *                 response:
 *                   type: string
 *                   example: "OK"
 *                 responseTime:
 *                   type: string
 *                   example: "500ms"
 *                 debug:
 *                   type: object
 *                 connection:
 *                   type: string
 *                   example: "OK"
 *                 timestamp:
 *                   type: string
 *       500:
 *         description: Erro na conexão
 *         content:
 *           application/json:
 *             schema:
 *               type: object
 *               properties:
 *                 success:
 *                   type: boolean
 *                   example: false
 *                 error:
 *                   type: string
 *                 message:
 *                   type: string
 *                 details:
 *                   type: string
 *                 connection:
 *                   type: string
 *                   example: "ERRO"
 */
router.get('/gemini', async (req, res) => {
  try {
    const { GoogleGenerativeAI } = await import('@google/generative-ai');
    
    // Verifica se a chave está configurada
    const geminiApiKey = process.env.GEMINI_API_KEY;
    const aiProvider = process.env.AI_PROVIDER || 'gemini';
    // Modelos válidos: gemini-3-flash-preview (mais recente), gemini-1.5-flash-preview, gemini-1.5-flash, gemini-1.5-pro
    let geminiModel = process.env.GEMINI_MODEL || 'gemini-3-flash-preview';
    
    // Se o modelo for gemini-pro (deprecated), força usar gemini-3-flash-preview
    if (geminiModel === 'gemini-pro') {
      console.warn(`⚠️  Modelo ${geminiModel} está deprecated. Usando gemini-3-flash-preview`);
      geminiModel = 'gemini-3-flash-preview';
    }
    
    const debugInfo = {
      hasApiKey: !!geminiApiKey,
      apiKeyPreview: geminiApiKey ? `${geminiApiKey.substring(0, 20)}...` : 'não definido',
      apiKeyLength: geminiApiKey ? geminiApiKey.length : 0,
      provider: aiProvider,
      model: geminiModel,
      modelFromEnv: process.env.GEMINI_MODEL || 'não definido (usando padrão)'
    };
    
    console.log(`🤖 Testando Gemini com modelo: ${geminiModel}`);

    if (!geminiApiKey) {
      return res.status(500).json({
        success: false,
        error: 'Gemini não configurado',
        message: 'GEMINI_API_KEY não encontrada no arquivo .env',
        debug: debugInfo,
        help: [
          '1. Obtenha uma API key em: https://ai.google.dev/',
          '2. Adicione no arquivo .env: GEMINI_API_KEY=sua-chave-aqui',
          '3. Reinicie o servidor após adicionar a variável'
        ]
      });
    }

    // Tenta inicializar o Gemini
    const genAI = new GoogleGenerativeAI(geminiApiKey);
    const model = genAI.getGenerativeModel({ model: geminiModel });

    // Faz uma requisição de teste simples
    const startTime = Date.now();
    const result = await model.generateContent({
      contents: [{ role: 'user', parts: [{ text: 'Responda apenas: OK' }] }],
      generationConfig: {
        temperature: 0.1,
        maxOutputTokens: 10,
      }
    });

    const response = await result.response;
    const responseText = response.text();
    const responseTime = Date.now() - startTime;

    res.json({
      success: true,
      message: 'Conexão com Gemini OK!',
      response: responseText,
      responseTime: `${responseTime}ms`,
      debug: debugInfo,
      connection: 'OK',
      timestamp: new Date().toISOString()
    });

  } catch (error) {
    console.error('Erro ao testar Gemini:', error);
    
    let errorMessage = error.message || 'Erro desconhecido';
    let errorDetails = null;

    // Tratamento de erros específicos
    if (error.message?.includes('API_KEY_INVALID') || error.message?.includes('401')) {
      errorMessage = 'Chave de API inválida';
      errorDetails = 'Verifique se a GEMINI_API_KEY está correta no arquivo .env';
    } else if (error.message?.includes('PERMISSION_DENIED')) {
      errorMessage = 'Permissão negada';
      errorDetails = 'A chave de API não tem permissão para usar o Gemini';
    } else if (error.message?.includes('QUOTA_EXCEEDED') || error.message?.includes('429')) {
      errorMessage = 'Quota excedida';
      errorDetails = 'Limite de requisições atingido. Aguarde alguns minutos.';
    } else if (error.message?.includes('404') || error.message?.includes('not found')) {
      errorMessage = 'Modelo não encontrado';
      errorDetails = `O modelo "${process.env.GEMINI_MODEL || 'gemini-pro'}" não está disponível. Use: gemini-1.5-flash ou gemini-1.5-pro`;
    }

    res.status(500).json({
      success: false,
      error: 'Erro ao conectar com Gemini',
      message: errorMessage,
      details: errorDetails,
      connection: 'ERRO',
      debug: {
        modelUsed: process.env.GEMINI_MODEL || 'gemini-3-flash-preview (padrão)',
        availableModels: ['gemini-3-flash-preview', 'gemini-1.5-flash-preview', 'gemini-1.5-flash', 'gemini-1.5-pro'],
        suggestion: 'Adicione no .env: GEMINI_MODEL=gemini-3-flash-preview'
      },
      timestamp: new Date().toISOString()
    });
  }
});

export default router;

