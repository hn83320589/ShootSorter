using System.Globalization;
using Microsoft.Data.Sqlite;
using ShootSorter.Core.Metadata;

namespace ShootSorter.Core.Logging;

public sealed class SqliteArchiveLog
{
    // 固定寬度的 UTC ISO 8601 字串可直接以字串比較排序與篩選。
    private const string InstantFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
    private const string LocalDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffff";

    private const string Columns =
        "Id, SourcePath, DestinationPath, Sha256, CapturedAt, DateSource, MissingExif, CaseName, Status, Reason, ProcessedAt, RetryCount";

    private readonly string _databasePath;
    private readonly string _connectionString;

    public SqliteArchiveLog(string databasePath)
    {
        _databasePath = databasePath;
        // 關閉 pooling，連線關閉後即釋放檔案，避免資料庫檔被長期鎖住。
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
    }

    public void EnsureCreated()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_databasePath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        Execute("""
            CREATE TABLE IF NOT EXISTS ArchiveRecords (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                SourcePath      TEXT    NOT NULL,
                DestinationPath TEXT,
                Sha256          TEXT,
                CapturedAt      TEXT,
                DateSource      TEXT,
                MissingExif     INTEGER NOT NULL,
                CaseName        TEXT,
                Status          TEXT    NOT NULL,
                Reason          TEXT,
                ProcessedAt     TEXT    NOT NULL,
                RetryCount      INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_ArchiveRecords_Sha256 ON ArchiveRecords (Sha256);
            CREATE INDEX IF NOT EXISTS IX_ArchiveRecords_Status ON ArchiveRecords (Status);
            CREATE INDEX IF NOT EXISTS IX_ArchiveRecords_ProcessedAt ON ArchiveRecords (ProcessedAt);
            """);
    }

    public long Add(ArchiveRecord record)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ArchiveRecords
                (SourcePath, DestinationPath, Sha256, CapturedAt, DateSource, MissingExif, CaseName, Status, Reason, ProcessedAt, RetryCount)
            VALUES
                ($source, $destination, $sha256, $capturedAt, $dateSource, $missingExif, $caseName, $status, $reason, $processedAt, $retryCount);
            SELECT last_insert_rowid();
            """;
        BindFields(command, record);
        return (long)command.ExecuteScalar()!;
    }

    public void Update(ArchiveRecord record)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ArchiveRecords SET
                SourcePath = $source, DestinationPath = $destination, Sha256 = $sha256, CapturedAt = $capturedAt,
                DateSource = $dateSource, MissingExif = $missingExif, CaseName = $caseName, Status = $status,
                Reason = $reason, ProcessedAt = $processedAt, RetryCount = $retryCount
            WHERE Id = $id;
            """;
        BindFields(command, record);
        command.Parameters.AddWithValue("$id", record.Id);

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException($"找不到 Id = {record.Id} 的歸檔紀錄，無法更新。");
        }
    }

    public ArchiveRecord? Get(long id)
    {
        return Read($"SELECT {Columns} FROM ArchiveRecords WHERE Id = $id;", ("$id", id)).SingleOrDefault();
    }

    /// <summary>找出同雜湊值的最新成功紀錄，用於判斷內容重複。</summary>
    public ArchiveRecord? FindSuccessByHash(string sha256)
    {
        return Read(
            $"SELECT {Columns} FROM ArchiveRecords WHERE Sha256 = $sha256 AND Status = $status ORDER BY Id DESC LIMIT 1;",
            ("$sha256", sha256),
            ("$status", ArchiveStatus.Success.ToString())).SingleOrDefault();
    }

    /// <summary>依條件查詢，最新的排在前面。</summary>
    public IReadOnlyList<ArchiveRecord> Query(ArchiveQuery query)
    {
        var conditions = new List<string>();
        var parameters = new List<(string, object)>();

        if (query.Status is { } status)
        {
            conditions.Add("Status = $status");
            parameters.Add(("$status", status.ToString()));
        }

        if (query.From is { } from)
        {
            conditions.Add("ProcessedAt >= $from");
            parameters.Add(("$from", FormatInstant(StartOfLocalDay(from))));
        }

        if (query.To is { } to)
        {
            conditions.Add("ProcessedAt < $toExclusive");
            parameters.Add(("$toExclusive", FormatInstant(StartOfLocalDay(to.AddDays(1)))));
        }

        var where = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        return Read($"SELECT {Columns} FROM ArchiveRecords {where} ORDER BY ProcessedAt DESC, Id DESC;", [.. parameters]);
    }

    private static DateTimeOffset StartOfLocalDay(DateOnly date)
    {
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local));
    }

    private static string FormatInstant(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString(InstantFormat, CultureInfo.InvariantCulture);
    }

    private static void BindFields(SqliteCommand command, ArchiveRecord record)
    {
        var p = command.Parameters;
        p.AddWithValue("$source", record.SourcePath);
        p.AddWithValue("$destination", (object?)record.DestinationPath ?? DBNull.Value);
        p.AddWithValue("$sha256", (object?)record.Sha256 ?? DBNull.Value);
        p.AddWithValue("$capturedAt", (object?)record.CapturedAt?.ToString(LocalDateTimeFormat, CultureInfo.InvariantCulture) ?? DBNull.Value);
        p.AddWithValue("$dateSource", (object?)record.DateSource?.ToString() ?? DBNull.Value);
        p.AddWithValue("$missingExif", record.MissingExif);
        p.AddWithValue("$caseName", (object?)record.CaseName ?? DBNull.Value);
        p.AddWithValue("$status", record.Status.ToString());
        p.AddWithValue("$reason", (object?)record.Reason ?? DBNull.Value);
        p.AddWithValue("$processedAt", FormatInstant(record.ProcessedAt));
        p.AddWithValue("$retryCount", record.RetryCount);
    }

    private List<ArchiveRecord> Read(string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        using var reader = command.ExecuteReader();
        var records = new List<ArchiveRecord>();
        while (reader.Read())
        {
            records.Add(new ArchiveRecord
            {
                Id = reader.GetInt64(0),
                SourcePath = reader.GetString(1),
                DestinationPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                Sha256 = reader.IsDBNull(3) ? null : reader.GetString(3),
                CapturedAt = reader.IsDBNull(4)
                    ? null
                    : DateTime.ParseExact(reader.GetString(4), LocalDateTimeFormat, CultureInfo.InvariantCulture),
                DateSource = reader.IsDBNull(5) ? null : Enum.Parse<DateSource>(reader.GetString(5)),
                MissingExif = reader.GetBoolean(6),
                CaseName = reader.IsDBNull(7) ? null : reader.GetString(7),
                Status = Enum.Parse<ArchiveStatus>(reader.GetString(8)),
                Reason = reader.IsDBNull(9) ? null : reader.GetString(9),
                ProcessedAt = DateTimeOffset.ParseExact(
                    reader.GetString(10), InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                RetryCount = reader.GetInt32(11),
            });
        }

        return records;
    }

    private void Execute(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
