using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShootSorter.App.Services;
using ShootSorter.Core.Logging;

namespace ShootSorter.App.ViewModels;

public sealed record StatusFilterOption(string Label, ArchiveStatus? Status);

public sealed partial class RecordsViewModel : ObservableObject
{
    private readonly ArchiveController _controller;
    private readonly DispatcherTimer _refreshDebounce;

    [ObservableProperty]
    private StatusFilterOption _selectedStatus;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _retryMessage = string.Empty;

    /// <summary>必須在 UI 執行緒建立，背景處理完成時才能安全地排程刷新。</summary>
    public RecordsViewModel(ArchiveController controller)
    {
        _controller = controller;
        _selectedStatus = StatusOptions[0];

        // 一批大量檔案會連續觸發事件，合併成一次刷新避免反覆查詢資料庫。
        var dispatcher = Dispatcher.CurrentDispatcher;
        _refreshDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) =>
        {
            _refreshDebounce!.Stop();
            Refresh();
        }, dispatcher);
        _refreshDebounce.Stop();

        controller.Worker.RecordProcessed += (_, _) => dispatcher.BeginInvoke(() =>
        {
            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        });

        Refresh();
    }

    public IReadOnlyList<StatusFilterOption> StatusOptions { get; } =
    [
        new("全部", null),
        new("成功", ArchiveStatus.Success),
        new("略過", ArchiveStatus.Skipped),
        new("失敗", ArchiveStatus.Failed),
    ];

    public ObservableCollection<RecordRowViewModel> Rows { get; } = [];

    [RelayCommand]
    private void Refresh()
    {
        var query = new ArchiveQuery(
            SelectedStatus.Status,
            FromDate is { } from ? DateOnly.FromDateTime(from) : null,
            ToDate is { } to ? DateOnly.FromDateTime(to) : null);

        Rows.Clear();
        foreach (var record in _controller.Log.Query(query))
        {
            Rows.Add(new RecordRowViewModel(record));
        }

        Summary = $"共 {Rows.Count} 筆：成功 {Count(ArchiveStatus.Success)}、略過 {Count(ArchiveStatus.Skipped)}、失敗 {Count(ArchiveStatus.Failed)}";
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SelectedStatus = StatusOptions[0];
        FromDate = null;
        ToDate = null;
    }

    [RelayCommand]
    private void RetrySelected()
    {
        var ids = Rows.Where(r => r.IsChecked && r.CanRetry).Select(r => r.Id).ToList();
        EnqueueRetries(ids, "請先勾選要重試的失敗項目。");
    }

    /// <summary>重試資料庫中所有失敗項目，不受目前篩選條件影響。</summary>
    [RelayCommand]
    private void RetryAllFailed()
    {
        var ids = _controller.Log.Query(new ArchiveQuery(Status: ArchiveStatus.Failed)).Select(r => r.Id).ToList();
        EnqueueRetries(ids, "目前沒有失敗的項目。");
    }

    partial void OnSelectedStatusChanged(StatusFilterOption value) => Refresh();

    partial void OnFromDateChanged(DateTime? value) => Refresh();

    partial void OnToDateChanged(DateTime? value) => Refresh();

    private void EnqueueRetries(IReadOnlyList<long> ids, string emptyMessage)
    {
        if (ids.Count == 0)
        {
            RetryMessage = emptyMessage;
            return;
        }

        foreach (var id in ids)
        {
            _controller.Worker.EnqueueRetry(id);
        }

        RetryMessage = $"已排入 {ids.Count} 筆重試，完成後清單會自動更新。";
    }

    private int Count(ArchiveStatus status) => Rows.Count(r => r.Status == status);
}
