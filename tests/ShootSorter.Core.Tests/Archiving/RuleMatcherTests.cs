using ShootSorter.Core.Archiving;

namespace ShootSorter.Core.Tests.Archiving;

public class RuleMatcherTests
{
    [Fact]
    public void Match_NoRules_ReturnsNull()
    {
        Assert.Null(RuleMatcher.Match("ACME_001.jpg", []));
    }

    [Fact]
    public void Match_FileNameContainsKeyword_ReturnsRule()
    {
        var rule = new CaseRule("ACME", "ACME");

        Assert.Equal(rule, RuleMatcher.Match("shoot_ACME_001.jpg", [rule]));
    }

    [Fact]
    public void Match_IgnoresCase()
    {
        var rule = new CaseRule("ACME", "ACME");

        Assert.Equal(rule, RuleMatcher.Match("acme_001.jpg", [rule]));
    }

    [Fact]
    public void Match_MultipleRulesMatch_FirstInListWins()
    {
        var first = new CaseRule("ACME", "ACME");
        var second = new CaseRule("001", "Other");

        Assert.Equal(first, RuleMatcher.Match("ACME_001.jpg", [first, second]));
        Assert.Equal(second, RuleMatcher.Match("ACME_001.jpg", [second, first]));
    }

    [Fact]
    public void Match_NoRuleMatches_ReturnsNull()
    {
        Assert.Null(RuleMatcher.Match("DSC_0001.jpg", [new CaseRule("ACME", "ACME")]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Match_BlankKeyword_NeverMatches(string keyword)
    {
        Assert.Null(RuleMatcher.Match("DSC_0001.jpg", [new CaseRule(keyword, "Any")]));
    }
}
