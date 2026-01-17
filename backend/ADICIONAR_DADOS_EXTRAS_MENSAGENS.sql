-- ============================================================
-- ADICIONAR CAMPO dados_extras NA TABELA mensagens_entrevista
-- ============================================================
-- Este campo permite armazenar informações adicionais como:
-- - questionIndex: índice da pergunta
-- - score: score da avaliação
-- - strengths: pontos fortes
-- - improvements: pontos a melhorar

ALTER TABLE public.mensagens_entrevista
ADD COLUMN IF NOT EXISTS dados_extras JSONB DEFAULT '{}'::JSONB;

-- Índice para busca por dados extras
CREATE INDEX IF NOT EXISTS idx_mensagens_dados_extras ON public.mensagens_entrevista USING GIN (dados_extras);

-- Comentário
COMMENT ON COLUMN public.mensagens_entrevista.dados_extras IS 'Dados extras da mensagem (score, questionIndex, strengths, improvements, etc.)';
