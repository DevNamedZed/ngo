using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sha256
{
    [GoPackage("crypto/sha256")]
    public static class Package
    {
        [GoConst] public static readonly long Size = 32;
        [GoConst] public static readonly long Size224 = 28;
        [GoConst] public static readonly long BlockSize = 64;

        [GoFunc]
        public static Slice<byte> Sum256(Slice<byte> data)
        {
            byte[] bytes;
            if (data.IsNil || data.Len == 0)
            {
                bytes = System.Array.Empty<byte>();
            }
            else
            {
                bytes = new byte[data.Len];
                for (int i = 0; i < data.Len; i++)
                    bytes[i] = data[i];
            }
            var hash = SHA256.HashData(bytes);
            return new Slice<byte>(hash);
        }

        [GoFunc]
        [return: GoReturn("[28]byte")]
        public static Slice<byte> Sum224(Slice<byte> data)
        {
            var hash = Sum256(data);
            // SHA-224 is the first 28 bytes of a SHA-256 variant
            var result = new byte[28];
            for (int i = 0; i < 28 && i < hash.Len; i++)
                result[i] = hash[i];
            return new Slice<byte>(result);
        }

        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static Hash New()
        {
            return new Hash();
        }

        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static Hash New224()
        {
            return new Hash();
        }
    }
}
