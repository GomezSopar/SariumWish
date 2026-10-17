using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PriceTrackerWorkspace.Scraping;

public static partial class PriceParser
{
    private static readonly (string Token, string Symbol)[] CurrencyTokens =
    [
        ("US$", "$"), ("USD", "$"),
        ("CA$", "C$"), ("CAD", "C$"), ("C$", "C$"),
        ("AU$", "A$"), ("AUD", "A$"), ("A$", "A$"),
        ("NZ$", "NZ$"), ("NZD", "NZ$"),
        ("HK$", "HK$"), ("HKD", "HK$"),
        ("MX$", "MX$"), ("MXN", "MX$"),
        ("SGD", "S$"), ("S$", "S$"),
        ("BRL", "R$"), ("R$", "R$"),
        ("EUR", "€"), ("€", "€"),
        ("GBP", "£"), ("£", "£"),
        ("JPY", "¥"), ("CNY", "¥"), ("RMB", "¥"), ("¥", "¥"), ("￥", "¥"),
        ("INR", "₹"), ("Rs.", "₹"), ("₹", "₹"),
        ("KRW", "₩"), ("₩", "₩"),
        ("RUB", "₽"), ("₽", "₽"),
        ("TRY", "₺"), ("₺", "₺"),
        ("ILS", "₪"), ("₪", "₪"),
        ("PHP", "₱"), ("₱", "₱"),
        ("THB", "฿"), ("฿", "฿"),
        ("VND", "₫"), ("₫", "₫"),
        ("PLN", "zł"), ("zł", "zł"),
        ("CZK", "Kč"), ("Kč", "Kč"),
        ("HUF", "Ft"), ("Ft", "Ft"),
        ("SEK", "kr"), ("NOK", "kr"), ("DKK", "kr"), ("kr", "kr"),
        ("CHF", "CHF"),
        ("ZAR", "R"),
        ("$", "$")
    ];

    public static ParsedPrice? Parse(string? text) => TryParse(text, out var price) ? price : null;

    public static bool TryParse(string? text, out ParsedPrice price)
    {
        price = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = NormalizeWhitespace(text);
        var numbers = NumberToken().Matches(normalized);
        if (numbers.Count == 0)
        {
            return false;
        }

        var currencies = FindCurrencyOccurrences(normalized);
        var chosen = currencies.Count == 0
            ? numbers[0]
            : numbers.MinBy(match => currencies.Min(currency => Distance(match, currency)))!;

        if (!TryInterpretNumber(chosen.Value, out var amount))
        {
            return false;
        }

        string? symbol = currencies.Count == 0
            ? null
            : currencies.MinBy(currency => Distance(chosen, currency)).Symbol;

        price = new ParsedPrice(Math.Round(amount, 2, MidpointRounding.AwayFromZero), symbol);
        return true;
    }

    public static string? ResolveCurrencySymbol(string? currencyCodeOrSymbol)
    {
        if (string.IsNullOrWhiteSpace(currencyCodeOrSymbol))
        {
            return null;
        }

        var candidate = currencyCodeOrSymbol.Trim();
        foreach (var (token, symbol) in CurrencyTokens)
        {
            if (string.Equals(token, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return symbol;
            }
        }

        return FindCurrencyOccurrences(candidate) is [var first, ..] ? first.Symbol : null;
    }

    internal static bool TryInterpretNumber(string token, out decimal value)
    {
        var compact = new string([.. token.Where(c => char.IsAsciiDigit(c) || c is '.' or ',')]);
        var lastDot = compact.LastIndexOf('.');
        var lastComma = compact.LastIndexOf(',');
        var decimalIndex = (lastDot, lastComma) switch
        {
            (< 0, < 0) => -1,
            (>= 0, >= 0) => Math.Max(lastDot, lastComma),
            _ => ResolveSingleSeparator(compact, lastDot >= 0 ? '.' : ',')
        };

        var integerDigits = OnlyDigits(decimalIndex < 0 ? compact : compact[..decimalIndex]);
        var fractionDigits = decimalIndex < 0 ? string.Empty : OnlyDigits(compact[(decimalIndex + 1)..]);
        var invariant = fractionDigits.Length == 0 ? integerDigits : $"{integerDigits}.{fractionDigits}";

        return decimal.TryParse(invariant, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static int ResolveSingleSeparator(string compact, char separator)
    {
        var index = compact.LastIndexOf(separator);
        var occurrences = compact.Count(c => c == separator);
        var digitsAfter = compact.Length - index - 1;
        var integerPart = compact[..index];

        var isThousandsGrouping = occurrences > 1 || (digitsAfter == 3 && integerPart.TrimStart('0').Length > 0);
        return isThousandsGrouping ? -1 : index;
    }

    private static string OnlyDigits(string value) => new([.. value.Where(char.IsAsciiDigit)]);

    private static List<CurrencyOccurrence> FindCurrencyOccurrences(string text)
    {
        var occurrences = new List<CurrencyOccurrence>();
        var claimed = new bool[text.Length];

        foreach (var (token, symbol) in CurrencyTokens)
        {
            var searchFrom = 0;
            while (searchFrom < text.Length)
            {
                var index = text.IndexOf(token, searchFrom, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                searchFrom = index + token.Length;

                if (claimed.AsSpan(index, token.Length).Contains(true) || !HasWordBoundaries(text, index, token))
                {
                    continue;
                }

                claimed.AsSpan(index, token.Length).Fill(true);
                occurrences.Add(new CurrencyOccurrence(index, token.Length, symbol));
            }
        }

        return occurrences;
    }

    private static bool HasWordBoundaries(string text, int index, string token)
    {
        var startsWithLetter = char.IsLetter(token[0]);
        var endsWithLetter = char.IsLetter(token[^1]);
        var end = index + token.Length;

        var leftOk = !startsWithLetter || index == 0 || !char.IsLetter(text[index - 1]);
        var rightOk = !endsWithLetter || end >= text.Length || !char.IsLetter(text[end]);
        return leftOk && rightOk;
    }

    private static int Distance(Match number, CurrencyOccurrence currency)
    {
        var numberEnd = number.Index + number.Length;
        var currencyEnd = currency.Index + currency.Length;

        if (currencyEnd <= number.Index)
        {
            return number.Index - currencyEnd;
        }

        return currency.Index >= numberEnd ? currency.Index - numberEnd : 0;
    }

    private static string NormalizeWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(char.IsWhiteSpace(c) || c is ' ' or ' ' or ' ' or ' ' ? ' ' : c);
        }

        return builder.ToString().Trim();
    }

    [GeneratedRegex(@"(?<!\d)(?:\d{1,3}(?:[ '’.,]\d{3})+(?:[.,]\d+)?|\d+(?:[.,]\d+)?)(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberToken();

    private readonly record struct CurrencyOccurrence(int Index, int Length, string Symbol);
}
