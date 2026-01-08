-- ============================================================
-- ESTRUTURA MELHORADA: CRÉDITOS INDIVIDUAIS POR COMPRA
-- ============================================================
-- Nova estrutura:
-- 1. compras: detalhes da compra (1 linha por compra)
-- 2. creditos: cada crédito é uma linha, vinculada à compra
-- 3. Quando usar crédito, marca como usado na tabela creditos
-- ============================================================

-- ============================================================
-- PARTE 1: CRIAR/ATUALIZAR TABELAS
-- ============================================================

-- Tabela de perfis de usuários (já existe, apenas verifica)
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

-- Tabela de compras/transações (já existe, apenas verifica)
CREATE TABLE IF NOT EXISTS public.compras (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_usuario TEXT NOT NULL,
  id_plano TEXT NOT NULL,
  nome_plano TEXT NOT NULL,
  quantidade_creditos INTEGER NOT NULL, -- Quantidade de créditos comprados nesta transação
  preco DECIMAL(10, 2) NOT NULL,
  moeda TEXT DEFAULT 'BRL',
  status TEXT NOT NULL DEFAULT 'concluida', -- concluida, pendente, cancelada, reembolsada
  metodo_pagamento TEXT DEFAULT 'mock', -- mock, stripe, pix, etc
  id_pagamento TEXT, -- ID da transação no gateway de pagamento (se houver)
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- NOVA TABELA: Créditos individuais (1 linha por crédito)
CREATE TABLE IF NOT EXISTS public.creditos (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_compra TEXT NOT NULL, -- Vinculado à compra
  id_usuario TEXT NOT NULL, -- Para facilitar consultas
  usado BOOLEAN DEFAULT FALSE, -- Se o crédito já foi usado
  usado_em TIMESTAMP WITH TIME ZONE, -- Quando foi usado
  tipo_acao TEXT, -- 'analise' ou 'geracao_pdf' (quando usado)
  nome_arquivo_curriculo TEXT, -- Nome do arquivo quando usado
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Índices para melhor performance
CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_email ON public.perfis_usuarios(email);
CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_tipo_usuario ON public.perfis_usuarios(tipo_usuario);
CREATE INDEX IF NOT EXISTS idx_perfis_usuarios_criado_em ON public.perfis_usuarios(criado_em);

CREATE INDEX IF NOT EXISTS idx_compras_id_usuario ON public.compras(id_usuario);
CREATE INDEX IF NOT EXISTS idx_compras_criado_em ON public.compras(criado_em);
CREATE INDEX IF NOT EXISTS idx_compras_status ON public.compras(status);

CREATE INDEX IF NOT EXISTS idx_creditos_id_compra ON public.creditos(id_compra);
CREATE INDEX IF NOT EXISTS idx_creditos_id_usuario ON public.creditos(id_usuario);
CREATE INDEX IF NOT EXISTS idx_creditos_usado ON public.creditos(usado);
CREATE INDEX IF NOT EXISTS idx_creditos_criado_em ON public.creditos(criado_em);

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
ALTER TABLE public.creditos DISABLE ROW LEVEL SECURITY;

-- ============================================================
-- PARTE 2: MIGRAR DADOS EXISTENTES (se houver)
-- ============================================================

-- Se já existir tabela uso_creditos, migra para creditos
DO $$
DECLARE
    usage_record RECORD;
    compra_record RECORD;
    creditos_criados INTEGER;
BEGIN
    -- Para cada registro em uso_creditos, cria créditos correspondentes
    FOR usage_record IN 
        SELECT * FROM public.uso_creditos WHERE EXISTS (
            SELECT 1 FROM information_schema.tables 
            WHERE table_schema = 'public' 
            AND table_name = 'uso_creditos'
        )
    LOOP
        -- Tenta encontrar uma compra relacionada (pela data ou cria uma genérica)
        SELECT id INTO compra_record 
        FROM public.compras 
        WHERE id_usuario = usage_record.id_usuario 
        AND criado_em <= usage_record.criado_em
        ORDER BY criado_em DESC
        LIMIT 1;
        
        -- Se não encontrou compra, cria uma genérica para histórico
        IF compra_record IS NULL THEN
            INSERT INTO public.compras (
                id, id_usuario, id_plano, nome_plano, 
                quantidade_creditos, preco, status, criado_em
            ) VALUES (
                gen_random_uuid()::TEXT,
                usage_record.id_usuario,
                'historico',
                'Migração de histórico',
                1,
                0.00,
                'concluida',
                usage_record.criado_em
            ) RETURNING id INTO compra_record;
        END IF;
        
        -- Cria créditos usados
        FOR i IN 1..usage_record.creditos_usados LOOP
            INSERT INTO public.creditos (
                id_compra,
                id_usuario,
                usado,
                usado_em,
                tipo_acao,
                nome_arquivo_curriculo,
                criado_em
            ) VALUES (
                compra_record.id,
                usage_record.id_usuario,
                TRUE,
                usage_record.criado_em,
                usage_record.tipo_acao,
                usage_record.nome_arquivo_curriculo,
                usage_record.criado_em
            ) ON CONFLICT DO NOTHING;
        END LOOP;
    END LOOP;
END $$;

-- Migra compras existentes criando créditos correspondentes
DO $$
DECLARE
    compra_record RECORD;
    i INTEGER;
BEGIN
    FOR compra_record IN 
        SELECT * FROM public.compras 
        WHERE NOT EXISTS (
            SELECT 1 FROM public.creditos 
            WHERE id_compra = compras.id
        )
    LOOP
        -- Cria N créditos para esta compra
        FOR i IN 1..compra_record.quantidade_creditos LOOP
            INSERT INTO public.creditos (
                id_compra,
                id_usuario,
                usado,
                criado_em
            ) VALUES (
                compra_record.id,
                compra_record.id_usuario,
                FALSE, -- Créditos não usados ainda
                compra_record.criado_em
            ) ON CONFLICT DO NOTHING;
        END LOOP;
    END LOOP;
END $$;

-- ============================================================
-- PARTE 3: REMOVER TABELA ANTIGA (se existir)
-- ============================================================
-- ATENÇÃO: Execute apenas após verificar que a migração foi bem-sucedida!

DROP TABLE IF EXISTS public.uso_creditos CASCADE;

-- ============================================================
-- VERIFICAÇÃO FINAL
-- ============================================================

-- Verifica estrutura das tabelas
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
    'creditos' as tabela,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'creditos'
ORDER BY ordinal_position;

-- Estatísticas
SELECT 
    (SELECT COUNT(*) FROM public.perfis_usuarios) as total_perfis,
    (SELECT COUNT(*) FROM public.compras) as total_compras,
    (SELECT COUNT(*) FROM public.creditos) as total_creditos,
    (SELECT COUNT(*) FROM public.creditos WHERE usado = FALSE) as creditos_disponiveis,
    (SELECT COUNT(*) FROM public.creditos WHERE usado = TRUE) as creditos_usados;

