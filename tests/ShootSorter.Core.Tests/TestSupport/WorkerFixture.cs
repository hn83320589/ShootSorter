using ShootSorter.Core.Archiving;
using ShootSorter.Core.Logging;
using ShootSorter.Core.Watching;
using ShootSorter.Core.Metadata;

namespace ShootSorter.Core.Tests.TestSupport;

/// <summary>Worker / Watcher 測試共用的收件匣、歸檔區、資料庫與事件收集。</summary>
public sealed class WorkerFixture : IDisposable
{
    public static readonly WorkerTimings FastTimings = new(
        ReadyPollInterval: TimeSpan.FromMilliseconds(20),
        ReadyTimeout: TimeSpan.FromMilliseconds(300),
        BatchQuietPeriod: TimeSpan.FromMilliseconds(200));

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly TempDirectory _temp = new();
    private readonly List<ArchiveRecord> _records = [];
    private readonly List<BatchSummary> _batches = [];

    public WorkerFixture(WorkerTimings? timings = null)
    {
        Inbox = Directory.CreateDirectory(Path.Combine(_temp.Path, "inbox")).FullName;
        Root = Directory.CreateDirectory(Path.Combine(_temp.Path, "archive")).FullName;
        Log = new SqliteArchiveLog(Path.Combine(_temp.Path, "archive.db"));
        Log.EnsureCreated();
        Options = new ArchiveOptions(Root, [], []);

        var processor = new ArchiveProcessor(
            new CaptureDateResolver(new MetadataExtractorExifReader()), Log, TimeProvider.System);
        Worker = new ArchiveWorker(processor, () => Options, timings ?? FastTimings);
        Worker.RecordProcessed += (_, record) => { lock (_records) { _records.Add(record); } };
        Worker.BatchCompleted += (_, batch) => { lock (_batches) { _batches.Add(batch); } };
    }

    public string Inbox { get; }

    public string Root { get; }

    public SqliteArchiveLog Log { get; }

    public ArchiveOptions Options { get; set; }

    public ArchiveWorker Worker { get; }

    public IReadOnlyList<ArchiveRecord> Records
    {
        get { lock (_records) { return [.. _records]; } }
    }

    public IReadOnlyList<BatchSummary> Batches
    {
        get { lock (_batches) { return [.. _batches]; } }
    }

    public string DropInInbox(string fileName, byte[] content)
    {
        var path = Path.Combine(Inbox, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    public Task WaitForRecordsAsync(int count) => WaitUntilAsync(() => Records.Count >= count, $"{count} 筆處理紀錄");

    public Task WaitForBatchesAsync(int count) => WaitUntilAsync(() => Batches.Count >= count, $"{count} 次批次摘要");

    public static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"等待 {description} 逾時。");
            }

            await Task.Delay(20);
        }
    }

    public void Dispose()
    {
        Worker.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _temp.Dispose();
    }
}
