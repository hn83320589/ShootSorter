using ShootSorter.Core.Archiving;

namespace ShootSorter.Core.Settings;

public sealed record AppSettings
{
    public string InboxPath { get; init; } = string.Empty;

    public string ArchiveRoot { get; init; } = string.Empty;

    /// <summary>依清單順序比對，第一個符合者生效。</summary>
    public IReadOnlyList<CaseRule> Rules { get; init; } = [];

    /// <summary>遇到就完全不處理的副檔名，可帶或不帶前導句點；優先於支援清單。</summary>
    public IReadOnlyList<string> ExcludedExtensions { get; init; } = [];

    public ArchiveOptions ToArchiveOptions() => new(ArchiveRoot, Rules, ExcludedExtensions);
}
