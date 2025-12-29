-- Script para criar tabela de compras/transações
-- Execute este SQL no SQL Editor do Supabase

-- Tabela de compras/transações
CREATE TABLE IF NOT EXISTS public.purchases (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  user_id TEXT NOT NULL,
  plan_id TEXT NOT NULL,
  plan_name TEXT NOT NULL,
  credits_amount INTEGER NOT NULL,
  price DECIMAL(10, 2) NOT NULL,
  currency TEXT DEFAULT 'BRL',
  status TEXT NOT NULL DEFAULT 'completed', -- completed, pending, cancelled, refunded
  payment_method TEXT DEFAULT 'mock', -- mock, stripe, pix, etc
  payment_id TEXT, -- ID da transação no gateway de pagamento (se houver)
  created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
  -- Nota: Foreign key removida devido à incompatibilidade de tipos (user_profiles.id pode ser UUID ou TEXT)
  -- A integridade referencial será mantida via aplicação
);

-- Índices para melhor performance
CREATE INDEX IF NOT EXISTS idx_purchases_user_id ON public.purchases(user_id);
CREATE INDEX IF NOT EXISTS idx_purchases_created_at ON public.purchases(created_at);
CREATE INDEX IF NOT EXISTS idx_purchases_status ON public.purchases(status);

-- Tabela de histórico de uso de créditos
CREATE TABLE IF NOT EXISTS public.credit_usage (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  user_id TEXT NOT NULL,
  credits_used INTEGER NOT NULL DEFAULT 1,
  action_type TEXT NOT NULL, -- 'analysis', 'pdf_generation'
  resume_file_name TEXT,
  created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
  -- Nota: Foreign key removida devido à incompatibilidade de tipos (user_profiles.id pode ser UUID ou TEXT)
  -- A integridade referencial será mantida via aplicação
);

-- Índices para melhor performance
CREATE INDEX IF NOT EXISTS idx_credit_usage_user_id ON public.credit_usage(user_id);
CREATE INDEX IF NOT EXISTS idx_credit_usage_created_at ON public.credit_usage(created_at);

-- Função para atualizar updated_at automaticamente
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger para atualizar updated_at na tabela purchases
DROP TRIGGER IF EXISTS update_purchases_updated_at ON public.purchases;
CREATE TRIGGER update_purchases_updated_at
    BEFORE UPDATE ON public.purchases
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Desabilita RLS (Row Level Security) para permitir acesso via service role
ALTER TABLE public.purchases DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.credit_usage DISABLE ROW LEVEL SECURITY;

-- Verifica estrutura criada
SELECT 
    'purchases' as table_name,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'purchases'
ORDER BY ordinal_position;

SELECT 
    'credit_usage' as table_name,
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
  AND table_name = 'credit_usage'
ORDER BY ordinal_position;

