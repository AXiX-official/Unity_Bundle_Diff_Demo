using System.Security.Cryptography;
using UnityAsset.NET;

namespace BundleDiff.Core.Utils;

public enum HashType
{
    SHA256,
    CRC32
}

public static class HashHelper
{
    public static string ComputeHash(ReadOnlySpan<byte> data, HashType hashType = HashType.SHA256)
    {
        return hashType switch
        {
            HashType.SHA256 => ComputeSHA256(data),
            HashType.CRC32 => ComputeCRC32(data),
            _ => throw new ArgumentException($"Unsupported hash type: {hashType}")
        };
    }
    
    private static string ComputeSHA256(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    private static string ComputeCRC32(ReadOnlySpan<byte> data)
    {
        return CRC32.CalculateCRC32(data).ToString();
    }

    public static bool VerifyHash(ReadOnlySpan<byte> data, string expectedHash, HashType hashType = HashType.SHA256)
    {
        var actualHash = ComputeHash(data, hashType);
        return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
