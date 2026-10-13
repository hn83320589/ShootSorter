using ShootSorter.Core.Archiving;
using ShootSorter.Core.Settings;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _file;
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _file = Path.Combine(_temp.Path, "nested", "settings.json");
        _store = new SettingsStore(_file);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_FileMissing_ReturnsDefaultsWithNotFound()
    {
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.NotFound, result.Status);
        Assert.Equivalent(new AppSettings(), result.Settings, strict: true);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsInOrder()
    {
        var settings = new AppSettings
        {
            InboxPath = @"D:\收件匣",
            ArchiveRoot = @"D:\歸檔",
            Rules = [new CaseRule("ACME", "ACME 案"), new CaseRule("SHOE", "鞋款")],
            ExcludedExtensions = [".xmp", "thm"],
        };

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equivalent(settings, result.Settings, strict: true);
        Assert.Contains("收件匣", File.ReadAllText(_file));
    }

    [Fact]
    public void Load_FileUnreadable_ReturnsDefaultsWithoutTouchingFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, "{}");

        SettingsLoadResult result;
        using (new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = _store.Load();
        }

        Assert.Equal(SettingsLoadStatus.Unreadable, result.Status);
        Assert.Equivalent(new AppSettings(), result.Settings, strict: true);
        Assert.Contains(nameof(IOException), result.Error);
        Assert.Null(result.BackupPath);
        Assert.Equal("{}", File.ReadAllText(_file));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Load_CorruptFile_BacksUpAndReturnsDefaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, content);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Corrupt, result.Status);
        Assert.Equivalent(new AppSettings(), result.Settings, strict: true);
        Assert.False(File.Exists(_file));
        Assert.Equal(content, File.ReadAllText(result.BackupPath!));
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Load_NullCollections_NormalisedToEmpty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, """{ "InboxPath": "D:\\in", "Rules": null, "ExcludedExtensions": null }""");

        var result = _store.Load();

        Assert.Equal(@"D:\in", result.Settings.InboxPath);
        Assert.Empty(result.Settings.Rules);
        Assert.Empty(result.Settings.ExcludedExtensions);
    }
}
