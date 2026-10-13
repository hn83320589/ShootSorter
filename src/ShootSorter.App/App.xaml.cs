using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ShootSorter.App.Services;
using ShootSorter.App.ViewModels;
using ShootSorter.Core.Logging;
using ShootSorter.Core.Settings;

namespace ShootSorter.App;

public partial class App : Application
{
    private ArchiveController? _controller;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorReporter.Log(args.Exception);
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                ErrorReporter.Log(ex);
            }
        };

        try
        {
            var log = new SqliteArchiveLog(AppPaths.DatabaseFile);
            log.EnsureCreated();
            var loadResult = new SettingsStore(AppPaths.SettingsFile).Load();

            _controller = new ArchiveController(new SettingsStore(AppPaths.SettingsFile), log, loadResult.Settings);
            _controller.Worker.ProcessingError += (_, ex) => Dispatcher.BeginInvoke(() => ErrorReporter.Show(ex));
            _controller.WatchError += (_, ex) => Dispatcher.BeginInvoke(() => ErrorReporter.Show(ex));

            var window = new MainWindow { DataContext = new MainViewModel(_controller, new FolderPicker(), loadResult) };
            window.Closed += (_, _) => Shutdown();

            _trayIcon = new TrayIcon(window, LoadAppIcon());
            _controller.Worker.BatchCompleted += (_, summary) => Dispatcher.BeginInvoke(() => _trayIcon.ShowBatchSummary(summary));

            window.Show();
        }
        catch (Exception ex)
        {
            // 資料庫或設定資料夾無法存取時無法正常運作，明確告知後結束，而不是閃退。
            ErrorReporter.Log(ex);
            MessageBox.Show(
                $"ShootSorter 無法啟動：{ex.Message}\n\n資料位置：{AppPaths.DataDirectory}\n詳細內容已記錄於：{ErrorReporter.LogFile}",
                "ShootSorter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _controller?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/ShootSorter.ico"))
            ?? throw new InvalidOperationException("找不到內嵌的程式圖示 Assets/ShootSorter.ico。");
        using var stream = resource.Stream;
        return new System.Drawing.Icon(stream);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorReporter.Show(e.Exception);
        e.Handled = true;
    }
}
