using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    // tls.ConnectionState struct
    [GoType("struct", Name = "ConnectionState", Package = "crypto/tls")]
    public class GoConnectionState
    {
        [GoField(Name = "Version")] public ushort Version;
        [GoField(Name = "HandshakeComplete")] public bool HandshakeComplete;
        [GoField(Name = "ServerName")] public string ServerName = "";
        [GoField(Name = "NegotiatedProtocol")] public string NegotiatedProtocol = "";
        [GoField(Name = "NegotiatedProtocolIsMutual")] public bool NegotiatedProtocolIsMutual;
        [GoField(Name = "CipherSuite")] public ushort CipherSuite;
        [GoField(Name = "PeerCertificates")] public Slice<object> PeerCertificates;
        [GoField(Name = "VerifiedChains")] public Slice<Slice<object>> VerifiedChains;
        [GoField(Name = "OCSPResponse")] public Slice<byte> OCSPResponse;
        [GoField(Name = "TLSUnique")] public Slice<byte> TLSUnique;
        [GoField(Name = "DidResume")] public bool DidResume;

        [GoMethod]
        [return: GoReturn("[]tls.Certificate")]
        public Slice<GoCertificate> ExportKeyingMaterial(string label, Slice<byte> context, long length) => new Slice<GoCertificate>();
    }
}
