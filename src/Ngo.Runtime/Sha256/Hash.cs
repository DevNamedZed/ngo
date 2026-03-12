using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sha256
{
    [GoType("struct", Name = "Hash", Package = "crypto/sha256")]
    public class Hash
    {
        private readonly IncrementalHash _hash;

        public Hash()
        {
            _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        }

        public (long, object?) Write(Slice<byte> p)
        {
            var arr = p.AsReadOnlySpan().ToArray();
            _hash.AppendData(arr);
            return ((long)arr.Length, null);
        }

        public Slice<byte> Sum(Slice<byte> b)
        {
            // Sum appends the current hash to b (does not reset)
            var hash = _hash.GetCurrentHash();
            var result = new byte[b.Len + hash.Length];
            for (int i = 0; i < b.Len; i++)
                result[i] = b[i];
            Array.Copy(hash, 0, result, (int)b.Len, hash.Length);
            return new Slice<byte>(result);
        }

        public void Reset()
        {
            // IncrementalHash doesn't support Reset, create new
        }

        public long Size()
        {
            return 32;
        }

        public long BlockSize()
        {
            return 64;
        }
    }
}
