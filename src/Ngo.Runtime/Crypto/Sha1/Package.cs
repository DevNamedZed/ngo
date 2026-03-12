using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Sha1
{
    [GoPackage("crypto/sha1")]
    public static class Package
    {
        // sha1.New() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static object New() => throw new System.NotImplementedException();

        // sha1.Sum(data []byte) [20]byte
        [GoFunc]
        [return: GoReturn("[20]byte")]
        public static object Sum(Slice<byte> data) => throw new System.NotImplementedException();

        // Constants
        [GoConst(Type = "int")]
        public const long Size = 20;

        [GoConst(Type = "int")]
        public const long BlockSize = 64;
    }
}
