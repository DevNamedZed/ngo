using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Sha1
{
    [GoPackage("crypto/sha1")]
    public static class Package
    {
        [GoConst(Type = "int")]
        public const long Size = 20;

        [GoConst(Type = "int")]
        public const long BlockSize = 64;

        // sha1.New() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static GoSha1Hash New() => new GoSha1Hash();

        // sha1.Sum(data []byte) [20]byte
        [GoFunc]
        [return: GoReturn("[20]byte")]
        public static Slice<byte> Sum(Slice<byte> data)
        {
            var bytes = SliceToArray(data);
            var hash = SHA1.HashData(bytes);
            return new Slice<byte>(hash);
        }

        internal static byte[] SliceToArray(Slice<byte> s)
        {
            if (s.IsNil || s.Len == 0)
            {
                return Array.Empty<byte>();
            }
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
            {
                arr[i] = s[i];
            }
            return arr;
        }
    }

    [GoType("struct", Name = "Hash", Package = "crypto/sha1")]
    public class GoSha1Hash
    {
        private IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> p)
        {
            var arr = Package.SliceToArray(p);
            _hash.AppendData(arr);
            return (arr.Length, null);
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> Sum(Slice<byte> b)
        {
            var hash = _hash.GetCurrentHash();
            var result = new byte[b.Len + hash.Length];
            for (int i = 0; i < b.Len; i++)
            {
                result[i] = b[i];
            }
            Array.Copy(hash, 0, result, b.Len, hash.Length);
            return new Slice<byte>(result);
        }

        [GoMethod]
        public void Reset()
        {
            _hash.Dispose();
            _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Size() => Package.Size;

        [GoMethod]
        [return: GoReturn("int")]
        public long BlockSize() => Package.BlockSize;
    }
}
