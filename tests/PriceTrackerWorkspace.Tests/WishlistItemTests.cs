using PriceTrackerWorkspace.Domain;

namespace PriceTrackerWorkspace.Tests;

public class WishlistItemTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static WishlistItem CreateItem() => new()
    {
        ProductName = "Widget",
        ProductUrl = "https://shop.example.com/widget",
        NormalizedDomain = "shop.example.com"
    };

    [Fact]
    public void First_scrape_sets_price_without_previous_price()
    {
        var item = CreateItem();

        item.ApplyScrapedPrice(49.99m, "€", Now);

        Assert.Equal(49.99m, item.CurrentPrice);
        Assert.Null(item.PreviousPrice);
        Assert.Equal("€", item.CurrencySymbol);
        Assert.Equal(WishlistItemStatus.Verified, item.Status);
        Assert.False(item.HasPriceDrop);
    }

    [Fact]
    public void Price_change_moves_current_price_to_previous_and_flags_drop()
    {
        var item = CreateItem();
        item.ApplyScrapedPrice(50m, "$", Now);

        item.ApplyScrapedPrice(42.50m, "$", Now.AddHours(1));

        Assert.Equal(50m, item.PreviousPrice);
        Assert.True(item.HasPriceDrop);
        Assert.Equal(-7.50m, item.PriceChange);
    }

    [Fact]
    public void Unchanged_price_keeps_last_known_previous_price()
    {
        var item = CreateItem();
        item.ApplyScrapedPrice(50m, "$", Now);
        item.ApplyScrapedPrice(45m, "$", Now.AddHours(1));

        item.ApplyScrapedPrice(45m, "$", Now.AddHours(2));

        Assert.Equal(50m, item.PreviousPrice);
        Assert.True(item.HasPriceDrop);
    }

    [Fact]
    public void Missing_currency_keeps_existing_symbol()
    {
        var item = CreateItem();
        item.ApplyScrapedPrice(10m, "£", Now);

        item.ApplyScrapedPrice(12m, null, Now);

        Assert.Equal("£", item.CurrencySymbol);
        Assert.True(item.HasPriceIncrease);
    }

    [Fact]
    public void Failure_states_keep_last_known_price()
    {
        var item = CreateItem();
        item.ApplyScrapedPrice(10m, "$", Now);

        item.MarkNeedsElementSelection("No price element found.", Now.AddDays(1));

        Assert.Equal(10m, item.CurrentPrice);
        Assert.Equal(WishlistItemStatus.NeedsElementSelection, item.Status);
        Assert.Equal("No price element found.", item.LastErrorMessage);
        Assert.Equal(Now.AddDays(1), item.LastScrapedAt);
    }

    [Fact]
    public void Rejects_non_positive_prices()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateItem().ApplyScrapedPrice(0m, "$", Now));
    }
}
