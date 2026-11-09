using Xunit;

namespace CYHLocker.Tests;

public static class TestSmsSegmentCalculator
{
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
            if (length <= 160)
                return (length, 1, 160 - length, false);

            int segments = (int)Math.Ceiling(length / 153.0);
            int currentSegmentCapacity = segments * 153;
            int remaining = currentSegmentCapacity - length;
            return (length, segments, remaining, false);
        }
        else
        {
            if (length <= 70)
                return (length, 1, 70 - length, true);

            int segments = (int)Math.Ceiling(length / 67.0);
            int currentSegmentCapacity = segments * 67;
            int remaining = currentSegmentCapacity - length;
            return (length, segments, remaining, true);
        }
    }
}

public class SmsMessagingTests
{
    [Fact]
    public void Test_Gsm7Bit_Detection_EnglishText_ReturnsTrue()
    {
        string text = "Hello this is a local SMS message 12345! @#$";
        Assert.True(TestSmsSegmentCalculator.IsGsm7Bit(text));
    }

    [Fact]
    public void Test_Gsm7Bit_Detection_HindiUnicode_ReturnsFalse()
    {
        string text = "नमस्ते, यह एक लोकल एसएमएस है";
        Assert.False(TestSmsSegmentCalculator.IsGsm7Bit(text));
    }

    [Fact]
    public void Test_Gsm7Bit_SingleSms_CalculatesCorrectly()
    {
        string text = new string('A', 160);
        var (charCount, segmentCount, remaining, isUnicode) = TestSmsSegmentCalculator.Calculate(text);

        Assert.Equal(160, charCount);
        Assert.Equal(1, segmentCount);
        Assert.Equal(0, remaining);
        Assert.False(isUnicode);
    }

    [Fact]
    public void Test_Gsm7Bit_MultiPartSms_CalculatesCorrectly()
    {
        // 161 characters should split into 2 segments of 153 chars (total capacity 306)
        string text = new string('A', 161);
        var (charCount, segmentCount, remaining, isUnicode) = TestSmsSegmentCalculator.Calculate(text);

        Assert.Equal(161, charCount);
        Assert.Equal(2, segmentCount);
        Assert.Equal(306 - 161, remaining);
        Assert.False(isUnicode);
    }

    [Fact]
    public void Test_Unicode_SingleSms_Under70Chars()
    {
        string text = "नमस्ते मित्र!";
        var (charCount, segmentCount, remaining, isUnicode) = TestSmsSegmentCalculator.Calculate(text);

        Assert.True(isUnicode);
        Assert.Equal(1, segmentCount);
        Assert.Equal(70 - text.Length, remaining);
    }

    [Fact]
    public void Test_Unicode_MultiPartSms_Over70Chars()
    {
        // 75 Unicode chars should take 2 segments (each 67 capacity, total 134)
        string text = new string('क', 75);
        var (charCount, segmentCount, remaining, isUnicode) = TestSmsSegmentCalculator.Calculate(text);

        Assert.True(isUnicode);
        Assert.Equal(2, segmentCount);
        Assert.Equal(134 - 75, remaining);
    }
}
