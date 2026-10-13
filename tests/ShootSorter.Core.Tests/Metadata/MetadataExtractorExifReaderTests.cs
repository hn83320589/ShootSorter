using ShootSorter.Core.Metadata;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Metadata;

public sealed class MetadataExtractorExifReaderTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly MetadataExtractorExifReader _reader = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ReadCaptureDate_DateTimeOriginal_ReturnsIt()
    {
        var original = new DateTime(2024, 5, 1, 10, 20, 30);
        var path = _temp.WriteFile("a.jpg", JpegExifBuilder.WithDates(original: original));

        Assert.Equal(original, _reader.ReadCaptureDate(path));
    }

    [Fact]
    public void ReadCaptureDate_PrefersOriginalOverDigitized()
    {
        var original = new DateTime(2024, 5, 1, 10, 20, 30);
        var path = _temp.WriteFile("a.jpg", JpegExifBuilder.WithDates(original, new DateTime(2025, 1, 1)));

        Assert.Equal(original, _reader.ReadCaptureDate(path));
    }

    [Fact]
    public void ReadCaptureDate_OnlyDigitized_FallsBackToIt()
    {
        var digitized = new DateTime(2023, 12, 31, 23, 59, 59);
        var path = _temp.WriteFile("a.jpg", JpegExifBuilder.WithDates(digitized: digitized));

        Assert.Equal(digitized, _reader.ReadCaptureDate(path));
    }

    [Fact]
    public void ReadCaptureDate_JpegWithoutExif_ReturnsNull()
    {
        var path = _temp.WriteFile("a.jpg", JpegExifBuilder.WithoutExif());

        Assert.Null(_reader.ReadCaptureDate(path));
    }

    [Theory]
    [InlineData("broken.jpg")]
    [InlineData("unreadable.cr3")]
    [InlineData("unreadable.arw")]
    public void ReadCaptureDate_UnrecognisedContent_ReturnsNull(string fileName)
    {
        var path = _temp.WriteFile(fileName, "not an image"u8.ToArray());

        Assert.Null(_reader.ReadCaptureDate(path));
    }

    [Fact]
    public void ReadCaptureDate_MissingFile_Throws()
    {
        Assert.ThrowsAny<IOException>(() => _reader.ReadCaptureDate(Path.Combine(_temp.Path, "missing.jpg")));
    }
}
