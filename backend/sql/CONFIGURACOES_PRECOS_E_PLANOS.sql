-- Tabela de configurações globais + seed de pagamento e preços (config_precos)
-- Execute no Supabase SQL Editor (ou psql) após criar o projeto.

CREATE TABLE IF NOT EXISTS app_configuracoes (
  chave TEXT PRIMARY KEY,
  valor TEXT NOT NULL,
  atualizado_em TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS config_precos (
  id TEXT PRIMARY KEY DEFAULT 'default',
  credit_unit_price_brl NUMERIC(10, 2) NOT NULL DEFAULT 7.90,
  single_discount_percent NUMERIC(6, 2) NOT NULL DEFAULT 0,
  pack3_discount_percent NUMERIC(6, 2) NOT NULL DEFAULT 0,
  pack5_discount_percent NUMERIC(6, 2) NOT NULL DEFAULT 0,
  english_price_brl NUMERIC(10, 2) NOT NULL DEFAULT 17.90,
  english_bundle_price_brl NUMERIC(10, 2) NOT NULL DEFAULT 5.90,
  transaction_fee_brl NUMERIC(10, 2) NOT NULL DEFAULT 0.99,
  single_price_override NUMERIC(10, 2),
  pack3_price_override NUMERIC(10, 2),
  pack5_price_override NUMERIC(10, 2),
  atualizado_em TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT config_precos_singleton CHECK (id = 'default')
);

-- Provedor de pagamento ativo: stripe | mercadopago | cakto
INSERT INTO app_configuracoes (chave, valor)
VALUES ('payment_provider', 'stripe')
ON CONFLICT (chave) DO NOTHING;

INSERT INTO config_precos (
  id,
  credit_unit_price_brl,
  single_discount_percent,
  pack3_discount_percent,
  pack5_discount_percent,
  english_price_brl,
  english_bundle_price_brl,
  transaction_fee_brl,
  pack3_price_override
)
VALUES (
  'default',
  7.90,
  0,
  0,
  4.05,
  17.90,
  5.90,
  0.99,
  27.90
)
ON CONFLICT (id) DO NOTHING;

COMMENT ON TABLE app_configuracoes IS 'Configurações chave-valor: payment_provider, mercadopago_mode, etc.';
COMMENT ON TABLE config_precos IS 'Preços e descontos dos planos (colunas, não JSON).';
