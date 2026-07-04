-- Adiciona Trabalhar Brasil e Empregos.com.br à lista de sites de vagas
INSERT INTO public.sites_vagas (nome, url_base, ativo, palavras_chave_padrao, caracteristicas)
VALUES
  (
    'Trabalhar Brasil',
    'https://www.trabalhabrasil.com.br',
    TRUE,
    '["experiência", "formação", "competências", "objetivos", "disponibilidade"]'::JSONB,
    '{"foco": "mercado brasileiro", "formato": "tradicional", "destaque": "clareza e objetividade"}'::JSONB
  ),
  (
    'Empregos.com.br',
    'https://www.empregos.com.br',
    TRUE,
    '["experiência", "habilidades", "formação", "realizações", "objetivos"]'::JSONB,
    '{"foco": "diversidade de vagas", "formato": "tradicional", "destaque": "experiência e competências"}'::JSONB
  )
ON CONFLICT (nome) DO NOTHING;
