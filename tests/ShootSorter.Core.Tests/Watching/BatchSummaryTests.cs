using ShootSorter.Core.Watching;

namespace ShootSorter.Core.Tests.Watching;

public class BatchSummaryTests
{
    [Fact]
    public void Describe_IncludesAllThreeCounts()
    {
        var text = new BatchSummary(Succeeded: 212, Skipped: 3, Failed: 1).Describe();

        Assert.Equal("已歸檔 212 筆、略過 3 筆、失敗 1 筆", text);
    }

    [Fact]
    public void Describe_ZeroCounts_StillListed()
    {
        Assert.Equal("已歸檔 0 筆、略過 2 筆、失敗 0 筆", new BatchSummary(0, 2, 0).Describe());
    }
}
