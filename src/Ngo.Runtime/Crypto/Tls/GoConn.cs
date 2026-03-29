using System;
using System.Net.Security;
using System.Net.Sockets;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    // tls.Conn struct
    [GoType("struct", Name = "Conn", Package = "crypto/tls")]
    public class GoConn
    {
        private TcpClient? _tcpClient;
        private SslStream? _sslStream;
        private string _serverName;

        public GoConn()
        {
            _serverName = "";
        }

        internal GoConn(TcpClient tcpClient, SslStream sslStream, string serverName)
        {
            _tcpClient = tcpClient;
            _sslStream = sslStream;
            _serverName = serverName;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> b)
        {
            if (_sslStream == null)
            {
                return (0, "tls: connection not established");
            }

            try
            {
                var buf = new byte[b.Len];
                int n = _sslStream.Read(buf, 0, buf.Length);
                for (int i = 0; i < n; i++)
                {
                    b[i] = buf[i];
                }
                if (n == 0)
                {
                    return (0, "EOF");
                }
                return (n, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> b)
        {
            if (_sslStream == null)
            {
                return (0, "tls: connection not established");
            }

            try
            {
                var buf = new byte[b.Len];
                for (int i = 0; i < b.Len; i++)
                {
                    buf[i] = b[i];
                }
                _sslStream.Write(buf, 0, buf.Length);
                return (b.Len, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close()
        {
            try
            {
                _sslStream?.Close();
                _tcpClient?.Close();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Handshake()
        {
            // Handshake is done during AuthenticateAsClient
            return null;
        }

        [GoMethod]
        [return: GoReturn("tls.ConnectionState")]
        public GoConnectionState ConnectionState()
        {
            var state = new GoConnectionState();
            if (_sslStream != null)
            {
                state.Version = (ushort)MapSslProtocol(_sslStream.SslProtocol);
                state.HandshakeComplete = _sslStream.IsAuthenticated;
                state.ServerName = _serverName;
                state.NegotiatedProtocol = _sslStream.NegotiatedApplicationProtocol.ToString();
            }
            return state;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? HandshakeContext([GoParam("context.Context")] object? ctx) => Handshake();

        [GoMethod]
        [return: GoReturn("net.Conn")]
        public object? NetConn()
        {
            if (_tcpClient != null)
            {
                return new Net.GoTCPConn(_tcpClient);
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("net.Addr")]
        public object? RemoteAddr()
        {
            if (_tcpClient?.Client?.RemoteEndPoint != null)
            {
                return _tcpClient.Client.RemoteEndPoint.ToString();
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("net.Addr")]
        public object? LocalAddr()
        {
            if (_tcpClient?.Client?.LocalEndPoint != null)
            {
                return _tcpClient.Client.LocalEndPoint.ToString();
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetDeadline([GoParam("time.Time")] object? t)
        {
            SetReadDeadline(t);
            SetWriteDeadline(t);
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetReadDeadline([GoParam("time.Time")] object? t)
        {
            if (_tcpClient?.Client != null && t is Ngo.Runtime.Time.GoTimeValue timeVal)
            {
                var duration = timeVal.Sub(Ngo.Runtime.Time.GoTime.Now());
                _tcpClient.Client.ReceiveTimeout = duration > 0 ? (int)(duration / 1_000_000) : 1;
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetWriteDeadline([GoParam("time.Time")] object? t)
        {
            if (_tcpClient?.Client != null && t is Ngo.Runtime.Time.GoTimeValue timeVal)
            {
                var duration = timeVal.Sub(Ngo.Runtime.Time.GoTime.Now());
                _tcpClient.Client.SendTimeout = duration > 0 ? (int)(duration / 1_000_000) : 1;
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? CloseWrite()
        {
            try
            {
                _tcpClient?.Client?.Shutdown(SocketShutdown.Send);
                return null;
            }
            catch (System.Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? VerifyHostname(string host)
        {
            if (_sslStream == null)
            {
                return "tls: no TLS connection";
            }
            if (!_sslStream.IsAuthenticated)
            {
                return "tls: handshake has not yet been performed";
            }
            if (_sslStream.RemoteCertificate == null)
            {
                return "tls: no peer certificate";
            }
            var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(_sslStream.RemoteCertificate);
            bool match = cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.DnsName, false) == host;
            if (!match)
            {
                return $"x509: certificate is valid for {cert.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.DnsName, false)}, not {host}";
            }
            return null;
        }

        private static long MapSslProtocol(System.Security.Authentication.SslProtocols protocol)
        {
            if (protocol.HasFlag(System.Security.Authentication.SslProtocols.Tls13))
            {
                return Package.VersionTLS13;
            }
            if (protocol.HasFlag(System.Security.Authentication.SslProtocols.Tls12))
            {
                return Package.VersionTLS12;
            }
            return 0;
        }
    }
}
