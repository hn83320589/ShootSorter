namespace ShootSorter.Core.Settings;

public enum SettingsLoadStatus
{
    Loaded,
    NotFound,
    Corrupt,
}

/// <param name="BackupPath">設定檔損毀時，原檔被改名保留的位置。</param>
/// <param name="Error">損毀原因，供畫面提示。</param>
public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadStatus Status,
    string? BackupPath = null,
    string? Error = null);
