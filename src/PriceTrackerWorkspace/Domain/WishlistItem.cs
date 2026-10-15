namespace PriceTrackerWorkspace.Domain;

public class WishlistItem
{
    public const string DefaultCurrencySymbol = "$";

    public int Id { get; set; }

    public int? WorkspaceProjectId { get; set; }

    public WorkspaceProject? WorkspaceProject { get; set; }

    public required string ProductName { get; set; }

    public required string ProductUrl { get; set; }

    public required string NormalizedDomain { get; set; }

    public decimal CurrentPrice { get; private set; }

    public decimal? PreviousPrice { get; private set; }

    public string CurrencySymbol { get; set; } = DefaultCurrencySymbol;

    public bool IsIncludedInTotal { get; set; } = true;

    public string? CustomCssSelectorOverride { get; set; }

    public DateTime? LastScrapedAt { get; private set; }

    public WishlistItemStatus Status { get; private set; } = WishlistItemStatus.Pending;

    public string? LastErrorMessage { get; private set; }

    public DateTime CreatedAt { get; set; }

    public bool HasPrice => CurrentPrice > 0;

    public bool HasPriceDrop => PreviousPrice is { } previous && HasPrice && CurrentPrice < previous;

    public bool HasPriceIncrease => PreviousPrice is { } previous && HasPrice && CurrentPrice > previous;

    public decimal PriceChange => PreviousPrice is { } previous && HasPrice ? CurrentPrice - previous : 0m;

    public void ApplyScrapedPrice(decimal price, string? currencySymbol, DateTime scrapedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        if (HasPrice && CurrentPrice != price)
        {
            PreviousPrice = CurrentPrice;
        }

        CurrentPrice = price;
        CurrencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? CurrencySymbol : currencySymbol;
        LastScrapedAt = scrapedAtUtc;
        Status = WishlistItemStatus.Verified;
        LastErrorMessage = null;
    }

    public void MarkNeedsElementSelection(string reason, DateTime attemptedAtUtc)
    {
        Status = WishlistItemStatus.NeedsElementSelection;
        LastErrorMessage = reason;
        LastScrapedAt = attemptedAtUtc;
    }

    public void MarkScrapeFailed(string reason, DateTime attemptedAtUtc)
    {
        Status = WishlistItemStatus.ScrapeFailed;
        LastErrorMessage = reason;
        LastScrapedAt = attemptedAtUtc;
    }

    public void ResetForRescan()
    {
        Status = WishlistItemStatus.Pending;
        LastErrorMessage = null;
    }
}
