using System.ComponentModel.DataAnnotations;

namespace CYHLocker.Data.DTOs;

public class RazorpayOrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public long AmountPaise { get; set; }
    public string Currency { get; set; } = "INR";
    public string KeyId { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
}

public class RazorpayPaymentVerifyDto
{
    [Required]
    public string RazorpayOrderId { get; set; } = string.Empty;

    [Required]
    public string RazorpayPaymentId { get; set; } = string.Empty;

    [Required]
    public string RazorpaySignature { get; set; } = string.Empty;

    [Required]
    public string PlanCode { get; set; } = string.Empty;
}

public class QuotaCheckResultDto
{
    public bool IsAllowed { get; set; }
    public string ItemType { get; set; } = "Login";
    public int CurrentCount { get; set; }
    public int MaxLimit { get; set; }
    public string? Message { get; set; }
}
