-- Adiciona coluna para armazenar arquivo em base64
ALTER TABLE public.curriculos_importados
ADD COLUMN IF NOT EXISTS arquivo_base64 TEXT;

-- Índice para busca (opcional)
CREATE INDEX IF NOT EXISTS idx_curriculos_arquivo_base64 
ON public.curriculos_importados(id) 
WHERE arquivo_base64 IS NOT NULL;
