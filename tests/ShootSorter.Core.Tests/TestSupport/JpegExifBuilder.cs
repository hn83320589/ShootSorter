using System.Text;

namespace ShootSorter.Core.Tests.TestSupport;

/// <summary>
/// 組出最小的 JPEG bytes（SOI + APP1 Exif + EOI），用來測試 EXIF 讀取而不必附帶真實照片。
/// </summary>
public static class JpegExifBuilder
{
    private const ushort TagExifIfdPointer = 0x8769;
    private const ushort TagDateTimeOriginal = 0x9003;
    private const ushort TagDateTimeDigitized = 0x9004;
    private const ushort TypeAscii = 2;
    private const ushort TypeLong = 4;
    private const int ExifDateLength = 20; // "yyyy:MM:dd HH:mm:ss\0"

    public static byte[] WithoutExif() => [0xFF, 0xD8, 0xFF, 0xD9];

    public static byte[] WithDates(DateTime? original = null, DateTime? digitized = null)
    {
        var tiff = BuildTiff(DateTags(original, digitized));
        var app1Length = 2 + 6 + tiff.Length;

        using var stream = new MemoryStream();
        stream.Write([0xFF, 0xD8, 0xFF, 0xE1, (byte)(app1Length >> 8), (byte)app1Length]);
        stream.Write("Exif\0\0"u8);
        stream.Write(tiff);
        stream.Write([0xFF, 0xD9]);
        return stream.ToArray();
    }

    /// <summary>EXIF 本身就是 TIFF 結構，直接輸出即為帶 EXIF 的 TIFF 檔（ARW、NEF 等 RAW 也是 TIFF 為基礎）。</summary>
    public static byte[] TiffWithDates(DateTime? original = null, DateTime? digitized = null)
    {
        return BuildTiff(DateTags(original, digitized));
    }

    private static List<(ushort Tag, DateTime Value)> DateTags(DateTime? original, DateTime? digitized)
    {
        var tags = new List<(ushort Tag, DateTime Value)>();
        if (original is { } o)
        {
            tags.Add((TagDateTimeOriginal, o));
        }

        if (digitized is { } d)
        {
            tags.Add((TagDateTimeDigitized, d));
        }

        return tags;
    }

    private static byte[] BuildTiff(List<(ushort Tag, DateTime Value)> tags)
    {
        const int ifd0Offset = 8;
        const int exifIfdOffset = ifd0Offset + 2 + 12 + 4;
        var dataOffset = exifIfdOffset + 2 + (12 * tags.Count) + 4;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream); // little-endian，對應 "II"

        writer.Write("II"u8);
        writer.Write((ushort)42);
        writer.Write((uint)ifd0Offset);

        writer.Write((ushort)1);
        WriteEntry(writer, TagExifIfdPointer, TypeLong, 1, exifIfdOffset);
        writer.Write(0u);

        writer.Write((ushort)tags.Count);
        for (var i = 0; i < tags.Count; i++)
        {
            WriteEntry(writer, tags[i].Tag, TypeAscii, ExifDateLength, (uint)(dataOffset + (i * ExifDateLength)));
        }

        writer.Write(0u);

        foreach (var (_, value) in tags)
        {
            writer.Write(Encoding.ASCII.GetBytes(value.ToString("yyyy:MM:dd HH:mm:ss") + "\0"));
        }

        return stream.ToArray();
    }

    private static void WriteEntry(BinaryWriter writer, ushort tag, ushort type, uint count, uint value)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(value);
    }
}
