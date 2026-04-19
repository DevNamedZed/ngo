using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    [GoType("named", Name = "RenegotiationSupport", Package = "crypto/tls", Underlying = "int")]
    public struct GoRenegotiationSupport
    {
        public long Value;
    }
}
