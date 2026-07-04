-- Tabela relacional de preços (substitui pricing_config JSON em app_configuracoes).
-- Execute uma vez no SQL Editor do Supabase.

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

COMMENT ON TABLE config_precos IS 'Preços e descontos dos planos (uma linha global).';
COMMENT ON COLUMN config_precos.credit_unit_price_brl IS 'Valor por crédito — base para pacotes (×1, ×3, ×5).';
COMMENT ON COLUMN config_precos.transaction_fee_brl IS 'Taxa fixa somada na vitrine (mesma para todos os pacotes).';
COMMENT ON COLUMN config_precos.single_discount_percent IS 'Desconto (%) análise única.';
COMMENT ON COLUMN config_precos.pack3_discount_percent IS 'Desconto (%) pacote 3.';
COMMENT ON COLUMN config_precos.pack5_discount_percent IS 'Desconto (%) pacote 5.';

-- Migra dados do JSON antigo (se existir) para colunas
INSERT INTO config_precos (
  id,
  credit_unit_price_brl,
  single_discount_percent,
  pack3_discount_percent,
  pack5_discount_percent,
  english_price_brl,
  english_bundle_price_brl,
  transaction_fee_brl,
  single_price_override,
  pack3_price_override,
  pack5_price_override,
  atualizado_em
)
SELECT
  'default',
  COALESCE((c.valor::jsonb ->> 'creditUnitPriceBRL')::numeric, 7.90),
  COALESCE((c.valor::jsonb ->> 'singleDiscountPercent')::numeric, 0),
  COALESCE((c.valor::jsonb ->> 'pack3DiscountPercent')::numeric, 0),
  COALESCE((c.valor::jsonb ->> 'pack5DiscountPercent')::numeric, 0),
  COALESCE((c.valor::jsonb ->> 'englishPriceBRL')::numeric, 17.90),
  COALESCE((c.valor::jsonb ->> 'englishBundlePriceBRL')::numeric, 5.90),
  COALESCE((c.valor::jsonb ->> 'transactionFeeBRL')::numeric, 0.99),
  NULLIF(c.valor::jsonb ->> 'singlePriceOverride', 'null')::numeric,
  NULLIF(c.valor::jsonb ->> 'pack3PriceOverride', 'null')::numeric,
  NULLIF(c.valor::jsonb ->> 'pack5PriceOverride', 'null')::numeric,
  COALESCE(c.atualizado_em, NOW())
FROM app_configuracoes c
WHERE c.chave = 'pricing_config'
ON CONFLICT (id) DO UPDATE SET
  credit_unit_price_brl = EXCLUDED.credit_unit_price_brl,
  single_discount_percent = EXCLUDED.single_discount_percent,
  pack3_discount_percent = EXCLUDED.pack3_discount_percent,
  pack5_discount_percent = EXCLUDED.pack5_discount_percent,
  english_price_brl = EXCLUDED.english_price_brl,
  english_bundle_price_brl = EXCLUDED.english_bundle_price_brl,
  transaction_fee_brl = EXCLUDED.transaction_fee_brl,
  single_price_override = EXCLUDED.single_price_override,
  pack3_price_override = EXCLUDED.pack3_price_override,
  pack5_price_override = EXCLUDED.pack5_price_override,
  atualizado_em = EXCLUDED.atualizado_em;

-- Seed padrão se não havia JSON
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

-- Remove chave JSON obsoleta
DELETE FROM app_configuracoes WHERE chave = 'pricing_config';

-- Alterar taxa depois:
-- UPDATE config_precos SET transaction_fee_brl = 0.99, atualizado_em = NOW() WHERE id = 'default';
