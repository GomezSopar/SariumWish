namespace PriceTrackerWorkspace.Domain;

public class WorkspaceProject
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal? TargetBudget { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<TodoItem> TodoItems { get; init; } = [];

    public ICollection<WishlistItem> WishlistItems { get; init; } = [];
}
