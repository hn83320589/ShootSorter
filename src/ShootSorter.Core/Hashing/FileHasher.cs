using System.Security.Cryptography;

namespace ShootSorter.Core.Hashing;

public static class FileHasher
{
    /// <summary>計算檔案內容的 SHA-256，回傳小寫十六進位字串。</summary>
    public static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1 << 20);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
