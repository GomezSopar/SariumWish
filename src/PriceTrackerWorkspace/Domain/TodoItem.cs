namespace PriceTrackerWorkspace.Domain;

public class TodoItem
{
    public int Id { get; set; }

    public int? WorkspaceProjectId { get; set; }

    public WorkspaceProject? WorkspaceProject { get; set; }

    public required string Title { get; set; }

    public string? Notes { get; set; }

    public bool IsCompleted { get; private set; }

    public TodoPriority Priority { get; set; } = TodoPriority.Medium;

    public DateOnly? DueDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; private set; }

    public bool IsOverdue(DateOnly today) => !IsCompleted && DueDate < today;

    public void SetCompleted(bool completed, DateTime timestampUtc)
    {
        if (IsCompleted == completed)
        {
            return;
        }

        IsCompleted = completed;
        CompletedAt = completed ? timestampUtc : null;
    }
}
