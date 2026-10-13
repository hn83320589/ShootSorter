using ShootSorter.Core.Archiving;
using ShootSorter.Core.Logging;
using ShootSorter.Core.Metadata;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Archiving;

public sealed class ArchiveProcessorTests : IDisposable
{
    private static readonly DateTime ExifDate = new(2024, 5, 1, 10, 20, 30);
    private static readonly DateTime CreatedAt = new(2026, 10, 13, 9, 0, 0);
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _temp = new();
    private readonly string _inbox;
    private readonly string _root;
    private readonly SqliteArchiveLog _log;
    private readonly ArchiveProcessor _processor;

    public ArchiveProcessorTests()
    {
        _inbox = Directory.CreateDirectory(Path.Combine(_temp.Path, "inbox")).FullName;
        _root = Directory.CreateDirectory(Path.Combine(_temp.Path, "archive")).FullName;
        _log = new SqliteArchiveLog(Path.Combine(_temp.Path, "archive.db"));
        _log.EnsureCreated();
        _processor = new ArchiveProcessor(
            new CaptureDateResolver(new MetadataExtractorExifReader()), _log, new FixedTimeProvider(Now));
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Process_ImageWithExif_MovesToExifDateFolder()
    {
        var source = DropInInbox("IMG_1.jpg", JpegExifBuilder.WithDates(original: ExifDate));

        var record = _processor.Process(source, Options())!;

        var expected = Path.Combine(_root, "2024", "2024-05-01", "IMG_1.jpg");
        Assert.True(File.Exists(expected));
        Assert.False(File.Exists(source));
        Assert.Equal(ArchiveStatus.Success, record.Status);
        Assert.Equal(expected, record.DestinationPath);
        Assert.Equal(DateSource.Exif, record.DateSource);
        Assert.False(record.MissingExif);
        Assert.Equal(Now, record.ProcessedAt);
        Assert.Equivalent(record, _log.Get(record.Id), strict: true);
    }

    [Fact]
    public void Process_ImageWithoutExif_UsesCreationDateAndFlagsMissingExif()
    {
        var source = DropInInbox("IMG_1.jpg", JpegExifBuilder.WithoutExif());

        var record = _processor.Process(source, Options())!;

        Assert.Equal(Path.Combine(_root, "2026", "2026-10-13", "IMG_1.jpg"), record.DestinationPath);
        Assert.Equal(DateSource.FileCreated, record.DateSource);
        Assert.True(record.MissingExif);
    }

    [Fact]
    public void Process_MatchingRule_MovesToCaseFolder()
    {
        var source = DropInInbox("ACME_001.jpg", JpegExifBuilder.WithDates(original: ExifDate));
        var options = Options(rules: [new CaseRule("NOPE", "Nope"), new CaseRule("acme", "ACME")]);

        var record = _processor.Process(source, options)!;

        Assert.Equal(Path.Combine(_root, "ACME", "2024-05-01", "ACME_001.jpg"), record.DestinationPath);
        Assert.Equal("ACME", record.CaseName);
    }

    [Fact]
    public void Process_SameContentAgain_SkipsAndMovesSourceToSkippedFolder()
    {
        var content = JpegExifBuilder.WithDates(original: ExifDate);
        var first = _processor.Process(DropInInbox("a.jpg", content), Options())!;

        var second = DropInInbox("b.jpg", content);
        var record = _processor.Process(second, Options())!;

        Assert.Equal(ArchiveStatus.Skipped, record.Status);
        Assert.Equal(first.DestinationPath, record.DestinationPath);
        Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
        Assert.False(File.Exists(second));
        Assert.True(File.Exists(Path.Combine(_inbox, "_Skipped", "b.jpg")));
    }

    [Fact]
    public void Process_SameNameDifferentContent_Appends001AndKeepsOriginal()
    {
        var firstContent = JpegExifBuilder.WithDates(original: ExifDate);
        var first = _processor.Process(DropInInbox("IMG_1.jpg", firstContent), Options())!;

        var secondContent = JpegExifBuilder.WithDates(original: ExifDate, digitized: ExifDate);
        var second = _processor.Process(DropInInbox("IMG_1.jpg", secondContent), Options())!;

        Assert.Equal(Path.Combine(_root, "2024", "2024-05-01", "IMG_1_001.jpg"), second.DestinationPath);
        Assert.Equal(firstContent, File.ReadAllBytes(first.DestinationPath!));
        Assert.Equal(secondContent, File.ReadAllBytes(second.DestinationPath!));
    }

    [Fact]
    public void Process_IdenticalFileAlreadyInDestinationButNotLogged_Skips()
    {
        var content = JpegExifBuilder.WithDates(original: ExifDate);
        var existing = Path.Combine(_root, "2024", "2024-05-01", "IMG_1.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllBytes(existing, content);

        var record = _processor.Process(DropInInbox("IMG_1.jpg", content), Options())!;

        Assert.Equal(ArchiveStatus.Skipped, record.Status);
        Assert.Equal(existing, record.DestinationPath);
        Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Process_DuplicateLoggedButArchivedFileDeleted_ArchivesAgain()
    {
        var content = JpegExifBuilder.WithDates(original: ExifDate);
        var first = _processor.Process(DropInInbox("a.jpg", content), Options())!;
        File.Delete(first.DestinationPath!);

        var record = _processor.Process(DropInInbox("a.jpg", content), Options())!;

        Assert.Equal(ArchiveStatus.Success, record.Status);
        Assert.True(File.Exists(record.DestinationPath));
    }

    [Theory]
    [InlineData("notes.txt", new string[0])]
    [InlineData("IMG_1.png", new[] { "png" })]
    public void Process_UnsupportedOrExcluded_IgnoresWithoutRecord(string fileName, string[] excluded)
    {
        var source = DropInInbox(fileName, [1, 2, 3]);

        var record = _processor.Process(source, Options(excluded: excluded));

        Assert.Null(record);
        Assert.True(File.Exists(source));
        Assert.Empty(_log.Query(new ArchiveQuery()));
    }

    [Fact]
    public void Process_ArchiveRootMissing_FailsAndLeavesSource()
    {
        var source = DropInInbox("IMG_1.jpg", JpegExifBuilder.WithoutExif());

        var record = _processor.Process(source, Options(root: Path.Combine(_temp.Path, "missing")))!;

        Assert.Equal(ArchiveStatus.Failed, record.Status);
        Assert.Contains("歸檔根目錄不存在", record.Reason);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Process_DateReaderThrowsUnexpectedly_FailsWithExceptionDetails()
    {
        var processor = new ArchiveProcessor(
            new CaptureDateResolver(new ThrowingExifReader()), _log, new FixedTimeProvider(Now));
        var source = DropInInbox("IMG_1.jpg", JpegExifBuilder.WithoutExif());

        var record = processor.Process(source, Options())!;

        Assert.Equal(ArchiveStatus.Failed, record.Status);
        Assert.Contains(nameof(ArgumentOutOfRangeException), record.Reason);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Retry_AfterLockReleased_SucceedsAndUpdatesSameRecord()
    {
        var source = DropInInbox("IMG_1.jpg", JpegExifBuilder.WithDates(original: ExifDate));
        ArchiveRecord failed;
        using (new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            failed = _processor.Process(source, Options())!;
        }

        var retried = _processor.Retry(failed.Id, Options());

        Assert.Equal(ArchiveStatus.Failed, failed.Status);
        Assert.False(string.IsNullOrEmpty(failed.Reason));
        Assert.Equal(failed.Id, retried.Id);
        Assert.Equal(ArchiveStatus.Success, retried.Status);
        Assert.Null(retried.Reason);
        Assert.Equal(1, retried.RetryCount);
        Assert.Equivalent(retried, _log.Get(failed.Id), strict: true);
        Assert.Single(_log.Query(new ArchiveQuery()));
    }

    [Fact]
    public void Retry_SourceGone_StaysFailedAndCountsAttempt()
    {
        var source = DropInInbox("IMG_1.jpg", JpegExifBuilder.WithoutExif());
        var failed = _processor.Process(source, Options(root: Path.Combine(_temp.Path, "missing")))!;
        File.Delete(source);

        var retried = _processor.Retry(failed.Id, Options());

        Assert.Equal(ArchiveStatus.Failed, retried.Status);
        Assert.Contains("原始檔案不存在", retried.Reason);
        Assert.Equal(1, retried.RetryCount);
    }

    [Fact]
    public void Retry_NonFailedRecord_Throws()
    {
        var record = _processor.Process(DropInInbox("IMG_1.jpg", JpegExifBuilder.WithoutExif()), Options())!;

        Assert.Throws<InvalidOperationException>(() => _processor.Retry(record.Id, Options()));
    }

    private ArchiveOptions Options(string? root = null, IReadOnlyList<CaseRule>? rules = null, IReadOnlyList<string>? excluded = null)
    {
        return new ArchiveOptions(root ?? _root, rules ?? [], excluded ?? []);
    }

    private sealed class ThrowingExifReader : IExifDateReader
    {
        public DateTime? ReadCaptureDate(string path) => throw new ArgumentOutOfRangeException(nameof(path));
    }

    private string DropInInbox(string fileName, byte[] content)
    {
        var path = Path.Combine(_inbox, fileName);
        File.WriteAllBytes(path, content);
        File.SetCreationTime(path, CreatedAt);
        return path;
    }
}
