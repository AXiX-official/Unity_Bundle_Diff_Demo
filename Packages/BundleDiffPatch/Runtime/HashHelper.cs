using System;
using System.Security.Cryptography;
using System.Text;
using UnityAsset.NET;

namespace BundleDiffPatch.Runtime
{
    public enum HashType
    {
        SHA256,
        CRC32
    }
    
    public class HashHelper
    {
        public static string ComputeHash(byte[] data, HashType hashType = HashType.SHA256)
        {
            return hashType switch
            {
                HashType.SHA256 => ComputeSHA256(data),
                HashType.CRC32 => ComputeCRC32(data),
                _ => throw new ArgumentException($"Unsupported hash type: {hashType}")
            };
        }
    
        private static string ComputeSHA256(byte[] data)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(data);
            return BytesToHex(hash).ToLowerInvariant();
        }
        
        private static string BytesToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    
        private static string ComputeCRC32(ReadOnlySpan<byte> data)
        {
            return CRC32.CalculateCRC32(data).ToString();
        }

        public static bool VerifyHash(byte[] data, string expectedHash, HashType hashType = HashType.SHA256)
        {
            var actualHash = ComputeHash(data, hashType);
            var verified = actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
            if (!verified)
                throw new InvalidOperationException($"Expected hash {expectedHash} to match hash {actualHash}.");
            return verified;
        }
    }
}