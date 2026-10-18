namespace PriceTrackerWorkspace.Scraping;

public interface IScraperService
{
    /// <summary>
    /// Scrapes the live price of a wishlist item by trying, in order, the item's selector override,
    /// the learned <see cref="Domain.DomainScrapeRule"/> for its domain, and generic heuristics.
    /// The outcome is persisted and rule success/failure statistics are updated.
    /// </summary>
    Task<ScrapeResult> RefreshItemAsync(int wishlistItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes several items with bounded parallelism, reporting progress after each item completes.
    /// </summary>
    Task<IReadOnlyList<ScrapeResult>> RefreshItemsAsync(
        IReadOnlyCollection<int> wishlistItemIds,
        IProgress<RefreshProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads <paramref name="productUrl"/> in a fresh headless page and evaluates <paramref name="cssSelector"/>
    /// against the live DOM without persisting anything.
    /// </summary>
    Task<SelectorTestResult> TestSelectorAsync(string productUrl, string cssSelector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a selector chosen in the visual picker as the domain rule and applies the picked price to the item.
    /// Returns the ids of other items on the same domain so callers can re-check them with the new rule.
    /// </summary>
    Task<VisualSelectionResult> SaveVisualSelectionAsync(VisualSelection selection, CancellationToken cancellationToken = default);
}
