using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Sha512
{
    [GoPackage("crypto/sha512")]
    public static class Package
    {
        [GoConst(Type = "int")]
        public const long Size = 64;

        [GoConst(Type = "int")]
        public const long Size384 = 48;

        [GoConst(Type = "int")]
        public const long Size224 = 28;

        [GoConst(Type = "int")]
        public const long Size256 = 32;

        [GoConst(Type = "int")]
        public const long BlockSize = 128;

        // sha512.New() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static GoSha512Hash New() => new GoSha512Hash(HashAlgorithmName.SHA512, Size);

        // sha512.New384() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static GoSha512Hash New384() => new GoSha512Hash(HashAlgorithmName.SHA384, Size384);

        // sha512.New512_224() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static GoSha512Hash New512_224() => new GoSha512Hash(HashAlgorithmName.SHA512, Size224);

        // sha512.New512_256() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static GoSha512Hash New512_256() => new GoSha512Hash(HashAlgorithmName.SHA512, Size256);

        // sha512.Sum512(data []byte) [64]byte
        [GoFunc]
        [return: GoReturn("[64]byte")]
        public static Slice<byte> Sum512(Slice<byte> data)
        {
            var bytes = SliceToArray(data);
            var hash = SHA512.HashData(bytes);
            return new Slice<byte>(hash);
        }

        // sha512.Sum384(data []byte) [48]byte
        [GoFunc]
        [return: GoReturn("[48]byte")]
        public static Slice<byte> Sum384(Slice<byte> data)
        {
            var bytes = SliceToArray(data);
            var hash = SHA384.HashData(bytes);
            return new Slice<byte>(hash);
        }

        // sha512.Sum512_224(data []byte) [28]byte
        [GoFunc]
        [return: GoReturn("[28]byte")]
        public static Slice<byte> Sum512_224(Slice<byte> data)
        {
            var full = Sum512(data);
            var result = new byte[Size224];
            for (int i = 0; i < Size224 && i < full.Len; i++)
            {
                result[i] = full[i];
            }
            return new Slice<byte>(result);
        }

        // sha512.Sum512_256(data []byte) [32]byte
        [GoFunc]
        [return: GoReturn("[32]byte")]
        public static Slice<byte> Sum512_256(Slice<byte> data)
        {
            var full = Sum512(data);
            var result = new byte[Size256];
            for (int i = 0; i < Size256 && i < full.Len; i++)
            {
                result[i] = full[i];
            }
            return new Slice<byte>(result);
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

    [GoType("struct", Name = "Hash", Package = "crypto/sha512")]
    public class GoSha512Hash
    {
        private IncrementalHash _hash;
        private readonly HashAlgorithmName _algorithm;
        private readonly long _size;

        public GoSha512Hash(HashAlgorithmName algorithm, long size)
        {
            _algorithm = algorithm;
            _size = size;
            _hash = IncrementalHash.CreateHash(algorithm);
        }

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
            int takeLen = (int)System.Math.Min(hash.Length, _size);
            var result = new byte[b.Len + takeLen];
            for (int i = 0; i < b.Len; i++)
            {
                result[i] = b[i];
            }
            Array.Copy(hash, 0, result, b.Len, takeLen);
            return new Slice<byte>(result);
        }

        [GoMethod]
        public void Reset()
        {
            _hash.Dispose();
            _hash = IncrementalHash.CreateHash(_algorithm);
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Size() => _size;

        [GoMethod]
        [return: GoReturn("int")]
        public long BlockSize() => Package.BlockSize;
    }
}
