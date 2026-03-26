using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    // tls.Certificate struct
    [GoType("struct", Name = "Certificate", Package = "crypto/tls")]
    public class GoCertificate
    {
        [GoField(Name = "Certificate")] public Slice<Slice<byte>> Certificate_;
        [GoField(Name = "PrivateKey")] public object? PrivateKey;
        [GoField(Name = "OCSPStaple")] public Slice<byte> OCSPStaple;
        [GoField(Name = "SignedCertificateTimestamps")] public Slice<Slice<byte>> SignedCertificateTimestamps;
        [GoField(Name = "Leaf")] public object? Leaf;
        [GoField(Name = "SupportedSignatureAlgorithms")] public Slice<long> SupportedSignatureAlgorithms;
    }
}
