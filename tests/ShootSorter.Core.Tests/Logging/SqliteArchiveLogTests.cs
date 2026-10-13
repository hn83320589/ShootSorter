using ShootSorter.Core.Logging;
using ShootSorter.Core.Metadata;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Logging;

public sealed class SqliteArchiveLogTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SqliteArchiveLog _log;

    public SqliteArchiveLogTests()
    {
        _log = new SqliteArchiveLog(Path.Combine(_temp.Path, "nested", "archive.db"));
        _log.EnsureCreated();
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Add_ThenGet_RoundTripsAllFields()
    {
        var record = new ArchiveRecord
        {
            SourcePath = @"C:\Inbox\a.jpg",
            DestinationPath = @"D:\Archive\2024\2024-05-01\a.jpg",
            Sha256 = "abc123",
            CapturedAt = new DateTime(2024, 5, 1, 10, 20, 30),
            DateSource = DateSource.FileCreated,
            MissingExif = true,
            CaseName = "ACME",
            Status = ArchiveStatus.Failed,
            Reason = "檔案被佔用",
            ProcessedAt = new DateTimeOffset(2026, 10, 13, 1, 2, 3, TimeSpan.Zero),
            RetryCount = 2,
        };

        var id = _log.Add(record);
        var loaded = _log.Get(id);

        Assert.Equivalent(record with { Id = id }, loaded, strict: true);
    }

    [Fact]
    public void EnsureCreated_CalledTwice_KeepsExistingRecords()
    {
        _log.Add(Record(ArchiveStatus.Success));

        _log.EnsureCreated();

        Assert.Single(_log.Query(new ArchiveQuery()));
    }

    [Fact]
    public void FindSuccessByHash_IgnoresSkippedAndFailed()
    {
        _log.Add(Record(ArchiveStatus.Failed, sha256: "h1"));
        _log.Add(Record(ArchiveStatus.Skipped, sha256: "h1"));
        Assert.Null(_log.FindSuccessByHash("h1"));

        var successId = _log.Add(Record(ArchiveStatus.Success, sha256: "h1"));
        Assert.Equal(successId, _log.FindSuccessByHash("h1")?.Id);
    }

    [Fact]
    public void Query_ByStatus_ReturnsOnlyThatStatus()
    {
        _log.Add(Record(ArchiveStatus.Success));
        _log.Add(Record(ArchiveStatus.Failed));
        _log.Add(Record(ArchiveStatus.Failed));

        var failed = _log.Query(new ArchiveQuery(Status: ArchiveStatus.Failed));

        Assert.Equal(2, failed.Count);
        Assert.All(failed, r => Assert.Equal(ArchiveStatus.Failed, r.Status));
    }

    [Fact]
    public void Query_ByLocalDateRange_IncludesBothEndDays()
    {
        _log.Add(Record(ArchiveStatus.Success, processedLocal: new DateTime(2026, 10, 9, 23, 59, 59)));
        var first = _log.Add(Record(ArchiveStatus.Success, processedLocal: new DateTime(2026, 10, 10, 0, 0, 0)));
        var last = _log.Add(Record(ArchiveStatus.Success, processedLocal: new DateTime(2026, 10, 12, 23, 59, 59)));
        _log.Add(Record(ArchiveStatus.Success, processedLocal: new DateTime(2026, 10, 13, 0, 0, 0)));

        var result = _log.Query(new ArchiveQuery(From: new DateOnly(2026, 10, 10), To: new DateOnly(2026, 10, 12)));

        Assert.Equal([last, first], result.Select(r => r.Id));
    }

    [Fact]
    public void Update_ChangesStoredRecord()
    {
        var id = _log.Add(Record(ArchiveStatus.Failed));
        var retried = _log.Get(id)! with
        {
            Status = ArchiveStatus.Success,
            DestinationPath = @"D:\Archive\x.jpg",
            Reason = null,
            RetryCount = 1,
        };

        _log.Update(retried);

        Assert.Equivalent(retried, _log.Get(id), strict: true);
    }

    private static ArchiveRecord Record(ArchiveStatus status, string sha256 = "hash", DateTime? processedLocal = null)
    {
        return new ArchiveRecord
        {
            SourcePath = @"C:\Inbox\a.jpg",
            Sha256 = sha256,
            Status = status,
            ProcessedAt = new DateTimeOffset(processedLocal ?? new DateTime(2026, 10, 13, 12, 0, 0)),
        };
    }
}
