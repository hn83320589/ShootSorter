namespace ShootSorter.Core.Metadata;

public enum DateSource
{
    Exif,
    FileCreated,
}

/// <param name="MissingExif">圖片讀不到 EXIF、改用建立日期時為 true；影片永遠為 false。</param>
public sealed record CaptureDate(DateTime CapturedAt, DateSource Source, bool MissingExif);
