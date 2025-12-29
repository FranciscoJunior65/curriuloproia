-- Script para remover a foreign key constraint da tabela user_profiles
-- Execute este SQL no SQL Editor do Supabase

-- 1. Verifica quais foreign keys existem na tabela user_profiles
SELECT
    tc.constraint_name,
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
    AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
    AND tc.table_name = 'user_profiles';

-- 2. Remove a foreign key constraint se existir
-- Substitua 'user_profiles_id_fkey' pelo nome real da constraint (veja resultado acima)
DO $$ 
DECLARE
    constraint_name_var TEXT;
BEGIN
    -- Busca o nome da constraint
    SELECT constraint_name INTO constraint_name_var
    FROM information_schema.table_constraints
    WHERE table_schema = 'public'
      AND table_name = 'user_profiles'
      AND constraint_type = 'FOREIGN KEY'
      AND constraint_name LIKE '%id%';
    
    -- Se encontrou a constraint, remove ela
    IF constraint_name_var IS NOT NULL THEN
        EXECUTE format('ALTER TABLE public.user_profiles DROP CONSTRAINT IF EXISTS %I', constraint_name_var);
        RAISE NOTICE 'Foreign key constraint % removida com sucesso!', constraint_name_var;
    ELSE
        RAISE NOTICE 'Nenhuma foreign key constraint encontrada na coluna id.';
    END IF;
END $$;

-- 3. Alternativa: Remove todas as foreign keys da tabela (se houver múltiplas)
-- Descomente as linhas abaixo se necessário:
/*
DO $$ 
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT constraint_name
        FROM information_schema.table_constraints
        WHERE table_schema = 'public'
          AND table_name = 'user_profiles'
          AND constraint_type = 'FOREIGN KEY'
    ) LOOP
        EXECUTE format('ALTER TABLE public.user_profiles DROP CONSTRAINT IF EXISTS %I', r.constraint_name);
        RAISE NOTICE 'Foreign key constraint % removida', r.constraint_name;
    END LOOP;
END $$;
*/

-- 4. Verifica se a constraint foi removida
SELECT
    tc.constraint_name,
    tc.table_name,
    kcu.column_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
    AND tc.table_name = 'user_profiles';

