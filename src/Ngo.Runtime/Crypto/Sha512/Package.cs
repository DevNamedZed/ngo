using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Sha512
{
    [GoPackage("crypto/sha512")]
    public static class Package
    {
        // sha512.New() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static object New() => throw new System.NotImplementedException();

        // sha512.New384() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static object New384() => throw new System.NotImplementedException();

        // sha512.Sum512(data []byte) [64]byte
        [GoFunc]
        [return: GoReturn("[64]byte")]
        public static object Sum512(Slice<byte> data) => throw new System.NotImplementedException();

        // sha512.Sum384(data []byte) [48]byte
        [GoFunc]
        [return: GoReturn("[48]byte")]
        public static object Sum384(Slice<byte> data) => throw new System.NotImplementedException();

        // Constants
        [GoConst(Type = "int")]
        public const long Size = 64;

        [GoConst(Type = "int")]
        public const long Size384 = 48;

        [GoConst(Type = "int")]
        public const long BlockSize = 128;

        [GoConst(Type = "int")]
        public const long Size224 = 28;

        [GoConst(Type = "int")]
        public const long Size256 = 32;
    }
}
