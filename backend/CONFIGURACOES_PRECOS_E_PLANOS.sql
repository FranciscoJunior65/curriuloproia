-- Tabela de configurações globais + seed de pagamento e preços (pricing_config)
-- Execute no Supabase SQL Editor (ou psql) após criar o projeto.

CREATE TABLE IF NOT EXISTS app_configuracoes (
  chave TEXT PRIMARY KEY,
  valor TEXT NOT NULL,
  atualizado_em TIMESTAMPTZ DEFAULT NOW()
);

-- Provedor de pagamento ativo: stripe | mercadopago | cakto
INSERT INTO app_configuracoes (chave, valor)
VALUES ('payment_provider', 'stripe')
ON CONFLICT (chave) DO NOTHING;

-- Preços e descontos (JSON). Admin altera via PUT /api/admin/settings/pricing
INSERT INTO app_configuracoes (chave, valor)
VALUES (
  'pricing_config',
  '{
    "creditUnitPriceBRL": 7.90,
    "singleDiscountPercent": 0,
    "pack3DiscountPercent": 0,
    "pack5DiscountPercent": 4.05,
    "englishPriceBRL": 17.90,
    "englishBundlePriceBRL": 5.90,
    "singlePriceOverride": null,
    "pack3PriceOverride": 27.90,
    "pack5PriceOverride": null
  }'
)
ON CONFLICT (chave) DO NOTHING;

-- Atualiza apenas pricing_config se já existir registro antigo sem os campos novos (opcional)
-- Descomente para forçar valores padrão em ambiente de desenvolvimento:
-- UPDATE app_configuracoes SET valor = (SELECT valor FROM app_configuracoes WHERE chave = 'pricing_config' LIMIT 1), atualizado_em = NOW() WHERE chave = 'pricing_config';

COMMENT ON TABLE app_configuracoes IS 'Configurações chave-valor: payment_provider, pricing_config, etc.';
