namespace ShootSorter.Core.Settings;

public enum SettingsLoadStatus
{
    Loaded,
    NotFound,
    Corrupt,

    /// <summary>檔案存在但讀不到（被其他程式鎖住、權限不足）。不改名也不覆寫，原檔保持原狀。</summary>
    Unreadable,
}

/// <param name="BackupPath">設定檔損毀時，原檔被改名保留的位置。</param>
/// <param name="Error">損毀或無法讀取的原因，供畫面提示。</param>
public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadStatus Status,
    string? BackupPath = null,
    string? Error = null);
