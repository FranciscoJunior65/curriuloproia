/**
 * Mapeia análise persistida (histórico/API) para o formato usado pelos serviços no analyzer.
 */

interface ResultadoCompletoParsed {
  habilidades?: string[];
  experiencia?: string;
  Experiencia?: string;
  formacao?: string;
  Formacao?: string;
  score?: number;
  areaAtuacao?: string;
  area_atuacao?: string;
}

export function mapPersistedAnalysisToResult(analysis: any): {
  originalText: string;
  analysis: {
    pontosFortes: string[];
    pontosMelhorar: string[];
    experiencia: string;
    formacao: string;
    habilidades: string[];
    recomendacoes: string[];
    score: number;
    areaAtuacao?: string;
  };
  resumeId?: string;
  analysisId?: string;
} {
  const rc = parseResultadoCompleto(analysis?.resultado_completo ?? analysis?.resultadoCompleto);

  const habilidadesFromRc =
    rc?.habilidades && Array.isArray(rc.habilidades) && rc.habilidades.length > 0
      ? rc.habilidades
      : null;

  const habilidades =
    habilidadesFromRc ??
    analysis?.palavras_chave_sugeridas ??
    analysis?.palavrasChaveSugeridas ??
    [];

  const pontosFortes = analysis?.pontos_fortes ?? analysis?.pontosFortes ?? [];
  let experiencia = rc?.experiencia ?? rc?.Experiencia ?? '';
  if (!experiencia && pontosFortes.length > 0) {
    experiencia = pontosFortes.slice(0, 5).join('. ');
  }

  const originalText =
    analysis?.curriculos_importados?.conteudo_extraido ??
    analysis?.curriculosImportados?.conteudoExtraido ??
    analysis?.originalText ??
    analysis?.original_text ??
    '';

  return {
    originalText,
    analysis: {
      pontosFortes,
      pontosMelhorar: analysis?.pontos_melhorar ?? analysis?.pontosMelhorar ?? [],
      experiencia,
      formacao: rc?.formacao ?? rc?.Formacao ?? '',
      habilidades: Array.isArray(habilidades) ? habilidades : [],
      recomendacoes: analysis?.recomendacoes ?? [],
      score: analysis?.score_geral ?? analysis?.scoreGeral ?? rc?.score ?? 0,
      areaAtuacao: rc?.areaAtuacao ?? rc?.area_atuacao
    },
    resumeId: analysis?.id_curriculo ?? analysis?.idCurriculo,
    analysisId: analysis?.id
  };
}

function parseResultadoCompleto(value: unknown): ResultadoCompletoParsed | null {
  if (!value) {
    return null;
  }
  if (typeof value === 'string') {
    try {
      return JSON.parse(value) as ResultadoCompletoParsed;
    } catch {
      return null;
    }
  }
  if (typeof value === 'object') {
    return value as ResultadoCompletoParsed;
  }
  return null;
}
