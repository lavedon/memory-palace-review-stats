using Spectre.Console;
using Spectre.Console.Rendering;

namespace LociStats;

public static class Render
{
    public static void StatsView(LociStatsRepository r, bool plain, int? palaceFilterId = null, int? activityFilterId = null)
    {
        var open = r.GetOpenSession();

        InProgress(plain, open, r, palaceFilterId, activityFilterId);

        List<Palace> palaces = palaceFilterId is int pid
            ? (r.GetPalaceById(pid) is { } p ? [p] : [])
            : r.GetPalaces();

        bool anyRendered = false;
        foreach (var palace in palaces)
        {
            var logs = r.GetCompletedLogsForPalaceNewestFirst(palace.Id);
            if (activityFilterId is int aid)
                logs = logs.Where(l => l.ActivityId == aid).ToList();
            if (logs.Count == 0 && (open is null || open.PalaceId != palace.Id)) continue;
            PalaceBlock(plain, palace, logs, open, r);
            anyRendered = true;
        }

        if (!anyRendered && open is null)
        {
            if (plain) Console.WriteLine("No logs yet. Run --start or --log to record a session.");
            else AnsiConsole.MarkupLine("[dim]No logs yet. Run [green]--start[/] or [green]--log[/] to record a session.[/]");
        }
    }

    public static void InProgress(bool plain, LogEntry? open, LociStatsRepository r, int? palaceFilter, int? activityFilter)
    {
        if (open is null) return;
        if (palaceFilter is int pf && open.PalaceId != pf) return;
        if (activityFilter is int af && open.ActivityId != af) return;
        var palace = r.GetPalaceById(open.PalaceId);
        var activity = r.GetActivityById(open.ActivityId);
        var elapsed = DateTime.Now - open.StartedAt;
        var label = $"{palace?.Description ?? "?"} | {activity?.Description ?? "?"}" +
            (open.FrontToBack ? " | front-to-back" : "");
        if (plain)
        {
            Console.WriteLine($"IN PROGRESS: {label}");
            Console.WriteLine($"  Started: {open.StartedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Elapsed: {Stats.FormatHms(elapsed)}");
            if (open.LociReviewed is int planned) Console.WriteLine($"  Planned loci: {planned}");
            Console.WriteLine();
        }
        else
        {
            var rows = new List<IRenderable>
            {
                new Markup($"[bold]{Markup.Escape(label)}[/]"),
                new Markup($"Started: [dim]{open.StartedAt:yyyy-MM-dd HH:mm:ss}[/]"),
                new Markup($"Elapsed: [green]{Stats.FormatHms(elapsed)}[/]"),
            };
            if (open.LociReviewed is int pl)
                rows.Add(new Markup($"Planned loci: [cyan]{pl}[/]"));
            AnsiConsole.Write(new Panel(new Rows(rows))
                .Header("[bold yellow]In Progress[/]")
                .Border(BoxBorder.Rounded));
            AnsiConsole.WriteLine();
        }
    }

    public static void PalaceBlock(bool plain, Palace palace, List<LogEntry> completedNewestFirst, LogEntry? open, LociStatsRepository r)
    {
        bool isPalaceInProgress = open is not null && open.PalaceId == palace.Id;

        string timeTaken;
        if (isPalaceInProgress)
        {
            timeTaken = Stats.FormatHms(DateTime.Now - open!.StartedAt);
        }
        else if (completedNewestFirst.Count > 0 && completedNewestFirst[0].Duration is { } d)
        {
            timeTaken = Stats.FormatHms(d);
        }
        else
        {
            timeTaken = "--";
        }

        var avgPalace = Stats.AverageDuration(completedNewestFirst);
        var avgLast7 = Stats.AverageOfLastN(completedNewestFirst, 7);
        var avgPerLoci = Stats.AverageTimePerLoci(completedNewestFirst);

        string Fmt(TimeSpan? t) => t is { } v ? Stats.FormatHms(v) : "--";
        string FmtMs(TimeSpan? t) => t is { } v ? Stats.FormatMs(v) : "--";

        string header = $"{palace.Description} (Loci: {palace.LociCount})";

        if (plain)
        {
            Console.WriteLine(header);
            Console.WriteLine(new string('-', header.Length));
            Console.WriteLine($"  Time Taken:              {timeTaken}");
            Console.WriteLine($"  Avg For This Palace:     {Fmt(avgPalace)}");
            Console.WriteLine($"  Avg For Last 7 Attempts: {Fmt(avgLast7)}");
            Console.WriteLine($"  Avg Time Per Loci:       {FmtMs(avgPerLoci)}");
            Console.WriteLine($"  Completed sessions:      {completedNewestFirst.Count}");
            Console.WriteLine();
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title($"[bold blue]{Markup.Escape(header)}[/]")
                .HideHeaders()
                .AddColumn("Label")
                .AddColumn("Value");
            table.AddRow("Time Taken", $"[green]{timeTaken}[/]");
            table.AddRow("Avg For This Palace", Fmt(avgPalace));
            table.AddRow("Avg For Last 7 Attempts", Fmt(avgLast7));
            table.AddRow("Avg Time Per Loci", FmtMs(avgPerLoci));
            table.AddRow("Completed sessions", completedNewestFirst.Count.ToString());
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }
}
