-- ============================================================
-- ADICIONAR CAMPO id_site_vagas NA TABELA creditos
-- ============================================================
-- Este script adiciona o campo id_site_vagas na tabela creditos
-- para rastrear qual site de vagas foi usado em cada análise
-- ============================================================

-- Adiciona coluna id_site_vagas na tabela creditos (se não existir)
ALTER TABLE public.creditos 
ADD COLUMN IF NOT EXISTS id_site_vagas TEXT;

-- Cria índice para melhorar performance nas consultas por site
CREATE INDEX IF NOT EXISTS idx_creditos_id_site_vagas ON public.creditos(id_site_vagas);

-- Adiciona comentário na coluna para documentação
COMMENT ON COLUMN public.creditos.id_site_vagas IS 'ID do site de vagas usado na análise (referência à tabela sites_vagas)';

-- ============================================================
-- VERIFICAÇÃO
-- ============================================================

-- Verifica se a coluna foi criada
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'creditos'
  AND column_name = 'id_site_vagas';

-- Mostra estatísticas de uso por site (se houver dados)
SELECT 
    c.id_site_vagas,
    sv.nome as nome_site,
    COUNT(*) as total_analises
FROM public.creditos c
LEFT JOIN public.sites_vagas sv ON c.id_site_vagas = sv.id
WHERE c.usado = TRUE
  AND c.tipo_acao = 'analysis'
GROUP BY c.id_site_vagas, sv.nome
ORDER BY total_analises DESC;
