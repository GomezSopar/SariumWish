using PriceTrackerWorkspace.Scraping;

namespace PriceTrackerWorkspace.Tests;

public class PriceParserTests
{
    [Theory]
    [InlineData("US$ 1,299.99", 1299.99, "$")]
    [InlineData("1.299,99 €", 1299.99, "€")]
    [InlineData("£45.00 incl. VAT", 45.00, "£")]
    [InlineData("$1,234.56", 1234.56, "$")]
    [InlineData("1.234,56", 1234.56, null)]
    [InlineData("€ 12", 12, "€")]
    [InlineData("12,5 €", 12.5, "€")]
    [InlineData("1 299,99 €", 1299.99, "€")]
    [InlineData("1 299,99 €", 1299.99, "€")]
    [InlineData("CHF 1'249.00", 1249.00, "CHF")]
    [InlineData("¥12,800", 12800, "¥")]
    [InlineData("R$ 2.499,90", 2499.90, "R$")]
    [InlineData("1.234.567,89 €", 1234567.89, "€")]
    [InlineData("1,234,567.89", 1234567.89, null)]
    [InlineData("Price: 89.95 USD", 89.95, "$")]
    [InlineData("EUR 19,99", 19.99, "€")]
    [InlineData("349 kr", 349, "kr")]
    [InlineData("C$ 59.00", 59.00, "C$")]
    [InlineData("  $ 9.99  ", 9.99, "$")]
    [InlineData("1299", 1299, null)]
    public void Parses_noisy_price_strings(string input, double expectedAmount, string? expectedSymbol)
    {
        var parsed = PriceParser.Parse(input);

        Assert.NotNull(parsed);
        Assert.Equal((decimal)expectedAmount, parsed.Value.Amount);
        Assert.Equal(expectedSymbol, parsed.Value.CurrencySymbol);
    }

    [Theory]
    [InlineData("1,299", 1299)]
    [InlineData("1.299", 1299)]
    [InlineData("0.999", 0.999)]
    [InlineData("12,50", 12.50)]
    [InlineData("12.5", 12.5)]
    public void Single_separator_is_resolved_by_trailing_digit_count(string token, double expected)
    {
        Assert.True(PriceParser.TryInterpretNumber(token, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void Chooses_the_number_closest_to_the_currency_symbol()
    {
        var parsed = PriceParser.Parse("Save 20% today — now only £39.99 (2 left)");

        Assert.Equal(new ParsedPrice(39.99m, "£"), parsed);
    }

    [Fact]
    public void Does_not_treat_letters_inside_words_as_currency()
    {
        var parsed = PriceParser.Parse("Ukraine edition 25");

        Assert.NotNull(parsed);
        Assert.Null(parsed.Value.CurrencySymbol);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Out of stock")]
    [InlineData("€")]
    public void Returns_null_when_no_number_is_present(string? input)
    {
        Assert.Null(PriceParser.Parse(input));
    }

    [Theory]
    [InlineData("USD", "$")]
    [InlineData("eur", "€")]
    [InlineData("GBP", "£")]
    [InlineData("£", "£")]
    [InlineData("XYZ", null)]
    [InlineData(null, null)]
    public void Resolves_iso_codes_to_display_symbols(string? code, string? expected)
    {
        Assert.Equal(expected, PriceParser.ResolveCurrencySymbol(code));
    }
}
