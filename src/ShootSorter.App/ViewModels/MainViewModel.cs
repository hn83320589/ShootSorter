using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ShootSorter.App.Services;
using ShootSorter.Core.Settings;

namespace ShootSorter.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ArchiveController _controller;
    private readonly List<string> _startupNotices = [];

    [ObservableProperty]
    private string _watchStatus = string.Empty;

    [ObservableProperty]
    private bool _hasProblems;

    public MainViewModel(ArchiveController controller, IFolderPicker folderPicker, SettingsLoadResult loadResult)
    {
        _controller = controller;
        Records = new RecordsViewModel(controller);
        Settings = new SettingsViewModel(folderPicker, SaveSettings);
        Settings.Load(loadResult.Settings);

        switch (loadResult.Status)
        {
            case SettingsLoadStatus.NotFound:
                _startupNotices.Add("找不到設定檔，看起來是第一次使用，請先在「設定」設定收件匣與歸檔根目錄後儲存。");
                break;
            case SettingsLoadStatus.Corrupt:
                _startupNotices.Add($"設定檔內容損毀，已改用預設值。原檔已備份至：{loadResult.BackupPath}（{loadResult.Error}）");
                break;
        }

        ShowProblems(controller.Apply(loadResult.Settings));
    }

    public RecordsViewModel Records { get; }

    public SettingsViewModel Settings { get; }

    /// <summary>顯示在視窗上方提示條的問題清單；有問題時不會監看。</summary>
    public ObservableCollection<string> Problems { get; } = [];

    private void SaveSettings(AppSettings settings)
    {
        _startupNotices.Clear();
        ShowProblems(_controller.SaveAndApply(settings));
    }

    private void ShowProblems(IReadOnlyList<string> errors)
    {
        Problems.Clear();
        foreach (var message in _startupNotices.Concat(errors))
        {
            Problems.Add(message);
        }

        HasProblems = Problems.Count > 0;
        WatchStatus = _controller.WatchingPath is { } path
            ? $"監看中：{path}"
            : "未監看：請修正上方問題後儲存設定。";
    }
}
