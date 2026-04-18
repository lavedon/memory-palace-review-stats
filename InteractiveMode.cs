using System.Globalization;
using Spectre.Console;

namespace LociStats;

public static class InteractiveMode
{
    public static int Run(LociStatsRepository r, bool plain)
    {
        while (true)
        {
            AnsiConsole.Clear();
            Render.StatsView(r, plain: false);

            var open = r.GetOpenSession();
            var palaces = r.GetPalaces();
            var activities = r.GetActivities();

            var choices = new List<string>();
            choices.Add("Start Session");
            choices.Add("Start Session With Custom Loci Count");
            if (open is not null) choices.Add("Stop Session");
            choices.Add("Log Completed Session");
            choices.Add("Log Completed Session With Custom Loci Count");
            choices.Add("View stats: all palaces");
            choices.Add("View stats: by palace");
            choices.Add("View stats: by activity");
            choices.Add("Manage palaces");
            choices.Add("Manage activities");
            choices.Add("Quit");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold blue]lociStats[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            try
            {
                switch (choice)
                {
                    case "Start Session":
                        StartSession(r, palaces, activities, open, customLoci: false); break;
                    case "Start Session With Custom Loci Count":
                        StartSession(r, palaces, activities, open, customLoci: true); break;
                    case "Stop Session":
                        StopSession(r, open); break;
                    case "Log Completed Session":
                        LogCompleted(r, palaces, activities, customLoci: false); break;
                    case "Log Completed Session With Custom Loci Count":
                        LogCompleted(r, palaces, activities, customLoci: true); break;
                    case "View stats: all palaces":
                        Pause(); break;
                    case "View stats: by palace":
                        ViewByPalace(r, palaces); break;
                    case "View stats: by activity":
                        ViewByActivity(r, activities); break;
                    case "Manage palaces":
                        ManagePalaces(r); break;
                    case "Manage activities":
                        ManageActivities(r); break;
                    case "Quit":
                        return 0;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
                Pause();
            }
        }
    }

    // ───────────── Session actions ─────────────

    static void StartSession(LociStatsRepository r, List<Palace> palaces, List<Activity> activities, LogEntry? open, bool customLoci)
    {
        if (open is not null)
        {
            AnsiConsole.MarkupLine($"[yellow]A session is already open (LogID {open.Id}). Stop it first.[/]");
            Pause(); return;
        }
        var palace = PickPalace(palaces); if (palace is null) return;
        var activity = PickActivity(activities); if (activity is null) return;
        bool ftb = AnsiConsole.Confirm("Front-to-back review?", false);
        int baseCount = customLoci
            ? AnsiConsole.Prompt(new TextPrompt<int>("Custom loci count:").Validate(v => v >= 0 ? ValidationResult.Success() : ValidationResult.Error("Must be non-negative")))
            : palace.LociCount;
        int planned = ftb ? baseCount * 2 : baseCount;
        var id = r.StartSession(palace.Id, activity.Id, ftb, DateTime.Now, planned);
        AnsiConsole.MarkupLine($"[green]Started session {id}: {Markup.Escape(palace.Description)} | {Markup.Escape(activity.Description)} | planned {planned} loci[/]");
        Pause();
    }

    static void StopSession(LociStatsRepository r, LogEntry? open)
    {
        if (open is null) { AnsiConsole.MarkupLine("[yellow]No open session.[/]"); Pause(); return; }
        var palace = r.GetPalaceById(open.PalaceId);
        var activity = r.GetActivityById(open.ActivityId);
        int defaultLoci = open.LociReviewed ?? (open.FrontToBack ? (palace?.LociCount ?? 0) * 2 : palace?.LociCount ?? 0);
        int finalLoci = AnsiConsole.Prompt(
            new TextPrompt<int>("Loci reviewed?")
                .DefaultValue(defaultLoci)
                .ShowDefaultValue()
                .Validate(v => v >= 0 ? ValidationResult.Success() : ValidationResult.Error("Must be non-negative")));
        var now = DateTime.Now;
        r.StopSession(open.Id, now, finalLoci);
        var dur = now - open.StartedAt;
        AnsiConsole.MarkupLine(
            $"[green]Stopped {Markup.Escape(palace?.Description ?? "?")} | {Markup.Escape(activity?.Description ?? "?")} | {Stats.FormatHms(dur)} | {finalLoci} loci[/]");
        Pause();
    }

    static void LogCompleted(LociStatsRepository r, List<Palace> palaces, List<Activity> activities, bool customLoci)
    {
        var palace = PickPalace(palaces); if (palace is null) return;
        var activity = PickActivity(activities); if (activity is null) return;
        var duration = AnsiConsole.Prompt(
            new TextPrompt<string>("Duration (HH:MM:SS):")
                .Validate(s => TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var ts) && ts >= TimeSpan.Zero
                    ? ValidationResult.Success() : ValidationResult.Error("Expected HH:MM:SS")));
        var ts = TimeSpan.Parse(duration, CultureInfo.InvariantCulture);
        bool ftb = AnsiConsole.Confirm("Front-to-back review?", false);
        int baseCount = customLoci
            ? AnsiConsole.Prompt(new TextPrompt<int>("Custom loci count:").Validate(v => v >= 0 ? ValidationResult.Success() : ValidationResult.Error("Must be non-negative")))
            : palace.LociCount;
        int recorded = ftb ? baseCount * 2 : baseCount;
        var endedAt = DateTime.Now;
        var startedAt = endedAt - ts;
        var id = r.LogCompletedSession(palace.Id, activity.Id, startedAt, endedAt, recorded, ftb);
        AnsiConsole.MarkupLine($"[green]Logged session {id}: {Markup.Escape(palace.Description)} | {Markup.Escape(activity.Description)} | {Stats.FormatHms(ts)} | {recorded} loci[/]");
        Pause();
    }

    // ───────────── Stats views ─────────────

    static void ViewByPalace(LociStatsRepository r, List<Palace> palaces)
    {
        var palace = PickPalace(palaces); if (palace is null) return;
        AnsiConsole.Clear();
        Render.StatsView(r, plain: false, palaceFilterId: palace.Id);
        Pause();
    }

    static void ViewByActivity(LociStatsRepository r, List<Activity> activities)
    {
        var activity = PickActivity(activities); if (activity is null) return;
        AnsiConsole.Clear();
        Render.StatsView(r, plain: false, activityFilterId: activity.Id);
        Pause();
    }

    // ───────────── Palace / Activity management ─────────────

    static void ManagePalaces(LociStatsRepository r)
    {
        while (true)
        {
            AnsiConsole.Clear();
            var palaces = r.GetPalaces();
            if (palaces.Count > 0)
            {
                var table = new Table().Border(TableBorder.Rounded).AddColumns("ID", "Description", "Loci");
                foreach (var p in palaces) table.AddRow(p.Id.ToString(), Markup.Escape(p.Description), p.LociCount.ToString());
                AnsiConsole.Write(table);
            }
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>().Title("Palaces").AddChoices("Add", "Rename", "Change LociCount", "Delete", "Back"));
            if (action == "Back") return;
            switch (action)
            {
                case "Add":
                    {
                        var desc = AnsiConsole.Ask<string>("Description:");
                        var loci = AnsiConsole.Prompt(new TextPrompt<int>("Loci count:").Validate(v => v > 0 ? ValidationResult.Success() : ValidationResult.Error("Must be positive")));
                        var p = r.CreatePalace(desc, loci);
                        AnsiConsole.MarkupLine($"[green]Created palace {p.Id}[/]");
                        Pause(); break;
                    }
                case "Rename":
                    {
                        var p = PickPalace(palaces); if (p is null) break;
                        var desc = AnsiConsole.Ask<string>("New description:");
                        r.UpdatePalaceDescription(p.Id, desc);
                        AnsiConsole.MarkupLine("[green]Renamed[/]"); Pause(); break;
                    }
                case "Change LociCount":
                    {
                        var p = PickPalace(palaces); if (p is null) break;
                        var n = AnsiConsole.Prompt(new TextPrompt<int>("New loci count:").Validate(v => v > 0 ? ValidationResult.Success() : ValidationResult.Error("Must be positive")));
                        r.UpdatePalaceLociCount(p.Id, n);
                        AnsiConsole.MarkupLine("[green]Updated[/]"); Pause(); break;
                    }
                case "Delete":
                    {
                        var p = PickPalace(palaces); if (p is null) break;
                        if (r.PalaceHasLogs(p.Id))
                        {
                            AnsiConsole.MarkupLine($"[red]Cannot delete {Markup.Escape(p.Description)} — it has log entries. Delete them first.[/]");
                        }
                        else
                        {
                            r.DeletePalace(p.Id);
                            AnsiConsole.MarkupLine("[green]Deleted[/]");
                        }
                        Pause(); break;
                    }
            }
        }
    }

    static void ManageActivities(LociStatsRepository r)
    {
        while (true)
        {
            AnsiConsole.Clear();
            var activities = r.GetActivities();
            if (activities.Count > 0)
            {
                var table = new Table().Border(TableBorder.Rounded).AddColumns("ID", "Description");
                foreach (var a in activities) table.AddRow(a.Id.ToString(), Markup.Escape(a.Description));
                AnsiConsole.Write(table);
            }
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>().Title("Activities").AddChoices("Add", "Rename", "Delete", "Back"));
            if (action == "Back") return;
            switch (action)
            {
                case "Add":
                    {
                        var desc = AnsiConsole.Ask<string>("Description:");
                        var a = r.CreateActivity(desc);
                        AnsiConsole.MarkupLine($"[green]Created activity {a.Id}[/]"); Pause(); break;
                    }
                case "Rename":
                    {
                        var a = PickActivity(activities); if (a is null) break;
                        var desc = AnsiConsole.Ask<string>("New description:");
                        r.UpdateActivityDescription(a.Id, desc);
                        AnsiConsole.MarkupLine("[green]Renamed[/]"); Pause(); break;
                    }
                case "Delete":
                    {
                        var a = PickActivity(activities); if (a is null) break;
                        if (r.ActivityHasLogs(a.Id))
                        {
                            AnsiConsole.MarkupLine($"[red]Cannot delete {Markup.Escape(a.Description)} — it has log entries. Delete them first.[/]");
                        }
                        else
                        {
                            r.DeleteActivity(a.Id);
                            AnsiConsole.MarkupLine("[green]Deleted[/]");
                        }
                        Pause(); break;
                    }
            }
        }
    }

    // ───────────── Pickers / helpers ─────────────

    static Palace? PickPalace(List<Palace> palaces)
    {
        if (palaces.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No palaces defined. Add one via 'Manage palaces'.[/]");
            Pause();
            return null;
        }
        var labels = palaces.Select(p => $"#{p.Id} {p.Description} ({p.LociCount} loci)").ToList();
        var pick = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Palace:").PageSize(15).AddChoices(labels));
        return palaces[labels.IndexOf(pick)];
    }

    static Activity? PickActivity(List<Activity> activities)
    {
        if (activities.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No activities defined. Add one via 'Manage activities'.[/]");
            Pause();
            return null;
        }
        var labels = activities.Select(a => $"#{a.Id} {a.Description}").ToList();
        var pick = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Activity:").PageSize(15).AddChoices(labels));
        return activities[labels.IndexOf(pick)];
    }

    static void Pause()
    {
        AnsiConsole.MarkupLine("[dim]Press Enter to continue...[/]");
        Console.ReadLine();
    }
}
