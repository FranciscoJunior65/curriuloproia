namespace CurriculosProIA.Domain.Signatures.Analyze;

public class AdminFreeCreditsSignature
{
    public string? PlanId { get; set; }

    /// <summary>Inclui add-on de currículo em inglês (compra filha pendente no bundle).</summary>
    public bool? IncludeEnglish { get; set; }

    /// <summary>Obrigatório quando PlanId = english — análise que receberá o direito.</summary>
    public string? AnalysisId { get; set; }
}
