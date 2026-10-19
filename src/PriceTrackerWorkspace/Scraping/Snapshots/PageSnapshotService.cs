using Microsoft.Extensions.Options;

namespace PriceTrackerWorkspace.Scraping.Snapshots;

public sealed class PageSnapshotService(
    PlaywrightBrowserManager browserManager,
    IOptions<ScraperOptions> options,
    ILogger<PageSnapshotService> logger) : IPageSnapshotService
{
    private const string AutoScrollScript = """
        async () => {
            const step = Math.max(400, Math.floor(window.innerHeight * 0.8));
            const limit = Math.min(document.body?.scrollHeight ?? 0, 6000);
            for (let y = 0; y < limit; y += step) {
                window.scrollTo(0, y);
                await new Promise((resolve) => setTimeout(resolve, 120));
            }
            window.scrollTo(0, 0);
        }
        """;

    private readonly ScraperOptions _options = options.Value;

    public async Task<PageSnapshot> CaptureAsync(string productUrl, CancellationToken cancellationToken = default)
    {
        if (!DomainNormalizer.TryNormalizeUrl(productUrl, out var domain))
        {
            throw new ArgumentException("The product URL must be an absolute http(s) address.", nameof(productUrl));
        }

        return await browserManager.ExecuteInPageAsync(async page =>
        {
            await page.GotoAndSettleAsync(productUrl, _options.NetworkIdleSettleMs);
            await page.EvaluateAsync(AutoScrollScript);
            await page.SettleAsync(_options.NetworkIdleSettleMs);

            var title = await page.TitleAsync();
            var baseHref = await page.EvaluateAsync<string>(ScriptResources.PrepareSnapshot);
            var html = await page.ContentAsync();

            logger.LogDebug("Captured {Length:N0} characters of DOM from {Url}", html.Length, page.Url);

            var document = SnapshotDocumentComposer.Compose(html, baseHref, ScriptResources.Inspector, ScriptResources.InspectorStyles);
            return new PageSnapshot(document, page.Url, title, domain);
        }, blockHeavyResources: false, cancellationToken);
    }
}
