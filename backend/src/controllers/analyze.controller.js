import { analyzeResumeWithAI } from '../services/ai.service.js';
import { extractTextFromFile } from '../services/file.service.js';
import { getUser } from '../models/user.model.js';
import { deductCreditsFromUser, recordCreditUsage } from '../services/supabase.service.js';

export const analyzeResume = async (req, res) => {
  const startTime = Date.now();
  
  try {
    // Obtém userId do token JWT ou do body
    let userId = req.body.userId || req.query.userId;
    
    // Se não tiver no body, tenta pegar do token JWT
    if (!userId) {
      const token = req.headers.authorization?.replace('Bearer ', '');
      if (token) {
        try {
          const jwt = await import('jsonwebtoken');
          const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
          userId = decoded.userId;
        } catch (err) {
          // Token inválido, continua sem userId
        }
      }
    }

    // Verificação de créditos (obrigatório se autenticado)
    if (userId) {
      const user = await getUser(userId);
      if (!user) {
        return res.status(404).json({
          success: false,
          error: 'Usuário não encontrado',
          message: 'Usuário não encontrado no sistema.'
        });
      }
      
      console.log(`🔍 Verificando créditos para usuário ${userId}. Créditos disponíveis: ${user.credits || 0}`);
      
      // Verifica créditos disponíveis (agora é async)
      const hasCredits = await user.hasCredits(1);
      console.log(`💳 Usuário tem créditos suficientes? ${hasCredits}`);
      
      if (!hasCredits) {
        console.log(`❌ Créditos insuficientes. Disponível: ${user.credits || 0}, Necessário: 1`);
        return res.status(402).json({
          success: false,
          error: 'Créditos insuficientes',
          message: 'Você não possui créditos suficientes. Por favor, adquira um plano.',
          requiresPayment: true,
          creditsAvailable: user.credits || 0
        });
      }
      
      console.log(`✅ Créditos verificados. Prosseguindo com análise...`);
    } else {
      // Se não estiver autenticado, requer login
      return res.status(401).json({
        success: false,
        error: 'Não autenticado',
        message: 'É necessário estar autenticado para analisar currículos.',
        requiresAuth: true
      });
    }

    // Validação de arquivo
    if (!req.file) {
      return res.status(400).json({ 
        success: false,
        error: 'Nenhum arquivo enviado',
        message: 'Por favor, envie um arquivo de currículo (PDF, DOC, DOCX ou TXT)'
      });
    }

    // Validação de tamanho do arquivo (já limitado pelo multer, mas validamos aqui também)
    const maxSize = 10 * 1024 * 1024; // 10MB
    if (req.file.size > maxSize) {
      return res.status(400).json({
        success: false,
        error: 'Arquivo muito grande',
        message: `O arquivo excede o tamanho máximo de ${maxSize / 1024 / 1024}MB`
      });
    }

    console.log(`📄 Processando arquivo: ${req.file.originalname} (${req.file.size} bytes)`);

    // Extrair texto do arquivo
    let text;
    try {
      text = await extractTextFromFile(req.file);
    } catch (extractError) {
      console.error('Erro ao extrair texto:', extractError);
      return res.status(400).json({
        success: false,
        error: 'Erro ao extrair texto',
        message: extractError.message || 'Não foi possível extrair texto do arquivo. Verifique se o arquivo não está corrompido.'
      });
    }
    
    if (!text || text.trim().length === 0) {
      return res.status(400).json({
        success: false,
        error: 'Texto vazio',
        message: 'Não foi possível extrair texto do arquivo. O arquivo pode estar vazio ou corrompido.'
      });
    }

    console.log(`✅ Texto extraído: ${text.length} caracteres`);

    // Analisar com IA
    console.log('🤖 Iniciando análise com IA...');
    const analysis = await analyzeResumeWithAI(text);

    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.log(`✨ Análise concluída em ${processingTime}s`);

    // Deduz crédito após análise bem-sucedida (só se não estiver em modo mock)
    let creditsRemaining = null;
    if (userId) {
      const user = await getUser(userId);
      if (user) {
        // Verifica se está em modo mock
        const useMock = process.env.USE_MOCK_AI === 'true' || process.env.USE_MOCK_AI === '1';
        
        if (!useMock) {
          // Modo real: deduz crédito
          await deductCreditsFromUser(userId, 1);
          // Registra o uso de crédito
          await recordCreditUsage(userId, 'analysis', 1, req.file.originalname);
          console.log(`💳 Crédito usado.`);
        } else {
          // Modo mock: não deduz crédito
          console.log('🎭 Modo MOCK: crédito NÃO foi deduzido');
        }
        
        const updatedUser = await getUser(userId);
        creditsRemaining = updatedUser?.credits || 0;
        console.log(`💳 Créditos restantes: ${creditsRemaining}`);
      }
    }

    res.json({
      success: true,
      originalText: text,
      analysis: analysis,
      metadata: {
        fileName: req.file.originalname,
        fileSize: req.file.size,
        textLength: text.length,
        processingTime: `${processingTime}s`
      },
      creditsRemaining
    });
  } catch (error) {
    const processingTime = ((Date.now() - startTime) / 1000).toFixed(2);
    console.error(`❌ Erro ao analisar currículo (${processingTime}s):`, error);
    
    // Determina o status code apropriado
    let statusCode = 500;
    if (error.message.includes('API inválida') || error.message.includes('OPENAI_API_KEY')) {
      statusCode = 500; // Erro de configuração
    } else if (error.message.includes('limite') || error.message.includes('429')) {
      statusCode = 429; // Too Many Requests
    }

    res.status(statusCode).json({ 
      success: false,
      error: 'Erro ao processar currículo',
      message: error.message || 'Ocorreu um erro inesperado ao analisar o currículo'
    });
  }
};


