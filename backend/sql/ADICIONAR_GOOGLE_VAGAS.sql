-- Google Vagas: agrega oportunidades de LinkedIn, Indeed, Catho, Gupy e outros portais
INSERT INTO public.sites_vagas (nome, url_base, ativo, palavras_chave_padrao, caracteristicas)
VALUES
  (
    'Google Vagas',
    'https://www.google.com/search?ibp=htl;jobs',
    TRUE,
    '["vagas", "emprego", "oportunidades", "carreira", "trabalho"]'::JSONB,
    '{"foco": "agregador multi-portais", "formato": "google jobs", "destaque": "vagas de diversos sites em um só lugar"}'::JSONB
  )
ON CONFLICT (nome) DO NOTHING;
