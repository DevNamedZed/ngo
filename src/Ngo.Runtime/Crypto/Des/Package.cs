using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Des
{
    [GoPackage("crypto/des")]
    public static class Package
    {
        // des.BlockSize
        [GoConst(Type = "int")]
        public const long BlockSize = 8;

        // des.NewCipher(key []byte) (cipher.Block, error)
        [GoFunc]
        [return: GoReturn("cipher.Block", "error")]
        public static (object?, object?) NewCipher(Slice<byte> key) => (null, null);

        // des.NewTripleDESCipher(key []byte) (cipher.Block, error)
        [GoFunc]
        [return: GoReturn("cipher.Block", "error")]
        public static (object?, object?) NewTripleDESCipher(Slice<byte> key) => (null, null);
    }
}
