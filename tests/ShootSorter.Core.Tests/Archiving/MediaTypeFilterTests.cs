using ShootSorter.Core.Archiving;

namespace ShootSorter.Core.Tests.Archiving;

public class MediaTypeFilterTests
{
    [Theory]
    [InlineData("a.jpg")]
    [InlineData("a.jpeg")]
    [InlineData("a.TIF")]
    [InlineData("a.tiff")]
    [InlineData("a.JPG")]
    [InlineData("a.png")]
    [InlineData("a.Arw")]
    [InlineData("a.cr3")]
    [InlineData("a.NEF")]
    public void Classify_SupportedImage_ReturnsImage(string path)
    {
        Assert.Equal(MediaKind.Image, MediaTypeFilter.Classify(path, []));
    }

    [Theory]
    [InlineData("a.mp4")]
    [InlineData("a.MOV")]
    public void Classify_SupportedVideo_ReturnsVideo(string path)
    {
        Assert.Equal(MediaKind.Video, MediaTypeFilter.Classify(path, []));
    }

    [Theory]
    [InlineData("a.xmp")]
    [InlineData("a.txt")]
    [InlineData("noextension")]
    public void Classify_UnsupportedExtension_ReturnsNull(string path)
    {
        Assert.Null(MediaTypeFilter.Classify(path, []));
    }

    [Theory]
    [InlineData(".png")]
    [InlineData("png")]
    [InlineData(".PNG")]
    public void Classify_ExcludedExtension_OverridesSupported(string excluded)
    {
        Assert.Null(MediaTypeFilter.Classify("a.png", [excluded]));
    }
}
