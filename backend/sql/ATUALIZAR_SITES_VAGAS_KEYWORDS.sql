-- Garante coluna descricao (instalações antigas não tinham esse campo)
ALTER TABLE public.sites_vagas
  ADD COLUMN IF NOT EXISTS descricao TEXT;

-- Atualiza descrições e palavras-chave técnicas dos portais para geração de currículo
UPDATE public.sites_vagas SET
  descricao = 'Portal tech com ATS rigoroso; priorize keywords técnicas, resultados quantificados e formato limpo.',
  palavras_chave_padrao = '["resultados mensuráveis", "tecnologias", "metodologias ágeis", "KPIs", "automação", "dados", "inovação", "impacto"]'::JSONB,
  caracteristicas = '{"foco": "tecnologia e ATS", "formato": "ATS-friendly", "destaque": "métricas e stack técnica"}'::JSONB,
  atualizado_em = NOW()
WHERE nome = 'Gupy';

UPDATE public.sites_vagas SET
  descricao = 'Rede profissional; resumo narrativo, progressão de carreira e conquistas com contexto.',
  palavras_chave_padrao = '["liderança", "colaboração", "estratégia", "crescimento", "impacto", "stakeholders", "inovação", "resultados"]'::JSONB,
  caracteristicas = '{"foco": "storytelling profissional", "formato": "perfil narrativo", "destaque": "progressão e conquistas"}'::JSONB,
  atualizado_em = NOW()
WHERE nome = 'LinkedIn';

UPDATE public.sites_vagas SET
  descricao = 'Portal tradicional brasileiro; estrutura formal, formação e experiência detalhada.',
  palavras_chave_padrao = '["qualificações", "competências técnicas", "experiência comprovada", "formação acadêmica", "certificações", "habilidades"]'::JSONB,
  caracteristicas = '{"foco": "qualificações", "formato": "estruturado", "destaque": "formação e experiência detalhada"}'::JSONB,
  atualizado_em = NOW()
WHERE nome = 'InfoJobs';

UPDATE public.sites_vagas SET
  descricao = 'Agregador multi-portais; máxima compatibilidade ATS e keywords distribuídas.',
  palavras_chave_padrao = '["ATS", "keywords", "experiência", "habilidades", "formação", "certificações", "resultados"]'::JSONB,
  caracteristicas = '{"foco": "compatibilidade ATS", "formato": "limpo e padronizado", "destaque": "keywords técnicas"}'::JSONB,
  atualizado_em = NOW()
WHERE nome = 'Indeed';

UPDATE public.sites_vagas SET
  descricao = 'Portal tradicional; experiência quantificada e objetivo profissional claro.',
  palavras_chave_padrao = '["experiência", "realizações", "competências", "objetivo profissional", "formação", "resultados"]'::JSONB,
  atualizado_em = NOW()
WHERE nome IN ('Vagas.com', 'Catho', 'Empregos.com.br', 'Trabalhar Brasil');
