namespace ShootSorter.Core.Tests.TestSupport;

/// <summary>每個測試一個獨立暫存資料夾，Dispose 時刪除。</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ShootSorterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string WriteFile(string fileName, byte[] content)
    {
        var fullPath = System.IO.Path.Combine(Path, fileName);
        File.WriteAllBytes(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
