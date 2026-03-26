using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    [GoType("named", Name = "CurveID", Package = "crypto/tls", Underlying = "uint16")]
    public struct GoCurveID
    {
        public long Value;
    }
}
