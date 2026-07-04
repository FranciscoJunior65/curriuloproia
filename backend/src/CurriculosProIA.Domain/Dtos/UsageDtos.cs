namespace CurriculosProIA.Domain.Dtos;

public class DailyUsageDto
{
    public string Date { get; set; } = string.Empty;
    public int Registrations { get; set; }
    public int Analyses { get; set; }
    public decimal Revenue { get; set; }
}

public class MonthlyUsageDto
{
    public string Month { get; set; } = string.Empty;
    public int Registrations { get; set; }
    public int Analyses { get; set; }
    public decimal Revenue { get; set; }
}
