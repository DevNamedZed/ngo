using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    [GoType("named", Name = "SignatureScheme", Package = "crypto/tls", Underlying = "uint16")]
    public struct GoSignatureScheme
    {
        public long Value;
    }
}
