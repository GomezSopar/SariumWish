using System.Globalization;

namespace PriceTrackerWorkspace.Components.Shared;

public static class Display
{
    private static readonly HashSet<string> PrefixedLetterSymbols = new(StringComparer.Ordinal) { "CHF", "R" };

    public static string Money(decimal amount, string? symbol)
    {
        var value = amount.ToString("N2", CultureInfo.InvariantCulture);
        var currency = string.IsNullOrWhiteSpace(symbol) ? "$" : symbol;

        if (currency.All(char.IsLetter))
        {
            return PrefixedLetterSymbols.Contains(currency) ? $"{currency} {value}" : $"{value} {currency}";
        }

        return $"{currency}{value}";
    }

    public static string Money(CurrencyTotal total) => Money(total.Amount, total.Symbol);

    public static string Totals(IReadOnlyCollection<CurrencyTotal> totals) =>
        totals.Count == 0 ? Money(0, null) : string.Join(" + ", totals.Select(Money));

    public static string Relative(DateTime? utc, DateTime nowUtc)
    {
        if (utc is not { } value)
        {
            return "never";
        }

        var elapsed = nowUtc - value;
        return elapsed switch
        {
            { TotalSeconds: < 45 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes}m ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours}h ago",
            { TotalDays: < 30 } => $"{(int)elapsed.TotalDays}d ago",
            _ => value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
        };
    }

    public static string Utc(DateTime? utc) =>
        utc is { } value ? value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : "Not yet checked";

    public static string Date(DateOnly date) => date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    public static string Percent(double ratio) => $"{(ratio * 100).ToString("0", CultureInfo.InvariantCulture)}%";
}
