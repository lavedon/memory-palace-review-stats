namespace LociStats;

public record Palace(int Id, string Description, int LociCount);

public record Activity(int Id, string Description);

public record LogEntry(
    int Id,
    int PalaceId,
    int ActivityId,
    DateTime StartedAt,
    DateTime? EndedAt,
    int? LociReviewed,
    bool FrontToBack)
{
    public bool IsInProgress => EndedAt is null;

    public TimeSpan? Duration => EndedAt is { } end ? end - StartedAt : null;
}
