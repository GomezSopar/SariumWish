using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace PriceTrackerWorkspace.Scraping;

public sealed class PlaywrightBrowserManager(IOptions<ScraperOptions> options, ILogger<PlaywrightBrowserManager> logger) : IAsyncDisposable
{
    private static readonly string[] HeavyResourceTypes = ["image", "media", "font"];

    private const string StealthInitScript =
        "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });";

    private readonly ScraperOptions _options = options.Value;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _pageSlots = new(options.Value.MaxConcurrentPages, options.Value.MaxConcurrentPages);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public async Task<T> ExecuteInPageAsync<T>(
        Func<IPage, Task<T>> work,
        bool blockHeavyResources,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _pageSlots.WaitAsync(cancellationToken);
        try
        {
            var browser = await GetBrowserAsync(cancellationToken);
            var context = await browser.NewContextAsync(CreateContextOptions());

            await using var abortOnCancel = cancellationToken.Register(() => _ = context.CloseAsync());
            try
            {
                await context.AddInitScriptAsync(StealthInitScript);

                if (blockHeavyResources)
                {
                    await context.RouteAsync("**/*", route =>
                        HeavyResourceTypes.Contains(route.Request.ResourceType) ? route.AbortAsync() : route.ContinueAsync());
                }

                var page = await context.NewPageAsync();
                page.SetDefaultNavigationTimeout(_options.NavigationTimeoutMs);
                page.SetDefaultTimeout(_options.SelectorTimeoutMs);

                return await work(page);
            }
            catch (PlaywrightException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            finally
            {
                await context.CloseAsync();
            }
        }
        finally
        {
            _pageSlots.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _initializationLock.Dispose();
        _pageSlots.Dispose();
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true } ready)
        {
            return ready;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser is { IsConnected: true } initialized)
            {
                return initialized;
            }

            _playwright ??= await Playwright.CreateAsync();

            if (_browser is not null)
            {
                logger.LogWarning("Headless browser disconnected unexpectedly; relaunching");
                await _browser.DisposeAsync();
            }

            _browser = await LaunchAsync(_playwright);
            logger.LogInformation("Launched headless Chromium {Version}", _browser.Version);
            return _browser;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<IBrowser> LaunchAsync(IPlaywright playwright)
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            Channel = string.IsNullOrWhiteSpace(_options.BrowserChannel) ? null : _options.BrowserChannel,
            Args = ["--disable-blink-features=AutomationControlled", "--no-first-run", "--no-default-browser-check"]
        };

        try
        {
            return await playwright.Chromium.LaunchAsync(launchOptions);
        }
        catch (PlaywrightException ex) when (_options.AutoInstallBrowser && IsMissingExecutable(ex))
        {
            logger.LogWarning("Playwright Chromium build not found; installing it now (one-time download)");

            var exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "chromium"]));
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Playwright could not install Chromium (exit code {exitCode}). Run 'pwsh bin/Debug/net10.0/playwright.ps1 install chromium' manually.",
                    ex);
            }

            return await playwright.Chromium.LaunchAsync(launchOptions);
        }
    }

    private BrowserNewContextOptions CreateContextOptions() => new()
    {
        UserAgent = _options.UserAgent,
        Locale = _options.Locale,
        TimezoneId = _options.TimezoneId,
        ViewportSize = new ViewportSize { Width = _options.ViewportWidth, Height = _options.ViewportHeight },
        DeviceScaleFactor = 1,
        JavaScriptEnabled = true,
        IgnoreHTTPSErrors = false,
        ExtraHTTPHeaders = new Dictionary<string, string>
        {
            ["Accept-Language"] = $"{_options.Locale},{_options.Locale.Split('-')[0]};q=0.9"
        }
    };

    private static bool IsMissingExecutable(PlaywrightException exception) =>
        exception.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("playwright install", StringComparison.OrdinalIgnoreCase);
}
