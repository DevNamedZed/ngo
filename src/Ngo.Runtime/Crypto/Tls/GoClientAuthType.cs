using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    // tls.ClientAuthType named type
    [GoType("named", Name = "ClientAuthType", Package = "crypto/tls", Underlying = "int")]
    public struct GoClientAuthType
    {
        public long Value;
    }
}
