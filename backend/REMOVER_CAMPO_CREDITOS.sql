-- ============================================================
-- REMOVER CAMPO CREDITOS DE PERFIS_USUARIOS
-- ============================================================
-- O campo creditos será calculado dinamicamente da tabela creditos
-- ============================================================

-- Remove a coluna creditos da tabela perfis_usuarios
ALTER TABLE public.perfis_usuarios 
DROP COLUMN IF EXISTS creditos;

-- Verifica se foi removido
SELECT 
    column_name, 
    data_type
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'perfis_usuarios'
  AND column_name = 'creditos';

-- Se retornar vazio, a coluna foi removida com sucesso ✅

