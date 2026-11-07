namespace CYHLocker.Logic.Interfaces;

public interface IFaviconService
{
    /// <summary>
    /// Resolves an automatic high-resolution icon URL or SVG badge for a given URL, service title, and item type.
    /// </summary>
    string ResolveIcon(string? url, string? title, string itemType = "Login", string? cardBrand = null);

    /// <summary>
    /// Extracts the clean base domain from a URL (e.g. "https://accounts.google.com/login" -> "google.com").
    /// </summary>
    string? ExtractDomain(string? url);

    /// <summary>
    /// Automatically detects the credit card brand (Visa, Mastercard, RuPay, Amex, Discover) from card number.
    /// </summary>
    string DetectCardBrand(string? cardNumber);
}
