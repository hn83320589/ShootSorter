using System.IO;
using System.Windows;
using ShootSorter.Core.Settings;

namespace ShootSorter.App.Services;

/// <summary>未預期錯誤的最後防線：寫入 error.log 並告知使用者，不讓程式閃退。</summary>
public static class ErrorReporter
{
    public static string LogFile { get; } = Path.Combine(AppPaths.DataDirectory, "error.log");

    public static void Log(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // 連錯誤紀錄都寫不進去時，仍要讓呼叫端繼續顯示訊息，不可在錯誤處理中再拋例外。
        }
    }

    public static void Show(Exception exception)
    {
        Log(exception);
        MessageBox.Show(
            $"發生未預期的錯誤，程式會繼續執行。\n\n{exception.Message}\n\n詳細內容已記錄於：{LogFile}",
            "ShootSorter",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
