using System.Text.RegularExpressions;
using PriceTrackerWorkspace.Scraping.Snapshots;

namespace PriceTrackerWorkspace.Tests;

public class SnapshotDocumentComposerTests
{
    private const string Inspector = "window.__inspector = true;";
    private const string Styles = ".__pt-hover{outline:2px solid #3b82f6}";

    private const string Page = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta http-equiv="refresh" content="0;url=https://evil.example">
            <base href="/old/">
            <title>Widget</title>
            <script>if (top !== self) top.location = self.location;</script>
        </head>
        <body>
            <noscript><img src="pixel.gif"></noscript>
            <span class="price">$19.99</span>
            <SCRIPT type="module" src="app.js"></SCRIPT>
        </body>
        </html>
        """;

    [Fact]
    public void Removes_scripts_noscript_meta_refresh_and_existing_base()
    {
        var document = SnapshotDocumentComposer.Compose(Page, "https://shop.example.com/p/1", Inspector, Styles);

        Assert.DoesNotContain("top.location", document);
        Assert.DoesNotContain("app.js", document);
        Assert.DoesNotContain("<noscript", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", document);
        Assert.DoesNotContain("/old/", document);
        Assert.Contains("<span class=\"price\">$19.99</span>", document);
    }

    [Fact]
    public void Injects_base_href_and_csp_at_the_start_of_head()
    {
        var document = SnapshotDocumentComposer.Compose(Page, "https://shop.example.com/p/1?a=1&b=2", Inspector, Styles);

        var headIndex = document.IndexOf("<head>", StringComparison.Ordinal);
        var cspIndex = document.IndexOf("Content-Security-Policy", StringComparison.Ordinal);
        var baseIndex = document.IndexOf("<base href=\"https://shop.example.com/p/1?a=1&amp;b=2\">", StringComparison.Ordinal);
        var titleIndex = document.IndexOf("<title>", StringComparison.Ordinal);

        Assert.True(headIndex >= 0 && cspIndex > headIndex && baseIndex > cspIndex && titleIndex > baseIndex);
    }

    [Fact]
    public void Appends_inspector_with_matching_nonce_before_body_close()
    {
        var document = SnapshotDocumentComposer.Compose(Page, "https://shop.example.com/", Inspector, Styles);

        var nonce = Regex.Match(document, "'nonce-([^']+)'").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(nonce));

        var scriptTag = $"<script id=\"__pt-inspector\" nonce=\"{nonce}\">{Inspector}</script>";
        var scriptIndex = document.IndexOf(scriptTag, StringComparison.Ordinal);
        var bodyCloseIndex = document.LastIndexOf("</body>", StringComparison.Ordinal);

        Assert.True(scriptIndex > 0 && scriptIndex < bodyCloseIndex);
        Assert.Contains($"<style id=\"__pt-inspector-style\">{Styles}</style>", document);
        Assert.Equal(1, Regex.Count(document, "<script", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Generates_a_fresh_nonce_per_snapshot()
    {
        var first = SnapshotDocumentComposer.Compose(Page, "https://a.example/", Inspector, Styles);
        var second = SnapshotDocumentComposer.Compose(Page, "https://a.example/", Inspector, Styles);

        Assert.NotEqual(
            Regex.Match(first, "'nonce-([^']+)'").Value,
            Regex.Match(second, "'nonce-([^']+)'").Value);
    }

    [Fact]
    public void Handles_fragments_without_head_or_body()
    {
        var document = SnapshotDocumentComposer.Compose("<div>£5.00</div>", "https://a.example/", Inspector, Styles);

        Assert.StartsWith("<head>", document);
        Assert.Contains("<div>£5.00</div>", document);
        Assert.EndsWith("</script>", document);
    }
}
