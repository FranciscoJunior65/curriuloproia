-- Script para corrigir/adicionar coluna password_hash na tabela user_profiles
-- Execute este SQL no SQL Editor do Supabase

-- Verifica e adiciona coluna password_hash se não existir
DO $$ 
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns 
    WHERE table_schema = 'public' 
    AND table_name = 'user_profiles' 
    AND column_name = 'password_hash'
  ) THEN
    ALTER TABLE public.user_profiles ADD COLUMN password_hash TEXT;
    RAISE NOTICE 'Coluna password_hash adicionada com sucesso!';
  ELSE
    RAISE NOTICE 'Coluna password_hash já existe.';
  END IF;
END $$;

-- Verifica e adiciona coluna email_verified se não existir
DO $$ 
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns 
    WHERE table_schema = 'public' 
    AND table_name = 'user_profiles' 
    AND column_name = 'email_verified'
  ) THEN
    ALTER TABLE public.user_profiles ADD COLUMN email_verified BOOLEAN DEFAULT FALSE;
    RAISE NOTICE 'Coluna email_verified adicionada com sucesso!';
  ELSE
    RAISE NOTICE 'Coluna email_verified já existe.';
  END IF;
END $$;

-- Verifica e adiciona coluna verification_code se não existir
DO $$ 
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns 
    WHERE table_schema = 'public' 
    AND table_name = 'user_profiles' 
    AND column_name = 'verification_code'
  ) THEN
    ALTER TABLE public.user_profiles ADD COLUMN verification_code TEXT;
    RAISE NOTICE 'Coluna verification_code adicionada com sucesso!';
  ELSE
    RAISE NOTICE 'Coluna verification_code já existe.';
  END IF;
END $$;

-- Verifica e adiciona coluna verification_code_expires_at se não existir
DO $$ 
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns 
    WHERE table_schema = 'public' 
    AND table_name = 'user_profiles' 
    AND column_name = 'verification_code_expires_at'
  ) THEN
    ALTER TABLE public.user_profiles ADD COLUMN verification_code_expires_at TIMESTAMP WITH TIME ZONE;
    RAISE NOTICE 'Coluna verification_code_expires_at adicionada com sucesso!';
  ELSE
    RAISE NOTICE 'Coluna verification_code_expires_at já existe.';
  END IF;
END $$;

-- Verifica a estrutura atual da tabela
SELECT 
  column_name, 
  data_type, 
  is_nullable,
  column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'user_profiles'
ORDER BY ordinal_position;

