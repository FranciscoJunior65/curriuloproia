using System.Text.Json;
using System.Text.Json.Serialization;
using Postgrest.Attributes;
using Postgrest.Models;

namespace CurriculosProIA.Repository.Persistence;

[Table("curriculos_importados")]
public class CurriculoImportadoRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("id_usuario")]
    public string? IdUsuario { get; set; }

    [Column("id_site_vagas")]
    public string? IdSiteVagas { get; set; }

    [Column("nome_arquivo_original")]
    public string? NomeArquivoOriginal { get; set; }

    [Column("tipo_arquivo")]
    public string? TipoArquivo { get; set; }

    [Column("caminho_arquivo")]
    public string? CaminhoArquivo { get; set; }

    [Column("conteudo_extraido")]
    public string? ConteudoExtraido { get; set; }

    [Column("dados_estruturados")]
    public JsonElement? DadosEstruturados { get; set; }

    [Column("id_credito_usado")]
    public string? IdCreditoUsado { get; set; }

    [Column("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }

    [Column("atualizado_em")]
    public DateTimeOffset? AtualizadoEm { get; set; }
}

[Table("curriculos_importados")]
public class CurriculoImportadoInsert : BaseModel
{
    [Column("id_usuario")]
    public string IdUsuario { get; set; } = string.Empty;

    [Column("id_site_vagas")]
    public string IdSiteVagas { get; set; } = string.Empty;

    [Column("nome_arquivo_original")]
    public string NomeArquivoOriginal { get; set; } = string.Empty;

    [Column("tipo_arquivo")]
    public string TipoArquivo { get; set; } = string.Empty;

    [Column("caminho_arquivo")]
    public string CaminhoArquivo { get; set; } = string.Empty;

    [Column("conteudo_extraido")]
    public string? ConteudoExtraido { get; set; }

    [Column("dados_estruturados")]
    public Dictionary<string, object?>? DadosEstruturados { get; set; }

    [Column("id_credito_usado")]
    public string? IdCreditoUsado { get; set; }
}

[Table("analises_curriculo")]
public class AnaliseCurriculoRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("id_curriculo")]
    public string? IdCurriculo { get; set; }

    [Column("id_usuario")]
    public string? IdUsuario { get; set; }

    [Column("id_site_vagas")]
    public string? IdSiteVagas { get; set; }

    [Column("score_geral")]
    public int? ScoreGeral { get; set; }

    [Column("pontos_fortes")]
    public List<string>? PontosFortes { get; set; }

    [Column("pontos_melhorar")]
    public List<string>? PontosMelhorar { get; set; }

    [Column("palavras_chave_sugeridas")]
    public List<string>? PalavrasChaveSugeridas { get; set; }

    [Column("recomendacoes")]
    public List<string>? Recomendacoes { get; set; }

    [Column("resultado_completo")]
    public JsonElement? ResultadoCompleto { get; set; }

    [Column("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }
}

[Table("simulacoes_entrevista")]
public class SimulacaoEntrevistaRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("id_curriculo")]
    public string? IdCurriculo { get; set; }

    [Column("id_usuario")]
    public string? IdUsuario { get; set; }

    [Column("id_site_vagas")]
    public string? IdSiteVagas { get; set; }

    [Column("titulo")]
    public string? Titulo { get; set; }

    [Column("area_foco")]
    public string? AreaFoco { get; set; }

    [Column("perguntas_feitas")]
    public List<string>? PerguntasFeitas { get; set; }

    [Column("respostas_dadas")]
    public JsonElement? RespostasDadas { get; set; }

    [Column("feedback_geral")]
    public JsonElement? FeedbackGeral { get; set; }

    [Column("score_geral")]
    public int? ScoreGeral { get; set; }

    [Column("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }

    [Column("atualizado_em")]
    public DateTimeOffset? AtualizadoEm { get; set; }
}

[Table("simulacoes_entrevista")]
public class SimulacaoEntrevistaInsert : BaseModel
{
    [Column("id_curriculo")]
    public string IdCurriculo { get; set; } = string.Empty;

    [Column("id_usuario")]
    public string IdUsuario { get; set; } = string.Empty;

    [Column("id_site_vagas")]
    public string IdSiteVagas { get; set; } = string.Empty;

    [Column("titulo")]
    public string Titulo { get; set; } = "Simulação de Entrevista";

    [Column("area_foco")]
    public string AreaFoco { get; set; } = "Geral";

    [Column("perguntas_feitas")]
    public List<string> PerguntasFeitas { get; set; } = new();

    [Column("respostas_dadas")]
    public List<object> RespostasDadas { get; set; } = new();
}

[Table("mensagens_entrevista")]
public class MensagemEntrevistaRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string? Id { get; set; }

    [Column("id_simulacao")]
    public string? IdSimulacao { get; set; }

    [Column("tipo")]
    public string? Tipo { get; set; }

    [Column("conteudo")]
    public string? Conteudo { get; set; }

    [Column("feedback")]
    public string? Feedback { get; set; }

    [Column("ordem")]
    public int Ordem { get; set; }

    [Column("dados_extras")]
    public Dictionary<string, object?>? DadosExtras { get; set; }

    [Column("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }
}

[Table("mensagens_entrevista")]
public class MensagemEntrevistaInsert : BaseModel
{
    [Column("id_simulacao")]
    public string IdSimulacao { get; set; } = string.Empty;

    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [Column("conteudo")]
    public string Conteudo { get; set; } = string.Empty;

    [Column("feedback")]
    public string? Feedback { get; set; }

    [Column("ordem")]
    public int Ordem { get; set; }

    [Column("dados_extras")]
    public Dictionary<string, object?>? DadosExtras { get; set; }
}

[Table("vagas_encontradas")]
public class VagaEncontradaInsert : BaseModel
{
    [Column("id_curriculo")]
    public string IdCurriculo { get; set; } = string.Empty;

    [Column("id_usuario")]
    public string IdUsuario { get; set; } = string.Empty;

    [Column("id_site_vagas")]
    public string IdSiteVagas { get; set; } = string.Empty;

    [Column("titulo_vaga")]
    public string TituloVaga { get; set; } = string.Empty;

    [Column("empresa")]
    public string? Empresa { get; set; }

    [Column("localizacao")]
    public string? Localizacao { get; set; }

    [Column("url_vaga")]
    public string UrlVaga { get; set; } = string.Empty;

    [Column("descricao_vaga")]
    public string? DescricaoVaga { get; set; }

    [Column("requisitos")]
    public List<string>? Requisitos { get; set; }

    [Column("score_compatibilidade")]
    public int ScoreCompatibilidade { get; set; }

    [Column("palavras_chave_match")]
    public List<string>? PalavrasChaveMatch { get; set; }

    [Column("dados_completos")]
    public Dictionary<string, object?>? DadosCompletos { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ativa";
}

