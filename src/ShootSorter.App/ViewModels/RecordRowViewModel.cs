using CommunityToolkit.Mvvm.ComponentModel;
using ShootSorter.Core.Logging;

namespace ShootSorter.App.ViewModels;

public sealed partial class RecordRowViewModel(ArchiveRecord record) : ObservableObject
{
    [ObservableProperty]
    private bool _isChecked;

    public ArchiveRecord Record { get; } = record;

    public long Id => Record.Id;

    public ArchiveStatus Status => Record.Status;

    public bool CanRetry => Record.Status == ArchiveStatus.Failed;

    public string StatusText => Record.Status switch
    {
        ArchiveStatus.Success => "成功",
        ArchiveStatus.Skipped => "略過",
        ArchiveStatus.Failed => "失敗",
        _ => Record.Status.ToString(),
    };

    public string ProcessedAt => Record.ProcessedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string CapturedAt => Record.CapturedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

    public string ExifText => Record.MissingExif ? "無 EXIF" : string.Empty;

    public string FileName => System.IO.Path.GetFileName(Record.SourcePath);

    public string SourcePath => Record.SourcePath;

    public string DestinationPath => Record.DestinationPath ?? string.Empty;

    public string CaseName => Record.CaseName ?? string.Empty;

    public string Reason => Record.Reason ?? string.Empty;

    public string Sha256 => Record.Sha256 ?? string.Empty;

    public int RetryCount => Record.RetryCount;
}
