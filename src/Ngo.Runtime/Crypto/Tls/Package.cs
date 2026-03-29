using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305 = 0xCCA9;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305 = 0xCCA8;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA = 0xc014;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA = 0xc013;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA = 0xc00a;
        [GoConst(Type = "uint16")]
        public const long TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA = 0xc009;

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

        // Renegotiation constants
        [GoConst(Type = "tls.RenegotiationSupport")]
        public const long RenegotiateNever = 0;
        [GoConst(Type = "tls.RenegotiationSupport")]
        public const long RenegotiateOnceAsClient = 1;
        [GoConst(Type = "tls.RenegotiationSupport")]
        public const long RenegotiateFreelyAsClient = 2;

        // CurveID constants
        [GoConst(Type = "tls.CurveID")]
        public const long CurveP256 = 23;
        [GoConst(Type = "tls.CurveID")]
        public const long CurveP384 = 24;
        [GoConst(Type = "tls.CurveID")]
        public const long CurveP521 = 25;
        [GoConst(Type = "tls.CurveID")]
        public const long X25519 = 29;

        // tls.Dial(network, addr string, config *Config) (*Conn, error)
        [GoFunc]
        [return: GoReturn("*tls.Conn", "error")]
        public static (object?, object?) Dial(string network, string addr, [GoParam("*tls.Config")] GoConfig? config)
        {
            try
            {
                // Parse host:port
                string host;
                int port;
                int lastColon = addr.LastIndexOf(':');
                if (lastColon >= 0)
                {
                    host = addr.Substring(0, lastColon);
                    if (!int.TryParse(addr.Substring(lastColon + 1), out port))
                    {
                        port = 443;
                    }
                }
                else
                {
                    host = addr;
                    port = 443;
                }

                string serverName = config?.ServerName ?? host;
                if (string.IsNullOrEmpty(serverName))
                {
                    serverName = host;
                }

                var tcpClient = new TcpClient();
                tcpClient.Connect(host, port);

                bool skipVerify = config?.InsecureSkipVerify ?? false;
                RemoteCertificateValidationCallback? validationCallback = null;
                if (skipVerify)
                {
                    validationCallback = (sender, certificate, chain, errors) => true;
                }

                var sslStream = new SslStream(tcpClient.GetStream(), false, validationCallback);

                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = serverName,
                    EnabledSslProtocols = MapTlsVersion(config),
                };

                sslStream.AuthenticateAsClient(sslOptions);

                var conn = new GoConn(tcpClient, sslStream, serverName);
                return (conn, null);
            }
            catch (Exception ex)
            {
                return (null, "tls: " + ex.Message);
            }
        }

        // tls.DialWithDialer(dialer *net.Dialer, network, addr string, config *Config) (*Conn, error)
        [GoFunc]
        [return: GoReturn("*tls.Conn", "error")]
        public static (object?, object?) DialWithDialer(object? dialer, string network, string addr, [GoParam("*tls.Config")] GoConfig? config)
        {
            return Dial(network, addr, config);
        }

        // tls.X509KeyPair(certPEMBlock, keyPEMBlock []byte) (Certificate, error)
        [GoFunc]
        [return: GoReturn("tls.Certificate", "error")]
        public static (GoCertificate, object?) X509KeyPair(Slice<byte> certPEMBlock, Slice<byte> keyPEMBlock)
        {
            try
            {
                var certBytes = X509.Package.SliceToArray(certPEMBlock);
                var keyBytes = X509.Package.SliceToArray(keyPEMBlock);

                var certPem = System.Text.Encoding.ASCII.GetString(certBytes);
                var keyPem = System.Text.Encoding.ASCII.GetString(keyBytes);

                var x509Cert = X509Certificate2.CreateFromPem(certPem, keyPem);
                var tlsCert = new GoCertificate
                {
                    Certificate_ = new Slice<Slice<byte>>(new[] { new Slice<byte>(x509Cert.RawData) }),
                };

                return (tlsCert, null);
            }
            catch (Exception ex)
            {
                return (new GoCertificate(), "tls: " + ex.Message);
            }
        }

        // tls.LoadX509KeyPair(certFile, keyFile string) (Certificate, error)
        [GoFunc]
        [return: GoReturn("tls.Certificate", "error")]
        public static (GoCertificate, object?) LoadX509KeyPair(string certFile, string keyFile)
        {
            try
            {
                var certPem = File.ReadAllBytes(certFile);
                var keyPem = File.ReadAllBytes(keyFile);
                return X509KeyPair(new Slice<byte>(certPem), new Slice<byte>(keyPem));
            }
            catch (Exception ex)
            {
                return (new GoCertificate(), "tls: " + ex.Message);
            }
        }

        // tls.Listen(network, laddr string, config *Config) (net.Listener, error)
        [GoFunc]
        [return: GoReturn("net.Listener", "error")]
        public static (object?, object?) Listen(string network, string laddr, [GoParam("*tls.Config")] GoConfig? config)
        {
            // For TLS server listeners, delegate to net.Listen — TLS wrapping happens per-connection
            var (listener, err) = Ngo.Runtime.Net.GoNet.Listen(network, laddr);
            if (err != null)
            {
                return (null, err);
            }
            return (listener, null);
        }

        // tls.Client(conn net.Conn, config *Config) *Conn
        [GoFunc]
        [return: GoReturn("*tls.Conn")]
        public static object? Client([GoParam("net.Conn")] object? conn, [GoParam("*tls.Config")] GoConfig? config)
        {
            if (conn is Net.GoTCPConn tcpConn && tcpConn.GetTcpClient() is TcpClient client)
            {
                try
                {
                    var serverName = config?.ServerName ?? "";
                    var sslStream = new SslStream(client.GetStream(), false);
                    sslStream.AuthenticateAsClient(serverName);
                    return new GoConn(client, sslStream, serverName);
                }
                catch (System.Exception)
                {
                    return null;
                }
            }
            return null;
        }

        // tls.Server(conn net.Conn, config *Config) *Conn
        [GoFunc]
        [return: GoReturn("*tls.Conn")]
        public static object? Server([GoParam("net.Conn")] object? conn, [GoParam("*tls.Config")] GoConfig? config)
        {
            // Server-side TLS requires a certificate
            if (conn is Net.GoTCPConn tcpConn && tcpConn.GetTcpClient() is TcpClient client
                && config?.Certificates != null && config.Certificates.Len > 0)
            {
                try
                {
                    var sslStream = new SslStream(client.GetStream(), false);
                    var tlsCert = config.Certificates[0];
                    if (tlsCert.Certificate_.Len > 0)
                    {
                        var certBytes = new byte[tlsCert.Certificate_[0].Len];
                        for (int i = 0; i < certBytes.Length; i++)
                        {
                            certBytes[i] = tlsCert.Certificate_[0][i];
                        }
                        var x509Cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certBytes);
                        sslStream.AuthenticateAsServer(x509Cert);
                        return new GoConn(client, sslStream, "");
                    }
                }
                catch (System.Exception)
                {
                }
            }
            return null;
        }

        // tls.NewListener(inner net.Listener, config *Config) net.Listener
        [GoFunc]
        [return: GoReturn("net.Listener")]
        public static object? NewListener([GoParam("net.Listener")] object? inner, [GoParam("*tls.Config")] GoConfig? config)
            => inner;

        // tls.CipherSuiteName(id uint16) string
        [GoFunc]
        public static string CipherSuiteName(long id)
        {
            return id switch
            {
                0x002F => "TLS_RSA_WITH_AES_128_CBC_SHA",
                0x0035 => "TLS_RSA_WITH_AES_256_CBC_SHA",
                0x009C => "TLS_RSA_WITH_AES_128_GCM_SHA256",
                0x009D => "TLS_RSA_WITH_AES_256_GCM_SHA384",
                0xC02F => "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256",
                0xC030 => "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
                0xC02B => "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256",
                0xC02C => "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384",
                0x1301 => "TLS_AES_128_GCM_SHA256",
                0x1302 => "TLS_AES_256_GCM_SHA384",
                0x1303 => "TLS_CHACHA20_POLY1305_SHA256",
                _ => $"0x{id:X4}",
            };
        }

        // tls.CipherSuites() []*CipherSuite
        [GoFunc]
        [return: GoReturn("[]*tls.CipherSuite")]
        public static Slice<GoCipherSuite?> CipherSuites()
        {
            return new Slice<GoCipherSuite?>(Array.Empty<GoCipherSuite?>());
        }

        // tls.InsecureCipherSuites() []*CipherSuite
        [GoFunc]
        [return: GoReturn("[]*tls.CipherSuite")]
        public static Slice<GoCipherSuite?> InsecureCipherSuites()
        {
            return new Slice<GoCipherSuite?>(Array.Empty<GoCipherSuite?>());
        }

        private static SslProtocols MapTlsVersion(GoConfig? config)
        {
            if (config == null)
            {
                return SslProtocols.None; // Let OS decide
            }

            SslProtocols protocols = SslProtocols.None;
            if (config.MinVersion > 0 || config.MaxVersion > 0)
            {
                ushort min = config.MinVersion > 0 ? config.MinVersion : (ushort)0x0303;
                ushort max = config.MaxVersion > 0 ? config.MaxVersion : (ushort)0x0304;

                // TLS 1.0 and 1.1 are obsolete in .NET — only support 1.2+
                if (min <= 0x0303 && max >= 0x0303)
                {
                    protocols |= SslProtocols.Tls12;
                }
                if (min <= 0x0304 && max >= 0x0304)
                {
                    protocols |= SslProtocols.Tls13;
                }
            }

            return protocols == 0 ? SslProtocols.None : protocols;
        }
    }

    [GoType("struct", Name = "CipherSuite", Package = "crypto/tls")]
    public class GoCipherSuite
    {
        [GoField(Name = "ID")] public long ID;
        [GoField(Name = "Name")] public string Name = "";
        [GoField(Name = "SupportedVersions")] public Slice<long> SupportedVersions;
        [GoField(Name = "Insecure")] public bool Insecure;
    }
}
