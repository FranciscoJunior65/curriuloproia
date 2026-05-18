-- ============================================================
-- Adiciona coluna CPF na tabela perfis_usuarios
-- Execute no Supabase SQL Editor
-- ============================================================

-- Adiciona coluna cpf (TEXT, opcional) para armazenar CPF do cliente
ALTER TABLE public.perfis_usuarios
ADD COLUMN IF NOT EXISTS cpf TEXT;

-- Índice opcional para buscas por CPF (se precisar no futuro)
-- CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_cpf ON public.perfis_usuarios(cpf) WHERE cpf IS NOT NULL;

COMMENT ON COLUMN public.perfis_usuarios.cpf IS 'CPF do usuário (apenas números ou formatado)';
