using Microsoft.EntityFrameworkCore;
using PriceTrackerWorkspace.Domain;

namespace PriceTrackerWorkspace.Data;

public static class SampleDataSeeder
{
    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        var db = (AppDbContext)context;

        if (await db.WorkspaceProjects.AnyAsync(cancellationToken) || await db.WishlistItems.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var readingNook = new WorkspaceProject
        {
            Name = "Reading Nook Makeover",
            Description = "Convert the spare corner into a cozy reading space with a curated starter library.",
            TargetBudget = 250m,
            CreatedAt = now,
            TodoItems =
            [
                new TodoItem { Title = "Measure the corner and window clearance", Priority = TodoPriority.High, DueDate = today.AddDays(2), CreatedAt = now },
                new TodoItem { Title = "Shortlist three classic novels", Priority = TodoPriority.Medium, DueDate = today.AddDays(5), CreatedAt = now },
                new TodoItem { Title = "Pick a warm 2700K reading lamp", Priority = TodoPriority.Low, CreatedAt = now }
            ],
            WishlistItems =
            [
                new WishlistItem
                {
                    ProductName = "A Light in the Attic",
                    ProductUrl = "https://books.toscrape.com/catalogue/a-light-in-the-attic_1000/index.html",
                    NormalizedDomain = "books.toscrape.com",
                    CurrencySymbol = "£",
                    CreatedAt = now
                },
                new WishlistItem
                {
                    ProductName = "Tipping the Velvet",
                    ProductUrl = "https://books.toscrape.com/catalogue/tipping-the-velvet_999/index.html",
                    NormalizedDomain = "books.toscrape.com",
                    CurrencySymbol = "£",
                    CreatedAt = now
                }
            ]
        };

        var generalTask = new TodoItem
        {
            Title = "Review learned selectors in the Domain Rules registry",
            Notes = "Run \"Refresh All Prices\" on the Reading Nook project first so the registry has data.",
            Priority = TodoPriority.Medium,
            DueDate = today.AddDays(7),
            CreatedAt = now
        };

        db.WorkspaceProjects.Add(readingNook);
        db.TodoItems.Add(generalTask);

        await db.SaveChangesAsync(cancellationToken);
    }
}
