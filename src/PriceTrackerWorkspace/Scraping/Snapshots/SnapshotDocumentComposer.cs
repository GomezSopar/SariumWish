using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PriceTrackerWorkspace.Scraping.Snapshots;

public static partial class SnapshotDocumentComposer
{
    public static string Compose(string html, string baseHref, string inspectorScript, string inspectorStyles)
    {
        ArgumentNullException.ThrowIfNull(html);

        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var sanitized = StripExecutableMarkup(html);

        var headPrelude =
            $"""<meta http-equiv="Content-Security-Policy" content="script-src 'nonce-{nonce}'; object-src 'none'; form-action 'none'; frame-src 'none'">"""
            + $"""<base href="{WebUtility.HtmlEncode(baseHref)}">""";

        var withHead = HeadOpenTag().Match(sanitized) is { Success: true } head
            ? sanitized.Insert(head.Index + head.Length, headPrelude)
            : InsertHead(sanitized, headPrelude);

        var inspectorBlock =
            $"""<style id="__pt-inspector-style">{inspectorStyles}</style><script id="__pt-inspector" nonce="{nonce}">{inspectorScript}</script>""";

        var bodyClose = withHead.LastIndexOf("</body", StringComparison.OrdinalIgnoreCase);
        return bodyClose >= 0
            ? withHead.Insert(bodyClose, inspectorBlock)
            : withHead + inspectorBlock;
    }

    internal static string StripExecutableMarkup(string html)
    {
        var withoutScripts = ScriptElement().Replace(html, string.Empty);
        var withoutNoscript = NoscriptElement().Replace(withoutScripts, string.Empty);
        var withoutDangling = DanglingScriptTag().Replace(withoutNoscript, string.Empty);
        var withoutBase = BaseTag().Replace(withoutDangling, string.Empty);
        return MetaHttpEquivTag().Replace(withoutBase, string.Empty);
    }

    private static string InsertHead(string html, string headPrelude)
    {
        var htmlOpen = HtmlOpenTag().Match(html);
        var head = $"<head>{headPrelude}</head>";
        return htmlOpen.Success ? html.Insert(htmlOpen.Index + htmlOpen.Length, head) : head + html;
    }

    [GeneratedRegex(@"<script\b[^>]*>[\s\S]*?</script\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptElement();

    [GeneratedRegex(@"<noscript\b[^>]*>[\s\S]*?</noscript\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoscriptElement();

    [GeneratedRegex(@"</?(?:script|noscript)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DanglingScriptTag();

    [GeneratedRegex(@"<base\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BaseTag();

    [GeneratedRegex(@"<meta\b[^>]*\bhttp-equiv\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaHttpEquivTag();

    [GeneratedRegex(@"<head\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeadOpenTag();

    [GeneratedRegex(@"<html\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlOpenTag();
}
