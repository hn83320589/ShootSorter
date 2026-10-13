namespace ShootSorter.Core.Watching;

public sealed record BatchSummary(int Succeeded, int Skipped, int Failed)
{
    /// <summary>系統匣氣泡通知的內文，三種數量即使為 0 也列出。</summary>
    public string Describe() => $"已歸檔 {Succeeded} 筆、略過 {Skipped} 筆、失敗 {Failed} 筆";
}
