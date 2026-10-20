using PriceTrackerWorkspace.Scraping;

namespace PriceTrackerWorkspace.Tests;

public class DomainNormalizerTests
{
    [Theory]
    [InlineData("https://www.amazon.com/dp/B0C1234", "amazon.com")]
    [InlineData("http://shop.example.co.uk/item?id=4", "shop.example.co.uk")]
    [InlineData("HTTPS://WWW.Example.COM/Path", "example.com")]
    [InlineData("https://www2.example.com/", "www2.example.com")]
    [InlineData("  https://books.toscrape.com/catalogue/x.html  ", "books.toscrape.com")]
    public void Normalizes_product_urls(string url, string expected)
    {
        Assert.True(DomainNormalizer.TryNormalizeUrl(url, out var domain));
        Assert.Equal(expected, domain);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file")]
    [InlineData("/relative/path")]
    public void Rejects_non_http_urls(string? url)
    {
        Assert.False(DomainNormalizer.TryNormalizeUrl(url, out _));
    }

    [Theory]
    [InlineData("www.store.example.com", "store.example.com")]
    [InlineData("Store.Example.com", "store.example.com")]
    [InlineData("https://www.example.com/anything", "example.com")]
    public void Normalizes_manually_entered_domains(string input, string expected)
    {
        Assert.True(DomainNormalizer.TryNormalizeDomainInput(input, out var domain));
        Assert.Equal(expected, domain);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("   ")]
    public void Rejects_domains_without_a_tld(string input)
    {
        Assert.False(DomainNormalizer.TryNormalizeDomainInput(input, out _));
    }
}
