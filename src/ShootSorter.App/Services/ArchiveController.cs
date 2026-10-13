using System.IO;
using ShootSorter.Core.Archiving;
using ShootSorter.Core.Logging;
using ShootSorter.Core.Metadata;
using ShootSorter.Core.Settings;
using ShootSorter.Core.Watching;

namespace ShootSorter.App.Services;

/// <summary>組裝 Core 元件並管理目前生效的設定；UI 只透過這裡改變監看狀態。</summary>
public sealed class ArchiveController : IAsyncDisposable
{
    private readonly SettingsStore _store;
    private readonly InboxWatcher _watcher;
    private volatile AppSettings _settings;

    public ArchiveController(SettingsStore store, SqliteArchiveLog log, AppSettings initialSettings)
    {
        _store = store;
        _settings = initialSettings;
        Log = log;

        var processor = new ArchiveProcessor(
            new CaptureDateResolver(new MetadataExtractorExifReader()), log, TimeProvider.System);
        Worker = new ArchiveWorker(processor, () => _settings.ToArchiveOptions(), WorkerTimings.Default);
        _watcher = new InboxWatcher(Worker, () => _settings.ToArchiveOptions());
        _watcher.WatchError += (_, ex) => WatchError?.Invoke(this, ex);
        Worker.Start();
    }

    public event EventHandler<Exception>? WatchError;

    public AppSettings Settings => _settings;

    public SqliteArchiveLog Log { get; }

    public ArchiveWorker Worker { get; }

    public string? WatchingPath => _watcher.WatchingPath;

    /// <summary>儲存設定並套用；有任何驗證錯誤就停止監看，並回傳錯誤說明。</summary>
    public IReadOnlyList<string> SaveAndApply(AppSettings settings)
    {
        _store.Save(settings);
        return Apply(settings);
    }

    public IReadOnlyList<string> Apply(AppSettings settings)
    {
        _settings = settings;
        _watcher.Stop();

        var errors = SettingsValidator.Validate(settings);
        if (errors.Count > 0)
        {
            return errors;
        }

        try
        {
            _watcher.Start(settings.InboxPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            // 驗證通過後資料夾才被刪除的極端情況。
            return [ex.Message];
        }

        return [];
    }

    public async ValueTask DisposeAsync()
    {
        _watcher.Dispose();
        await Worker.DisposeAsync();
    }
}
