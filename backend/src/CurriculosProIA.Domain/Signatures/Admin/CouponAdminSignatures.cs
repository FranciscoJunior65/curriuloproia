namespace CurriculosProIA.Domain.Signatures.Admin;

public class CreatePartnerSignature
{
    public string? Nome { get; set; }
    public string? Cpf { get; set; }
    public string? Descricao { get; set; }
    public string? Email { get; set; }
}

public class CreateCouponSignature
{
    public string? Nome { get; set; }
    public decimal? PorcentagemDesconto { get; set; }
    public string? ParceiroId { get; set; }
    public decimal? PorcentagemParceiro { get; set; }
    public bool? Ativo { get; set; }
}

public class UpdateCouponSignature
{
    public decimal? PorcentagemDesconto { get; set; }
    public string? ParceiroId { get; set; }
    public decimal? PorcentagemParceiro { get; set; }
    public bool? Ativo { get; set; }
    public bool ClearParceiro { get; set; }
}
