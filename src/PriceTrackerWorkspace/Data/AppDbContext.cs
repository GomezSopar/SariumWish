using Microsoft.EntityFrameworkCore;
using PriceTrackerWorkspace.Domain;

namespace PriceTrackerWorkspace.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WorkspaceProject> WorkspaceProjects => Set<WorkspaceProject>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    public DbSet<DomainScrapeRule> DomainScrapeRules => Set<DomainScrapeRule>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(3)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkspaceProject>(project =>
        {
            project.Property(p => p.Name).HasMaxLength(120);
            project.Property(p => p.Description).HasMaxLength(2000);
            project.Property(p => p.TargetBudget).HasPrecision(18, 2);
            project.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            project.HasIndex(p => p.Name);

            project.HasMany(p => p.TodoItems)
                .WithOne(t => t.WorkspaceProject)
                .HasForeignKey(t => t.WorkspaceProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            project.HasMany(p => p.WishlistItems)
                .WithOne(w => w.WorkspaceProject)
                .HasForeignKey(w => w.WorkspaceProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TodoItem>(todo =>
        {
            todo.Property(t => t.Title).HasMaxLength(200);
            todo.Property(t => t.Notes).HasMaxLength(4000);
            todo.Property(t => t.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            todo.HasIndex(t => new { t.WorkspaceProjectId, t.IsCompleted });
            todo.HasIndex(t => t.DueDate);
        });

        modelBuilder.Entity<WishlistItem>(item =>
        {
            item.Property(w => w.ProductName).HasMaxLength(300);
            item.Property(w => w.ProductUrl).HasMaxLength(2048);
            item.Property(w => w.NormalizedDomain).HasMaxLength(253);
            item.Property(w => w.CurrentPrice).HasPrecision(18, 2);
            item.Property(w => w.PreviousPrice).HasPrecision(18, 2);
            item.Property(w => w.CurrencySymbol).HasMaxLength(8);
            item.Property(w => w.CustomCssSelectorOverride).HasMaxLength(1024);
            item.Property(w => w.Status).HasConversion<string>().HasMaxLength(32);
            item.Property(w => w.LastErrorMessage).HasMaxLength(1024);
            item.Property(w => w.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            item.HasIndex(w => w.NormalizedDomain);
            item.HasIndex(w => new { w.WorkspaceProjectId, w.IsIncludedInTotal });
        });

        modelBuilder.Entity<DomainScrapeRule>(rule =>
        {
            rule.Property(r => r.NormalizedDomain).HasMaxLength(253);
            rule.Property(r => r.PriceCssSelector).HasMaxLength(1024);
            rule.Property(r => r.TitleCssSelector).HasMaxLength(1024);
            rule.HasIndex(r => r.NormalizedDomain).IsUnique();
        });
    }
}
