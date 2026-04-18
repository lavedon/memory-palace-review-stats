using LociStats;

namespace LociStats.Tests;

[TestFixture]
public class StatsTests
{
    static LogEntry Completed(int id, int palaceId, int activityId, DateTime started, TimeSpan duration, int loci, bool ftb = false)
        => new(id, palaceId, activityId, started, started + duration, loci, ftb);

    [Test]
    public void AverageDuration_Empty_ReturnsNull()
    {
        Assert.That(Stats.AverageDuration(new List<LogEntry>()), Is.Null);
    }

    [Test]
    public void AverageDuration_MeanOfDurations()
    {
        var anchor = new DateTime(2026, 1, 1);
        var logs = new List<LogEntry>
        {
            Completed(1, 1, 1, anchor, TimeSpan.FromMinutes(10), 5),
            Completed(2, 1, 1, anchor, TimeSpan.FromMinutes(20), 5),
            Completed(3, 1, 1, anchor, TimeSpan.FromMinutes(30), 5),
        };
        Assert.That(Stats.AverageDuration(logs), Is.EqualTo(TimeSpan.FromMinutes(20)));
    }

    [Test]
    public void AverageOfLastN_TakesFirstN_InInputOrder()
    {
        // Input is already sorted newest-first by convention.
        var anchor = new DateTime(2026, 1, 1);
        var logs = new List<LogEntry>
        {
            Completed(1, 1, 1, anchor, TimeSpan.FromMinutes(10), 5), // newest
            Completed(2, 1, 1, anchor, TimeSpan.FromMinutes(20), 5),
            Completed(3, 1, 1, anchor, TimeSpan.FromMinutes(30), 5), // 3rd newest
            Completed(4, 1, 1, anchor, TimeSpan.FromMinutes(999), 5), // should be ignored
        };
        var avg = Stats.AverageOfLastN(logs, 3);
        Assert.That(avg, Is.EqualTo(TimeSpan.FromMinutes(20)));
    }

    [Test]
    public void AverageOfLastN_FewerThanN_UsesAvailable()
    {
        var anchor = new DateTime(2026, 1, 1);
        var logs = new List<LogEntry>
        {
            Completed(1, 1, 1, anchor, TimeSpan.FromMinutes(10), 5),
            Completed(2, 1, 1, anchor, TimeSpan.FromMinutes(30), 5),
        };
        Assert.That(Stats.AverageOfLastN(logs, 7), Is.EqualTo(TimeSpan.FromMinutes(20)));
    }

    [Test]
    public void AverageTimePerLoci_WeightedBySessionLength()
    {
        // 10 min over 10 loci → 60s/loci; 30 min over 10 loci → 180s/loci.
        // Weighted: (10+30)*60 / 20 = 120 s/loci.
        // Naive per-session-ratio mean would be (60+180)/2 = 120 s/loci here (same) — so use asymmetric:
        // 10 min / 5 loci = 120 s/loci; 30 min / 30 loci = 60 s/loci.
        // Weighted: (10+30)*60 / 35 ≈ 68.57 s/loci.
        // Naive mean: (120+60)/2 = 90 s/loci.
        var anchor = new DateTime(2026, 1, 1);
        var logs = new List<LogEntry>
        {
            Completed(1, 1, 1, anchor, TimeSpan.FromMinutes(10), 5),
            Completed(2, 1, 1, anchor, TimeSpan.FromMinutes(30), 30),
        };
        var avg = Stats.AverageTimePerLoci(logs);
        Assert.That(avg, Is.Not.Null);
        Assert.That(avg!.Value.TotalSeconds, Is.EqualTo(40.0 * 60 / 35).Within(0.01));
    }

    [Test]
    public void AverageTimePerLoci_SkipsEntriesWithZeroLoci()
    {
        var anchor = new DateTime(2026, 1, 1);
        var logs = new List<LogEntry>
        {
            Completed(1, 1, 1, anchor, TimeSpan.FromMinutes(10), 0),
            Completed(2, 1, 1, anchor, TimeSpan.FromMinutes(20), 10),
        };
        // Should use only the second entry: 20min / 10 = 2 min/loci = 120 s/loci.
        Assert.That(Stats.AverageTimePerLoci(logs)!.Value, Is.EqualTo(TimeSpan.FromMinutes(2)));
    }

    [Test]
    public void AverageTimePerLoci_AllZero_ReturnsNull()
    {
        var anchor = new DateTime(2026, 1, 1);
        var logs = new List<LogEntry>
        {
            Completed(1, 1, 1, anchor, TimeSpan.FromMinutes(10), 0),
        };
        Assert.That(Stats.AverageTimePerLoci(logs), Is.Null);
    }

    [Test]
    public void FormatHms_ZeroAndAboveHour()
    {
        Assert.That(Stats.FormatHms(TimeSpan.Zero), Is.EqualTo("0:00:00"));
        Assert.That(Stats.FormatHms(TimeSpan.FromSeconds(59)), Is.EqualTo("0:00:59"));
        Assert.That(Stats.FormatHms(TimeSpan.FromMinutes(65) + TimeSpan.FromSeconds(7)), Is.EqualTo("1:05:07"));
        Assert.That(Stats.FormatHms(TimeSpan.FromHours(12) + TimeSpan.FromMinutes(34) + TimeSpan.FromSeconds(56)), Is.EqualTo("12:34:56"));
    }

    [Test]
    public void FormatHms_NegativeClampsToZero()
    {
        Assert.That(Stats.FormatHms(TimeSpan.FromSeconds(-5)), Is.EqualTo("0:00:00"));
    }

    [Test]
    public void FormatMs_UsesTotalMinutes()
    {
        Assert.That(Stats.FormatMs(TimeSpan.Zero), Is.EqualTo("00:00"));
        Assert.That(Stats.FormatMs(TimeSpan.FromSeconds(9)), Is.EqualTo("00:09"));
        Assert.That(Stats.FormatMs(TimeSpan.FromMinutes(90) + TimeSpan.FromSeconds(15)), Is.EqualTo("90:15"));
    }
}
