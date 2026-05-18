-- Script para corrigir o tipo da coluna id na tabela user_profiles
-- Execute este SQL no SQL Editor do Supabase

-- Verifica o tipo atual da coluna id
SELECT 
  column_name, 
  data_type,
  udt_name
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'user_profiles' 
  AND column_name = 'id';

-- Se a coluna id for UUID, altera para TEXT
-- IMPORTANTE: Isso só funciona se a tabela estiver vazia ou se você tiver backup
-- Se já houver dados, você precisará migrar os dados primeiro

-- Opção 1: Se a tabela estiver vazia ou você quiser recriar
-- DROP TABLE IF EXISTS public.user_profiles CASCADE;

-- Opção 2: Se já houver dados e a coluna for UUID, você precisa:
-- 1. Criar uma coluna temporária
-- 2. Converter os UUIDs para TEXT
-- 3. Remover a coluna antiga
-- 4. Renomear a nova coluna

-- Solução mais simples: Garantir que a coluna seja TEXT desde o início
-- Se a tabela não existe, cria com TEXT
CREATE TABLE IF NOT EXISTS public.user_profiles (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  name TEXT,
  password_hash TEXT,
  credits INTEGER DEFAULT 0,
  plan TEXT,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  last_analysis TIMESTAMP WITH TIME ZONE,
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  email_verified BOOLEAN DEFAULT FALSE,
  verification_code TEXT,
  verification_code_expires_at TIMESTAMP WITH TIME ZONE
);

-- Se a tabela já existe e a coluna id é UUID, você precisa alterar manualmente
-- ATENÇÃO: Isso pode causar perda de dados se não for feito corretamente
-- Recomendação: Use a solução abaixo apenas se a tabela estiver vazia

-- Para alterar de UUID para TEXT (APENAS SE A TABELA ESTIVER VAZIA):
-- ALTER TABLE public.user_profiles ALTER COLUMN id TYPE TEXT USING id::TEXT;

-- Desabilita RLS para permitir acesso via service_role
ALTER TABLE public.user_profiles DISABLE ROW LEVEL SECURITY;

