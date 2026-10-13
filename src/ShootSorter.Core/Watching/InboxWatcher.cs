using ShootSorter.Core.Archiving;

namespace ShootSorter.Core.Watching;

/// <summary>
/// 監看收件匣第一層（不遞迴），把可處理的檔案交給 <see cref="ArchiveWorker"/>。
/// 被排除或不支援的副檔名在收到事件的第一步就丟棄，不做任何偵測或紀錄。
/// </summary>
public sealed class InboxWatcher(ArchiveWorker worker, Func<ArchiveOptions> optionsProvider) : IDisposable
{
    private FileSystemWatcher? _watcher;
    private string? _inboxPath;

    public string? WatchingPath => _inboxPath;

    /// <summary>非預期的監看錯誤；buffer 溢位時會自動重新掃描，不需處理。</summary>
    public event EventHandler<Exception>? WatchError;

    /// <summary>開始監看並排入收件匣中既有的檔案。已在監看時會先停止舊的。</summary>
    public void Start(string inboxPath)
    {
        if (!Directory.Exists(inboxPath))
        {
            throw new DirectoryNotFoundException($"收件匣資料夾不存在：{inboxPath}");
        }

        Stop();

        var watcher = new FileSystemWatcher(inboxPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName,
            InternalBufferSize = 64 * 1024,
        };
        watcher.Created += (_, e) => Offer(e.FullPath);
        watcher.Renamed += (_, e) => Offer(e.FullPath);
        watcher.Error += (_, e) => OnError(e.GetException());

        _watcher = watcher;
        _inboxPath = inboxPath;
        watcher.EnableRaisingEvents = true;

        // 先開始監看再掃描，掃描期間進來的檔案不會漏掉；重複排入由 worker 去除。
        Rescan();
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
        _inboxPath = null;
    }

    public void Dispose() => Stop();

    private void Rescan()
    {
        if (_inboxPath is not { } inbox)
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(inbox))
        {
            Offer(path);
        }
    }

    private void Offer(string path)
    {
        if (MediaTypeFilter.Classify(path, optionsProvider().ExcludedExtensions) is null)
        {
            return;
        }

        worker.EnqueueFile(path);
    }

    private void OnError(Exception exception)
    {
        if (exception is InternalBufferOverflowException)
        {
            // 一次湧入太多檔案導致事件遺失，改以重新掃描補齊。
            Rescan();
            return;
        }

        WatchError?.Invoke(this, exception);
    }
}
