namespace CYHLocker.Mobile.App.Helpers;

public static class SmsSegmentCalculator
{
    // Standard GSM 7-bit default alphabet characters
    private const string Gsm7BitChars =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞ\x1bÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

    public static bool IsGsm7Bit(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        foreach (char c in text)
        {
            if (!Gsm7BitChars.Contains(c) && c != '^' && c != '{' && c != '}' && c != '\\' && c != '[' && c != '~' && c != ']' && c != '|' && c != '€')
            {
                return false;
            }
        }
        return true;
    }

    public static (int charCount, int segmentCount, int remainingInSegment, bool isUnicode) Calculate(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return (0, 0, 160, false);

        bool isUnicode = !IsGsm7Bit(text);
        int length = text.Length;

        if (!isUnicode)
        {
            // GSM 7-bit
            if (length <= 160)
            {
                return (length, 1, 160 - length, false);
            }
            int segments = (int)Math.Ceiling(length / 153.0);
            int currentSegmentCapacity = segments * 153;
            int remaining = currentSegmentCapacity - length;
            return (length, segments, remaining, false);
        }
        else
        {
            // Unicode (Hindi, Emoji, etc.)
            if (length <= 70)
            {
                return (length, 1, 70 - length, true);
            }
            int segments = (int)Math.Ceiling(length / 67.0);
            int currentSegmentCapacity = segments * 67;
            int remaining = currentSegmentCapacity - length;
            return (length, segments, remaining, true);
        }
    }

    public static string FormatCounter(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "0 / 160 (1 SMS)";

        var (count, segments, remaining, isUnicode) = Calculate(text);
        string type = isUnicode ? "Unicode" : "GSM";
        return $"{count} chars • {segments} SMS ({remaining} left) [{type}]";
    }
}
