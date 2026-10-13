using ShootSorter.Core.Archiving;
using ShootSorter.Core.Logging;
using ShootSorter.Core.Watching;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Watching;

public sealed class InboxWatcherTests : IDisposable
{
    private readonly WorkerFixture _f = new();
    private readonly InboxWatcher _watcher;

    public InboxWatcherTests()
    {
        _f.Worker.Start();
        _watcher = new InboxWatcher(_f.Worker, () => _f.Options);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _f.Dispose();
    }

    [Fact]
    public void Start_MissingDirectory_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => _watcher.Start(Path.Combine(_f.Inbox, "missing")));
    }

    [Fact]
    public async Task Start_ProcessesFilesAlreadyInInbox()
    {
        _f.DropInInbox("existing.jpg", JpegExifBuilder.WithoutExif());

        _watcher.Start(_f.Inbox);
        await _f.WaitForRecordsAsync(1);

        Assert.Equal(ArchiveStatus.Success, Assert.Single(_f.Records).Status);
    }

    [Fact]
    public async Task NewFileInInbox_IsArchived()
    {
        _watcher.Start(_f.Inbox);

        var path = _f.DropInInbox("new.jpg", JpegExifBuilder.WithoutExif());
        await _f.WaitForRecordsAsync(1);

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(Assert.Single(_f.Records).DestinationPath));
    }

    [Fact]
    public async Task ExcludedAndSubfolderFiles_AreNeverQueued()
    {
        _f.Options = _f.Options with { ExcludedExtensions = ["xmp"] };
        var subfolder = Directory.CreateDirectory(Path.Combine(_f.Inbox, "sub")).FullName;
        _watcher.Start(_f.Inbox);

        var excluded = _f.DropInInbox("a.xmp", [1]);
        var nested = Path.Combine(subfolder, "nested.jpg");
        File.WriteAllBytes(nested, JpegExifBuilder.WithoutExif());
        _f.DropInInbox("marker.jpg", JpegExifBuilder.WithDates(original: new DateTime(2024, 1, 1)));
        await _f.WaitForBatchesAsync(1);

        Assert.Equal("marker.jpg", Path.GetFileName(Assert.Single(_f.Records).SourcePath));
        Assert.True(File.Exists(excluded));
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public async Task Stop_ThenNewFile_IsNotProcessed()
    {
        _watcher.Start(_f.Inbox);
        _watcher.Stop();

        var path = _f.DropInInbox("late.jpg", JpegExifBuilder.WithoutExif());
        await Task.Delay(500);

        Assert.Empty(_f.Records);
        Assert.True(File.Exists(path));
    }
}
