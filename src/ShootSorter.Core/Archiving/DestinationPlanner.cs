namespace ShootSorter.Core.Archiving;

public static class DestinationPlanner
{
    /// <summary>
    /// 有案件：{root}\{案件}\{yyyy-MM-dd}；無案件：{root}\{yyyy}\{yyyy-MM-dd}。
    /// </summary>
    public static string GetDirectory(string archiveRoot, DateOnly date, string? caseName)
    {
        var dateFolder = date.ToString("yyyy-MM-dd");
        var parent = string.IsNullOrWhiteSpace(caseName) ? date.ToString("yyyy") : caseName;

        return Path.Combine(archiveRoot, parent, dateFolder);
    }
}
