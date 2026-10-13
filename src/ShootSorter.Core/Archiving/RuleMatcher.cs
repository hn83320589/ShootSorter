namespace ShootSorter.Core.Archiving;

public static class RuleMatcher
{
    /// <summary>依清單順序比對，回傳第一個符合的規則；都不符合時回傳 null。</summary>
    public static CaseRule? Match(string fileName, IReadOnlyList<CaseRule> rules)
    {
        return rules.FirstOrDefault(rule =>
            !string.IsNullOrWhiteSpace(rule.Keyword)
            && fileName.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase));
    }
}
