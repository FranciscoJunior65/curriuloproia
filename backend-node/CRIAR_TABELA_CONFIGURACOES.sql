-- Configurações globais da aplicação (ex.: provedor de pagamento)
CREATE TABLE IF NOT EXISTS app_configuracoes (
  chave TEXT PRIMARY KEY,
  valor TEXT NOT NULL,
  atualizado_em TIMESTAMPTZ DEFAULT NOW()
);

INSERT INTO app_configuracoes (chave, valor)
VALUES ('payment_provider', 'stripe')
ON CONFLICT (chave) DO NOTHING;

-- Valores aceitos para payment_provider: stripe | mercadopago
