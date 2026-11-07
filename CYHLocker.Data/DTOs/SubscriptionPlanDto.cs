namespace CYHLocker.Data.DTOs;

public class SubscriptionPlanDto
{
    public string Id { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceINR { get; set; }
    public string BillingCycle { get; set; } = "MONTHLY";
    public int MaxPasswords { get; set; }
    public int MaxCards { get; set; }
    public int MaxDocuments { get; set; }
    public bool CanSync { get; set; }
    public bool IsActive { get; set; } = true;
}
