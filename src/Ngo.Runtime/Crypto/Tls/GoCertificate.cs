using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    // tls.Certificate struct
    [GoType("struct", Name = "Certificate", Package = "crypto/tls")]
    public class GoCertificate
    {
        [GoField(Name = "Certificate")] public Slice<Slice<byte>> Certificate_;
        [GoField(Name = "PrivateKey")] public object? PrivateKey; // crypto.PrivateKey
        [GoField(Name = "Leaf")] public object? Leaf; // *x509.Certificate
    }
}
