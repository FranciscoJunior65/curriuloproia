-- ============================================================
-- SCRIPT PARA VERIFICAR E CORRIGIR TABELAS
-- ============================================================
-- Este script verifica se as tabelas antigas existem e as remove
-- Garante que apenas as tabelas em português existam
-- ============================================================

-- Verifica quais tabelas existem
SELECT 
    table_name,
    table_schema
FROM information_schema.tables 
WHERE table_schema = 'public' 
  AND table_name IN ('user_profiles', 'perfis_usuarios', 'purchases', 'compras', 'credit_usage', 'uso_creditos', 'creditos')
ORDER BY table_name;

-- ============================================================
-- REMOVE TABELAS ANTIGAS (se existirem)
-- ============================================================

-- Remove tabela user_profiles se existir
DROP TABLE IF EXISTS public.user_profiles CASCADE;

-- Remove tabela purchases se existir (será substituída por compras)
-- ATENÇÃO: Só execute se já migrou os dados!
-- DROP TABLE IF EXISTS public.purchases CASCADE;

-- Remove tabela credit_usage se existir
DROP TABLE IF EXISTS public.credit_usage CASCADE;

-- Remove tabela uso_creditos se existir (será substituída por creditos)
DROP TABLE IF EXISTS public.uso_creditos CASCADE;

-- ============================================================
-- GARANTE QUE AS TABELAS NOVAS EXISTEM
-- ============================================================

-- Tabela de perfis de usuários
CREATE TABLE IF NOT EXISTS public.perfis_usuarios (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  nome TEXT,
  hash_senha TEXT,
  -- creditos removido - será calculado dinamicamente da tabela creditos
  plano TEXT,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  ultima_analise TIMESTAMP WITH TIME ZONE,
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  email_verificado BOOLEAN DEFAULT FALSE,
  codigo_verificacao TEXT,
  codigo_verificacao_expira_em TIMESTAMP WITH TIME ZONE,
  tipo_usuario TEXT DEFAULT 'cliente'
);

-- Remove coluna creditos se existir
ALTER TABLE public.perfis_usuarios DROP COLUMN IF EXISTS creditos;

-- Tabela de compras
CREATE TABLE IF NOT EXISTS public.compras (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_usuario TEXT NOT NULL,
  id_plano TEXT NOT NULL,
  nome_plano TEXT NOT NULL,
  quantidade_creditos INTEGER NOT NULL,
  preco DECIMAL(10, 2) NOT NULL,
  moeda TEXT DEFAULT 'BRL',
  status TEXT NOT NULL DEFAULT 'concluida',
  metodo_pagamento TEXT DEFAULT 'mock',
  id_pagamento TEXT,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Tabela de créditos individuais
CREATE TABLE IF NOT EXISTS public.creditos (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_compra TEXT NOT NULL,
  id_usuario TEXT NOT NULL,
  usado BOOLEAN DEFAULT FALSE,
  usado_em TIMESTAMP WITH TIME ZONE,
  tipo_acao TEXT,
  nome_arquivo_curriculo TEXT,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- ============================================================
-- CRIA ÍNDICES
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_email ON public.perfis_usuarios(email);
CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_tipo_usuario ON public.perfis_usuarios(tipo_usuario);
CREATE INDEX IF NOT EXISTS idx_compras_id_usuario ON public.compras(id_usuario);
CREATE INDEX IF NOT EXISTS idx_creditos_id_compra ON public.creditos(id_compra);
CREATE INDEX IF NOT EXISTS idx_creditos_id_usuario ON public.creditos(id_usuario);
CREATE INDEX IF NOT EXISTS idx_creditos_usado ON public.creditos(usado);

-- ============================================================
-- DESABILITA RLS
-- ============================================================

ALTER TABLE public.perfis_usuarios DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.compras DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.creditos DISABLE ROW LEVEL SECURITY;

-- ============================================================
-- VERIFICAÇÃO FINAL
-- ============================================================

-- Lista todas as tabelas do schema public
SELECT 
    'Tabelas existentes' as info,
    table_name,
    CASE 
        WHEN table_name IN ('perfis_usuarios', 'compras', 'creditos') THEN '✅ OK'
        WHEN table_name IN ('user_profiles', 'purchases', 'credit_usage', 'uso_creditos') THEN '❌ ANTIGA (deve ser removida)'
        ELSE '⚠️ OUTRA'
    END as status
FROM information_schema.tables 
WHERE table_schema = 'public' 
  AND table_type = 'BASE TABLE'
ORDER BY table_name;

-- Conta registros nas tabelas principais
SELECT 
    'perfis_usuarios' as tabela,
    COUNT(*) as total_registros
FROM public.perfis_usuarios
UNION ALL
SELECT 
    'compras' as tabela,
    COUNT(*) as total_registros
FROM public.compras
UNION ALL
SELECT 
    'creditos' as tabela,
    COUNT(*) as total_registros
FROM public.creditos;

