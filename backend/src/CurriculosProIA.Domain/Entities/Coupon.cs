namespace CurriculosProIA.Domain.Entities;

public class Coupon
{
    public string Id { get; set; } = string.Empty;
    public string? Nome { get; set; }
    public double PorcentagemDesconto { get; set; }
}
