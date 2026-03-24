using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    // tls.Config struct
    [GoType("struct", Name = "Config", Package = "crypto/tls")]
    public class GoConfig
    {
        [GoField(Name = "Certificates")] public Slice<GoCertificate> Certificates;
        [GoField(Name = "InsecureSkipVerify")] public bool InsecureSkipVerify;
        [GoField(Name = "MinVersion")] public ushort MinVersion;
        [GoField(Name = "MaxVersion")] public ushort MaxVersion;
        [GoField(Name = "ServerName")] public string ServerName = "";
        [GoField(Name = "RootCAs")] public object? RootCAs; // *x509.CertPool
        [GoField(Name = "ClientCAs")] public object? ClientCAs; // *x509.CertPool
        [GoField(Name = "ClientAuth")] public long ClientAuth; // ClientAuthType
        [GoField(Name = "CipherSuites")] public Slice<ushort> CipherSuites;
        [GoField(Name = "NextProtos")] public Slice<string> NextProtos;
        [GoField(Name = "CurvePreferences")] public Slice<ushort> CurvePreferences;
        [GoField(Name = "PreferServerCipherSuites")] public bool PreferServerCipherSuites;
        [GoField(Name = "GetCertificate", Type = "func(*tls.ClientHelloInfo) (*tls.Certificate, error)")] public object? GetCertificate;
        [GoField(Name = "GetConfigForClient", Type = "func(*tls.ClientHelloInfo) (*tls.Config, error)")] public object? GetConfigForClient;
        [GoField(Name = "VerifyPeerCertificate", Type = "func([][]byte, [][]*x509.Certificate) error")] public object? VerifyPeerCertificate;
        [GoField(Name = "VerifyConnection", Type = "func(tls.ConnectionState) error")] public object? VerifyConnection;
        [GoField(Name = "SessionTicketsDisabled")] public bool SessionTicketsDisabled;
        [GoField(Name = "Renegotiation")] public long Renegotiation;
        [GoField(Name = "KeyLogWriter", Type = "io.Writer")] public object? KeyLogWriter;
        [GoField(Name = "Time", Type = "func() time.Time")] public object? Time;

        [GoMethod]
        [return: GoReturn("*tls.Config")]
        public GoConfig Clone()
        {
            return new GoConfig
            {
                Certificates = Certificates,
                InsecureSkipVerify = InsecureSkipVerify,
                MinVersion = MinVersion,
                MaxVersion = MaxVersion,
                ServerName = ServerName,
                RootCAs = RootCAs,
                ClientCAs = ClientCAs,
                ClientAuth = ClientAuth,
                CipherSuites = CipherSuites,
                NextProtos = NextProtos,
                CurvePreferences = CurvePreferences,
                PreferServerCipherSuites = PreferServerCipherSuites,
                GetCertificate = GetCertificate,
                GetConfigForClient = GetConfigForClient,
                VerifyPeerCertificate = VerifyPeerCertificate,
                VerifyConnection = VerifyConnection,
                SessionTicketsDisabled = SessionTicketsDisabled,
                Renegotiation = Renegotiation,
                KeyLogWriter = KeyLogWriter,
                Time = Time,
            };
        }
    }

    [GoType("struct", Name = "ClientHelloInfo", Package = "crypto/tls")]
    public class GoClientHelloInfo
    {
        [GoField(Name = "Conn", Type = "net.Conn")] public object? Conn;
        [GoField] public string ServerName = "";
        [GoField] public Slice<long> CipherSuites;
        [GoField] public Slice<long> SupportedVersions;
        [GoField] public Slice<string> SupportedProtos;
        [GoField] public Slice<byte> SignatureSchemes;
    }
}
