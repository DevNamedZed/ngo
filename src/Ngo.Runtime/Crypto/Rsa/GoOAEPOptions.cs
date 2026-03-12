using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Rsa
{
    // rsa.OAEPOptions struct
    [GoType("struct", Name = "OAEPOptions", Package = "crypto/rsa")]
    public class GoOAEPOptions
    {
        [GoField(Name = "Hash")] public long Hash; // crypto.Hash
        [GoField(Name = "Label")] public Slice<byte> Label;
    }
}
