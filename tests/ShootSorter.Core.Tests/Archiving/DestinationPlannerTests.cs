using ShootSorter.Core.Archiving;

namespace ShootSorter.Core.Tests.Archiving;

public class DestinationPlannerTests
{
    private const string Root = @"D:\Archive";

    [Fact]
    public void GetDirectory_NoCase_UsesYearThenDate()
    {
        var dir = DestinationPlanner.GetDirectory(Root, new DateOnly(2026, 3, 7), caseName: null);

        Assert.Equal(@"D:\Archive\2026\2026-03-07", dir);
    }

    [Fact]
    public void GetDirectory_WithCase_UsesCaseThenDate()
    {
        var dir = DestinationPlanner.GetDirectory(Root, new DateOnly(2026, 3, 7), caseName: "ACME");

        Assert.Equal(@"D:\Archive\ACME\2026-03-07", dir);
    }
}
