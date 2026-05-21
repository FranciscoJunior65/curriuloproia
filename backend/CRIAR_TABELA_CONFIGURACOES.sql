-- Configurações globais da aplicação (ex.: provedor de pagamento)
CREATE TABLE IF NOT EXISTS app_configuracoes (
  chave TEXT PRIMARY KEY,
  valor TEXT NOT NULL,
  atualizado_em TIMESTAMPTZ DEFAULT NOW()
);

INSERT INTO app_configuracoes (chave, valor)
VALUES ('payment_provider', 'stripe')
ON CONFLICT (chave) DO NOTHING;

INSERT INTO app_configuracoes (chave, valor)
VALUES (
  'pricing_config',
  '{"creditUnitPriceBRL":7.90,"singleDiscountPercent":0,"pack3DiscountPercent":0,"pack5DiscountPercent":4.05,"englishPriceBRL":17.90,"englishBundlePriceBRL":5.90,"singlePriceOverride":null,"pack3PriceOverride":27.90,"pack5PriceOverride":null}'
)
ON CONFLICT (chave) DO NOTHING;

-- Valores aceitos para payment_provider: stripe | mercadopago
-- pricing_config: ver CONFIGURACOES_PRECOS_E_PLANOS.sql
