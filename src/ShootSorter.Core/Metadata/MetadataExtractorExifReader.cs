using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ShootSorter.Core.Metadata;

public sealed class MetadataExtractorExifReader : IExifDateReader
{
    public DateTime? ReadCaptureDate(string path)
    {
        IReadOnlyList<MetadataExtractor.Directory> directories;
        try
        {
            directories = ImageMetadataReader.ReadMetadata(path);
        }
        catch (ImageProcessingException)
        {
            // 依需求：套件讀不出來的格式（含部分 RAW）一律視為無 EXIF。
            return null;
        }

        foreach (var exif in directories.OfType<ExifSubIfdDirectory>())
        {
            if (exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original))
            {
                return AsLocal(original);
            }

            if (exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var digitized))
            {
                return AsLocal(digitized);
            }
        }

        return null;
    }

    // EXIF 沒有時區資訊，依需求視為本機時間。
    private static DateTime AsLocal(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Local);
}
