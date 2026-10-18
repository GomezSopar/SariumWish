using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PriceTrackerWorkspace.Data;
using PriceTrackerWorkspace.Domain;

namespace PriceTrackerWorkspace.Scraping;

public sealed class ScraperService(
    IDbContextFactory<AppDbContext> dbFactory,
    PlaywrightBrowserManager browserManager,
    IOptions<ScraperOptions> options,
    TimeProvider timeProvider,
    ILogger<ScraperService> logger) : IScraperService
{
    private const int MaxProductNameLength = 300;
    private const string JsonLdSource = "script[type=\"application/ld+json\"]";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ScraperOptions _options = options.Value;

    public async Task<ScrapeResult> RefreshItemAsync(int wishlistItemId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.WishlistItems.SingleOrDefaultAsync(i => i.Id == wishlistItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Wishlist item {wishlistItemId} does not exist.");

        if (!DomainNormalizer.TryNormalizeUrl(item.ProductUrl, out var domain))
        {
            item.MarkScrapeFailed("The product URL is not a valid http(s) address.", UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(item, ScrapeSource.None, null);
        }

        item.NormalizedDomain = domain;

        var rule = await db.DomainScrapeRules
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.NormalizedDomain == domain, cancellationToken);

        var plan = BuildSelectorPlan(item, rule);

        PageExtraction extraction;
        try
        {
            extraction = await browserManager.ExecuteInPageAsync(
                page => ExtractAsync(page, item.ProductUrl, plan),
                blockHeavyResources: true,
                cancellationToken);
        }
        catch (Exception ex) when (IsScrapeFailure(ex))
        {
            logger.LogWarning(ex, "Scraping {Url} failed", item.ProductUrl);
            item.MarkScrapeFailed(DescribeFailure(ex), UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(item, ScrapeSource.None, null);
        }

        var now = UtcNow;

        if (extraction.Price is { } price)
        {
            item.ApplyScrapedPrice(price.Amount, price.CurrencySymbol, now);
        }
        else
        {
            item.MarkNeedsElementSelection(extraction.FailureReason ?? "No price element could be located.", now);
        }

        AdoptPageTitleIfPlaceholder(item, extraction.PageTitle);

        await db.SaveChangesAsync(cancellationToken);
        await UpdateRuleStatisticsAsync(db, rule, extraction, now, cancellationToken);

        if (rule is null && extraction is { Source: ScrapeSource.Heuristic, IsLearnable: true, MatchedSelector: { } learned })
        {
            await TryLearnRuleAsync(db, domain, learned, now, cancellationToken);
        }

        logger.LogInformation(
            "Scraped item {ItemId} on {Domain}: {Status} via {Source} ({Selector})",
            item.Id, domain, item.Status, extraction.Source, extraction.MatchedSelector ?? "none");

        return ToResult(item, extraction.Source, extraction.MatchedSelector);
    }

    public async Task<IReadOnlyList<ScrapeResult>> RefreshItemsAsync(
        IReadOnlyCollection<int> wishlistItemIds,
        IProgress<RefreshProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentBag<ScrapeResult>();
        var completed = 0;
        var total = wishlistItemIds.Count;

        progress?.Report(new RefreshProgress(0, total, null));

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxConcurrentPages,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(wishlistItemIds, parallelOptions, async (id, token) =>
        {
            ScrapeResult? result = null;
            try
            {
                result = await RefreshItemAsync(id, token);
                results.Add(result);
            }
            catch (KeyNotFoundException)
            {
                logger.LogDebug("Wishlist item {ItemId} was deleted before it could be refreshed", id);
            }

            progress?.Report(new RefreshProgress(Interlocked.Increment(ref completed), total, result));
        });

        return [.. results];
    }

    public async Task<SelectorTestResult> TestSelectorAsync(string productUrl, string cssSelector, CancellationToken cancellationToken = default)
    {
        if (!DomainNormalizer.TryNormalizeUrl(productUrl, out _))
        {
            return new SelectorTestResult(false, null, null, "The product URL is not a valid http(s) address.");
        }

        if (string.IsNullOrWhiteSpace(cssSelector))
        {
            return new SelectorTestResult(false, null, null, "Enter a CSS selector to test.");
        }

        try
        {
            return await browserManager.ExecuteInPageAsync(async page =>
            {
                await page.GotoAndSettleAsync(productUrl, _options.NetworkIdleSettleMs);
                var text = await page.ReadSelectorTextAsync(cssSelector.Trim(), _options.SelectorTimeoutMs);

                if (text is null)
                {
                    return new SelectorTestResult(false, null, null, "The selector did not match any element on the live page.");
                }

                var parsed = PriceParser.Parse(text);
                return parsed is { IsPositive: true }
                    ? new SelectorTestResult(true, text, parsed, null)
                    : new SelectorTestResult(false, text, parsed, "The matched element does not contain a recognizable price.");
            }, blockHeavyResources: true, cancellationToken);
        }
        catch (Exception ex) when (IsScrapeFailure(ex))
        {
            return new SelectorTestResult(false, null, null, DescribeFailure(ex));
        }
    }

    public async Task<VisualSelectionResult> SaveVisualSelectionAsync(VisualSelection selection, CancellationToken cancellationToken = default)
    {
        var selector = selection.CssSelector.Trim();
        if (selector.Length == 0)
        {
            throw new ArgumentException("A CSS selector is required.", nameof(selection));
        }

        if (!PriceParser.TryParse(selection.RawText, out var parsed) || !parsed.IsPositive)
        {
            throw new ArgumentException("The selected element does not contain a recognizable price.", nameof(selection));
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.WishlistItems.SingleOrDefaultAsync(i => i.Id == selection.WishlistItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Wishlist item {selection.WishlistItemId} does not exist.");

        if (!DomainNormalizer.TryNormalizeUrl(item.ProductUrl, out var domain))
        {
            throw new InvalidOperationException("The wishlist item has an invalid product URL.");
        }

        var now = UtcNow;
        item.NormalizedDomain = domain;

        var rule = await db.DomainScrapeRules.SingleOrDefaultAsync(r => r.NormalizedDomain == domain, cancellationToken);
        if (rule is null)
        {
            rule = new DomainScrapeRule { NormalizedDomain = domain, PriceCssSelector = selector };
            db.DomainScrapeRules.Add(rule);
        }
        else if (!string.Equals(rule.PriceCssSelector, selector, StringComparison.Ordinal))
        {
            rule.PriceCssSelector = selector;
            rule.SuccessCount = 0;
            rule.FailureCount = 0;
        }

        rule.SuccessCount++;
        rule.LastVerifiedAt = now;

        if (item.CustomCssSelectorOverride is { } overrideSelector && !string.Equals(overrideSelector, selector, StringComparison.Ordinal))
        {
            item.CustomCssSelectorOverride = null;
        }

        item.ApplyScrapedPrice(parsed.Amount, parsed.CurrencySymbol, now);
        AdoptPageTitleIfPlaceholder(item, selection.PageTitle);

        await db.SaveChangesAsync(cancellationToken);

        var otherItemIds = await db.WishlistItems
            .Where(w => w.NormalizedDomain == domain && w.Id != item.Id)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Saved visually picked selector {Selector} for {Domain}", selector, domain);

        return new VisualSelectionResult(ToResult(item, ScrapeSource.VisualPicker, selector), otherItemIds);
    }

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    private async Task<PageExtraction> ExtractAsync(IPage page, string url, IReadOnlyList<SelectorStep> plan)
    {
        var response = await page.GotoAndSettleAsync(url, _options.NetworkIdleSettleMs);
        var ruleFailed = false;

        foreach (var step in plan)
        {
            var text = await page.ReadSelectorTextAsync(step.Selector, _options.SelectorTimeoutMs);
            if (PriceParser.TryParse(text, out var parsed) && parsed.IsPositive)
            {
                return new PageExtraction(parsed, step.Selector, step.Source, ruleFailed, await page.TitleAsync());
            }

            ruleFailed |= step.Source == ScrapeSource.DomainRule;
        }

        var json = await page.EvaluateAsync<string>(ScriptResources.DetectPrice);
        var detection = JsonSerializer.Deserialize<HeuristicDetection>(json, JsonOptions) ?? new HeuristicDetection(null, []);

        foreach (var candidate in detection.Candidates)
        {
            if (TryAcceptCandidate(candidate, out var price))
            {
                return new PageExtraction(price, candidate.Selector ?? JsonLdSource, ScrapeSource.Heuristic, ruleFailed, detection.Title)
                {
                    IsLearnable = candidate.Selector is not null && candidate.Kind is not "broad"
                };
            }
        }

        var reason = response?.Status >= 400
            ? $"The site responded with HTTP {response.Status} and may be blocking automated browsers. Pick the price element manually."
            : "No reliable price element was detected. Use the visual picker to select it.";

        return new PageExtraction(null, null, ScrapeSource.None, ruleFailed, detection.Title) { FailureReason = reason };
    }

    private static bool TryAcceptCandidate(HeuristicCandidate candidate, out ParsedPrice price)
    {
        price = default;

        if (!PriceParser.TryParse(candidate.Text, out var parsed) || !parsed.IsPositive)
        {
            return false;
        }

        price = parsed with { CurrencySymbol = parsed.CurrencySymbol ?? PriceParser.ResolveCurrencySymbol(candidate.Currency) };

        return candidate.Kind switch
        {
            "structured" or "specific" => true,
            "generic" => price.HasCurrency,
            "broad" => parsed.HasCurrency && candidate.Text.Length <= 40,
            _ => false
        };
    }

    private static List<SelectorStep> BuildSelectorPlan(WishlistItem item, DomainScrapeRule? rule)
    {
        List<SelectorStep> plan = [];

        if (!string.IsNullOrWhiteSpace(item.CustomCssSelectorOverride))
        {
            plan.Add(new SelectorStep(item.CustomCssSelectorOverride.Trim(), ScrapeSource.ItemOverride));
        }

        if (rule is not null && plan.All(step => step.Selector != rule.PriceCssSelector))
        {
            plan.Add(new SelectorStep(rule.PriceCssSelector, ScrapeSource.DomainRule));
        }

        return plan;
    }

    private static async Task UpdateRuleStatisticsAsync(
        AppDbContext db,
        DomainScrapeRule? rule,
        PageExtraction extraction,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (rule is null)
        {
            return;
        }

        var rules = db.DomainScrapeRules.Where(r => r.Id == rule.Id);

        if (extraction.Source == ScrapeSource.DomainRule)
        {
            await rules.ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.SuccessCount, r => r.SuccessCount + 1)
                .SetProperty(r => r.LastVerifiedAt, now), cancellationToken);
        }
        else if (extraction.RuleFailed)
        {
            await rules.ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.FailureCount, r => r.FailureCount + 1), cancellationToken);
        }
    }

    private async Task TryLearnRuleAsync(AppDbContext db, string domain, string selector, DateTime now, CancellationToken cancellationToken)
    {
        var inserted = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO DomainScrapeRules (NormalizedDomain, PriceCssSelector, LastVerifiedAt, SuccessCount, FailureCount)
            SELECT {domain}, {selector}, {now}, 1, 0
            WHERE NOT EXISTS (
                SELECT 1 FROM DomainScrapeRules WITH (UPDLOCK, HOLDLOCK) WHERE NormalizedDomain = {domain})
            """,
            cancellationToken);

        if (inserted > 0)
        {
            logger.LogInformation("Learned price selector {Selector} for {Domain}", selector, domain);
        }
    }

    private static void AdoptPageTitleIfPlaceholder(WishlistItem item, string? pageTitle)
    {
        var isPlaceholder = string.IsNullOrWhiteSpace(item.ProductName)
            || string.Equals(item.ProductName, item.NormalizedDomain, StringComparison.OrdinalIgnoreCase);

        if (!isPlaceholder || string.IsNullOrWhiteSpace(pageTitle))
        {
            return;
        }

        var title = string.Join(' ', pageTitle.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var primary = title.Split(" | ", 2, StringSplitOptions.TrimEntries)[0];
        item.ProductName = primary.Length > MaxProductNameLength ? primary[..MaxProductNameLength] : primary;
    }

    private static ScrapeResult ToResult(WishlistItem item, ScrapeSource source, string? selector) => new(
        item.Id,
        item.Status,
        item.HasPrice ? item.CurrentPrice : null,
        item.CurrencySymbol,
        selector,
        source,
        item.LastErrorMessage);

    private static bool IsScrapeFailure(Exception exception) =>
        exception is PlaywrightException or TimeoutException or InvalidOperationException or JsonException;

    private static string DescribeFailure(Exception exception)
    {
        var message = exception.Message;

        if (exception is TimeoutException)
        {
            return "The page took too long to load.";
        }

        if (message.Contains("ERR_NAME_NOT_RESOLVED", StringComparison.Ordinal))
        {
            return "The site could not be reached (DNS lookup failed).";
        }

        if (message.Contains("ERR_CONNECTION", StringComparison.Ordinal) || message.Contains("ERR_INTERNET_DISCONNECTED", StringComparison.Ordinal))
        {
            return "The site refused or dropped the connection.";
        }

        if (message.StartsWith("spawn ", StringComparison.Ordinal) || message.Contains("<launching>", StringComparison.Ordinal))
        {
            return "The headless browser could not be started. Check the Scraper:BrowserChannel setting and the Playwright browser installation.";
        }

        var firstLine = message.Split('\n', 2)[0].Trim();
        return firstLine.Length > 300 ? firstLine[..300] : firstLine;
    }

    private sealed record SelectorStep(string Selector, ScrapeSource Source);

    private sealed record PageExtraction(
        ParsedPrice? Price,
        string? MatchedSelector,
        ScrapeSource Source,
        bool RuleFailed,
        string? PageTitle)
    {
        public bool IsLearnable { get; init; }

        public string? FailureReason { get; init; }
    }

    private sealed record HeuristicDetection(string? Title, IReadOnlyList<HeuristicCandidate> Candidates);

    private sealed record HeuristicCandidate(string Kind, string? Selector, string Text, string? Currency);
}
