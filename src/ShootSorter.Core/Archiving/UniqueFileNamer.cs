namespace ShootSorter.Core.Archiving;

public static class UniqueFileNamer
{
    private const int MaxSuffix = 999;

    /// <summary>回傳第一個不存在的候選路徑：原檔名，其次 _001、_002…</summary>
    public static string Resolve(string directory, string fileName, Func<string, bool> exists)
    {
        return Candidates(directory, fileName).FirstOrDefault(candidate => !exists(candidate))
            ?? throw new IOException($"'{fileName}' 在 '{directory}' 的流水號已用盡（上限 _{MaxSuffix}）。");
    }

    /// <summary>依序列出候選路徑：原檔名，接著 _001 到 _999。</summary>
    public static IEnumerable<string> Candidates(string directory, string fileName)
    {
        yield return Path.Combine(directory, fileName);

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var i = 1; i <= MaxSuffix; i++)
        {
            yield return Path.Combine(directory, $"{name}_{i:000}{extension}");
        }
    }
}
