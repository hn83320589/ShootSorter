using ShootSorter.Core.Hashing;
using ShootSorter.Core.Tests.TestSupport;

namespace ShootSorter.Core.Tests.Hashing;

public sealed class FileHasherTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ComputeSha256_KnownContent_ReturnsLowercaseHex()
    {
        var path = _temp.WriteFile("abc.bin", "abc"u8.ToArray());

        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            FileHasher.ComputeSha256(path));
    }

    [Fact]
    public void ComputeSha256_SameContentDifferentName_SameHash()
    {
        var a = _temp.WriteFile("a.jpg", [1, 2, 3]);
        var b = _temp.WriteFile("b.jpg", [1, 2, 3]);
        var c = _temp.WriteFile("c.jpg", [1, 2, 4]);

        Assert.Equal(FileHasher.ComputeSha256(a), FileHasher.ComputeSha256(b));
        Assert.NotEqual(FileHasher.ComputeSha256(a), FileHasher.ComputeSha256(c));
    }
}
