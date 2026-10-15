namespace PriceTrackerWorkspace.Domain;

public class DomainScrapeRule
{
    public int Id { get; set; }

    public required string NormalizedDomain { get; set; }

    public required string PriceCssSelector { get; set; }

    public string? TitleCssSelector { get; set; }

    public DateTime LastVerifiedAt { get; set; }

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }

    public double SuccessRate => SuccessCount + FailureCount == 0
        ? 0
        : (double)SuccessCount / (SuccessCount + FailureCount);
}
