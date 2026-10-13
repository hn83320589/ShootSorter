using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using ShootSorter.Core.Archiving;
using ShootSorter.Core.Logging;
using ShootSorter.Core.Watching;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Watching;

public sealed class ArchiveWorkerTests : IDisposable
{
    private readonly WorkerFixture _f = new();

    public ArchiveWorkerTests() => _f.Worker.Start();

    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task EnqueueFile_ProcessesAndReportsBatch()
    {
        _f.Worker.EnqueueFile(_f.DropInInbox("a.jpg", JpegExifBuilder.WithoutExif()));

        await _f.WaitForBatchesAsync(1);

        Assert.Equal(ArchiveStatus.Success, Assert.Single(_f.Records).Status);
        Assert.Equal(new BatchSummary(Succeeded: 1, Skipped: 0, Failed: 0), Assert.Single(_f.Batches));
    }

    [Fact]
    public async Task FilesArrivingTogether_AreSummarisedAsOneBatch()
    {
        var content = JpegExifBuilder.WithoutExif();
        _f.Worker.EnqueueFile(_f.DropInInbox("a.jpg", content));
        _f.Worker.EnqueueFile(_f.DropInInbox("b.jpg", content));
        _f.Worker.EnqueueFile(_f.DropInInbox("c.png", [9, 9, 9]));

        await _f.WaitForBatchesAsync(1);

        Assert.Equal(new BatchSummary(Succeeded: 2, Skipped: 1, Failed: 0), Assert.Single(_f.Batches));
    }

    [Fact]
    public async Task SamePathEnqueuedTwice_IsProcessedOnce()
    {
        var path = _f.DropInInbox("a.jpg", JpegExifBuilder.WithoutExif());
        _f.Worker.EnqueueFile(path);
        _f.Worker.EnqueueFile(path);

        await _f.WaitForBatchesAsync(1);

        Assert.Single(_f.Records);
        Assert.Single(_f.Log.Query(new ArchiveQuery()));
    }

    [Fact]
    public async Task FileRemovedBeforeProcessing_IsIgnoredWithoutRecord()
    {
        var gone = Path.Combine(_f.Inbox, "gone.jpg");
        _f.Worker.EnqueueFile(gone);
        _f.Worker.EnqueueFile(_f.DropInInbox("a.jpg", JpegExifBuilder.WithoutExif()));

        await _f.WaitForBatchesAsync(1);

        Assert.Equal("a.jpg", Path.GetFileName(Assert.Single(_f.Records).SourcePath));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task UnreadableFile_IsRecordedAsFailedInsteadOfOnlyRaisingError()
    {
        var path = _f.DropInInbox("a.jpg", JpegExifBuilder.WithoutExif());
        var errors = new List<Exception>();
        _f.Worker.ProcessingError += (_, ex) => { lock (errors) { errors.Add(ex); } };
        var file = new FileInfo(path);
        var denyRead = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        var acl = file.GetAccessControl();
        acl.AddAccessRule(denyRead);
        file.SetAccessControl(acl);
        try
        {
            _f.Worker.EnqueueFile(path);
            await _f.WaitForRecordsAsync(1);
        }
        finally
        {
            acl.RemoveAccessRule(denyRead);
            file.SetAccessControl(acl);
        }

        var record = Assert.Single(_f.Records);
        Assert.Equal(ArchiveStatus.Failed, record.Status);
        Assert.Contains(nameof(UnauthorizedAccessException), record.Reason);
        Assert.Equivalent(record, _f.Log.Get(record.Id), strict: true);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task LockedFile_WaitsUntilReleasedThenSucceeds()
    {
        var path = _f.DropInInbox("a.jpg", JpegExifBuilder.WithoutExif());
        var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None);

        _f.Worker.EnqueueFile(path);
        for (var i = 0; i < 5; i++)
        {
            writer.WriteByte(0); // 模擬仍在複製中：持續寫入讓大小變動
            await writer.FlushAsync();
            await Task.Delay(100);
        }

        await writer.DisposeAsync();
        await _f.WaitForRecordsAsync(1);

        Assert.Equal(ArchiveStatus.Success, Assert.Single(_f.Records).Status);
    }

    [Fact]
    public async Task FileLockedWithoutProgress_FailsAfterTimeout()
    {
        var path = _f.DropInInbox("a.jpg", JpegExifBuilder.WithoutExif());
        await using var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        _f.Worker.EnqueueFile(path);
        await _f.WaitForBatchesAsync(1);

        Assert.Equal(ArchiveStatus.Failed, Assert.Single(_f.Records).Status);
        Assert.Equal(new BatchSummary(Succeeded: 0, Skipped: 0, Failed: 1), Assert.Single(_f.Batches));
    }

    [Fact]
    public void DisposeAsync_WaitedSynchronouslyOnBlockedUiContext_DoesNotDeadlock()
    {
        // WPF 在 OnExit 同步等待關閉時，UI 執行緒被佔住，任何回到該 context 的 continuation 都不會執行。
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new BlockedSynchronizationContext());
        try
        {
#pragma warning disable xUnit1031 // 本測試的目的就是重現 UI 執行緒同步等待的情境。
            Assert.True(_f.Worker.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5)));
#pragma warning restore xUnit1031
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public async Task EnqueueRetry_ReprocessesFailedRecord()
    {
        _f.Options = _f.Options with { ArchiveRoot = Path.Combine(_f.Root, "missing") };
        _f.Worker.EnqueueFile(_f.DropInInbox("a.jpg", JpegExifBuilder.WithoutExif()));
        await _f.WaitForBatchesAsync(1);
        var failed = Assert.Single(_f.Records);

        _f.Options = _f.Options with { ArchiveRoot = _f.Root };
        _f.Worker.EnqueueRetry(failed.Id);
        await _f.WaitForBatchesAsync(2);

        var retried = _f.Records[1];
        Assert.Equal(failed.Id, retried.Id);
        Assert.Equal(ArchiveStatus.Success, retried.Status);
        Assert.Equal(new BatchSummary(Succeeded: 1, Skipped: 0, Failed: 0), _f.Batches[1]);
    }

    private sealed class BlockedSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // 刻意不執行，模擬被同步等待卡住的 UI 執行緒。
        }
    }
}
