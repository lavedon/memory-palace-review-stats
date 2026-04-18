using LociStats;

namespace LociStats.Tests;

[TestFixture]
public class RepositoryTests
{
    LociStatsRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new LociStatsRepository(":memory:");
        _repo.Initialize();
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [Test]
    public void CreatePalace_RoundTrip()
    {
        var p = _repo.CreatePalace("Test Palace", 15);
        Assert.That(p.Id, Is.GreaterThan(0));
        Assert.That(p.Description, Is.EqualTo("Test Palace"));
        Assert.That(p.LociCount, Is.EqualTo(15));

        var byId = _repo.GetPalaceByNameOrId(p.Id.ToString());
        Assert.That(byId, Is.EqualTo(p));

        var byName = _repo.GetPalaceByNameOrId("test palace"); // COLLATE NOCASE
        Assert.That(byName, Is.EqualTo(p));
    }

    [Test]
    public void CreateActivity_RoundTrip()
    {
        var a = _repo.CreateActivity("Study");
        var fetched = _repo.GetActivityByNameOrId("Study");
        Assert.That(fetched, Is.EqualTo(a));
    }

    [Test]
    public void StartSession_InsertsOpenRow_WithPlannedLoci()
    {
        var palace = _repo.CreatePalace("P", 10);
        var activity = _repo.CreateActivity("A");
        var started = new DateTime(2026, 1, 2, 15, 0, 0);
        var id = _repo.StartSession(palace.Id, activity.Id, frontToBack: false, startedAt: started, plannedLoci: 8);
        Assert.That(id, Is.GreaterThan(0));

        var open = _repo.GetOpenSession();
        Assert.That(open, Is.Not.Null);
        Assert.That(open!.PalaceId, Is.EqualTo(palace.Id));
        Assert.That(open.ActivityId, Is.EqualTo(activity.Id));
        Assert.That(open.StartedAt, Is.EqualTo(started));
        Assert.That(open.EndedAt, Is.Null);
        Assert.That(open.LociReviewed, Is.EqualTo(8));
        Assert.That(open.FrontToBack, Is.False);
        Assert.That(open.IsInProgress, Is.True);
    }

    [Test]
    public void StopSession_ClosesOpenRow()
    {
        var palace = _repo.CreatePalace("P", 10);
        var activity = _repo.CreateActivity("A");
        var started = new DateTime(2026, 1, 2, 15, 0, 0);
        var id = _repo.StartSession(palace.Id, activity.Id, false, started, 10);

        var ended = started.AddMinutes(20);
        var affected = _repo.StopSession(id, ended, 12);
        Assert.That(affected, Is.EqualTo(1));

        Assert.That(_repo.GetOpenSession(), Is.Null);
        var logs = _repo.GetCompletedLogsForPalaceNewestFirst(palace.Id);
        Assert.That(logs, Has.Count.EqualTo(1));
        Assert.That(logs[0].EndedAt, Is.EqualTo(ended));
        Assert.That(logs[0].LociReviewed, Is.EqualTo(12));
        Assert.That(logs[0].Duration, Is.EqualTo(TimeSpan.FromMinutes(20)));
    }

    [Test]
    public void StopSession_SkipsAlreadyClosed()
    {
        var palace = _repo.CreatePalace("P", 10);
        var activity = _repo.CreateActivity("A");
        var id = _repo.LogCompletedSession(palace.Id, activity.Id,
            new DateTime(2026, 1, 2), new DateTime(2026, 1, 2, 0, 10, 0), 10, false);

        // Attempting to stop a completed row should affect 0 rows.
        var affected = _repo.StopSession(id, DateTime.Now, 99);
        Assert.That(affected, Is.EqualTo(0));
    }

    [Test]
    public void LogCompletedSession_StoresFrontToBack()
    {
        var palace = _repo.CreatePalace("P", 10);
        var activity = _repo.CreateActivity("A");
        var s = new DateTime(2026, 1, 2);
        var e = s.AddMinutes(5);
        _repo.LogCompletedSession(palace.Id, activity.Id, s, e, 20, frontToBack: true);

        var logs = _repo.GetCompletedLogsForPalaceNewestFirst(palace.Id);
        Assert.That(logs, Has.Count.EqualTo(1));
        Assert.That(logs[0].FrontToBack, Is.True);
        Assert.That(logs[0].LociReviewed, Is.EqualTo(20));
    }

    [Test]
    public void PalaceHasLogs_ReturnsTrueAfterInsert()
    {
        var palace = _repo.CreatePalace("P", 10);
        var activity = _repo.CreateActivity("A");
        Assert.That(_repo.PalaceHasLogs(palace.Id), Is.False);
        _repo.LogCompletedSession(palace.Id, activity.Id,
            new DateTime(2026, 1, 2), new DateTime(2026, 1, 2, 0, 5, 0), 5, false);
        Assert.That(_repo.PalaceHasLogs(palace.Id), Is.True);
        Assert.That(_repo.ActivityHasLogs(activity.Id), Is.True);
    }

    [Test]
    public void GetCompletedLogs_IgnoresOpenSession()
    {
        var palace = _repo.CreatePalace("P", 10);
        var activity = _repo.CreateActivity("A");
        _repo.StartSession(palace.Id, activity.Id, false, DateTime.Now, 10);

        Assert.That(_repo.GetCompletedLogsForPalaceNewestFirst(palace.Id), Is.Empty);
        Assert.That(_repo.GetCompletedLogsForActivityNewestFirst(activity.Id), Is.Empty);
    }

    [Test]
    public void GetCompletedLogsForPalaceNewestFirst_OrderedByStartedAtDesc()
    {
        var palace = _repo.CreatePalace("P", 10);
        var activity = _repo.CreateActivity("A");
        var t0 = new DateTime(2026, 1, 2, 10, 0, 0);
        for (int i = 0; i < 3; i++)
        {
            var s = t0.AddMinutes(i * 60);
            _repo.LogCompletedSession(palace.Id, activity.Id, s, s.AddMinutes(5), 5, false);
        }
        var logs = _repo.GetCompletedLogsForPalaceNewestFirst(palace.Id);
        Assert.That(logs, Has.Count.EqualTo(3));
        Assert.That(logs[0].StartedAt, Is.GreaterThan(logs[1].StartedAt));
        Assert.That(logs[1].StartedAt, Is.GreaterThan(logs[2].StartedAt));
    }

    [Test]
    public void UpdatePalace_DescriptionAndLociCount()
    {
        var palace = _repo.CreatePalace("Old", 5);
        _repo.UpdatePalaceDescription(palace.Id, "New");
        _repo.UpdatePalaceLociCount(palace.Id, 42);
        var fetched = _repo.GetPalaceByNameOrId(palace.Id.ToString())!;
        Assert.That(fetched.Description, Is.EqualTo("New"));
        Assert.That(fetched.LociCount, Is.EqualTo(42));
    }

    [Test]
    public void DeletePalace_WithoutLogs_Succeeds()
    {
        var palace = _repo.CreatePalace("Empty", 5);
        _repo.DeletePalace(palace.Id);
        Assert.That(_repo.GetPalaceByNameOrId(palace.Id.ToString()), Is.Null);
    }
}
