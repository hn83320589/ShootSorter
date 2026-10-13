using System.Diagnostics;

namespace ShootSorter.Core.Watching;

public static class FileReadiness
{
    /// <summary>
    /// 等到檔案可獨佔開啟（代表複製已結束）。檔案仍被佔用但大小持續成長時會一直等；
    /// 大小停止變動超過 timeout 就不再等，交由處理流程記錄失敗。
    /// </summary>
    /// <returns>檔案已不存在時回傳 false，呼叫端應略過該檔案。</returns>
    public static async Task<bool> WaitUntilReadyAsync(
        string path, TimeSpan pollInterval, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var lastSize = -1L;
        var sinceLastProgress = Stopwatch.StartNew();

        while (true)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            if (CanStopWaiting(path))
            {
                return true;
            }

            var size = new FileInfo(path).Length;
            if (size != lastSize)
            {
                lastSize = size;
                sinceLastProgress.Restart();
            }
            else if (sinceLastProgress.Elapsed > timeout)
            {
                return true;
            }

            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    private static bool CanStopWaiting(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // 權限不足不會因為等待而改變，交給處理流程記錄失敗（含原因），使用者才看得到這個檔案。
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            // 另一個程序仍開著檔案（例如還在複製），這正是要輪詢等待的狀態。
            return false;
        }
    }
}
