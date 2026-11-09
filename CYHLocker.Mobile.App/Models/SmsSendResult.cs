namespace CYHLocker.Mobile.App.Models;

public class SmsSendResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SmsMessageItem? SentMessage { get; set; }
    public string? ErrorCode { get; set; }

    public static SmsSendResult Ok(SmsMessageItem message, string info = "SMS sent successfully") => new()
    {
        Success = true,
        Message = info,
        SentMessage = message
    };

    public static SmsSendResult Fail(string error, string? code = null) => new()
    {
        Success = false,
        Message = error,
        ErrorCode = code
    };
}

public class SmsQuickTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
}

public class SimCardInfo
{
    public int SlotIndex { get; set; }
    public string DisplayName { get; set; } = "SIM 1";
    public string CarrierName { get; set; } = "Airtel / Jio";
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
}
