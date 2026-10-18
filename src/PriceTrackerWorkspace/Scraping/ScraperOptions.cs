using System.ComponentModel.DataAnnotations;

namespace PriceTrackerWorkspace.Scraping;

public sealed class ScraperOptions
{
    public const string SectionName = "Scraper";

    [Required]
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36";

    [Required]
    public string Locale { get; set; } = "en-US";

    public string? TimezoneId { get; set; }

    [Range(800, 3840)]
    public int ViewportWidth { get; set; } = 1440;

    [Range(600, 2160)]
    public int ViewportHeight { get; set; } = 900;

    public string? BrowserChannel { get; set; }

    public bool Headless { get; set; } = true;

    public bool AutoInstallBrowser { get; set; } = true;

    [Range(1, 16)]
    public int MaxConcurrentPages { get; set; } = 3;

    [Range(1_000, 120_000)]
    public int NavigationTimeoutMs { get; set; } = 30_000;

    [Range(0, 30_000)]
    public int NetworkIdleSettleMs { get; set; } = 4_000;

    [Range(250, 30_000)]
    public int SelectorTimeoutMs { get; set; } = 5_000;
}
