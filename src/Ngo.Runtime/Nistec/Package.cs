using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Nistec
{
    [GoPackage("crypto/internal/nistec")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*P224Point")]
        public static P224Point NewP224Point() => new P224Point();

        [GoFunc]
        [return: GoReturn("*P256Point")]
        public static P256Point NewP256Point() => new P256Point();

        [GoFunc]
        [return: GoReturn("*P384Point")]
        public static P384Point NewP384Point() => new P384Point();

        [GoFunc]
        [return: GoReturn("*P521Point")]
        public static P521Point NewP521Point() => new P521Point();

        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) P256OrdInverse(Slice<byte> k)
        {
            return (k, null);
        }
    }
}
