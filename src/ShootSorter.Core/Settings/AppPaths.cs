namespace ShootSorter.Core.Settings;

/// <summary>資料庫與設定檔一律放在 %LocalAppData%\ShootSorter\。</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShootSorter");

    public static string SettingsFile { get; } = Path.Combine(DataDirectory, "settings.json");

    public static string DatabaseFile { get; } = Path.Combine(DataDirectory, "archive.db");
}
