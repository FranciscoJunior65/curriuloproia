-- ============================================================
-- ESTRUTURA DE CUPONS E VÍNCULO COM COMPRAS
-- ============================================================
-- Tabela de cupons: nome (código), porcentagem de desconto, ativo
-- Compras: registra se foi com cupom (id_cupom, nome_cupom, porcentagem, preço original)
-- ============================================================

-- Tabela de cupons
CREATE TABLE IF NOT EXISTS public.cupons (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  nome TEXT NOT NULL UNIQUE,
  porcentagem_desconto NUMERIC(5, 2) NOT NULL CHECK (porcentagem_desconto >= 0 AND porcentagem_desconto <= 100),
  ativo BOOLEAN NOT NULL DEFAULT true,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_cupons_nome ON public.cupons(nome);
CREATE INDEX IF NOT EXISTS idx_cupons_ativo ON public.cupons(ativo);

COMMENT ON TABLE public.cupons IS 'Cupons de desconto por código (nome) e porcentagem';
COMMENT ON COLUMN public.cupons.nome IS 'Código do cupom (ex: PROMO10)';
COMMENT ON COLUMN public.cupons.porcentagem_desconto IS 'Desconto em % (0-100)';
COMMENT ON COLUMN public.cupons.ativo IS 'Se o cupom está ativo para uso';

-- Colunas de cupom na tabela compras
ALTER TABLE public.compras
  ADD COLUMN IF NOT EXISTS id_cupom UUID REFERENCES public.cupons(id),
  ADD COLUMN IF NOT EXISTS nome_cupom TEXT,
  ADD COLUMN IF NOT EXISTS porcentagem_desconto_aplicado NUMERIC(5, 2),
  ADD COLUMN IF NOT EXISTS preco_original NUMERIC(10, 2);

COMMENT ON COLUMN public.compras.id_cupom IS 'Cupom utilizado na compra (opcional)';
COMMENT ON COLUMN public.compras.nome_cupom IS 'Nome/código do cupom no momento da compra';
COMMENT ON COLUMN public.compras.porcentagem_desconto_aplicado IS 'Porcentagem de desconto aplicada';
COMMENT ON COLUMN public.compras.preco_original IS 'Preço antes do desconto (quando usou cupom)';

CREATE INDEX IF NOT EXISTS idx_compras_id_cupom ON public.compras(id_cupom);

-- ============================================================
-- Uso de cupom por CPF (1 uso por cupom por CPF)
-- ============================================================
CREATE TABLE IF NOT EXISTS public.cupom_uso (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cupom UUID NOT NULL REFERENCES public.cupons(id) ON DELETE CASCADE,
  cpf_normalizado TEXT NOT NULL,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  UNIQUE(id_cupom, cpf_normalizado)
);

CREATE INDEX IF NOT EXISTS idx_cupom_uso_cpf ON public.cupom_uso(cpf_normalizado);
CREATE INDEX IF NOT EXISTS idx_cupom_uso_id_cupom ON public.cupom_uso(id_cupom);

COMMENT ON TABLE public.cupom_uso IS 'Registro de uso de cupom por CPF (cada CPF pode usar cada cupom apenas 1 vez)';
COMMENT ON COLUMN public.cupom_uso.cpf_normalizado IS 'CPF apenas dígitos (11 caracteres)';

-- Cupom de teste: Análise Única 100% grátis (uso único por CPF)
INSERT INTO public.cupons (nome, porcentagem_desconto, ativo)
VALUES ('TESTE100', 100, true)
ON CONFLICT (nome) DO NOTHING;
