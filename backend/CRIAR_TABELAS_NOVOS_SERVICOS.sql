-- ============================================================
-- CRIAÇÃO DAS TABELAS PARA OS NOVOS SERVIÇOS
-- ============================================================
-- Este script cria todas as tabelas necessárias para:
-- - Sites de vagas
-- - Importação de currículos
-- - Análises de currículo
-- - Cartas de apresentação
-- - Currículos otimizados
-- - Busca de vagas
-- - Simulação de entrevista
-- ============================================================

-- ============================================================
-- 1. TABELA: sites_vagas
-- ============================================================
CREATE TABLE IF NOT EXISTS public.sites_vagas (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  nome TEXT NOT NULL UNIQUE,
  url_base TEXT NOT NULL,
  ativo BOOLEAN DEFAULT TRUE,
  palavras_chave_padrao JSONB DEFAULT '[]'::JSONB,
  caracteristicas JSONB DEFAULT '{}'::JSONB,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Índices para sites_vagas
CREATE INDEX IF NOT EXISTS idx_sites_vagas_ativo ON public.sites_vagas(ativo);
CREATE INDEX IF NOT EXISTS idx_sites_vagas_nome ON public.sites_vagas(nome);

-- ============================================================
-- 2. TABELA: curriculos_importados
-- ============================================================
CREATE TABLE IF NOT EXISTS public.curriculos_importados (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_usuario TEXT NOT NULL,
  id_site_vagas TEXT NOT NULL,
  nome_arquivo_original TEXT NOT NULL,
  tipo_arquivo TEXT NOT NULL,
  caminho_arquivo TEXT NOT NULL,
  conteudo_extraido TEXT,
  dados_estruturados JSONB DEFAULT '{}'::JSONB,
  id_credito_usado TEXT,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  FOREIGN KEY (id_usuario) REFERENCES perfis_usuarios(id) ON DELETE CASCADE,
  FOREIGN KEY (id_site_vagas) REFERENCES sites_vagas(id),
  FOREIGN KEY (id_credito_usado) REFERENCES creditos(id)
);

-- Índices para curriculos_importados
CREATE INDEX IF NOT EXISTS idx_curriculos_id_usuario ON public.curriculos_importados(id_usuario);
CREATE INDEX IF NOT EXISTS idx_curriculos_id_site_vagas ON public.curriculos_importados(id_site_vagas);
CREATE INDEX IF NOT EXISTS idx_curriculos_criado_em ON public.curriculos_importados(criado_em);

-- ============================================================
-- 3. TABELA: analises_curriculo
-- ============================================================
-- NOTA: Não precisa de id_credito_usado aqui, pois o crédito
-- já está vinculado ao currículo importado. A análise faz
-- parte do pacote do crédito.
CREATE TABLE IF NOT EXISTS public.analises_curriculo (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_curriculo TEXT NOT NULL,
  id_usuario TEXT NOT NULL,
  id_site_vagas TEXT NOT NULL,
  score_geral INTEGER,
  pontos_fortes JSONB DEFAULT '[]'::JSONB,
  pontos_melhorar JSONB DEFAULT '[]'::JSONB,
  palavras_chave_sugeridas JSONB DEFAULT '[]'::JSONB,
  recomendacoes JSONB DEFAULT '[]'::JSONB,
  resultado_completo JSONB DEFAULT '{}'::JSONB,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  FOREIGN KEY (id_curriculo) REFERENCES curriculos_importados(id) ON DELETE CASCADE,
  FOREIGN KEY (id_usuario) REFERENCES perfis_usuarios(id) ON DELETE CASCADE,
  FOREIGN KEY (id_site_vagas) REFERENCES sites_vagas(id)
);

-- Índices para analises_curriculo
CREATE INDEX IF NOT EXISTS idx_analises_id_curriculo ON public.analises_curriculo(id_curriculo);
CREATE INDEX IF NOT EXISTS idx_analises_id_usuario ON public.analises_curriculo(id_usuario);
CREATE INDEX IF NOT EXISTS idx_analises_id_site_vagas ON public.analises_curriculo(id_site_vagas);
CREATE INDEX IF NOT EXISTS idx_analises_criado_em ON public.analises_curriculo(criado_em);

-- ============================================================
-- 4. TABELA: cartas_apresentacao
-- ============================================================
-- NOTA: Não precisa de id_credito_usado aqui, pois o crédito
-- já está vinculado ao currículo importado. A carta faz
-- parte do pacote do crédito.
--
-- palavras_chave_usadas: Armazena as palavras-chave utilizadas
-- na geração da carta. Vêm de:
-- - Análise do site de vagas escolhido
-- - Análise do currículo do usuário
-- - Análise da vaga específica (se vinculada)
-- - Palavras-chave padrão do site
-- Formato: Array de strings ou objetos com detalhes
CREATE TABLE IF NOT EXISTS public.cartas_apresentacao (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_curriculo TEXT NOT NULL,
  id_usuario TEXT NOT NULL,
  id_site_vagas TEXT NOT NULL,
  id_vaga TEXT,
  titulo TEXT,
  conteudo TEXT NOT NULL,
  palavras_chave_usadas JSONB DEFAULT '[]'::JSONB, -- Palavras-chave utilizadas na geração
  caminho_arquivo_pdf TEXT,
  caminho_arquivo_word TEXT,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  FOREIGN KEY (id_curriculo) REFERENCES curriculos_importados(id) ON DELETE CASCADE,
  FOREIGN KEY (id_usuario) REFERENCES perfis_usuarios(id) ON DELETE CASCADE,
  FOREIGN KEY (id_site_vagas) REFERENCES sites_vagas(id)
);

-- Índices para cartas_apresentacao
CREATE INDEX IF NOT EXISTS idx_cartas_id_curriculo ON public.cartas_apresentacao(id_curriculo);
CREATE INDEX IF NOT EXISTS idx_cartas_id_usuario ON public.cartas_apresentacao(id_usuario);
CREATE INDEX IF NOT EXISTS idx_cartas_id_site_vagas ON public.cartas_apresentacao(id_site_vagas);
CREATE INDEX IF NOT EXISTS idx_cartas_criado_em ON public.cartas_apresentacao(criado_em);

-- ============================================================
-- 5. TABELA: curriculos_otimizados
-- ============================================================
-- NOTA: Não precisa de id_credito_usado aqui, pois o crédito
-- já está vinculado ao currículo importado. A otimização faz
-- parte do pacote do crédito.
--
-- palavras_chave_aplicadas: Armazena as palavras-chave que
-- foram aplicadas na otimização do currículo. Vêm de:
-- - Análise do site de vagas escolhido (palavras-chave padrão)
-- - Análise do currículo original (palavras-chave sugeridas)
-- - Análise de vagas similares no site escolhido
-- - Palavras-chave estratégicas identificadas pela IA
-- Formato: Array de strings ou objetos com detalhes
--
-- alteracoes_realizadas: Resumo das alterações feitas no currículo
-- Formato: Objeto JSON com secoes_modificadas, palavras_adicionadas, etc.
CREATE TABLE IF NOT EXISTS public.curriculos_otimizados (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_curriculo_original TEXT NOT NULL,
  id_usuario TEXT NOT NULL,
  id_site_vagas TEXT NOT NULL,
  palavras_chave_aplicadas JSONB DEFAULT '[]'::JSONB, -- Palavras-chave aplicadas na otimização
  alteracoes_realizadas JSONB DEFAULT '{}'::JSONB, -- Resumo das alterações realizadas
  caminho_arquivo_pdf TEXT NOT NULL,
  caminho_arquivo_word TEXT NOT NULL,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  FOREIGN KEY (id_curriculo_original) REFERENCES curriculos_importados(id) ON DELETE CASCADE,
  FOREIGN KEY (id_usuario) REFERENCES perfis_usuarios(id) ON DELETE CASCADE,
  FOREIGN KEY (id_site_vagas) REFERENCES sites_vagas(id)
);

-- Índices para curriculos_otimizados
CREATE INDEX IF NOT EXISTS idx_curriculos_otimizados_id_curriculo ON public.curriculos_otimizados(id_curriculo_original);
CREATE INDEX IF NOT EXISTS idx_curriculos_otimizados_id_usuario ON public.curriculos_otimizados(id_usuario);
CREATE INDEX IF NOT EXISTS idx_curriculos_otimizados_id_site_vagas ON public.curriculos_otimizados(id_site_vagas);
CREATE INDEX IF NOT EXISTS idx_curriculos_otimizados_criado_em ON public.curriculos_otimizados(criado_em);

-- ============================================================
-- 6. TABELA: vagas_encontradas
-- ============================================================
CREATE TABLE IF NOT EXISTS public.vagas_encontradas (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_curriculo TEXT NOT NULL,
  id_usuario TEXT NOT NULL,
  id_site_vagas TEXT NOT NULL,
  titulo_vaga TEXT NOT NULL,
  empresa TEXT,
  localizacao TEXT,
  url_vaga TEXT NOT NULL,
  descricao_vaga TEXT,
  requisitos JSONB DEFAULT '[]'::JSONB,
  score_compatibilidade INTEGER,
  palavras_chave_match JSONB DEFAULT '[]'::JSONB,
  dados_completos JSONB DEFAULT '{}'::JSONB,
  status TEXT DEFAULT 'ativa',
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  FOREIGN KEY (id_curriculo) REFERENCES curriculos_importados(id) ON DELETE CASCADE,
  FOREIGN KEY (id_usuario) REFERENCES perfis_usuarios(id) ON DELETE CASCADE,
  FOREIGN KEY (id_site_vagas) REFERENCES sites_vagas(id)
);

-- Índices para vagas_encontradas
CREATE INDEX IF NOT EXISTS idx_vagas_id_curriculo ON public.vagas_encontradas(id_curriculo);
CREATE INDEX IF NOT EXISTS idx_vagas_id_usuario ON public.vagas_encontradas(id_usuario);
CREATE INDEX IF NOT EXISTS idx_vagas_id_site_vagas ON public.vagas_encontradas(id_site_vagas);
CREATE INDEX IF NOT EXISTS idx_vagas_score_compatibilidade ON public.vagas_encontradas(score_compatibilidade);
CREATE INDEX IF NOT EXISTS idx_vagas_status ON public.vagas_encontradas(status);
CREATE INDEX IF NOT EXISTS idx_vagas_criado_em ON public.vagas_encontradas(criado_em);

-- ============================================================
-- 7. TABELA: simulacoes_entrevista
-- ============================================================
-- NOTA: Não precisa de id_credito_usado aqui, pois o crédito
-- já está vinculado ao currículo importado. A simulação faz
-- parte do pacote do crédito.
CREATE TABLE IF NOT EXISTS public.simulacoes_entrevista (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_curriculo TEXT NOT NULL,
  id_usuario TEXT NOT NULL,
  id_site_vagas TEXT NOT NULL,
  titulo TEXT,
  area_foco TEXT,
  perguntas_feitas JSONB DEFAULT '[]'::JSONB,
  respostas_dadas JSONB DEFAULT '[]'::JSONB,
  feedback_geral JSONB DEFAULT '{}'::JSONB,
  score_geral INTEGER,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  atualizado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  FOREIGN KEY (id_curriculo) REFERENCES curriculos_importados(id) ON DELETE CASCADE,
  FOREIGN KEY (id_usuario) REFERENCES perfis_usuarios(id) ON DELETE CASCADE,
  FOREIGN KEY (id_site_vagas) REFERENCES sites_vagas(id)
);

-- Índices para simulacoes_entrevista
CREATE INDEX IF NOT EXISTS idx_simulacoes_id_curriculo ON public.simulacoes_entrevista(id_curriculo);
CREATE INDEX IF NOT EXISTS idx_simulacoes_id_usuario ON public.simulacoes_entrevista(id_usuario);
CREATE INDEX IF NOT EXISTS idx_simulacoes_id_site_vagas ON public.simulacoes_entrevista(id_site_vagas);
CREATE INDEX IF NOT EXISTS idx_simulacoes_criado_em ON public.simulacoes_entrevista(criado_em);

-- ============================================================
-- 8. TABELA: mensagens_entrevista
-- ============================================================
CREATE TABLE IF NOT EXISTS public.mensagens_entrevista (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::TEXT,
  id_simulacao TEXT NOT NULL,
  tipo TEXT NOT NULL,
  conteudo TEXT NOT NULL,
  feedback TEXT,
  ordem INTEGER NOT NULL,
  criado_em TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
  FOREIGN KEY (id_simulacao) REFERENCES simulacoes_entrevista(id) ON DELETE CASCADE
);

-- Índices para mensagens_entrevista
CREATE INDEX IF NOT EXISTS idx_mensagens_id_simulacao ON public.mensagens_entrevista(id_simulacao);
CREATE INDEX IF NOT EXISTS idx_mensagens_ordem ON public.mensagens_entrevista(id_simulacao, ordem);

-- ============================================================
-- INSERIR SITES DE VAGAS INICIAIS
-- ============================================================
INSERT INTO public.sites_vagas (nome, url_base, ativo, palavras_chave_padrao, caracteristicas)
VALUES
  (
    'Gupy',
    'https://www.gupy.io',
    TRUE,
    '["tecnologia", "startup", "inovação", "agilidade", "resultados", "impacto"]'::JSONB,
    '{"foco": "tecnologia", "formato": "ATS-friendly", "destaque": "resultados quantificados"}'::JSONB
  ),
  (
    'LinkedIn',
    'https://www.linkedin.com/jobs',
    TRUE,
    '["networking", "carreira", "profissional", "experiência", "conquistas", "recomendações"]'::JSONB,
    '{"foco": "storytelling", "formato": "perfil profissional", "destaque": "histórico de carreira"}'::JSONB
  ),
  (
    'Vagas.com',
    'https://www.vagas.com.br',
    TRUE,
    '["experiência", "formação", "competências", "realizações", "objetivos"]'::JSONB,
    '{"foco": "descrições detalhadas", "formato": "tradicional", "destaque": "experiência quantificada"}'::JSONB
  ),
  (
    'InfoJobs',
    'https://www.infojobs.com.br',
    TRUE,
    '["qualificações", "habilidades", "experiência profissional", "formação acadêmica"]'::JSONB,
    '{"foco": "qualificações", "formato": "estruturado", "destaque": "formação e experiência"}'::JSONB
  ),
  (
    'Catho',
    'https://www.catho.com.br',
    TRUE,
    '["competências", "realizações", "objetivo profissional", "formação"]'::JSONB,
    '{"foco": "objetivo profissional", "formato": "tradicional", "destaque": "competências e realizações"}'::JSONB
  ),
  (
    'Indeed',
    'https://br.indeed.com',
    TRUE,
    '["skills", "experience", "education", "achievements", "keywords"]'::JSONB,
    '{"foco": "palavras-chave", "formato": "ATS-friendly", "destaque": "keywords relevantes"}'::JSONB
  )
ON CONFLICT (nome) DO NOTHING;

-- ============================================================
-- ATUALIZAR TABELA creditos PARA SUPORTAR NOVO TIPO DE AÇÃO
-- ============================================================
-- Adicionar campo para rastrear qual site foi usado
ALTER TABLE public.creditos 
ADD COLUMN IF NOT EXISTS id_site_vagas TEXT;

-- Criar índice para melhor performance
CREATE INDEX IF NOT EXISTS idx_creditos_id_site_vagas ON public.creditos(id_site_vagas);

-- Adicionar foreign key (opcional, mas recomendado)
-- ALTER TABLE public.creditos 
-- ADD CONSTRAINT fk_creditos_site_vagas 
-- FOREIGN KEY (id_site_vagas) REFERENCES sites_vagas(id);

-- ============================================================
-- DOCUMENTAÇÃO DO TIPO DE AÇÃO
-- ============================================================
-- Tipo de ação: 'pacote_site_vagas'
-- 
-- 1 crédito = 1 pacote completo de serviços para 1 site de vagas
-- O crédito é usado quando o usuário importa o currículo para um site
-- Com esse crédito, o usuário tem acesso a:
-- - Análise de currículo
-- - Carta de apresentação
-- - Otimização de currículo
-- - Busca de vagas
-- - Simulação de entrevista
-- 
-- Tudo isso com 1 crédito para o site escolhido!
-- Se quiser fazer para outro site, precisa de outro crédito.
-- 
-- O campo id_site_vagas na tabela creditos permite rastrear
-- qual site foi usado com cada crédito.

-- ============================================================
-- COMENTÁRIOS E DOCUMENTAÇÃO
-- ============================================================
COMMENT ON TABLE public.sites_vagas IS 'Armazena os sites de vagas disponíveis no sistema';
COMMENT ON TABLE public.curriculos_importados IS 'Armazena os currículos importados pelos usuários';
COMMENT ON TABLE public.analises_curriculo IS 'Armazena as análises realizadas nos currículos';
COMMENT ON TABLE public.cartas_apresentacao IS 'Armazena as cartas de apresentação geradas';
COMMENT ON TABLE public.curriculos_otimizados IS 'Armazena os currículos refeitos/otimizados';
COMMENT ON TABLE public.vagas_encontradas IS 'Armazena as vagas encontradas pelo robô de busca';
COMMENT ON TABLE public.simulacoes_entrevista IS 'Armazena as simulações de entrevista realizadas';
COMMENT ON TABLE public.mensagens_entrevista IS 'Armazena as mensagens do chat de entrevista';
