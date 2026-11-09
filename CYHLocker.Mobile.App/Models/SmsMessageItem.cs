namespace CYHLocker.Mobile.App.Models;

public enum SmsDeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Failed,
    Received
}

public class SmsMessageItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SenderNumber { get; set; } = string.Empty;
    public string RecipientNumber { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsIncoming { get; set; }
    public SmsDeliveryStatus Status { get; set; } = SmsDeliveryStatus.Sent;
    public int SimSlot { get; set; } = 0;
    public bool IsSecret { get; set; }

    public string FormattedTime
    {
        get
        {
            var local = Timestamp.ToLocalTime();
            var diff = DateTime.Now.Date - local.Date;
            if (diff.TotalDays == 0)
                return local.ToString("h:mm tt");
            if (diff.TotalDays == 1)
                return "Kal " + local.ToString("h:mm tt");
            if (diff.TotalDays < 7)
                return local.ToString("ddd h:mm tt");
            return local.ToString("dd MMM, h:mm tt");
        }
    }

    public string StatusText => Status switch
    {
        SmsDeliveryStatus.Pending => "Bhej rahe hain...",
        SmsDeliveryStatus.Sent => "Sent ✓",
        SmsDeliveryStatus.Delivered => "Delivered ✓✓",
        SmsDeliveryStatus.Failed => "Failed ✕",
        SmsDeliveryStatus.Received => "Received",
        _ => string.Empty
    };
}
