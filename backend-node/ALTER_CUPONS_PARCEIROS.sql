-- Parceiros e vínculo com cupons + métricas em compras
-- Execute no Supabase após CRIAR_TABELA_CUPONS.sql

CREATE TABLE IF NOT EXISTS public.parceiros (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  nome TEXT NOT NULL,
  cpf TEXT,
  descricao TEXT,
  email TEXT,
  ativo BOOLEAN NOT NULL DEFAULT true,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

ALTER TABLE public.parceiros
  ADD COLUMN IF NOT EXISTS cpf TEXT,
  ADD COLUMN IF NOT EXISTS descricao TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS idx_parceiros_cpf ON public.parceiros(cpf) WHERE cpf IS NOT NULL AND cpf <> '';

CREATE INDEX IF NOT EXISTS idx_parceiros_ativo ON public.parceiros(ativo);

ALTER TABLE public.cupons
  ADD COLUMN IF NOT EXISTS id_parceiro UUID REFERENCES public.parceiros(id) ON DELETE SET NULL,
  ADD COLUMN IF NOT EXISTS porcentagem_parceiro NUMERIC(5, 2)
    CHECK (porcentagem_parceiro IS NULL OR (porcentagem_parceiro >= 0 AND porcentagem_parceiro <= 100));

ALTER TABLE public.compras
  ADD COLUMN IF NOT EXISTS id_parceiro UUID REFERENCES public.parceiros(id),
  ADD COLUMN IF NOT EXISTS porcentagem_parceiro_aplicada NUMERIC(5, 2),
  ADD COLUMN IF NOT EXISTS valor_parceiro NUMERIC(10, 2);

CREATE INDEX IF NOT EXISTS idx_cupons_id_parceiro ON public.cupons(id_parceiro);
CREATE INDEX IF NOT EXISTS idx_compras_id_parceiro ON public.compras(id_parceiro);
