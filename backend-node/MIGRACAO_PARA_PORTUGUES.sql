-- ============================================================
-- MIGRAÇÃO COMPLETA: TABELAS E COLUNAS PARA PORTUGUÊS
-- ============================================================
-- Este script:
-- 1. Cria as novas tabelas em português
-- 2. Migra os dados das tabelas antigas para as novas
-- 3. Exclui as tabelas antigas
-- ============================================================

-- ============================================================
-- PARTE 1: CRIAR NOVAS TABELAS EM PORTUGUÊS
-- ============================================================

-- Tabela de perfis de usuários
CREATE TABLE IF NOT EXISTS public.perfis_usuarios (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  nome TEXT,
  hash_senha TEXT,
  creditos INTEGER DEFAULT 0,
  plano TEXT,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  ultima_analise TIMESTAMP WITH TIME ZONE,
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  email_verificado BOOLEAN DEFAULT FALSE,
  codigo_verificacao TEXT,
  codigo_verificacao_expira_em TIMESTAMP WITH TIME ZONE,
  tipo_usuario TEXT DEFAULT 'cliente'
);

-- Índices para perfis_usuarios
CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_email ON public.perfis_usuarios(email);
CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_tipo_usuario ON public.perfis_usuarios(tipo_usuario);
CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_criado_em ON public.perfis_usuarios(criado_em);

-- Tabela de compras/transações
CREATE TABLE IF NOT EXISTS public.compras (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_usuario TEXT NOT NULL,
  id_plano TEXT NOT NULL,
  nome_plano TEXT NOT NULL,
  quantidade_creditos INTEGER NOT NULL,
  preco DECIMAL(10, 2) NOT NULL,
  moeda TEXT DEFAULT 'BRL',
  status TEXT NOT NULL DEFAULT 'concluida', -- concluida, pendente, cancelada, reembolsada
  metodo_pagamento TEXT DEFAULT 'mock', -- mock, stripe, pix, etc
  id_pagamento TEXT, -- ID da transação no gateway de pagamento (se houver)
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Índices para compras
CREATE INDEX IF NOT EXISTS idx_compras_id_usuario ON public.compras(id_usuario);
CREATE INDEX IF NOT EXISTS idx_compras_criado_em ON public.compras(criado_em);
CREATE INDEX IF NOT EXISTS idx_compras_status ON public.compras(status);

-- Tabela de histórico de uso de créditos
CREATE TABLE IF NOT EXISTS public.uso_creditos (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_usuario TEXT NOT NULL,
  creditos_usados INTEGER NOT NULL DEFAULT 1,
  tipo_acao TEXT NOT NULL, -- 'analise', 'geracao_pdf'
  nome_arquivo_curriculo TEXT,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Índices para uso_creditos
CREATE INDEX IF NOT EXISTS idx_uso_creditos_id_usuario ON public.uso_creditos(id_usuario);
CREATE INDEX IF NOT EXISTS idx_uso_creditos_criado_em ON public.uso_creditos(criado_em);

-- Função para atualizar atualizado_em automaticamente
CREATE OR REPLACE FUNCTION atualizar_atualizado_em()
RETURNS TRIGGER AS $$
BEGIN
    NEW.atualizado_em = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger para atualizar atualizado_em na tabela compras
DROP TRIGGER IF EXISTS atualizar_compras_atualizado_em ON public.compras;
CREATE TRIGGER atualizar_compras_atualizado_em
    BEFORE UPDATE ON public.compras
    FOR EACH ROW
    EXECUTE FUNCTION atualizar_atualizado_em();

-- Desabilita RLS (Row Level Security) para permitir acesso via service role
ALTER TABLE public.perfis_usuarios DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.compras DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.uso_creditos DISABLE ROW LEVEL SECURITY;

-- ============================================================
-- PARTE 2: MIGRAR DADOS DAS TABELAS ANTIGAS PARA AS NOVAS
-- ============================================================

-- Migrar dados de user_profiles para perfis_usuarios
INSERT INTO public.perfis_usuarios (
  id,
  email,
  nome,
  hash_senha,
  creditos,
  plano,
  criado_em,
  ultima_analise,
  atualizado_em,
  email_verificado,
  codigo_verificacao,
  codigo_verificacao_expira_em,
  tipo_usuario
)
SELECT 
  id,
  email,
  name,
  password_hash,
  credits,
  plan,
  created_at,
  last_analysis,
  updated_at,
  email_verified,
  verification_code,
  verification_code_expires_at,
  COALESCE(user_type, 'cliente')
FROM public.user_profiles
ON CONFLICT (id) DO NOTHING;

-- Migrar dados de purchases para compras
-- Mapear status: completed -> concluida, pending -> pendente, cancelled -> cancelada, refunded -> reembolsada
INSERT INTO public.compras (
  id,
  id_usuario,
  id_plano,
  nome_plano,
  quantidade_creditos,
  preco,
  moeda,
  status,
  metodo_pagamento,
  id_pagamento,
  criado_em,
  atualizado_em
)
SELECT 
  id,
  user_id,
  plan_id,
  plan_name,
  credits_amount,
  price,
  currency,
  CASE 
    WHEN status = 'completed' THEN 'concluida'
    WHEN status = 'pending' THEN 'pendente'
    WHEN status = 'cancelled' THEN 'cancelada'
    WHEN status = 'refunded' THEN 'reembolsada'
    ELSE 'concluida'
  END,
  payment_method,
  payment_id,
  created_at,
  updated_at
FROM public.purchases
ON CONFLICT (id) DO NOTHING;

-- Migrar dados de credit_usage para uso_creditos
-- Mapear action_type: analysis -> analise, pdf_generation -> geracao_pdf
INSERT INTO public.uso_creditos (
  id,
  id_usuario,
  creditos_usados,
  tipo_acao,
  nome_arquivo_curriculo,
  criado_em
)
SELECT 
  id,
  user_id,
  credits_used,
  CASE 
    WHEN action_type = 'analysis' THEN 'analise'
    WHEN action_type = 'pdf_generation' THEN 'geracao_pdf'
    ELSE action_type
  END,
  resume_file_name,
  created_at
FROM public.credit_usage
ON CONFLICT (id) DO NOTHING;

-- ============================================================
-- PARTE 3: EXCLUIR TABELAS ANTIGAS
-- ============================================================
-- ATENÇÃO: Execute esta parte apenas após verificar que a migração foi bem-sucedida!

-- Remover triggers antigos
DROP TRIGGER IF EXISTS update_purchases_updated_at ON public.purchases;

-- Remover funções antigas (se não estiverem sendo usadas por outras tabelas)
-- DROP FUNCTION IF EXISTS update_updated_at_column();

-- Excluir tabelas antigas
DROP TABLE IF EXISTS public.credit_usage CASCADE;
DROP TABLE IF EXISTS public.purchases CASCADE;
DROP TABLE IF EXISTS public.user_profiles CASCADE;

-- ============================================================
-- VERIFICAÇÃO FINAL
-- ============================================================

-- Verifica estrutura das novas tabelas
SELECT 
    'perfis_usuarios' as tabela,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'perfis_usuarios'
ORDER BY ordinal_position;

SELECT 
    'compras' as tabela,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'compras'
ORDER BY ordinal_position;

SELECT 
    'uso_creditos' as tabela,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'uso_creditos'
ORDER BY ordinal_position;

-- Conta registros migrados
SELECT 
    (SELECT COUNT(*) FROM public.perfis_usuarios) as total_perfis,
    (SELECT COUNT(*) FROM public.compras) as total_compras,
    (SELECT COUNT(*) FROM public.uso_creditos) as total_uso_creditos;

