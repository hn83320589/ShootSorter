using ShootSorter.Core.Hashing;
using ShootSorter.Core.Logging;
using ShootSorter.Core.Metadata;

namespace ShootSorter.Core.Archiving;

/// <summary>
/// 單檔歸檔流程：雜湊 → 判斷重複 → 決定日期 → 比對規則 → 處理同名 → 搬移 → 寫紀錄。
/// 非執行緒安全：重複與同名判斷都是「先查再寫」，呼叫端必須序列化處理。
/// </summary>
public sealed class ArchiveProcessor(CaptureDateResolver dateResolver, SqliteArchiveLog log, TimeProvider time)
{
    public const string SkippedFolderName = "_Skipped";

    /// <summary>
    /// 處理一個檔案並寫入紀錄。被排除或不支援的副檔名回傳 null，不碰檔案也不寫紀錄。
    /// 同一路徑已有失敗紀錄時（失敗的檔案留在收件匣，重新啟動會再被掃到）視為重試，更新那一筆而不新增。
    /// </summary>
    public ArchiveRecord? Process(string sourcePath, ArchiveOptions options)
    {
        if (MediaTypeFilter.Classify(sourcePath, options.ExcludedExtensions) is not { } kind)
        {
            return null;
        }

        if (log.FindFailedBySource(sourcePath) is { } previous)
        {
            return UpdateAttempt(previous, Archive(sourcePath, kind, options));
        }

        var record = Archive(sourcePath, kind, options);
        return record with { Id = log.Add(record) };
    }

    /// <summary>重新處理一筆失敗紀錄，結果覆寫同一列並累加重試次數。</summary>
    public ArchiveRecord Retry(long recordId, ArchiveOptions options)
    {
        var previous = log.Get(recordId)
            ?? throw new InvalidOperationException($"找不到 Id = {recordId} 的歸檔紀錄。");
        if (previous.Status != ArchiveStatus.Failed)
        {
            throw new InvalidOperationException($"紀錄 {recordId} 的狀態為 {previous.Status}，只有失敗的紀錄可以重試。");
        }

        var outcome = MediaTypeFilter.Classify(previous.SourcePath, options.ExcludedExtensions) is { } kind
            ? Archive(previous.SourcePath, kind, options)
            : Failed(previous.SourcePath, sha256: null, "副檔名不支援或已被排除。");

        return UpdateAttempt(previous, outcome);
    }

    private ArchiveRecord UpdateAttempt(ArchiveRecord previous, ArchiveRecord outcome)
    {
        var updated = outcome with { Id = previous.Id, RetryCount = previous.RetryCount + 1 };
        log.Update(updated);
        return updated;
    }

    private ArchiveRecord Archive(string sourcePath, MediaKind kind, ArchiveOptions options)
    {
        if (!File.Exists(sourcePath))
        {
            return Failed(sourcePath, sha256: null, $"原始檔案不存在：{sourcePath}");
        }

        if (!Directory.Exists(options.ArchiveRoot))
        {
            return Failed(sourcePath, sha256: null, $"歸檔根目錄不存在：{options.ArchiveRoot}");
        }

        string? sha256 = null;
        try
        {
            sha256 = FileHasher.ComputeSha256(sourcePath);

            var logged = log.FindSuccessByHash(sha256);
            if (logged?.DestinationPath is { } loggedPath && File.Exists(loggedPath))
            {
                return Skipped(sourcePath, sha256, loggedPath);
            }

            var date = dateResolver.Resolve(sourcePath, kind);
            var caseName = RuleMatcher.Match(Path.GetFileName(sourcePath), options.Rules)?.CaseName;
            var directory = DestinationPlanner.GetDirectory(options.ArchiveRoot, DateOnly.FromDateTime(date.CapturedAt), caseName);
            Directory.CreateDirectory(directory);

            string? destination = null;
            foreach (var candidate in UniqueFileNamer.Candidates(directory, Path.GetFileName(sourcePath)))
            {
                if (!File.Exists(candidate))
                {
                    destination = candidate;
                    break;
                }

                if (FileHasher.ComputeSha256(candidate) == sha256)
                {
                    return Skipped(sourcePath, sha256, candidate);
                }
            }

            if (destination is null)
            {
                return Failed(sourcePath, sha256, $"'{Path.GetFileName(sourcePath)}' 在 '{directory}' 的流水號已用盡。");
            }

            File.Move(sourcePath, destination, overwrite: false);

            return new ArchiveRecord
            {
                SourcePath = sourcePath,
                DestinationPath = destination,
                Sha256 = sha256,
                CapturedAt = date.CapturedAt,
                DateSource = date.Source,
                MissingExif = date.MissingExif,
                CaseName = caseName,
                Status = ArchiveStatus.Success,
                ProcessedAt = time.GetUtcNow(),
            };
        }
        catch (Exception ex)
        {
            // 以單一檔案為錯誤隔離邊界：被佔用、權限不足、或第三方套件解析壞檔時拋出的任何例外，
            // 都記為失敗並保留型別與訊息供畫面顯示與重試，不讓一個壞檔中斷整批處理。
            return Failed(sourcePath, sha256, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private ArchiveRecord Skipped(string sourcePath, string sha256, string existingPath)
    {
        // 移出收件匣第一層，避免重新啟動掃描時重複記錄；監看不遞迴，所以 _Skipped 不會再被處理。
        var skippedDirectory = Path.Combine(Path.GetDirectoryName(sourcePath)!, SkippedFolderName);
        Directory.CreateDirectory(skippedDirectory);
        var movedTo = UniqueFileNamer.Resolve(skippedDirectory, Path.GetFileName(sourcePath), File.Exists);
        File.Move(sourcePath, movedTo, overwrite: false);

        return new ArchiveRecord
        {
            SourcePath = sourcePath,
            DestinationPath = existingPath,
            Sha256 = sha256,
            Status = ArchiveStatus.Skipped,
            Reason = $"內容與已歸檔檔案相同，原檔已移至 {movedTo}",
            ProcessedAt = time.GetUtcNow(),
        };
    }

    private ArchiveRecord Failed(string sourcePath, string? sha256, string reason)
    {
        return new ArchiveRecord
        {
            SourcePath = sourcePath,
            Sha256 = sha256,
            Status = ArchiveStatus.Failed,
            Reason = reason,
            ProcessedAt = time.GetUtcNow(),
        };
    }
}
