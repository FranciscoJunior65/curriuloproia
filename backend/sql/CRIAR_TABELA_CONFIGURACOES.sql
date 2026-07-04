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

VALUES ('mercadopago_mode', 'test')

ON CONFLICT (chave) DO NOTHING;



-- Preços: tabela config_precos (ver CONFIGURACOES_PRECOS_E_PLANOS.sql ou ALTER_PRICING_TRANSACTION_FEE.sql)



-- Valores aceitos para payment_provider: stripe | mercadopago | cakto

-- Valores aceitos para mercadopago_mode: test | production

