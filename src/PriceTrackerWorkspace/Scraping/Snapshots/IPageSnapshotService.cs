namespace PriceTrackerWorkspace.Scraping.Snapshots;

public interface IPageSnapshotService
{
    /// <summary>
    /// Renders the page in headless Chromium and returns a script-free HTML document with a nonce-scoped
    /// element inspector injected, intended to be loaded into a sandboxed iframe via <c>srcdoc</c>.
    /// </summary>
    Task<PageSnapshot> CaptureAsync(string productUrl, CancellationToken cancellationToken = default);
}
