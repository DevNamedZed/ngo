using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Aes
{
    [GoPackage("crypto/aes")]
    public static class Package
    {
        // aes.BlockSize
        [GoConst(Type = "int")]
        public const long BlockSize = 16;

        // aes.NewCipher(key []byte) (cipher.Block, error)
        [GoFunc]
        [return: GoReturn("cipher.Block", "error")]
        public static (object?, object?) NewCipher(Slice<byte> key) => (null, null);
    }
}
