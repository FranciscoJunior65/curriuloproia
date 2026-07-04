-- Vincula compras de currículo em inglês à análise (bundle ou compra posterior)
ALTER TABLE public.compras
ADD COLUMN IF NOT EXISTS id_analise TEXT;

COMMENT ON COLUMN public.compras.id_analise IS 'Análise que recebeu o direito ao currículo em inglês';

CREATE INDEX IF NOT EXISTS idx_compras_id_analise ON public.compras(id_analise);
