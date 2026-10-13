using ShootSorter.Core.Metadata;

namespace ShootSorter.Core.Logging;

public sealed record ArchiveRecord
{
    public long Id { get; init; }

    public required string SourcePath { get; init; }

    /// <summary>成功時為歸檔路徑；因內容重複而略過時為已存在的那份檔案。</summary>
    public string? DestinationPath { get; init; }

    public string? Sha256 { get; init; }

    public DateTime? CapturedAt { get; init; }

    public DateSource? DateSource { get; init; }

    public bool MissingExif { get; init; }

    public string? CaseName { get; init; }

    public required ArchiveStatus Status { get; init; }

    /// <summary>失敗原因或略過原因。</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }

    public int RetryCount { get; init; }
}
