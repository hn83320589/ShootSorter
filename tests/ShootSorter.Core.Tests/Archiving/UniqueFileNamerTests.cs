using ShootSorter.Core.Archiving;

namespace ShootSorter.Core.Tests.Archiving;

public class UniqueFileNamerTests
{
    private const string Dir = @"D:\Archive\2026\2026-03-07";

    [Fact]
    public void Resolve_NoConflict_KeepsOriginalName()
    {
        var path = UniqueFileNamer.Resolve(Dir, "IMG_1.jpg", _ => false);

        Assert.Equal(Path.Combine(Dir, "IMG_1.jpg"), path);
    }

    [Fact]
    public void Resolve_NameTaken_Appends001()
    {
        var taken = new HashSet<string> { Path.Combine(Dir, "IMG_1.jpg") };

        var path = UniqueFileNamer.Resolve(Dir, "IMG_1.jpg", taken.Contains);

        Assert.Equal(Path.Combine(Dir, "IMG_1_001.jpg"), path);
    }

    [Fact]
    public void Resolve_001Taken_Appends002()
    {
        var taken = new HashSet<string>
        {
            Path.Combine(Dir, "IMG_1.jpg"),
            Path.Combine(Dir, "IMG_1_001.jpg"),
        };

        var path = UniqueFileNamer.Resolve(Dir, "IMG_1.jpg", taken.Contains);

        Assert.Equal(Path.Combine(Dir, "IMG_1_002.jpg"), path);
    }

    [Fact]
    public void Resolve_CandidatesEnumeratesOriginalThenSuffixes()
    {
        var candidates = UniqueFileNamer.Candidates(Dir, "a.b.CR3").Take(3).ToList();

        Assert.Equal(
            [Path.Combine(Dir, "a.b.CR3"), Path.Combine(Dir, "a.b_001.CR3"), Path.Combine(Dir, "a.b_002.CR3")],
            candidates);
    }
}
