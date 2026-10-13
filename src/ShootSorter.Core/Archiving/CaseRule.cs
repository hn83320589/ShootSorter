namespace ShootSorter.Core.Archiving;

/// <summary>檔名包含 <see cref="Keyword"/>（不分大小寫）時，歸到 <see cref="CaseName"/> 案件資料夾。</summary>
public sealed record CaseRule(string Keyword, string CaseName);
