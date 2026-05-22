-- Rastreia quais serviços inclusos em cada análise (1 crédito = 1 pacote) já foram utilizados.
-- O usuário pode concluir os serviços em momentos diferentes; pacotes distintos acumulam.

ALTER TABLE public.analises_curriculo
ADD COLUMN IF NOT EXISTS servicos_utilizados JSONB DEFAULT '{
  "analise": true,
  "carta_apresentacao": false,
  "curriculo_melhorado": false,
  "entrevista": false,
  "busca_vagas": false
}'::jsonb;

COMMENT ON COLUMN public.analises_curriculo.servicos_utilizados IS
  'Serviços do pacote pago: cada chave indica se o recurso já foi utilizado para esta análise/crédito.';

-- Análises antigas: análise sempre considerada feita
UPDATE public.analises_curriculo
SET servicos_utilizados = COALESCE(servicos_utilizados, '{}'::jsonb) || '{"analise": true}'::jsonb
WHERE servicos_utilizados IS NULL;
