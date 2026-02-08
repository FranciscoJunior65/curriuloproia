import { generateInterviewQuestions, evaluateAnswer } from '../services/interview-simulation.service.js';
import { supabaseAdmin } from '../services/supabase.service.js';
import { saveInterviewMessage, updateSimulationAnswers, getInterviewById, getUserInterviews } from '../services/interview-db.service.js';

/**
 * Inicia uma nova simulação de entrevista
 */
export const startInterview = async (req, res) => {
  try {
    const { resumeText, analysis, siteId, resumeId } = req.body;

    // Obtém userId do token
    let userId = null;
    const token = req.headers.authorization?.replace('Bearer ', '');
    if (token) {
      try {
        const jwt = await import('jsonwebtoken');
        const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
        userId = decoded.userId;
      } catch (err) {
        // Token inválido
      }
    }

    if (!resumeText || !analysis) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer resumeText e analysis'
      });
    }

    console.log('🎤 Iniciando simulação de entrevista...');

    // Gera perguntas
    const questions = await generateInterviewQuestions(resumeText, analysis, siteId || null);

    // Cria simulação no banco se userId e resumeId foram fornecidos
    let simulationId = null;
    console.log('🔍 Verificando condições para criar simulação:', { 
      userId: userId ? 'presente' : 'ausente',
      resumeId: resumeId ? 'presente' : 'ausente',
      siteId: siteId ? 'presente' : 'ausente'
    });
    
    // Tenta criar simulação no banco
    // NOTA: A tabela requer id_curriculo, id_usuario e id_site_vagas como NOT NULL
    // Então só cria se tiver todos os dados necessários
    if (userId && resumeId && siteId) {
      const insertData = {
        id_curriculo: resumeId,
        id_usuario: userId,
        id_site_vagas: siteId,
        titulo: 'Simulação de Entrevista',
        area_foco: analysis.areaAtuacao || 'Geral',
        perguntas_feitas: questions,
        respostas_dadas: []
      };
      try {
        const { data, error } = await supabaseAdmin
          .from('simulacoes_entrevista')
          .insert(insertData)
          .select()
          .single();

        if (!error && data) {
          simulationId = data.id;
          console.log(`✅ Simulação criada no banco: ${simulationId}`);
        } else {
          console.error('❌ Erro ao criar simulação:', error);
          if (error?.code === '23503') {
            console.error('❌ Erro de foreign key - verifique se resumeId, userId ou siteId são válidos');
          }
        }
      } catch (dbError) {
        console.error('❌ Erro ao salvar simulação no banco:', dbError);
        // Continua mesmo se não conseguir salvar
      }
    } else {
      console.warn('⚠️ Não foi possível criar simulação - faltam userId e resumeId');
      console.warn('⚠️ A entrevista funcionará, mas não será salva no banco de dados');
    }

    res.json({
      success: true,
      simulationId,
      questions,
      message: `${questions.length} perguntas geradas`
    });

  } catch (error) {
    console.error('❌ Erro ao iniciar entrevista:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao iniciar entrevista',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};

/**
 * Avalia uma resposta do candidato
 */
export const evaluateInterviewAnswer = async (req, res) => {
  try {
    const { question, answer, resumeText, analysis, simulationId } = req.body;

    if (!question || !answer || !resumeText || !analysis) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer question, answer, resumeText e analysis'
      });
    }

    console.log('📝 Avaliando resposta...');

    // Avalia a resposta
    const evaluation = await evaluateAnswer(question, answer, resumeText, analysis);

    // Salva a mensagem no banco se simulationId foi fornecido
    console.log('🔍 Tentando salvar mensagem:', { simulationId, hasQuestion: !!question, hasAnswer: !!answer });
    
    if (simulationId) {
      try {
        // Busca quantas respostas já foram salvas para determinar a ordem
        const { data: existingMessages, error: selectError } = await supabaseAdmin
          .from('mensagens_entrevista')
          .select('ordem')
          .eq('id_simulacao', simulationId)
          .eq('tipo', 'pergunta')
          .order('ordem', { ascending: false })
          .limit(1);

        if (selectError) {
          console.error('❌ Erro ao buscar mensagens existentes:', selectError);
        }

        const questionOrder = existingMessages && existingMessages.length > 0 
          ? Math.floor(existingMessages[0].ordem / 3) + 1 
          : 1;

        // Salva pergunta, resposta e feedback usando o serviço
        const saved = await saveInterviewMessage(simulationId, question, answer, evaluation, questionOrder);
        if (saved) {
          console.log(`✅ Mensagens salvas no banco (pergunta ${questionOrder})`);
        } else {
          console.warn('⚠️ Algumas mensagens não foram salvas');
        }
      } catch (dbError) {
        console.error('❌ Erro ao salvar mensagens no banco:', dbError);
        // Continua mesmo se não conseguir salvar
      }
    } else {
      console.warn('⚠️ simulationId não fornecido - mensagens não serão salvas no banco');
    }

    res.json({
      success: true,
      evaluation
    });

  } catch (error) {
    console.error('❌ Erro ao avaliar resposta:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao avaliar resposta',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};

/**
 * Finaliza a simulação e gera feedback geral
 */
export const finishInterview = async (req, res) => {
  try {
    const { simulationId, allAnswers } = req.body;

    if (!simulationId) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer simulationId'
      });
    }

    console.log('🏁 Finalizando simulação...');

    // Atualiza simulação no banco usando o serviço
    let averageScore = 70;
    try {
      const result = await updateSimulationAnswers(simulationId, allAnswers);
      averageScore = result.averageScore;
      console.log(`✅ Simulação finalizada: ${simulationId} (Score: ${averageScore})`);
    } catch (dbError) {
      console.warn('⚠️ Erro ao finalizar simulação no banco:', dbError);
      // Calcula score localmente se falhar
      const scores = allAnswers.map(a => a.evaluation?.score || 70);
      averageScore = Math.round(scores.reduce((a, b) => a + b, 0) / scores.length);
    }

    res.json({
      success: true,
      score: averageScore,
      simulationId,
      message: 'Simulação finalizada com sucesso'
    });

  } catch (error) {
    console.error('❌ Erro ao finalizar simulação:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao finalizar simulação',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};

/**
 * Busca uma entrevista salva por ID
 */
export const getInterview = async (req, res) => {
  try {
    const { simulationId } = req.params;

    if (!simulationId) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer simulationId'
      });
    }

    // Obtém userId do token para verificar permissão
    let userId = null;
    const token = req.headers.authorization?.replace('Bearer ', '');
    if (token) {
      try {
        const jwt = await import('jsonwebtoken');
        const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
        userId = decoded.userId;
      } catch (err) {
        // Token inválido
      }
    }

    const interview = await getInterviewById(simulationId);

    // Verifica se o usuário tem permissão (se autenticado)
    if (userId && interview.id_usuario !== userId) {
      return res.status(403).json({
        success: false,
        error: 'Acesso negado',
        message: 'Você não tem permissão para acessar esta entrevista'
      });
    }

    res.json({
      success: true,
      interview
    });

  } catch (error) {
    console.error('❌ Erro ao buscar entrevista:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao buscar entrevista',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};

/**
 * Lista todas as entrevistas do usuário
 */
export const listUserInterviews = async (req, res) => {
  try {
    // Obtém userId do token
    let userId = null;
    const token = req.headers.authorization?.replace('Bearer ', '');
    if (token) {
      try {
        const jwt = await import('jsonwebtoken');
        const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
        userId = decoded.userId;
      } catch (err) {
        return res.status(401).json({
          success: false,
          error: 'Não autenticado',
          message: 'Token inválido ou expirado'
        });
      }
    } else {
      return res.status(401).json({
        success: false,
        error: 'Não autenticado',
        message: 'É necessário estar autenticado'
      });
    }

    const interviews = await getUserInterviews(userId);

    res.json({
      success: true,
      interviews: interviews || []
    });

  } catch (error) {
    console.error('❌ Erro ao listar entrevistas:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao listar entrevistas',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};

/**
 * Gera arquivo de download da entrevista
 */
export const downloadInterview = async (req, res) => {
  try {
    const { simulationId } = req.params;

    if (!simulationId) {
      return res.status(400).json({
        success: false,
        error: 'Dados incompletos',
        message: 'É necessário fornecer simulationId'
      });
    }

    // Obtém userId do token para verificar permissão
    let userId = null;
    const token = req.headers.authorization?.replace('Bearer ', '');
    if (token) {
      try {
        const jwt = await import('jsonwebtoken');
        const decoded = jwt.default.verify(token, process.env.JWT_SECRET || 'seu_secret_key_super_seguro_aqui_mude_em_producao');
        userId = decoded.userId;
      } catch (err) {
        // Token inválido
      }
    }

    const interview = await getInterviewById(simulationId);

    // Verifica permissão
    if (userId && interview.id_usuario !== userId) {
      return res.status(403).json({
        success: false,
        error: 'Acesso negado',
        message: 'Você não tem permissão para acessar esta entrevista'
      });
    }

    // Gera conteúdo do arquivo
    let content = `========================================\n`;
    content += `SIMULAÇÃO DE ENTREVISTA - RELATÓRIO COMPLETO\n`;
    content += `========================================\n\n`;
    content += `ID da Simulação: ${interview.id}\n`;
    content += `Data: ${new Date(interview.criado_em).toLocaleString('pt-BR')}\n`;
    content += `Área de Foco: ${interview.area_foco || 'Geral'}\n`;
    content += `Total de Perguntas: ${interview.perguntas_feitas?.length || 0}\n`;
    content += `Score Médio: ${interview.score_geral || 0}/100\n\n`;

    if (interview.feedback_geral?.statistics) {
      const stats = interview.feedback_geral.statistics;
      content += `Estatísticas:\n`;
      content += `- Respostas Boas (≥70): ${stats.goodAnswers || 0}\n`;
      content += `- Respostas Médias (50-69): ${stats.averageAnswers || 0}\n`;
      content += `- Precisam Melhorar (<50): ${stats.poorAnswers || 0}\n`;
      content += `- Score Mínimo: ${stats.minScore || 0}\n`;
      content += `- Score Máximo: ${stats.maxScore || 0}\n\n`;
    }

    content += `========================================\n`;
    content += `PERGUNTAS E RESPOSTAS\n`;
    content += `========================================\n\n`;

    // Organiza mensagens por pergunta
    const questions = interview.messages.filter(m => m.tipo === 'pergunta');
    
    questions.forEach((questionMsg, index) => {
      const questionOrder = questionMsg.dados_extras?.questionIndex ?? index;
      const answerMsg = interview.messages.find(m => 
        m.tipo === 'resposta' && 
        m.dados_extras?.questionIndex === questionOrder
      );
      const feedbackMsg = interview.messages.find(m => 
        m.tipo === 'feedback' && 
        m.dados_extras?.questionIndex === questionOrder
      );

      content += `PERGUNTA ${index + 1}:\n`;
      content += `${questionMsg.conteudo}\n\n`;
      
      if (answerMsg) {
        content += `RESPOSTA:\n`;
        content += `${answerMsg.conteudo}\n\n`;
      }

      if (feedbackMsg) {
        try {
          const evaluation = JSON.parse(feedbackMsg.conteudo);
          content += `AVALIAÇÃO:\n`;
          content += `Score: ${evaluation.score || feedbackMsg.dados_extras?.score || 0}/100\n`;
          content += `Feedback: ${evaluation.feedback || feedbackMsg.feedback || ''}\n`;
          
          if (evaluation.strengths && evaluation.strengths.length > 0) {
            content += `Pontos Fortes:\n`;
            evaluation.strengths.forEach(strength => {
              content += `- ${strength}\n`;
            });
          }
          
          if (evaluation.improvements && evaluation.improvements.length > 0) {
            content += `Pontos a Melhorar:\n`;
            evaluation.improvements.forEach(improvement => {
              content += `- ${improvement}\n`;
            });
          }
        } catch (parseError) {
          content += `Feedback: ${feedbackMsg.feedback || ''}\n`;
        }
      }
      
      content += `\n${'='.repeat(40)}\n\n`;
    });

    // Define headers para download
    res.setHeader('Content-Type', 'text/plain; charset=utf-8');
    res.setHeader('Content-Disposition', `attachment; filename="entrevista_${interview.id}_${new Date().toISOString().split('T')[0]}.txt"`);
    
    res.send(content);

  } catch (error) {
    console.error('❌ Erro ao gerar download da entrevista:', error);
    res.status(500).json({
      success: false,
      error: 'Erro ao gerar download',
      message: error.message || 'Ocorreu um erro inesperado'
    });
  }
};
