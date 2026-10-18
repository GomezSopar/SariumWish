using Microsoft.Playwright;

namespace PriceTrackerWorkspace.Scraping;

internal static class PageNavigationExtensions
{
    public static async Task<IResponse?> GotoAndSettleAsync(this IPage page, string url, int networkIdleSettleMs)
    {
        var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.SettleAsync(networkIdleSettleMs);
        return response;
    }

    public static async Task SettleAsync(this IPage page, int networkIdleSettleMs)
    {
        if (networkIdleSettleMs <= 0)
        {
            return;
        }

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = networkIdleSettleMs });
        }
        catch (TimeoutException)
        {
        }
    }

    public static async Task<string?> ReadSelectorTextAsync(this IPage page, string cssSelector, int timeoutMs)
    {
        try
        {
            var locator = page.Locator($"css={cssSelector}").First;
            await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = timeoutMs });

            var text = (await locator.InnerTextAsync(new LocatorInnerTextOptions { Timeout = timeoutMs })).Trim();
            if (text.Length > 0)
            {
                return text;
            }

            var content = await locator.GetAttributeAsync("content");
            return string.IsNullOrWhiteSpace(content) ? (await locator.TextContentAsync())?.Trim() : content.Trim();
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (PlaywrightException)
        {
            return null;
        }
    }
}
