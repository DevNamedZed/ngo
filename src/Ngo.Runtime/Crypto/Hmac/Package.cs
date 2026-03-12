using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Hmac
{
    [GoPackage("crypto/hmac")]
    public static class Package
    {
        // hmac.New(h func() hash.Hash, key []byte) hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static object New([GoParam("func() hash.Hash")] Func<object> h, Slice<byte> key)
            => throw new NotImplementedException();

        // hmac.Equal(mac1, mac2 []byte) bool
        [GoFunc]
        public static bool Equal(Slice<byte> mac1, Slice<byte> mac2) => false;
    }
}
