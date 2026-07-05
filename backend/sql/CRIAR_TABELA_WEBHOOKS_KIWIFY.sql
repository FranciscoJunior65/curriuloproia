-- ============================================================
-- TABELA: webhooks_kiwify_log
-- ============================================================
-- Auditoria de entregas do webhook Kiwify (payload, erro, resposta)
-- ============================================================

CREATE TABLE IF NOT EXISTS public.webhooks_kiwify_log (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  payload_recebido TEXT,
  payload_parseado JSONB,
  order_id TEXT,
  order_ref TEXT,
  event_type TEXT,
  payment_status TEXT,
  processed BOOLEAN NOT NULL DEFAULT FALSE,
  already_fulfilled BOOLEAN NOT NULL DEFAULT FALSE,
  credits INTEGER,
  id_usuario TEXT,
  http_status INTEGER NOT NULL DEFAULT 200,
  api_version TEXT,
  message TEXT,
  resposta_json JSONB,
  erro TEXT,
  estagio_falha TEXT,
  detalhes_processamento TEXT,
  criado_em TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_webhooks_kiwify_log_criado_em
  ON public.webhooks_kiwify_log (criado_em DESC);

CREATE INDEX IF NOT EXISTS idx_webhooks_kiwify_log_order_ref
  ON public.webhooks_kiwify_log (order_ref);

CREATE INDEX IF NOT EXISTS idx_webhooks_kiwify_log_order_id
  ON public.webhooks_kiwify_log (order_id);

CREATE INDEX IF NOT EXISTS idx_webhooks_kiwify_log_processed
  ON public.webhooks_kiwify_log (processed);

COMMENT ON TABLE public.webhooks_kiwify_log IS 'Log de entregas do webhook Kiwify para diagnóstico';
COMMENT ON COLUMN public.webhooks_kiwify_log.payload_recebido IS 'Corpo bruto recebido na requisição';
COMMENT ON COLUMN public.webhooks_kiwify_log.resposta_json IS 'JSON retornado ao chamador (Kiwify/Postman)';
COMMENT ON COLUMN public.webhooks_kiwify_log.estagio_falha IS 'Etapa onde o processamento parou (auth, evento, order_id, sck, etc.)';
