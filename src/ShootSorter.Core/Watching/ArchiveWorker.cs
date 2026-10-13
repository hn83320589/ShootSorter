using System.Threading.Channels;
using ShootSorter.Core.Archiving;
using ShootSorter.Core.Logging;

namespace ShootSorter.Core.Watching;

/// <summary>
/// 以單一 consumer 序列化所有歸檔工作（新檔案與重試），因為 <see cref="ArchiveProcessor"/> 不是執行緒安全的。
/// 事件在背景執行緒觸發，UI 需自行切回 UI 執行緒。
/// </summary>
public sealed class ArchiveWorker(ArchiveProcessor processor, Func<ArchiveOptions> optionsProvider, WorkerTimings timings)
    : IAsyncDisposable
{
    private readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private int _disposed;
    private int _succeeded;
    private int _skipped;
    private int _failed;

    public event EventHandler<ArchiveRecord>? RecordProcessed;

    public event EventHandler<BatchSummary>? BatchCompleted;

    /// <summary>非預期錯誤（例如資料庫無法寫入）。單一工作失敗不會停止 worker。</summary>
    public event EventHandler<Exception>? ProcessingError;

    public void Start()
    {
        if (_loop is not null)
        {
            throw new InvalidOperationException("ArchiveWorker 已經啟動。");
        }

        _loop = Task.Run(() => RunAsync(_stopping.Token));
    }

    /// <summary>排入一個檔案；同一路徑尚在佇列中時不重複排入。</summary>
    public void EnqueueFile(string path)
    {
        lock (_pendingPaths)
        {
            if (!_pendingPaths.Add(path))
            {
                return;
            }
        }

        _queue.Writer.TryWrite(new FileItem(path));
    }

    public void EnqueueRetry(long recordId)
    {
        _queue.Writer.TryWrite(new RetryItem(recordId));
    }

    /// <summary>
    /// 停止 worker，可重複呼叫。內部 await 一律不回到呼叫端的 context：
    /// WPF 在 OnExit 會同步等待這個方法，回到 UI 執行緒的 continuation 會造成 deadlock。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _queue.Writer.TryComplete();
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常關閉。
            }
        }

        _stopping.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var reader = _queue.Reader;
        while (true)
        {
            if (reader.TryRead(out var item))
            {
                await HandleAsync(item, cancellationToken);
                continue;
            }

            if (HasPendingSummary())
            {
                var next = reader.WaitToReadAsync(cancellationToken).AsTask();
                var quiet = Task.Delay(timings.BatchQuietPeriod, cancellationToken);
                if (await Task.WhenAny(next, quiet) == quiet)
                {
                    PublishSummary();
                }
            }

            if (!await reader.WaitToReadAsync(cancellationToken))
            {
                return;
            }
        }
    }

    private async Task HandleAsync(WorkItem item, CancellationToken cancellationToken)
    {
        try
        {
            var record = item switch
            {
                FileItem file => await ProcessFileAsync(file.Path, cancellationToken),
                RetryItem retry => processor.Retry(retry.RecordId, optionsProvider()),
                _ => throw new InvalidOperationException($"未知的工作類型：{item.GetType().Name}"),
            };

            if (record is not null)
            {
                Count(record.Status);
                RecordProcessed?.Invoke(this, record);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ProcessingError?.Invoke(this, ex);
        }
    }

    private async Task<ArchiveRecord?> ProcessFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await FileReadiness.WaitUntilReadyAsync(
                path, timings.ReadyPollInterval, timings.ReadyTimeout, cancellationToken);

            // 檔案在等待期間被移走（例如使用者自行處理）就不需要歸檔，也不留紀錄。
            return exists ? processor.Process(path, optionsProvider()) : null;
        }
        finally
        {
            lock (_pendingPaths)
            {
                _pendingPaths.Remove(path);
            }
        }
    }

    private void Count(ArchiveStatus status)
    {
        switch (status)
        {
            case ArchiveStatus.Success: _succeeded++; break;
            case ArchiveStatus.Skipped: _skipped++; break;
            case ArchiveStatus.Failed: _failed++; break;
            default: throw new InvalidOperationException($"未知的歸檔狀態：{status}");
        }
    }

    private bool HasPendingSummary() => _succeeded + _skipped + _failed > 0;

    private void PublishSummary()
    {
        var summary = new BatchSummary(_succeeded, _skipped, _failed);
        _succeeded = _skipped = _failed = 0;
        BatchCompleted?.Invoke(this, summary);
    }

    private abstract record WorkItem;

    private sealed record FileItem(string Path) : WorkItem;

    private sealed record RetryItem(long RecordId) : WorkItem;
}
