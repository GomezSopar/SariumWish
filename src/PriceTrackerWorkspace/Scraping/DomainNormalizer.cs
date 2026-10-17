using System.Diagnostics.CodeAnalysis;

namespace PriceTrackerWorkspace.Scraping;

public static class DomainNormalizer
{
    public static bool TryNormalizeUrl(string? url, [NotNullWhen(true)] out string? domain)
    {
        domain = null;

        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        domain = StripWww(uri.IdnHost.ToLowerInvariant());
        return domain.Length > 0;
    }

    public static bool TryNormalizeDomainInput(string? input, [NotNullWhen(true)] out string? domain)
    {
        domain = null;
        var trimmed = input?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var candidate = trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : $"https://{trimmed}";
        return TryNormalizeUrl(candidate, out domain) && domain.Contains('.');
    }

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
}
