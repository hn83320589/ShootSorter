using System.Text.Encodings.Web;
using System.Text.Json;

namespace ShootSorter.Core.Settings;

public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // 讓中文路徑與案件名稱在設定檔中保持可讀，方便使用者手動檢查。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string FilePath => filePath;

    /// <summary>
    /// 讀取設定。檔案不存在或內容損毀時回傳預設值與對應狀態，不拋例外；
    /// 損毀的檔案會改名為 .bak 保留，避免下次存檔時覆蓋掉使用者的資料。
    /// </summary>
    public SettingsLoadResult Load()
    {
        if (!File.Exists(filePath))
        {
            return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.NotFound);
        }

        string? error;
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), JsonOptions);
            if (settings is not null)
            {
                return new SettingsLoadResult(Normalise(settings), SettingsLoadStatus.Loaded);
            }

            error = "設定檔內容為 null。";
        }
        catch (JsonException ex)
        {
            error = ex.Message;
        }

        var backupPath = $"{filePath}.{DateTime.Now:yyyyMMdd-HHmmss-fff}.bak";
        File.Move(filePath, backupPath);
        return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.Corrupt, backupPath, error);
    }

    /// <summary>先寫暫存檔再取代，寫到一半當機也不會留下損毀的設定檔。</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tempPath, filePath, overwrite: true);
    }

    // JSON 中明確寫 null 時，System.Text.Json 會略過屬性預設值，這裡補回空集合。
    private static AppSettings Normalise(AppSettings settings)
    {
        return settings with
        {
            InboxPath = settings.InboxPath ?? string.Empty,
            ArchiveRoot = settings.ArchiveRoot ?? string.Empty,
            Rules = settings.Rules ?? [],
            ExcludedExtensions = settings.ExcludedExtensions ?? [],
        };
    }
}
