using ShootSorter.Core.Archiving;

namespace ShootSorter.Core.Metadata;

public sealed class CaptureDateResolver(IExifDateReader exifReader)
{
    /// <summary>圖片優先用 EXIF，讀不到改用檔案建立日期；影片一律用檔案建立日期。</summary>
    public CaptureDate Resolve(string path, MediaKind kind)
    {
        if (kind == MediaKind.Image && exifReader.ReadCaptureDate(path) is { } exifDate)
        {
            return new CaptureDate(exifDate, DateSource.Exif, MissingExif: false);
        }

        return new CaptureDate(File.GetCreationTime(path), DateSource.FileCreated, MissingExif: kind == MediaKind.Image);
    }
}
