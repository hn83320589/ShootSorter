namespace ShootSorter.Core.Logging;

/// <summary>篩選條件；日期為本機日期，依處理時間比對，兩端皆包含。null 代表不限。</summary>
public sealed record ArchiveQuery(ArchiveStatus? Status = null, DateOnly? From = null, DateOnly? To = null);
