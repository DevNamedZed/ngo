using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Rsa
{
    // rsa.PSSOptions struct
    [GoType("struct", Name = "PSSOptions", Package = "crypto/rsa")]
    public class GoPSSOptions
    {
        [GoField(Name = "SaltLength")] public long SaltLength;
        [GoField(Name = "Hash")] public long Hash; // crypto.Hash

        [GoMethod]
        [return: GoReturn("crypto.Hash")]
        public long HashFunc() => Hash;
    }
}
