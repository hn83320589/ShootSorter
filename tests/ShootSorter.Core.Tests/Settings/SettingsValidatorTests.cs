using ShootSorter.Core.Archiving;
using ShootSorter.Core.Settings;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Settings;

public sealed class SettingsValidatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AppSettings _valid;

    public SettingsValidatorTests()
    {
        _valid = new AppSettings
        {
            InboxPath = Directory.CreateDirectory(Path.Combine(_temp.Path, "inbox")).FullName,
            ArchiveRoot = Directory.CreateDirectory(Path.Combine(_temp.Path, "archive")).FullName,
            Rules = [new CaseRule("ACME", "ACME")],
        };
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Validate_ValidSettings_NoErrors()
    {
        Assert.Empty(SettingsValidator.Validate(_valid));
    }

    [Fact]
    public void Validate_EmptyPaths_ReportsNotConfigured()
    {
        var errors = SettingsValidator.Validate(new AppSettings());

        Assert.Contains(errors, e => e.Contains("尚未設定收件匣"));
        Assert.Contains(errors, e => e.Contains("尚未設定歸檔根目錄"));
    }

    [Fact]
    public void Validate_MissingDirectories_ReportsPaths()
    {
        var inbox = Path.Combine(_temp.Path, "no-inbox");
        var root = Path.Combine(_temp.Path, "no-root");

        var errors = SettingsValidator.Validate(_valid with { InboxPath = inbox, ArchiveRoot = root });

        Assert.Contains(errors, e => e.Contains("收件匣資料夾不存在") && e.Contains(inbox));
        Assert.Contains(errors, e => e.Contains("歸檔根目錄不存在") && e.Contains(root));
    }

    [Fact]
    public void Validate_RootSameAsInboxIgnoringCaseAndTrailingSlash_ReportsOverlap()
    {
        var errors = SettingsValidator.Validate(_valid with { ArchiveRoot = _valid.InboxPath.ToUpperInvariant() + @"\" });

        Assert.Contains(errors, e => e.Contains("不可與收件匣相同"));
    }

    [Fact]
    public void Validate_RootInsideInbox_ReportsOverlap()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_valid.InboxPath, "archive")).FullName;

        var errors = SettingsValidator.Validate(_valid with { ArchiveRoot = nested });

        Assert.Contains(errors, e => e.Contains("不可位於收件匣底下"));
    }

    [Fact]
    public void Validate_InboxInsideRoot_IsAllowed()
    {
        var inbox = Directory.CreateDirectory(Path.Combine(_valid.ArchiveRoot, "_Inbox")).FullName;

        Assert.Empty(SettingsValidator.Validate(_valid with { InboxPath = inbox }));
    }

    [Fact]
    public void Validate_RuleWithBlankKeyword_ReportsRuleNumber()
    {
        var errors = SettingsValidator.Validate(_valid with { Rules = [new CaseRule("ACME", "ACME"), new CaseRule(" ", "X")] });

        Assert.Contains(errors, e => e.Contains("規則 2") && e.Contains("關鍵字"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A:B")]
    [InlineData("A/B")]
    [InlineData("A?")]
    [InlineData("..")]
    [InlineData(" ACME")]
    public void Validate_RuleWithInvalidCaseName_Reports(string caseName)
    {
        var errors = SettingsValidator.Validate(_valid with { Rules = [new CaseRule("ACME", caseName)] });

        Assert.Contains(errors, e => e.Contains("規則 1") && e.Contains("案件資料夾"));
    }

    [Fact]
    public void Validate_BlankExcludedExtension_Reports()
    {
        var errors = SettingsValidator.Validate(_valid with { ExcludedExtensions = [".xmp", " "] });

        Assert.Contains(errors, e => e.Contains("排除副檔名"));
    }
}
