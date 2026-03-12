using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Md5
{
    [GoPackage("crypto/md5")]
    public static class Package
    {
        [GoConst]
        public static readonly long Size = 16;
        [GoConst]
        public static readonly long BlockSize = 64;

        public static Slice<byte> Sum(Slice<byte> data)
        {
            byte[] bytes;
            if (data.IsNil || data.Len == 0)
                bytes = System.Array.Empty<byte>();
            else
            {
                bytes = new byte[data.Len];
                for (int i = 0; i < data.Len; i++)
                    bytes[i] = data[i];
            }
            var hash = MD5.HashData(bytes);
            return new Slice<byte>(hash);
        }

        [return: GoReturn("hash.Hash")]
        public static object New()
        {
            return MD5.Create();
        }
    }
}
