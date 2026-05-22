using System.Text.Json;
using System.Text.Json.Serialization;

namespace CurriculosProIA.Domain.Dtos;

public class AnalysisInput
{
    [JsonPropertyName("pontosFortes")]
    public List<string>? PontosFortes { get; set; }

    [JsonPropertyName("pontosMelhorar")]
    public List<string>? PontosMelhorar { get; set; }

    [JsonPropertyName("experiencia")]
    public string? Experiencia { get; set; }

    [JsonPropertyName("formacao")]
    public string? Formacao { get; set; }

    [JsonPropertyName("habilidades")]
    public List<string>? Habilidades { get; set; }

    [JsonPropertyName("recomendacoes")]
    public List<string>? Recomendacoes { get; set; }

    [JsonPropertyName("score")]
    public int? Score { get; set; }

    [JsonPropertyName("areaAtuacao")]
    public string? AreaAtuacao { get; set; }
}

public class InterviewEvaluation
{
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = string.Empty;

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [JsonPropertyName("improvements")]
    public List<string> Improvements { get; set; } = new();
}

public class InterviewAnswerItem
{
    [JsonPropertyName("evaluation")]
    public InterviewEvaluation? Evaluation { get; set; }
}

public class JobSearchResult
{
    [JsonPropertyName("site")]
    public string Site { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("jobs")]
    public List<JobListing> Jobs { get; set; } = new();

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("searchTerms")]
    public List<string>? SearchTerms { get; set; }

    [JsonPropertyName("totalFound")]
    public int? TotalFound { get; set; }

    [JsonPropertyName("searchKeywords")]
    public List<string>? SearchKeywords { get; set; }

    [JsonPropertyName("searchCombinations")]
    public int? SearchCombinations { get; set; }
}

public class JobListing
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("company")]
    public string Company { get; set; } = "Não informado";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "Não informado";

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("site")]
    public string? Site { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("requirements")]
    public List<string>? Requirements { get; set; }

    [JsonPropertyName("salary")]
    public string? Salary { get; set; }

    [JsonPropertyName("contractType")]
    public string? ContractType { get; set; }

    [JsonPropertyName("experienceLevel")]
    public string? ExperienceLevel { get; set; }

    [JsonPropertyName("compatibilityScore")]
    public int? CompatibilityScore { get; set; }

    [JsonPropertyName("matchedKeywords")]
    public List<string>? MatchedKeywords { get; set; }
}

public class AnaliseCurriculoListItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("id_curriculo")]
    public string? IdCurriculo { get; set; }

    [JsonPropertyName("id_usuario")]
    public string? IdUsuario { get; set; }

    [JsonPropertyName("id_site_vagas")]
    public string? IdSiteVagas { get; set; }

    [JsonPropertyName("score_geral")]
    public int? ScoreGeral { get; set; }

    [JsonPropertyName("pontos_fortes")]
    public List<string>? PontosFortes { get; set; }

    [JsonPropertyName("pontos_melhorar")]
    public List<string>? PontosMelhorar { get; set; }

    [JsonPropertyName("palavras_chave_sugeridas")]
    public List<string>? PalavrasChaveSugeridas { get; set; }

    [JsonPropertyName("recomendacoes")]
    public List<string>? Recomendacoes { get; set; }

    [JsonPropertyName("resultado_completo")]
    public JsonElement? ResultadoCompleto { get; set; }

    [JsonPropertyName("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }

    [JsonPropertyName("curriculos_importados")]
    public CurriculoImportadoRefDto? CurriculosImportados { get; set; }

    [JsonPropertyName("sites_vagas")]
    public SiteVagasRefDto? SitesVagas { get; set; }

    [JsonPropertyName("servicos")]
    public AnalysisServicesStatusDto? Servicos { get; set; }
}

public class CurriculoImportadoRefDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nome_arquivo_original")]
    public string? NomeArquivoOriginal { get; set; }

    [JsonPropertyName("tipo_arquivo")]
    public string? TipoArquivo { get; set; }

    [JsonPropertyName("conteudo_extraido")]
    public string? ConteudoExtraido { get; set; }

    [JsonPropertyName("dados_estruturados")]
    public JsonElement? DadosEstruturados { get; set; }

    [JsonPropertyName("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }
}

public class SiteVagasRefDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("url_base")]
    public string? UrlBase { get; set; }
}

public class InterviewDetailDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("id_curriculo")]
    public string? IdCurriculo { get; set; }

    [JsonPropertyName("id_usuario")]
    public string? IdUsuario { get; set; }

    [JsonPropertyName("id_site_vagas")]
    public string? IdSiteVagas { get; set; }

    [JsonPropertyName("titulo")]
    public string? Titulo { get; set; }

    [JsonPropertyName("area_foco")]
    public string? AreaFoco { get; set; }

    [JsonPropertyName("perguntas_feitas")]
    public List<string>? PerguntasFeitas { get; set; }

    [JsonPropertyName("respostas_dadas")]
    public JsonElement? RespostasDadas { get; set; }

    [JsonPropertyName("feedback_geral")]
    public JsonElement? FeedbackGeral { get; set; }

    [JsonPropertyName("score_geral")]
    public int? ScoreGeral { get; set; }

    [JsonPropertyName("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }

    [JsonPropertyName("atualizado_em")]
    public DateTimeOffset? AtualizadoEm { get; set; }

    [JsonPropertyName("messages")]
    public List<InterviewMessageDto> Messages { get; set; } = new();
}

public class FinishInterviewResult
{
    public int AverageScore { get; set; }
}


public class InterviewMessageDto
{
    public string? Id { get; set; }
    public string? IdSimulacao { get; set; }
    public string? Tipo { get; set; }
    public string? Conteudo { get; set; }
    public string? Feedback { get; set; }
    public int Ordem { get; set; }
    public Dictionary<string, object?>? DadosExtras { get; set; }
    public DateTimeOffset? CriadoEm { get; set; }
}

/// <summary>DTO serializável para listagem de sites (evita metadados Postgrest no JSON).</summary>
public class JobSiteListItemDto
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? UrlBase { get; set; }
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
}
