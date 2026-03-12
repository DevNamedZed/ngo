using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    [GoPackage("crypto/tls")]
    public static class Package
    {
        // TLS version constants
        [GoConst(Type = "uint16")]
        public const long VersionTLS10 = 0x0301;
        [GoConst(Type = "uint16")]
        public const long VersionTLS11 = 0x0302;
        [GoConst(Type = "uint16")]
        public const long VersionTLS12 = 0x0303;
        [GoConst(Type = "uint16")]
        public const long VersionTLS13 = 0x0304;
        [GoConst(Type = "uint16")]
        public const long VersionSSL30 = 0x0300;

        // Common cipher suite constants
        [GoConst(Type = "uint16")]
        public const long TLS_RSA_WITH_AES_128_CBC_SHA = 0x002F;
        [GoConst(Type = "uint16")]
        public const long TLS_RSA_WITH_AES_256_CBC_SHA = 0x0035;
        [GoConst(Type = "uint16")]
        public const long TLS_RSA_WITH_AES_128_GCM_SHA256 = 0x009C;
        [GoConst(Type = "uint16")]
        public const long TLS_RSA_WITH_AES_256_GCM_SHA384 = 0x009D;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 = 0xC02F;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384 = 0xC030;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 = 0xC02B;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384 = 0xC02C;
        [GoConst(Type = "uint16")]
        public const long TLS_AES_128_GCM_SHA256 = 0x1301;
        [GoConst(Type = "uint16")]
        public const long TLS_AES_256_GCM_SHA384 = 0x1302;
        [GoConst(Type = "uint16")]
        public const long TLS_CHACHA20_POLY1305_SHA256 = 0x1303;

        // Client auth type constants
        [GoConst(Type = "tls.ClientAuthType")]
        public const long NoClientCert = 0;
        [GoConst(Type = "tls.ClientAuthType")]
        public const long RequestClientCert = 1;
        [GoConst(Type = "tls.ClientAuthType")]
        public const long RequireAnyClientCert = 2;
        [GoConst(Type = "tls.ClientAuthType")]
        public const long VerifyClientCertIfGiven = 3;
        [GoConst(Type = "tls.ClientAuthType")]
        public const long RequireAndVerifyClientCert = 4;

        // tls.Dial(network, addr string, config *Config) (*Conn, error)
        [GoFunc]
        [return: GoReturn("*tls.Conn", "error")]
        public static (object?, object?) Dial(string network, string addr, [GoParam("*tls.Config")] GoConfig? config)
            => (null, null);

        // tls.X509KeyPair(certPEMBlock, keyPEMBlock []byte) (Certificate, error)
        [GoFunc]
        [return: GoReturn("tls.Certificate", "error")]
        public static (GoCertificate, object?) X509KeyPair(Slice<byte> certPEMBlock, Slice<byte> keyPEMBlock)
            => (new GoCertificate(), null);

        // tls.LoadX509KeyPair(certFile, keyFile string) (Certificate, error)
        [GoFunc]
        [return: GoReturn("tls.Certificate", "error")]
        public static (GoCertificate, object?) LoadX509KeyPair(string certFile, string keyFile)
            => (new GoCertificate(), null);

        // tls.Listen(network, laddr string, config *Config) (net.Listener, error)
        [GoFunc]
        [return: GoReturn("net.Listener", "error")]
        public static (object?, object?) Listen(string network, string laddr, [GoParam("*tls.Config")] GoConfig? config)
            => (null, null);

        // tls.Client(conn net.Conn, config *Config) *Conn
        [GoFunc]
        [return: GoReturn("*tls.Conn")]
        public static object? Client([GoParam("net.Conn")] object? conn, [GoParam("*tls.Config")] GoConfig? config)
            => null;

        // tls.Server(conn net.Conn, config *Config) *Conn
        [GoFunc]
        [return: GoReturn("*tls.Conn")]
        public static object? Server([GoParam("net.Conn")] object? conn, [GoParam("*tls.Config")] GoConfig? config)
            => null;

        // tls.NewListener(inner net.Listener, config *Config) net.Listener
        [GoFunc]
        [return: GoReturn("net.Listener")]
        public static object? NewListener([GoParam("net.Listener")] object? inner, [GoParam("*tls.Config")] GoConfig? config)
            => inner;
    }
}
