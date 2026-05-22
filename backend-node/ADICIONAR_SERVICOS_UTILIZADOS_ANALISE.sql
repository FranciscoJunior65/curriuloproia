-- Mesmo script do backend C# — executar no Supabase uma vez.
ALTER TABLE public.analises_curriculo
ADD COLUMN IF NOT EXISTS servicos_utilizados JSONB DEFAULT '{
  "analise": true,
  "carta_apresentacao": false,
  "curriculo_melhorado": false,
  "entrevista": false,
  "busca_vagas": false
}'::jsonb;

UPDATE public.analises_curriculo
SET servicos_utilizados = COALESCE(servicos_utilizados, '{}'::jsonb) || '{"analise": true}'::jsonb
WHERE servicos_utilizados IS NULL;
