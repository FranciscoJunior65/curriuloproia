-- ============================================================
-- TABELA: ai_usage_log
-- ============================================================
-- Armazena log de todas as requisições de IA para controle de uso
-- ============================================================

CREATE TABLE IF NOT EXISTS public.ai_usage_log (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  provider TEXT NOT NULL, -- 'gemini', 'openai', 'groq', etc.
  service_type TEXT NOT NULL, -- 'analise', 'carta', 'otimizacao', 'busca_vagas', 'entrevista'
  tokens_input INTEGER,
  tokens_output INTEGER,
  cost_estimate DECIMAL(10, 6), -- Custo estimado
  response_time_ms INTEGER,
  success BOOLEAN DEFAULT TRUE,
  error_message TEXT,
  id_usuario TEXT,
  id_curriculo TEXT,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  FOREIGN KEY (id_usuario) REFERENCES perfis_usuarios(id) ON DELETE SET NULL,
  FOREIGN KEY (id_curriculo) REFERENCES curriculos_importados(id) ON DELETE SET NULL
);

-- Índices para melhor performance
CREATE INDEX IF NOT EXISTS idx_ai_usage_provider ON public.ai_usage_log(provider);
CREATE INDEX IF NOT EXISTS idx_ai_usage_service_type ON public.ai_usage_log(service_type);
CREATE INDEX IF NOT EXISTS idx_ai_usage_id_usuario ON public.ai_usage_log(id_usuario);
CREATE INDEX IF NOT EXISTS idx_ai_usage_criado_em ON public.ai_usage_log(criado_em);
CREATE INDEX IF NOT EXISTS idx_ai_usage_success ON public.ai_usage_log(success);

-- Índice composto para consultas por data e provider
CREATE INDEX IF NOT EXISTS idx_ai_usage_date_provider ON public.ai_usage_log(criado_em, provider);

-- Comentários
COMMENT ON TABLE public.ai_usage_log IS 'Log de uso de APIs de IA para controle e monitoramento';
COMMENT ON COLUMN public.ai_usage_log.provider IS 'Provedor de IA usado (gemini, openai, etc.)';
COMMENT ON COLUMN public.ai_usage_log.service_type IS 'Tipo de serviço (analise, carta, otimizacao, etc.)';
COMMENT ON COLUMN public.ai_usage_log.cost_estimate IS 'Custo estimado da requisição em dólares';
