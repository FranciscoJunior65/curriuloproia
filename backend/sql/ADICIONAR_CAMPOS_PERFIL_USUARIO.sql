-- ============================================================
-- Adiciona colunas: data_nascimento, cidade, pais na tabela perfis_usuarios
-- Execute no Supabase SQL Editor (após ADICIONAR_CPF_PERFIS_USUARIOS.sql se ainda não rodou)
-- ============================================================

-- Data de nascimento (opcional)
ALTER TABLE public.perfis_usuarios
ADD COLUMN IF NOT EXISTS data_nascimento DATE;

-- Cidade (opcional)
ALTER TABLE public.perfis_usuarios
ADD COLUMN IF NOT EXISTS cidade TEXT;

-- País (opcional)
ALTER TABLE public.perfis_usuarios
ADD COLUMN IF NOT EXISTS pais TEXT;

COMMENT ON COLUMN public.perfis_usuarios.data_nascimento IS 'Data de nascimento do usuário';
COMMENT ON COLUMN public.perfis_usuarios.cidade IS 'Cidade do usuário';
COMMENT ON COLUMN public.perfis_usuarios.pais IS 'País do usuário';
