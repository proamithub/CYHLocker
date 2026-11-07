using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CYHLocker.Logic.Interfaces;

namespace CYHLocker.Logic.Services;

public class FaviconService : IFaviconService
{
    private static readonly ConcurrentDictionary<string, string> IconCache = new(StringComparer.OrdinalIgnoreCase);

    // Dictionary of well-known platforms mapped to trusted high-res CDN SVG/PNG icons
    private static readonly Dictionary<string, string> BrandIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        // Social Media & Tech
        { "google", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/google/google-original.svg" },
        { "google.com", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/google/google-original.svg" },
        { "facebook", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/facebook/facebook-original.svg" },
        { "facebook.com", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/facebook/facebook-original.svg" },
        { "github", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/github/github-original.svg" },
        { "github.com", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/github/github-original.svg" },
        { "twitter", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/twitter/twitter-original.svg" },
        { "x.com", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/twitter/twitter-original.svg" },
        { "linkedin", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/linkedin/linkedin-original.svg" },
        { "linkedin.com", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/linkedin/linkedin-original.svg" },
        { "apple", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/apple/apple-original.svg" },
        { "apple.com", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/apple/apple-original.svg" },
        { "amazon", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/amazonwebservices/amazonwebservices-plain-wordmark.svg" },
        { "amazon.com", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/amazonwebservices/amazonwebservices-plain-wordmark.svg" },
        { "amazon.in", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/amazonwebservices/amazonwebservices-plain-wordmark.svg" },
        { "netflix", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/netflix.svg" },
        { "netflix.com", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/netflix.svg" },
        { "spotify", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/spotify.svg" },
        { "spotify.com", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/spotify.svg" },
        { "instagram", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/instagram.svg" },
        { "instagram.com", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/instagram.svg" },
        { "youtube", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/youtube.svg" },
        { "youtube.com", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/youtube.svg" },
        { "reddit", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/reddit.svg" },
        { "reddit.com", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/reddit.svg" },
        { "discord", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/discord.svg" },
        { "discord.com", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/discord.svg" },
        { "slack", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/slack/slack-original.svg" },
        { "slack.com", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/slack/slack-original.svg" },
        { "whatsapp", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/whatsapp.svg" },
        { "telegram", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/telegram.svg" },

        // Developer & Cloud Tools
        { "aws", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/amazonwebservices/amazonwebservices-original.svg" },
        { "azure", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/azure/azure-original.svg" },
        { "openai", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/openai.svg" },
        { "chatgpt", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/openai.svg" },
        { "cloudflare", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/cloudflare/cloudflare-original.svg" },
        { "digitalocean", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/digitalocean/digitalocean-original.svg" },
        { "gitlab", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/gitlab/gitlab-original.svg" },
        { "bitbucket", "https://cdn.jsdelivr.net/gh/devicons/devicon/icons/bitbucket/bitbucket-original.svg" },

        // Banking & Payments
        { "paypal", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/paypal.svg" },
        { "stripe", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/stripe.svg" },
        { "razorpay", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/razorpay.svg" },
        { "paytm", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/paytm.svg" },
        { "phonepe", "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/phonepe.svg" }
    };

    public string ResolveIcon(string? url, string? title, string itemType = "Login", string? cardBrand = null)
    {
        var cacheKey = $"{url}|{title}|{itemType}|{cardBrand}";
        if (IconCache.TryGetValue(cacheKey, out var cached)) return cached;

        var resolved = ComputeIcon(url, title, itemType, cardBrand);
        IconCache.TryAdd(cacheKey, resolved);
        return resolved;
    }

    private string ComputeIcon(string? url, string? title, string itemType, string? cardBrand)
    {
        // 1. Payment Cards
        if (string.Equals(itemType, "Card", StringComparison.OrdinalIgnoreCase))
        {
            var brand = cardBrand?.Trim().ToLowerInvariant() ?? "";
            return brand switch
            {
                "visa"       => "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/visa.svg",
                "mastercard" => "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/mastercard.svg",
                "rupay"      => "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/rupay.svg",
                "amex"       => "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/americanexpress.svg",
                "discover"   => "https://cdn.jsdelivr.net/gh/simple-icons/simple-icons/icons/discover.svg",
                _            => "💳"
            };
        }

        // 2. Secure Documents
        if (string.Equals(itemType, "Document", StringComparison.OrdinalIgnoreCase))
        {
            var lowerTitle = title?.ToLowerInvariant() ?? "";
            if (lowerTitle.EndsWith(".pdf")) return "📄";
            if (lowerTitle.EndsWith(".md") || lowerTitle.EndsWith(".txt")) return "📝";
            if (lowerTitle.Contains("lease") || lowerTitle.Contains("agreement") || lowerTitle.Contains("contract")) return "📑";
            return "📁";
        }

        // 3. API Keys
        if (string.Equals(itemType, "ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            var lowerTitle = title?.ToLowerInvariant() ?? "";
            foreach (var kv in BrandIcons)
            {
                if (lowerTitle.Contains(kv.Key)) return kv.Value;
            }
            return "🔑";
        }

        // 4. Login & Social Media (Domain or Title Matching)
        var domain = ExtractDomain(url);
        if (!string.IsNullOrEmpty(domain))
        {
            // Exact domain match in predefined catalog
            if (BrandIcons.TryGetValue(domain, out var iconUrl)) return iconUrl;

            // Partial domain match
            var clean = domain.Replace(".com", "").Replace(".in", "").Replace(".org", "").Replace(".net", "");
            if (BrandIcons.TryGetValue(clean, out var matchedIcon)) return matchedIcon;

            // Automatic Google Favicon API Proxy for any web domain in high-res (128px)
            return $"https://www.google.com/s2/favicons?domain={domain}&sz=128";
        }

        // Match by title keywords if URL was not specified
        if (!string.IsNullOrEmpty(title))
        {
            var words = title.ToLowerInvariant().Split(' ', '-', '_');
            foreach (var w in words)
            {
                if (BrandIcons.TryGetValue(w, out var iconByTitle)) return iconByTitle;
            }
        }

        return "🌐";
    }

    public string? ExtractDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var clean = url.Trim();
        if (!clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            clean = "https://" + clean;
        }

        if (Uri.TryCreate(clean, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host[4..];
            return host;
        }

        return null;
    }

    public string DetectCardBrand(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return "Card";

        var clean = Regex.Replace(cardNumber, @"\D", ""); // Keep digits only
        if (clean.Length < 2) return "Card";

        // Visa: starts with 4
        if (clean.StartsWith("4")) return "Visa";

        // Mastercard: 51-55 or 2221-2720
        if (Regex.IsMatch(clean, @"^(5[1-5]|2[2-7])")) return "MasterCard";

        // RuPay: 60, 65, 81, 82, 508
        if (Regex.IsMatch(clean, @"^(60|65|81|82|508)")) return "RuPay";

        // American Express: 34 or 37
        if (clean.StartsWith("34") || clean.StartsWith("37")) return "Amex";

        // Discover: 6011, 622126-622925, 644-649, 65
        if (clean.StartsWith("6011") || clean.StartsWith("65")) return "Discover";

        return "Card";
    }
}
