using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShootSorter.App.Services;
using ShootSorter.Core.Settings;

namespace ShootSorter.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly char[] ExtensionSeparators = [',', ';', ' ', '\t', '\r', '\n'];

    private readonly IFolderPicker _folderPicker;
    private readonly Action<AppSettings> _save;

    [ObservableProperty]
    private string _inboxPath = string.Empty;

    [ObservableProperty]
    private string _archiveRoot = string.Empty;

    [ObservableProperty]
    private string _excludedExtensionsText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveRuleCommand), nameof(MoveRuleUpCommand), nameof(MoveRuleDownCommand))]
    private RuleItemViewModel? _selectedRule;

    public SettingsViewModel(IFolderPicker folderPicker, Action<AppSettings> save)
    {
        _folderPicker = folderPicker;
        _save = save;
    }

    public ObservableCollection<RuleItemViewModel> Rules { get; } = [];

    public void Load(AppSettings settings)
    {
        InboxPath = settings.InboxPath;
        ArchiveRoot = settings.ArchiveRoot;
        ExcludedExtensionsText = string.Join(", ", settings.ExcludedExtensions);
        Rules.Clear();
        foreach (var rule in settings.Rules)
        {
            Rules.Add(RuleItemViewModel.From(rule));
        }
    }

    public AppSettings ToSettings()
    {
        return new AppSettings
        {
            InboxPath = InboxPath.Trim(),
            ArchiveRoot = ArchiveRoot.Trim(),
            Rules = Rules.Select(r => r.ToRule()).ToList(),
            ExcludedExtensions = ExcludedExtensionsText
                .Split(ExtensionSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
        };
    }

    [RelayCommand]
    private void BrowseInbox()
    {
        InboxPath = _folderPicker.PickFolder("選擇收件匣資料夾", InboxPath) ?? InboxPath;
    }

    [RelayCommand]
    private void BrowseArchiveRoot()
    {
        ArchiveRoot = _folderPicker.PickFolder("選擇歸檔根目錄", ArchiveRoot) ?? ArchiveRoot;
    }

    [RelayCommand]
    private void AddRule()
    {
        var rule = new RuleItemViewModel();
        Rules.Add(rule);
        SelectedRule = rule;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedRule))]
    private void RemoveRule()
    {
        Rules.Remove(SelectedRule!);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedRule))]
    private void MoveRuleUp() => MoveSelected(-1);

    [RelayCommand(CanExecute = nameof(HasSelectedRule))]
    private void MoveRuleDown() => MoveSelected(+1);

    [RelayCommand]
    private void Save() => _save(ToSettings());

    private bool HasSelectedRule() => SelectedRule is not null;

    private void MoveSelected(int offset)
    {
        var index = Rules.IndexOf(SelectedRule!);
        var target = index + offset;
        if (target < 0 || target >= Rules.Count)
        {
            return;
        }

        Rules.Move(index, target);
    }
}
