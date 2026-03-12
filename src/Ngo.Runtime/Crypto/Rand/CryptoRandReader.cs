using System.Security.Cryptography;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Crypto.Rand
{
    public sealed class CryptoRandReader : IGoReader
    {
        public (int, string) Read(Slice<byte> p)
        {
            var arr = new byte[p.Len];
            RandomNumberGenerator.Fill(arr);
            for (int i = 0; i < arr.Length; i++)
                p[i] = arr[i];
            return (arr.Length, "");
        }
    }
}
