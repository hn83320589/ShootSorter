namespace ShootSorter.Core.Settings;

public static class SettingsValidator
{
    private static readonly char[] InvalidFolderChars = Path.GetInvalidFileNameChars();

    /// <summary>回傳所有問題的說明；空清單代表可以開始監看。</summary>
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();

        var inboxOk = CheckDirectory(settings.InboxPath, "收件匣資料夾", errors);
        var rootOk = CheckDirectory(settings.ArchiveRoot, "歸檔根目錄", errors);
        if (inboxOk && rootOk)
        {
            CheckOverlap(settings.InboxPath, settings.ArchiveRoot, errors);
        }

        for (var i = 0; i < settings.Rules.Count; i++)
        {
            var rule = settings.Rules[i];
            if (string.IsNullOrWhiteSpace(rule.Keyword))
            {
                errors.Add($"規則 {i + 1}：檔名關鍵字不可空白。");
            }

            if (!IsValidFolderName(rule.CaseName))
            {
                errors.Add($"規則 {i + 1}：案件資料夾名稱「{rule.CaseName}」無效，不可空白、前後不可有空白、不可以句點結尾，也不可包含 \\ / : * ? \" < > |。");
            }
        }

        if (settings.ExcludedExtensions.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("排除副檔名清單中有空白項目。");
        }

        return errors;
    }

    private static bool CheckDirectory(string path, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add($"尚未設定{label}。");
            return false;
        }

        if (!Directory.Exists(path))
        {
            errors.Add($"{label}不存在：{path}");
            return false;
        }

        return true;
    }

    private static void CheckOverlap(string inboxPath, string archiveRoot, List<string> errors)
    {
        var inbox = NormaliseDirectory(inboxPath);
        var root = NormaliseDirectory(archiveRoot);

        if (string.Equals(inbox, root, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("歸檔根目錄不可與收件匣相同。");
        }
        else if (root.StartsWith(inbox + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("歸檔根目錄不可位於收件匣底下。");
        }
    }

    private static string NormaliseDirectory(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsValidFolderName(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name == name.Trim()
            && !name.EndsWith('.')
            && name.IndexOfAny(InvalidFolderChars) < 0;
    }
}
