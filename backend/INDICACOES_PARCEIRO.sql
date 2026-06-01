-- Cadastros via link de parceiro/cupom
-- Execute no Supabase após ALTER_CUPONS_PARCEIROS.sql

CREATE TABLE IF NOT EXISTS public.indicacoes_parceiro (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  id_usuario TEXT NOT NULL,
  id_cupom UUID NOT NULL REFERENCES public.cupons(id) ON DELETE CASCADE,
  codigo_cupom TEXT NOT NULL,
  id_parceiro UUID REFERENCES public.parceiros(id) ON DELETE SET NULL,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  UNIQUE(id_usuario)
);

CREATE INDEX IF NOT EXISTS idx_indicacoes_parceiro_id_cupom ON public.indicacoes_parceiro(id_cupom);
CREATE INDEX IF NOT EXISTS idx_indicacoes_parceiro_id_parceiro ON public.indicacoes_parceiro(id_parceiro);
CREATE INDEX IF NOT EXISTS idx_indicacoes_parceiro_criado_em ON public.indicacoes_parceiro(criado_em DESC);

COMMENT ON TABLE public.indicacoes_parceiro IS 'Registro de contas criadas via link de cupom/parceiro';
COMMENT ON COLUMN public.indicacoes_parceiro.id_usuario IS 'ID do usuário (perfis_usuarios.id)';
COMMENT ON COLUMN public.indicacoes_parceiro.codigo_cupom IS 'Código do cupom usado no link (?cupom=)';
