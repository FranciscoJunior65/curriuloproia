import { supabaseAdmin } from './supabase.service.js';

/**
 * Salva uma pergunta e resposta da entrevista no banco
 */
export const saveInterviewMessage = async (simulationId, question, answer, evaluation, order) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    // Salva pergunta
    const { error: questionError } = await supabaseAdmin
      .from('mensagens_entrevista')
      .insert({
        id_simulacao: simulationId,
        tipo: 'pergunta',
        conteudo: question,
        ordem: order * 3 - 2, // Ordem: 1, 4, 7, 10... (pergunta, resposta, feedback)
        dados_extras: {
          questionIndex: order - 1
        }
      });

    if (questionError) {
      console.error('Erro ao salvar pergunta:', questionError);
    }

    // Salva resposta
    const { error: answerError } = await supabaseAdmin
      .from('mensagens_entrevista')
      .insert({
        id_simulacao: simulationId,
        tipo: 'resposta',
        conteudo: answer,
        ordem: order * 3 - 1, // Ordem: 2, 5, 8, 11...
        dados_extras: {
          questionIndex: order - 1
        }
      });

    if (answerError) {
      console.error('Erro ao salvar resposta:', answerError);
    }

    // Salva feedback
    if (evaluation) {
      const { error: feedbackError } = await supabaseAdmin
        .from('mensagens_entrevista')
        .insert({
          id_simulacao: simulationId,
          tipo: 'feedback',
          conteudo: JSON.stringify(evaluation),
          feedback: evaluation.feedback || '',
          ordem: order * 3, // Ordem: 3, 6, 9, 12...
          dados_extras: {
            questionIndex: order - 1,
            score: evaluation.score,
            strengths: evaluation.strengths || [],
            improvements: evaluation.improvements || []
          }
        });

      if (feedbackError) {
        console.error('Erro ao salvar feedback:', feedbackError);
      }
    }

    return !questionError && !answerError;
  } catch (error) {
    console.error('Erro ao salvar mensagens da entrevista:', error);
    throw error;
  }
};

/**
 * Atualiza as respostas dadas na simulação
 */
export const updateSimulationAnswers = async (simulationId, allAnswers) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    // Calcula score médio
    const scores = allAnswers.map(a => a.evaluation?.score || 70);
    const averageScore = Math.round(scores.reduce((a, b) => a + b, 0) / scores.length);

    // Atualiza simulação
    const { error } = await supabaseAdmin
      .from('simulacoes_entrevista')
      .update({
        respostas_dadas: allAnswers,
        score_geral: averageScore,
        feedback_geral: {
          score: averageScore,
          totalPerguntas: allAnswers.length,
          respostas: allAnswers,
          statistics: {
            goodAnswers: scores.filter(s => s >= 70).length,
            averageAnswers: scores.filter(s => s >= 50 && s < 70).length,
            poorAnswers: scores.filter(s => s < 50).length,
            minScore: Math.min(...scores),
            maxScore: Math.max(...scores)
          }
        },
        atualizado_em: new Date().toISOString()
      })
      .eq('id', simulationId);

    if (error) {
      console.error('Erro ao atualizar simulação:', error);
      throw error;
    }

    return { averageScore };
  } catch (error) {
    console.error('Erro ao atualizar respostas da simulação:', error);
    throw error;
  }
};

/**
 * Busca uma simulação completa com todas as mensagens
 */
export const getInterviewById = async (simulationId) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    // Busca simulação
    const { data: simulation, error: simError } = await supabaseAdmin
      .from('simulacoes_entrevista')
      .select('*')
      .eq('id', simulationId)
      .single();

    if (simError) {
      throw simError;
    }

    // Busca mensagens ordenadas
    const { data: messages, error: msgError } = await supabaseAdmin
      .from('mensagens_entrevista')
      .select('*')
      .eq('id_simulacao', simulationId)
      .order('ordem', { ascending: true });

    if (msgError) {
      console.warn('Erro ao buscar mensagens:', msgError);
    }

    return {
      ...simulation,
      messages: messages || []
    };
  } catch (error) {
    console.error('Erro ao buscar entrevista:', error);
    throw error;
  }
};

/**
 * Lista todas as entrevistas de um usuário
 */
export const getUserInterviews = async (userId, limit = 50) => {
  if (!supabaseAdmin) {
    throw new Error('Supabase não configurado');
  }

  try {
    const { data, error } = await supabaseAdmin
      .from('simulacoes_entrevista')
      .select('*')
      .eq('id_usuario', userId)
      .order('criado_em', { ascending: false })
      .limit(limit);

    if (error) {
      throw error;
    }

    return data || [];
  } catch (error) {
    console.error('Erro ao buscar entrevistas do usuário:', error);
    throw error;
  }
};
