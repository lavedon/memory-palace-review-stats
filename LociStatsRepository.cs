using Microsoft.Data.Sqlite;

namespace LociStats;

public sealed class LociStatsRepository : IDisposable
{
    private readonly SqliteConnection _connection;

    public LociStatsRepository(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
    }

    public void Initialize()
    {
        Exec("PRAGMA foreign_keys = ON;");

        Exec("""
            CREATE TABLE IF NOT EXISTS palaces (
                PalaceID INTEGER PRIMARY KEY AUTOINCREMENT,
                PalaceDescription TEXT NOT NULL,
                LociCount INTEGER NOT NULL
            );
            """);

        Exec("""
            CREATE TABLE IF NOT EXISTS activities (
                ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
                ActivityDescription TEXT NOT NULL
            );
            """);

        Exec("""
            CREATE TABLE IF NOT EXISTS log (
                LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                PalaceID INTEGER NOT NULL,
                ActivityID INTEGER NOT NULL,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT NULL,
                LociReviewed INTEGER NULL,
                FrontToBack INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (PalaceID) REFERENCES palaces(PalaceID),
                FOREIGN KEY (ActivityID) REFERENCES activities(ActivityID)
            );
            """);
    }

    private void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ── Palaces ─────────────────────────────────────────────────────────

    public List<Palace> GetPalaces()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT PalaceID, PalaceDescription, LociCount FROM palaces ORDER BY PalaceID;";
        var result = new List<Palace>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Palace(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
        }
        return result;
    }

    public Palace? GetPalaceById(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT PalaceID, PalaceDescription, LociCount FROM palaces WHERE PalaceID = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Palace(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2));
        }
        return null;
    }

    public Palace? GetPalaceByNameOrId(string input)
    {
        using var cmd = _connection.CreateCommand();
        if (int.TryParse(input, out var id))
        {
            cmd.CommandText = "SELECT PalaceID, PalaceDescription, LociCount FROM palaces WHERE PalaceID = @id;";
            cmd.Parameters.AddWithValue("@id", id);
        }
        else
        {
            cmd.CommandText = "SELECT PalaceID, PalaceDescription, LociCount FROM palaces WHERE PalaceDescription = @desc COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("@desc", input);
        }
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Palace(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2));
        }
        return null;
    }

    public Palace CreatePalace(string description, int lociCount)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO palaces (PalaceDescription, LociCount) VALUES (@desc, @loci); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@desc", description);
        cmd.Parameters.AddWithValue("@loci", lociCount);
        var id = Convert.ToInt32(cmd.ExecuteScalar());
        return new Palace(id, description, lociCount);
    }

    public void UpdatePalaceDescription(int id, string newDescription)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE palaces SET PalaceDescription = @desc WHERE PalaceID = @id;";
        cmd.Parameters.AddWithValue("@desc", newDescription);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdatePalaceLociCount(int id, int lociCount)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE palaces SET LociCount = @loci WHERE PalaceID = @id;";
        cmd.Parameters.AddWithValue("@loci", lociCount);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeletePalace(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM palaces WHERE PalaceID = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public bool PalaceHasLogs(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM log WHERE PalaceID = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteScalar() is not null;
    }

    // ── Activities ──────────────────────────────────────────────────────

    public List<Activity> GetActivities()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT ActivityID, ActivityDescription FROM activities ORDER BY ActivityID;";
        var result = new List<Activity>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Activity(reader.GetInt32(0), reader.GetString(1)));
        }
        return result;
    }

    public Activity? GetActivityById(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT ActivityID, ActivityDescription FROM activities WHERE ActivityID = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Activity(reader.GetInt32(0), reader.GetString(1));
        }
        return null;
    }

    public Activity? GetActivityByNameOrId(string input)
    {
        using var cmd = _connection.CreateCommand();
        if (int.TryParse(input, out var id))
        {
            cmd.CommandText = "SELECT ActivityID, ActivityDescription FROM activities WHERE ActivityID = @id;";
            cmd.Parameters.AddWithValue("@id", id);
        }
        else
        {
            cmd.CommandText = "SELECT ActivityID, ActivityDescription FROM activities WHERE ActivityDescription = @desc COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("@desc", input);
        }
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Activity(reader.GetInt32(0), reader.GetString(1));
        }
        return null;
    }

    public Activity CreateActivity(string description)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO activities (ActivityDescription) VALUES (@desc); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@desc", description);
        var id = Convert.ToInt32(cmd.ExecuteScalar());
        return new Activity(id, description);
    }

    public void UpdateActivityDescription(int id, string newDescription)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE activities SET ActivityDescription = @desc WHERE ActivityID = @id;";
        cmd.Parameters.AddWithValue("@desc", newDescription);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteActivity(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM activities WHERE ActivityID = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public bool ActivityHasLogs(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM log WHERE ActivityID = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteScalar() is not null;
    }

    // ── Log ─────────────────────────────────────────────────────────────

    public LogEntry? GetOpenSession()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT LogID, PalaceID, ActivityID, StartedAt, EndedAt, LociReviewed, FrontToBack
            FROM log WHERE EndedAt IS NULL
            ORDER BY LogID DESC LIMIT 1;
            """;
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) return ReadLogEntry(reader);
        return null;
    }

    public int StartSession(int palaceId, int activityId, bool frontToBack, DateTime startedAt, int plannedLoci)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO log (PalaceID, ActivityID, StartedAt, LociReviewed, FrontToBack)
            VALUES (@p, @a, @s, @loci, @ftb);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@p", palaceId);
        cmd.Parameters.AddWithValue("@a", activityId);
        cmd.Parameters.AddWithValue("@s", startedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@loci", plannedLoci);
        cmd.Parameters.AddWithValue("@ftb", frontToBack ? 1 : 0);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int StopSession(int logId, DateTime endedAt, int lociReviewed)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE log SET EndedAt = @e, LociReviewed = @loci
            WHERE LogID = @id AND EndedAt IS NULL;
            """;
        cmd.Parameters.AddWithValue("@e", endedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@loci", lociReviewed);
        cmd.Parameters.AddWithValue("@id", logId);
        return cmd.ExecuteNonQuery();
    }

    public int LogCompletedSession(int palaceId, int activityId, DateTime startedAt, DateTime endedAt, int lociReviewed, bool frontToBack)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO log (PalaceID, ActivityID, StartedAt, EndedAt, LociReviewed, FrontToBack)
            VALUES (@p, @a, @s, @e, @loci, @ftb);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@p", palaceId);
        cmd.Parameters.AddWithValue("@a", activityId);
        cmd.Parameters.AddWithValue("@s", startedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@e", endedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@loci", lociReviewed);
        cmd.Parameters.AddWithValue("@ftb", frontToBack ? 1 : 0);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<LogEntry> GetCompletedLogsForPalaceNewestFirst(int palaceId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT LogID, PalaceID, ActivityID, StartedAt, EndedAt, LociReviewed, FrontToBack
            FROM log WHERE PalaceID = @p AND EndedAt IS NOT NULL
            ORDER BY StartedAt DESC;
            """;
        cmd.Parameters.AddWithValue("@p", palaceId);
        return ReadLogEntries(cmd);
    }

    public List<LogEntry> GetCompletedLogsForActivityNewestFirst(int activityId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT LogID, PalaceID, ActivityID, StartedAt, EndedAt, LociReviewed, FrontToBack
            FROM log WHERE ActivityID = @a AND EndedAt IS NOT NULL
            ORDER BY StartedAt DESC;
            """;
        cmd.Parameters.AddWithValue("@a", activityId);
        return ReadLogEntries(cmd);
    }

    public List<LogEntry> GetAllCompletedLogsNewestFirst()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT LogID, PalaceID, ActivityID, StartedAt, EndedAt, LociReviewed, FrontToBack
            FROM log WHERE EndedAt IS NOT NULL
            ORDER BY StartedAt DESC;
            """;
        return ReadLogEntries(cmd);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static List<LogEntry> ReadLogEntries(SqliteCommand cmd)
    {
        var result = new List<LogEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) result.Add(ReadLogEntry(reader));
        return result;
    }

    private static LogEntry ReadLogEntry(SqliteDataReader reader)
    {
        return new LogEntry(
            Id: reader.GetInt32(0),
            PalaceId: reader.GetInt32(1),
            ActivityId: reader.GetInt32(2),
            StartedAt: DateTime.Parse(reader.GetString(3)),
            EndedAt: reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
            LociReviewed: reader.IsDBNull(5) ? null : reader.GetInt32(5),
            FrontToBack: reader.GetInt32(6) != 0);
    }

    public void Dispose() => _connection.Dispose();
}
