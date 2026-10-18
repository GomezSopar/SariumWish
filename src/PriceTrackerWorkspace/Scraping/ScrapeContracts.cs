using PriceTrackerWorkspace.Domain;

namespace PriceTrackerWorkspace.Scraping;

public enum ScrapeSource
{
    None,
    ItemOverride,
    DomainRule,
    Heuristic,
    VisualPicker
}

public sealed record ScrapeResult(
    int WishlistItemId,
    WishlistItemStatus Status,
    decimal? Price,
    string? CurrencySymbol,
    string? MatchedSelector,
    ScrapeSource Source,
    string? Message)
{
    public bool Succeeded => Status == WishlistItemStatus.Verified;
}

public sealed record RefreshProgress(int Completed, int Total, ScrapeResult? LastResult)
{
    public int Percent => Total == 0 ? 100 : (int)Math.Round(Completed * 100d / Total);
}

public sealed record PageSnapshot(string Html, string FinalUrl, string? Title, string NormalizedDomain);

public sealed record SelectorTestResult(bool Success, string? RawText, ParsedPrice? Price, string? Error);

public sealed record VisualSelection(int WishlistItemId, string CssSelector, string RawText, string? PageTitle);

public sealed record VisualSelectionResult(ScrapeResult Item, IReadOnlyList<int> OtherDomainItemIds);
