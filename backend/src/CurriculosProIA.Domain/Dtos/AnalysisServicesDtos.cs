using System.Text.Json.Serialization;

namespace CurriculosProIA.Domain.Dtos;

public static class AnalysisBundledServiceKeys
{
    public const string Analise = "analise";
    public const string CartaApresentacao = "carta_apresentacao";
    public const string CurriculoMelhorado = "curriculo_melhorado";
    public const string Entrevista = "entrevista";
    public const string BuscaVagas = "busca_vagas";

    /// <summary>Compra à parte: direito de gerar currículo em inglês nesta análise.</summary>
    public const string CurriculoInglesPago = "curriculo_ingles_pago";

    /// <summary>Currículo em inglês — PDF já baixado nesta análise.</summary>
    public const string CurriculoInglesPdf = "curriculo_ingles_pdf";

    /// <summary>Currículo em inglês — Word já baixado nesta análise.</summary>
    public const string CurriculoInglesWord = "curriculo_ingles_word";

    /// <summary>Legado: ambos formatos gerados (preferir pdf/word).</summary>
    public const string CurriculoIngles = "curriculo_ingles";

    /// <summary>Serviços de uso único por análise paga (carta, currículo melhorado, entrevista).</summary>
    public static readonly string[] Optional =
    [
        CartaApresentacao,
        CurriculoMelhorado,
        Entrevista
    ];

    /// <summary>Inclusos na análise paga, sem limite de uso no mesmo currículo.</summary>
    public static readonly string[] Unlimited =
    [
        BuscaVagas
    ];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Analise] = "Análise do currículo",
        [CartaApresentacao] = "Carta de apresentação",
        [CurriculoMelhorado] = "Currículo melhorado",
        [Entrevista] = "Simulação de entrevista",
        [BuscaVagas] = "Busca de vagas",
        [CurriculoInglesPago] = "Currículo em inglês (comprado)",
        [CurriculoInglesPdf] = "Currículo em inglês (PDF)",
        [CurriculoInglesWord] = "Currículo em inglês (Word)",
        [CurriculoIngles] = "Currículo em inglês (gerado)"
    };

    public static Dictionary<string, bool> CreateDefaultStatus() => new()
    {
        [Analise] = true,
        [CartaApresentacao] = false,
        [CurriculoMelhorado] = false,
        [Entrevista] = false,
        [BuscaVagas] = false,
        [CurriculoInglesPago] = false,
        [CurriculoInglesPdf] = false,
        [CurriculoInglesWord] = false,
        [CurriculoIngles] = false
    };
}

public class AnalysisServiceItemDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("usado")]
    public bool Usado { get; set; }

    [JsonPropertyName("pendente")]
    public bool Pendente { get; set; }

    [JsonPropertyName("ilimitado")]
    public bool Ilimitado { get; set; }
}

public class AnalysisServicesStatusDto
{
    [JsonPropertyName("itens")]
    public List<AnalysisServiceItemDto> Itens { get; set; } = new();

    [JsonPropertyName("servicos_pendentes")]
    public int ServicosPendentes { get; set; }

    [JsonPropertyName("pacote_concluido")]
    public bool PacoteConcluido { get; set; }

    [JsonPropertyName("curriculo_ingles_pago")]
    public bool CurriculoInglesPago { get; set; }

    [JsonPropertyName("curriculo_ingles_gerado")]
    public bool CurriculoInglesGerado { get; set; }

    [JsonPropertyName("curriculo_ingles_pdf")]
    public bool CurriculoInglesPdf { get; set; }

    [JsonPropertyName("curriculo_ingles_word")]
    public bool CurriculoInglesWord { get; set; }
}

public class PendingServicesSummaryDto
{
    [JsonPropertyName("total_servicos_pendentes")]
    public int TotalServicosPendentes { get; set; }

    [JsonPropertyName("analises_com_pendencias")]
    public int AnalisesComPendencias { get; set; }

    [JsonPropertyName("analises")]
    public List<PendingAnalysisItemDto> Analises { get; set; } = new();
}

public class PendingAnalysisItemDto
{
    [JsonPropertyName("analysis_id")]
    public string AnalysisId { get; set; } = string.Empty;

    [JsonPropertyName("nome_arquivo")]
    public string? NomeArquivo { get; set; }

    [JsonPropertyName("site_nome")]
    public string? SiteNome { get; set; }

    [JsonPropertyName("score_geral")]
    public int? ScoreGeral { get; set; }

    [JsonPropertyName("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }

    [JsonPropertyName("servicos_pendentes")]
    public int ServicosPendentes { get; set; }

    [JsonPropertyName("pendentes")]
    public List<string> Pendentes { get; set; } = new();

    [JsonPropertyName("servicos")]
    public AnalysisServicesStatusDto Servicos { get; set; } = new();
}
