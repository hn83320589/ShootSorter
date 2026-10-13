using ShootSorter.Core.Archiving;
using ShootSorter.Core.Metadata;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Metadata;

public sealed class CaptureDateResolverTests : IDisposable
{
    private static readonly DateTime CreatedAt = new(2026, 10, 13, 9, 0, 0);
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Resolve_ImageWithExif_UsesExifEvenWhenCreationDiffers()
    {
        var exifDate = new DateTime(2024, 5, 1, 10, 20, 30);
        var resolver = new CaptureDateResolver(new FakeExifReader(exifDate));

        var result = resolver.Resolve(CreateFile("a.jpg"), MediaKind.Image);

        Assert.Equal(new CaptureDate(exifDate, DateSource.Exif, MissingExif: false), result);
    }

    [Fact]
    public void Resolve_ImageWithoutExif_UsesCreationTimeAndFlagsMissingExif()
    {
        var resolver = new CaptureDateResolver(new FakeExifReader(null));

        var result = resolver.Resolve(CreateFile("a.png"), MediaKind.Image);

        Assert.Equal(new CaptureDate(CreatedAt, DateSource.FileCreated, MissingExif: true), result);
    }

    [Fact]
    public void Resolve_Video_UsesCreationTimeWithoutReadingExif()
    {
        var reader = new FakeExifReader(new DateTime(2000, 1, 1));
        var resolver = new CaptureDateResolver(reader);

        var result = resolver.Resolve(CreateFile("a.mp4"), MediaKind.Video);

        Assert.Equal(new CaptureDate(CreatedAt, DateSource.FileCreated, MissingExif: false), result);
        Assert.Equal(0, reader.Calls);
    }

    private string CreateFile(string name)
    {
        var path = _temp.WriteFile(name, [1, 2, 3]);
        File.SetCreationTime(path, CreatedAt);
        return path;
    }

    private sealed class FakeExifReader(DateTime? date) : IExifDateReader
    {
        public int Calls { get; private set; }

        public DateTime? ReadCaptureDate(string path)
        {
            Calls++;
            return date;
        }
    }
}
