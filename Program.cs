using System.Globalization;
using Spectre.Console;
using LociStats;

var opts = ParseArgs(args);
if (opts is null) return 1;
if (opts.Help) { PrintUsage(opts.Plain); return 0; }

var dbPath = Path.Combine(AppContext.BaseDirectory, "Data", "lociStatsDatabase.db");
var dataDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dataDir)) Directory.CreateDirectory(dataDir);
using var repo = new LociStatsRepository(dbPath);
repo.Initialize();

if (opts.Start) return DoStart(opts, repo);
if (opts.Stop) return DoStop(opts, repo);
if (opts.LogCmd) return DoLog(opts, repo);
if (opts.Interactive) return InteractiveMode.Run(repo, opts.Plain);
return ShowStats(opts, repo);

// ───────────────────────────── Commands ─────────────────────────────

int DoStart(CliOptions o, LociStatsRepository r)
{
    if (string.IsNullOrWhiteSpace(o.PalaceArg) || string.IsNullOrWhiteSpace(o.ActivityArg))
    {
        PrintError(o.Plain, "--start requires both --palace and --activity.");
        return 1;
    }
    var palace = r.GetPalaceByNameOrId(o.PalaceArg!);
    if (palace is null) { PrintError(o.Plain, $"Palace not found: {o.PalaceArg}"); return 1; }
    var activity = r.GetActivityByNameOrId(o.ActivityArg!);
    if (activity is null) { PrintError(o.Plain, $"Activity not found: {o.ActivityArg}"); return 1; }
    if (r.GetOpenSession() is { } open)
    {
        PrintError(o.Plain, $"A session is already open (LogID {open.Id}). Run --stop first.");
        return 1;
    }

    int baseCount = o.LociCount ?? palace.LociCount;
    int planned = o.FrontToBack ? baseCount * 2 : baseCount;
    var now = DateTime.Now;
    var id = r.StartSession(palace.Id, activity.Id, o.FrontToBack, now, planned);
    PrintSuccess(o.Plain, $"Started session {id}: {palace.Description} | {activity.Description}" +
        (o.FrontToBack ? " | front-to-back" : "") + $" | planned {planned} loci");
    return 0;
}

int DoStop(CliOptions o, LociStatsRepository r)
{
    var open = r.GetOpenSession();
    if (open is null) { PrintError(o.Plain, "No open session to stop."); return 1; }
    var palace = r.GetPalaceById(open.PalaceId);
    var activity = r.GetActivityById(open.ActivityId);

    int finalLoci;
    if (o.LociCount is int cli)
    {
        finalLoci = cli;
    }
    else
    {
        int defaultLoci = open.LociReviewed ?? (open.FrontToBack ? (palace?.LociCount ?? 0) * 2 : palace?.LociCount ?? 0);
        finalLoci = PromptInt(o.Plain, $"Loci reviewed", defaultLoci);
    }

    var now = DateTime.Now;
    r.StopSession(open.Id, now, finalLoci);
    var dur = now - open.StartedAt;
    PrintSuccess(o.Plain,
        $"Stopped session {open.Id}: {(palace?.Description ?? "?")} | {(activity?.Description ?? "?")} | " +
        $"duration {Stats.FormatHms(dur)} | {finalLoci} loci");
    return 0;
}

int DoLog(CliOptions o, LociStatsRepository r)
{
    if (string.IsNullOrWhiteSpace(o.PalaceArg) || string.IsNullOrWhiteSpace(o.ActivityArg))
    {
        PrintError(o.Plain, "--log requires --palace and --activity."); return 1;
    }
    if (o.Duration is null)
    {
        PrintError(o.Plain, "--log requires --duration HH:MM:SS."); return 1;
    }
    if (o.LociCount is null)
    {
        PrintError(o.Plain, "--log requires --loci-count N."); return 1;
    }

    var palace = r.GetPalaceByNameOrId(o.PalaceArg!);
    if (palace is null) { PrintError(o.Plain, $"Palace not found: {o.PalaceArg}"); return 1; }
    var activity = r.GetActivityByNameOrId(o.ActivityArg!);
    if (activity is null) { PrintError(o.Plain, $"Activity not found: {o.ActivityArg}"); return 1; }

    int recorded = o.FrontToBack ? o.LociCount.Value * 2 : o.LociCount.Value;
    DateTime endedAt = DateTime.Now;
    DateTime startedAt = o.StartedAt ?? (endedAt - o.Duration.Value);
    if (o.StartedAt is { } sa) endedAt = sa + o.Duration.Value;

    var id = r.LogCompletedSession(palace.Id, activity.Id, startedAt, endedAt, recorded, o.FrontToBack);
    PrintSuccess(o.Plain,
        $"Logged session {id}: {palace.Description} | {activity.Description} | " +
        $"duration {Stats.FormatHms(o.Duration.Value)} | {recorded} loci" +
        (o.FrontToBack ? " (front-to-back)" : ""));
    return 0;
}

int ShowStats(CliOptions o, LociStatsRepository r)
{
    int? palaceId = null;
    if (!string.IsNullOrWhiteSpace(o.PalaceArg))
    {
        var p = r.GetPalaceByNameOrId(o.PalaceArg!);
        if (p is null) { PrintError(o.Plain, $"Palace not found: {o.PalaceArg}"); return 1; }
        palaceId = p.Id;
    }
    int? activityId = null;
    if (!string.IsNullOrWhiteSpace(o.ActivityArg))
    {
        var a = r.GetActivityByNameOrId(o.ActivityArg!);
        if (a is null) { PrintError(o.Plain, $"Activity not found: {o.ActivityArg}"); return 1; }
        activityId = a.Id;
    }
    Render.StatsView(r, o.Plain, palaceId, activityId);
    return 0;
}

// ───────────────────────────── Prompts ─────────────────────────────

int PromptInt(bool plain, string label, int defaultValue)
{
    if (plain)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var line = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line)) return defaultValue;
        if (int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        Console.WriteLine($"(invalid - using default {defaultValue})");
        return defaultValue;
    }
    return AnsiConsole.Prompt(
        new TextPrompt<int>($"{label}?")
            .DefaultValue(defaultValue)
            .ShowDefaultValue());
}

// ───────────────────────────── Output helpers ─────────────────────────────

void PrintError(bool plain, string msg)
{
    if (plain) Console.Error.WriteLine($"ERROR: {msg}");
    else AnsiConsole.MarkupLine($"[red]{Markup.Escape(msg)}[/]");
}

void PrintSuccess(bool plain, string msg)
{
    if (plain) Console.WriteLine(msg);
    else AnsiConsole.MarkupLine($"[green]{Markup.Escape(msg)}[/]");
}

void PrintUsage(bool plain)
{
    string[] lines =
    [
        "lociStats (los) - track memory-palace review sessions",
        "",
        "USAGE:",
        "  los                                     show stats (in-progress session first, then per-palace)",
        "  los --start --palace P --activity A [--loci-count N] [--front-to-back]",
        "                                          start a timed session",
        "  los --stop [--loci-count N]             stop the open session (prompts for loci reviewed)",
        "  los --log --palace P --activity A --duration HH:MM:SS --loci-count N",
        "        [--front-to-back] [--started-at <ISO 8601>]",
        "                                          record a completed session (manual entry)",
        "  los --palace P                          show stats filtered to palace P",
        "  los --activity A                        show stats filtered to activity A",
        "  los --interactive                       launch interactive menu",
        "  los --plain                             plain-text output (no Spectre rendering)",
        "  los --help                              show this help",
        "",
        "NOTES:",
        "  --front-to-back doubles the recorded loci count (10 loci front-to-back = 20 reviews).",
        "  Only one session may be open at a time.",
        "  Palaces and activities can be referenced by name or numeric ID."
    ];
    if (plain)
    {
        foreach (var l in lines) Console.WriteLine(l);
    }
    else
    {
        AnsiConsole.Write(new Panel(new Text(string.Join("\n", lines)))
            .Header("[bold blue]los[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0));
    }
}

// ───────────────────────────── Arg parsing ─────────────────────────────

CliOptions? ParseArgs(string[] a)
{
    var o = new CliOptions();
    for (int i = 0; i < a.Length; i++)
    {
        var raw = a[i];
        var arg = raw.TrimStart('-').ToLowerInvariant();
        switch (arg)
        {
            case "help" or "h" or "?":
                o.Help = true; break;
            case "plain":
                o.Plain = true; break;
            case "interactive":
                o.Interactive = true; break;
            case "start":
                o.Start = true; break;
            case "stop":
                o.Stop = true; break;
            case "log":
                o.LogCmd = true; break;
            case "front-to-back":
                o.FrontToBack = true; break;
            case "palace":
                if (!TakeValue(a, ref i, raw, out var p)) return null;
                o.PalaceArg = p; break;
            case "activity":
                if (!TakeValue(a, ref i, raw, out var ac)) return null;
                o.ActivityArg = ac; break;
            case "loci-count":
                if (!TakeValue(a, ref i, raw, out var lc)) return null;
                if (!int.TryParse(lc, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 0)
                {
                    Console.Error.WriteLine($"ERROR: --loci-count expects a non-negative integer, got: {lc}");
                    return null;
                }
                o.LociCount = n; break;
            case "duration":
                if (!TakeValue(a, ref i, raw, out var d)) return null;
                if (!TimeSpan.TryParse(d, CultureInfo.InvariantCulture, out var ts) || ts < TimeSpan.Zero)
                {
                    Console.Error.WriteLine($"ERROR: --duration expects HH:MM:SS, got: {d}");
                    return null;
                }
                o.Duration = ts; break;
            case "started-at":
                if (!TakeValue(a, ref i, raw, out var sa)) return null;
                if (!DateTime.TryParse(sa, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    Console.Error.WriteLine($"ERROR: --started-at expects ISO 8601, got: {sa}");
                    return null;
                }
                o.StartedAt = dt; break;
            default:
                Console.Error.WriteLine($"ERROR: unknown argument: {raw}");
                return null;
        }
    }
    return o;
}

static bool TakeValue(string[] a, ref int i, string flag, out string value)
{
    if (i + 1 >= a.Length)
    {
        Console.Error.WriteLine($"ERROR: {flag} requires a value.");
        value = string.Empty;
        return false;
    }
    value = a[++i];
    return true;
}

// ───────────────────────────── Types ─────────────────────────────

sealed class CliOptions
{
    public bool Help;
    public bool Plain;
    public bool Interactive;
    public bool Start;
    public bool Stop;
    public bool LogCmd;
    public bool FrontToBack;
    public string? PalaceArg;
    public string? ActivityArg;
    public int? LociCount;
    public TimeSpan? Duration;
    public DateTime? StartedAt;
}
