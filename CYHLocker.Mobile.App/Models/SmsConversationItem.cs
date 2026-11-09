using System.Collections.ObjectModel;

namespace CYHLocker.Mobile.App.Models;

public class SmsConversationItem
{
    public string ContactNumber { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string LastMessageBody { get; set; } = string.Empty;
    public DateTime LastMessageTime { get; set; } = DateTime.UtcNow;
    public int UnreadCount { get; set; }
    public string AvatarColor { get; set; } = "#512BD4";
    public string Initials { get; set; } = "??";
    public bool IsPinned { get; set; }

    public ObservableCollection<SmsMessageItem> Messages { get; set; } = new();

    public string DisplayTitle => string.IsNullOrWhiteSpace(ContactName) ? ContactNumber : ContactName;
    public string Subtitle => string.IsNullOrWhiteSpace(ContactName) ? string.Empty : ContactNumber;

    public string FormattedTime
    {
        get
        {
            var local = LastMessageTime.ToLocalTime();
            var diff = DateTime.Now.Date - local.Date;
            if (diff.TotalDays == 0)
                return local.ToString("h:mm tt");
            if (diff.TotalDays == 1)
                return "Kal";
            if (diff.TotalDays < 7)
                return local.ToString("ddd");
            return local.ToString("dd/MM/yy");
        }
    }

    public static string GenerateInitials(string name, string number)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && parts[0].Length > 0)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(number))
        {
            var digits = new string(number.Where(char.IsDigit).ToArray());
            if (digits.Length >= 2)
                return digits.Substring(digits.Length - 2);
            return digits;
        }

        return "??";
    }

    public static string PickAvatarColor(string seed)
    {
        string[] colors =
        [
            "#1E88E5", // Blue
            "#43A047", // Green
            "#FB8C00", // Orange
            "#8E24AA", // Purple
            "#E53935", // Red
            "#00ACC1", // Cyan
            "#D81B60", // Pink
            "#3949AB"  // Indigo
        ];

        int hash = Math.Abs(seed.GetHashCode());
        return colors[hash % colors.Length];
    }
}
