namespace ShootSorter.Core.Watching;

/// <param name="ReadyPollInterval">檔案仍被佔用時，重新檢查的間隔。</param>
/// <param name="ReadyTimeout">檔案被佔用且大小不再變動超過此時間，就放棄等待並交給處理流程記為失敗。</param>
/// <param name="BatchQuietPeriod">佇列清空後再等這麼久沒有新項目，才視為一批結束並發出摘要。</param>
public sealed record WorkerTimings(TimeSpan ReadyPollInterval, TimeSpan ReadyTimeout, TimeSpan BatchQuietPeriod)
{
    public static WorkerTimings Default { get; } = new(
        ReadyPollInterval: TimeSpan.FromMilliseconds(500),
        ReadyTimeout: TimeSpan.FromSeconds(30),
        BatchQuietPeriod: TimeSpan.FromSeconds(3));
}
