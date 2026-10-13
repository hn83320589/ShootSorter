namespace ShootSorter.Core.Archiving;

public static class MediaTypeFilter
{
    private static readonly HashSet<string> ImageExtensions =
        new([".jpg", ".png", ".arw", ".cr3", ".nef"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> VideoExtensions =
        new([".mp4", ".mov"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 判斷檔案類型。排除清單優先於支援清單；不支援或被排除時回傳 null，呼叫端應直接忽略該檔案。
    /// 排除清單的項目可帶或不帶前導句點。
    /// </summary>
    public static MediaKind? Classify(string path, IReadOnlyCollection<string> excludedExtensions)
    {
        var extension = Path.GetExtension(path);
        if (extension.Length == 0 || IsExcluded(extension, excludedExtensions))
        {
            return null;
        }

        if (ImageExtensions.Contains(extension))
        {
            return MediaKind.Image;
        }

        return VideoExtensions.Contains(extension) ? MediaKind.Video : null;
    }

    private static bool IsExcluded(string extension, IReadOnlyCollection<string> excludedExtensions)
    {
        return excludedExtensions.Any(excluded =>
            string.Equals(extension.TrimStart('.'), excluded.Trim().TrimStart('.'), StringComparison.OrdinalIgnoreCase));
    }
}
