using Microsoft.EntityFrameworkCore;
using PriceTrackerWorkspace.Components;
using PriceTrackerWorkspace.Data;
using PriceTrackerWorkspace.Scraping;
using PriceTrackerWorkspace.Scraping.Snapshots;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());

var seedSampleData = builder.Configuration.GetValue("Database:SeedSampleData", true);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("WorkspaceDb")
            ?? throw new InvalidOperationException("Connection string 'WorkspaceDb' is not configured."),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 3));

    if (seedSampleData)
    {
        options.UseAsyncSeeding((context, _, cancellationToken) => SampleDataSeeder.SeedAsync(context, cancellationToken));
    }
});

builder.Services.AddOptions<ScraperOptions>()
    .Bind(builder.Configuration.GetSection(ScraperOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PlaywrightBrowserManager>();
builder.Services.AddSingleton<IPageSnapshotService, PageSnapshotService>();
builder.Services.AddSingleton<IScraperService, ScraperService>();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRequestLocalization(options => options
    .SetDefaultCulture("en-US")
    .AddSupportedCultures("en-US")
    .AddSupportedUICultures("en-US"));

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
