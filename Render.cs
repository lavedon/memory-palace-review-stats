using Spectre.Console;
using Spectre.Console.Rendering;

namespace LociStats;

public static class Render
{
    const int RecentCount = 4;

    public static void StatsView(LociStatsRepository r, bool plain, int? palaceFilterId = null, int? activityFilterId = null)
    {
        var open = r.GetOpenSession();
        InProgress(plain, open, r, palaceFilterId, activityFilterId);

        var recent = FilterLogs(r.GetRecentCompletedLogs(RecentCount * 4), palaceFilterId, activityFilterId)
            .Take(RecentCount).ToList();
        RecentSessions(plain, recent, r);

        ByPalace(plain, r, palaceFilterId, activityFilterId, open);
        ByActivity(plain, r, palaceFilterId, activityFilterId);

        if (open is null && recent.Count == 0)
        {
            if (plain) Console.WriteLine("No logs yet. Run --start or --log to record a session.");
            else AnsiConsole.MarkupLine("[dim]No logs yet. Run [green]--start[/] or [green]--log[/] to record a session.[/]");
        }
    }

    static IEnumerable<LogEntry> FilterLogs(IEnumerable<LogEntry> logs, int? palaceId, int? activityId)
    {
        if (palaceId is int p) logs = logs.Where(l => l.PalaceId == p);
        if (activityId is int a) logs = logs.Where(l => l.ActivityId == a);
        return logs;
    }

    // ── In-progress panel ───────────────────────────────────────────────

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

    // ── Recent sessions ─────────────────────────────────────────────────

    static void RecentSessions(bool plain, List<LogEntry> logs, LociStatsRepository r)
    {
        if (logs.Count == 0) return;
        var rows = logs.Select(l =>
        {
            var palaceDesc = r.GetPalaceById(l.PalaceId)?.Description ?? "?";
            var activityDesc = r.GetActivityById(l.ActivityId)?.Description ?? "?";
            var lociCell = (l.LociReviewed?.ToString() ?? "?") + (l.FrontToBack ? "*" : "");
            var durationCell = l.Duration is { } d ? Stats.FormatHms(d) : "--";
            return new[]
            {
                palaceDesc,
                activityDesc,
                l.StartedAt.ToString("yyyy-MM-dd HH:mm"),
                durationCell,
                lociCell,
            };
        }).ToList();

        var headers = new[] { "Palace", "Activity", "Started", "Duration", "Loci" };
        if (plain)
        {
            PrintPlainTable($"Recent Sessions (last {RecentCount})", headers, rows, alignRight: [false, false, false, true, true]);
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold blue]Recent Sessions[/]")
                .AddColumn("Palace")
                .AddColumn("Activity")
                .AddColumn("Started")
                .AddColumn(new TableColumn("Duration").RightAligned())
                .AddColumn(new TableColumn("Loci").RightAligned());
            foreach (var row in rows)
                table.AddRow(Markup.Escape(row[0]), Markup.Escape(row[1]), Markup.Escape(row[2]), row[3], row[4]);
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    // ── By palace ───────────────────────────────────────────────────────

    static void ByPalace(bool plain, LociStatsRepository r, int? palaceFilter, int? activityFilter, LogEntry? open)
    {
        List<Palace> palaces = palaceFilter is int pid
            ? (r.GetPalaceById(pid) is { } pp ? [pp] : [])
            : r.GetPalaces();

        var rows = new List<string[]>();
        foreach (var palace in palaces)
        {
            var logs = r.GetCompletedLogsForPalaceNewestFirst(palace.Id);
            if (activityFilter is int aid) logs = logs.Where(l => l.ActivityId == aid).ToList();
            if (logs.Count == 0 && (open is null || open.PalaceId != palace.Id)) continue;

            var avg = Stats.AverageDuration(logs);
            var avg7 = Stats.AverageOfLastN(logs, 7);
            var perLoci = Stats.AverageTimePerLoci(logs);
            rows.Add(new[]
            {
                palace.Description,
                palace.LociCount.ToString(),
                logs.Count.ToString(),
                Fmt(avg),
                Fmt(avg7),
                FmtMs(perLoci),
            });
        }
        if (rows.Count == 0) return;

        var headers = new[] { "Palace", "Loci", "Sessions", "Avg Dur", "Avg 7", "Per Loci" };
        if (plain)
        {
            PrintPlainTable("By Palace", headers, rows, alignRight: [false, true, true, true, true, true]);
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold blue]By Palace[/]")
                .AddColumn("Palace")
                .AddColumn(new TableColumn("Loci").RightAligned())
                .AddColumn(new TableColumn("Sessions").RightAligned())
                .AddColumn(new TableColumn("Avg Dur").RightAligned())
                .AddColumn(new TableColumn("Avg 7").RightAligned())
                .AddColumn(new TableColumn("Per Loci").RightAligned());
            foreach (var row in rows)
                table.AddRow(Markup.Escape(row[0]), row[1], row[2], row[3], row[4], row[5]);
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    // ── By activity ─────────────────────────────────────────────────────

    static void ByActivity(bool plain, LociStatsRepository r, int? palaceFilter, int? activityFilter)
    {
        List<Activity> activities = activityFilter is int aid
            ? (r.GetActivityById(aid) is { } aa ? [aa] : [])
            : r.GetActivities();

        var rows = new List<string[]>();
        foreach (var activity in activities)
        {
            var logs = r.GetCompletedLogsForActivityNewestFirst(activity.Id);
            if (palaceFilter is int pid) logs = logs.Where(l => l.PalaceId == pid).ToList();
            if (logs.Count == 0) continue;

            var avg = Stats.AverageDuration(logs);
            var avg7 = Stats.AverageOfLastN(logs, 7);
            var perLoci = Stats.AverageTimePerLoci(logs);
            rows.Add(new[]
            {
                activity.Description,
                logs.Count.ToString(),
                Fmt(avg),
                Fmt(avg7),
                FmtMs(perLoci),
            });
        }
        if (rows.Count == 0) return;

        var headers = new[] { "Activity", "Sessions", "Avg Dur", "Avg 7", "Per Loci" };
        if (plain)
        {
            PrintPlainTable("By Activity", headers, rows, alignRight: [false, true, true, true, true]);
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold blue]By Activity[/]")
                .AddColumn("Activity")
                .AddColumn(new TableColumn("Sessions").RightAligned())
                .AddColumn(new TableColumn("Avg Dur").RightAligned())
                .AddColumn(new TableColumn("Avg 7").RightAligned())
                .AddColumn(new TableColumn("Per Loci").RightAligned());
            foreach (var row in rows)
                table.AddRow(Markup.Escape(row[0]), row[1], row[2], row[3], row[4]);
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    static string Fmt(TimeSpan? t) => t is { } v ? Stats.FormatHms(v) : "--";
    static string FmtMs(TimeSpan? t) => t is { } v ? Stats.FormatMs(v) : "--";

    static void PrintPlainTable(string title, string[] headers, List<string[]> rows, bool[] alignRight)
    {
        var widths = new int[headers.Length];
        for (int i = 0; i < headers.Length; i++) widths[i] = headers[i].Length;
        foreach (var row in rows)
            for (int i = 0; i < row.Length; i++)
                if (row[i].Length > widths[i]) widths[i] = row[i].Length;

        Console.WriteLine(title);
        Console.WriteLine(new string('-', title.Length));
        Console.WriteLine(FormatRow(headers, widths, alignRight));
        Console.WriteLine(FormatRow(widths.Select(w => new string('-', w)).ToArray(), widths, alignRight));
        foreach (var row in rows) Console.WriteLine(FormatRow(row, widths, alignRight));
        Console.WriteLine();
    }

    static string FormatRow(string[] cells, int[] widths, bool[] alignRight)
    {
        var parts = new string[cells.Length];
        for (int i = 0; i < cells.Length; i++)
            parts[i] = alignRight[i] ? cells[i].PadLeft(widths[i]) : cells[i].PadRight(widths[i]);
        return "  " + string.Join("  ", parts);
    }
}
