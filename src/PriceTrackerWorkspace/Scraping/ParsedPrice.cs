namespace PriceTrackerWorkspace.Scraping;

public readonly record struct ParsedPrice(decimal Amount, string? CurrencySymbol)
{
    public bool HasCurrency => CurrencySymbol is not null;

    public bool IsPositive => Amount > 0;
}
