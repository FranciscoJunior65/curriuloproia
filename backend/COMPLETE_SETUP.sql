-- Script completo para configurar a tabela user_profiles
-- Execute este SQL no SQL Editor do Supabase

-- Remove foreign key constraint se existir
DO $$ 
DECLARE
    constraint_name_var TEXT;
BEGIN
    SELECT constraint_name INTO constraint_name_var
    FROM information_schema.table_constraints
    WHERE table_schema = 'public'
      AND table_name = 'user_profiles'
      AND constraint_type = 'FOREIGN KEY'
      AND constraint_name LIKE '%id%';
    
    IF constraint_name_var IS NOT NULL THEN
        EXECUTE format('ALTER TABLE public.user_profiles DROP CONSTRAINT IF EXISTS %I', constraint_name_var);
        RAISE NOTICE 'Foreign key constraint % removida', constraint_name_var;
    END IF;
END $$;

-- Garante que a coluna id é TEXT (não UUID)
DO $$ 
BEGIN
    -- Se a coluna for UUID, altera para TEXT (apenas se a tabela estiver vazia)
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'user_profiles' 
        AND column_name = 'id'
        AND data_type = 'uuid'
    ) THEN
        -- Verifica se a tabela está vazia
        IF NOT EXISTS (SELECT 1 FROM public.user_profiles LIMIT 1) THEN
            ALTER TABLE public.user_profiles ALTER COLUMN id TYPE TEXT USING id::TEXT;
            RAISE NOTICE 'Coluna id alterada de UUID para TEXT';
        ELSE
            RAISE NOTICE 'Tabela não está vazia. Não é possível alterar o tipo da coluna id automaticamente.';
        END IF;
    END IF;
END $$;

-- Adiciona todas as colunas necessárias
DO $$ 
BEGIN
    -- password_hash
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'user_profiles' 
        AND column_name = 'password_hash'
    ) THEN
        ALTER TABLE public.user_profiles ADD COLUMN password_hash TEXT;
    END IF;

    -- email_verified
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'user_profiles' 
        AND column_name = 'email_verified'
    ) THEN
        ALTER TABLE public.user_profiles ADD COLUMN email_verified BOOLEAN DEFAULT FALSE;
    END IF;

    -- verification_code (usado tanto para código quanto para token)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'user_profiles' 
        AND column_name = 'verification_code'
    ) THEN
        ALTER TABLE public.user_profiles ADD COLUMN verification_code TEXT;
    END IF;

    -- verification_code_expires_at
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'user_profiles' 
        AND column_name = 'verification_code_expires_at'
    ) THEN
        ALTER TABLE public.user_profiles ADD COLUMN verification_code_expires_at TIMESTAMP WITH TIME ZONE;
    END IF;
END $$;

-- Desabilita RLS
ALTER TABLE public.user_profiles DISABLE ROW LEVEL SECURITY;

-- Verifica estrutura final
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'user_profiles'
ORDER BY ordinal_position;

