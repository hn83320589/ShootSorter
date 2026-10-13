namespace ShootSorter.Core.Archiving;

/// <summary>每次處理時的設定快照，讓執行中修改設定能立即生效。</summary>
public sealed record ArchiveOptions(
    string ArchiveRoot,
    IReadOnlyList<CaseRule> Rules,
    IReadOnlyList<string> ExcludedExtensions);
