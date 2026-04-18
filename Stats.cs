namespace LociStats;

public static class Stats
{
    public static TimeSpan? AverageDuration(IReadOnlyList<LogEntry> completed)
    {
        if (completed.Count == 0) return null;
        long ticks = 0;
        foreach (var e in completed)
        {
            if (e.EndedAt is null) continue;
            ticks += (e.EndedAt.Value - e.StartedAt).Ticks;
        }
        return TimeSpan.FromTicks(ticks / completed.Count);
    }

    public static TimeSpan? AverageOfLastN(IReadOnlyList<LogEntry> completedNewestFirst, int n)
    {
        if (completedNewestFirst.Count == 0) return null;
        int take = Math.Min(n, completedNewestFirst.Count);
        long ticks = 0;
        for (int i = 0; i < take; i++)
        {
            var e = completedNewestFirst[i];
            if (e.EndedAt is null) continue;
            ticks += (e.EndedAt.Value - e.StartedAt).Ticks;
        }
        return TimeSpan.FromTicks(ticks / take);
    }

    public static TimeSpan? AverageTimePerLoci(IReadOnlyList<LogEntry> completed)
    {
        long totalTicks = 0;
        long totalLoci = 0;
        foreach (var e in completed)
        {
            if (e.EndedAt is null || e.LociReviewed is null or <= 0) continue;
            totalTicks += (e.EndedAt.Value - e.StartedAt).Ticks;
            totalLoci += e.LociReviewed.Value;
        }
        if (totalLoci == 0) return null;
        return TimeSpan.FromTicks(totalTicks / totalLoci);
    }

    public static string FormatHms(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        int h = (int)duration.TotalHours;
        return $"{h}:{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    public static string FormatMs(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        int m = (int)duration.TotalMinutes;
        return $"{m:D2}:{duration.Seconds:D2}";
    }
}
