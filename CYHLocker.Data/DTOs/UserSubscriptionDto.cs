namespace CYHLocker.Data.DTOs;

public class UserSubscriptionDto
{
    public string PlanId { get; set; } = string.Empty;
    public string PlanCode { get; set; } = "FREE";
    public string PlanName { get; set; } = "Free Starter Vault";
    public decimal PriceINR { get; set; }
    public string BillingCycle { get; set; } = "FREE";
    public int MaxPasswords { get; set; } = 15;
    public int MaxCards { get; set; } = 2;
    public int MaxDocuments { get; set; } = 1;
    public bool CanSync { get; set; }
    public string SubscriptionStatus { get; set; } = "Active";
    public DateTime? ExpirationDate { get; set; }

    // Live Vault Item Counts
    public int CurrentPasswords { get; set; }
    public int CurrentCards { get; set; }
    public int CurrentDocuments { get; set; }
    public int CurrentApiKeys { get; set; }

    public bool IsPro => PlanCode != "FREE" && SubscriptionStatus == "Active";
}
