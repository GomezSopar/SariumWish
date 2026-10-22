using PriceTrackerWorkspace.Domain;

namespace PriceTrackerWorkspace.Components.Shared;

public readonly record struct CurrencyTotal(string Symbol, decimal Amount);

public sealed record WishlistTotals(
    IReadOnlyList<CurrencyTotal> ByCurrency,
    CurrencyTotal Primary,
    int IncludedCount,
    int PricedCount,
    int PriceDropCount,
    decimal PrimarySavings)
{
    public static WishlistTotals Empty { get; } = new([], new CurrencyTotal(WishlistItem.DefaultCurrencySymbol, 0), 0, 0, 0, 0);

    public static WishlistTotals From(IEnumerable<WishlistItem> items)
    {
        var priced = items.Where(i => i.HasPrice).ToList();
        var included = priced.Where(i => i.IsIncludedInTotal).ToList();

        var byCurrency = included
            .GroupBy(i => i.CurrencySymbol)
            .Select(g => new { Symbol = g.Key, Count = g.Count(), Amount = g.Sum(i => i.CurrentPrice) })
            .OrderByDescending(g => g.Count)
            .ThenByDescending(g => g.Amount)
            .Select(g => new CurrencyTotal(g.Symbol, g.Amount))
            .ToList();

        var primary = byCurrency.Count > 0 ? byCurrency[0] : Empty.Primary;

        var savings = included
            .Where(i => i.HasPriceDrop && i.CurrencySymbol == primary.Symbol)
            .Sum(i => -i.PriceChange);

        return new WishlistTotals(
            byCurrency,
            primary,
            included.Count,
            priced.Count,
            priced.Count(i => i.HasPriceDrop),
            savings);
    }

    public double? BudgetUtilization(decimal? budget) =>
        budget is > 0 ? (double)(Primary.Amount / budget.Value) : null;
}
