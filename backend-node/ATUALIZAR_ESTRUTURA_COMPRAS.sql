-- ============================================================
-- ATUALIZAÇÃO: ESTRUTURA PARA SUPORTAR CURRÍCULO EM INGLÊS
-- ============================================================
-- Adiciona campo para relacionar compras (venda casada)
-- Exemplo: Compra de análise + Currículo em Inglês
-- ============================================================

-- Adiciona coluna para relacionar compras (se não existir)
ALTER TABLE public.compras 
ADD COLUMN IF NOT EXISTS id_compra_pai TEXT,
ADD COLUMN IF NOT EXISTS tipo_servico TEXT DEFAULT 'analise'; -- 'analise' ou 'curriculo_ingles'

-- Adiciona comentários para documentação
COMMENT ON COLUMN public.compras.id_compra_pai IS 'ID da compra principal (para venda casada). NULL se for compra principal.';
COMMENT ON COLUMN public.compras.tipo_servico IS 'Tipo de serviço: analise ou curriculo_ingles';

-- Cria índice para melhor performance
CREATE INDEX IF NOT EXISTS idx_compras_id_compra_pai ON public.compras(id_compra_pai);
CREATE INDEX IF NOT EXISTS idx_compras_tipo_servico ON public.compras(tipo_servico);

-- Atualiza compras existentes de currículo em inglês
UPDATE public.compras 
SET tipo_servico = 'curriculo_ingles'
WHERE id_plano = 'english' AND tipo_servico IS NULL;

-- Verifica estrutura
SELECT 
    'compras' as tabela,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'compras'
ORDER BY ordinal_position;

