-- Script para adicionar coluna user_type na tabela user_profiles
-- Execute este SQL no SQL Editor do Supabase

-- Adiciona coluna user_type se não existir
DO $$ 
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns 
    WHERE table_schema = 'public' 
    AND table_name = 'user_profiles' 
    AND column_name = 'user_type'
  ) THEN
    ALTER TABLE public.user_profiles ADD COLUMN user_type TEXT DEFAULT 'cliente';
    RAISE NOTICE 'Coluna user_type adicionada com sucesso!';
  ELSE
    RAISE NOTICE 'Coluna user_type já existe.';
  END IF;
END $$;

-- Atualiza usuários existentes para 'cliente' se estiverem NULL
UPDATE public.user_profiles 
SET user_type = 'cliente' 
WHERE user_type IS NULL;

-- Define o usuário específico como admin
UPDATE public.user_profiles 
SET user_type = 'admin' 
WHERE id = '1ad469d3-2c57-453e-b69f-ac22fc0fb735';

-- Verifica a estrutura
SELECT 
  column_name, 
  data_type, 
  is_nullable,
  column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'user_profiles'
  AND column_name = 'user_type';

-- Verifica o usuário admin
SELECT id, email, name, user_type 
FROM public.user_profiles 
WHERE id = '1ad469d3-2c57-453e-b69f-ac22fc0fb735';

