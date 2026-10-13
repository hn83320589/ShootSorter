using CommunityToolkit.Mvvm.ComponentModel;
using ShootSorter.Core.Archiving;

namespace ShootSorter.App.ViewModels;

public sealed partial class RuleItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _keyword = string.Empty;

    [ObservableProperty]
    private string _caseName = string.Empty;

    public static RuleItemViewModel From(CaseRule rule) => new() { Keyword = rule.Keyword, CaseName = rule.CaseName };

    public CaseRule ToRule() => new(Keyword.Trim(), CaseName.Trim());
}
