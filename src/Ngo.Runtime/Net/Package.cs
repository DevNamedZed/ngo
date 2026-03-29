using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Net
{
    [GoPackage("net")]
    public static class GoNet
    {
        // --- Functions ---

        [GoFunc]
        [return: GoReturn("Listener", "error")]
        public static (object?, object?) Listen(string network, string address)
        {
            try
            {
                if (network == "unix" || network == "unixpacket")
                {
                    var (listener, err) = GoUnixConn.Listen(address);
                    if (err != null)
                    {
                        return (null, err);
                    }
                    return (listener, null);
                }

                var parts = address.Split(':');
                int port = parts.Length > 1 ? int.Parse(parts[^1]) : 0;
                var listener2 = new TcpListener(IPAddress.Any, port);
                listener2.Start();
                return (new GoTCPListener(listener2), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("Conn", "error")]
        public static (object?, object?) Dial(string network, string address)
        {
            try
            {
                if (network == "unix" || network == "unixpacket")
                {
                    var (conn, err) = GoUnixConn.Dial(address);
                    if (err != null)
                    {
                        return (null, err);
                    }
                    return (conn, null);
                }

                var parts = address.Split(':');
                string host = parts.Length > 1 ? string.Join(":", parts[..^1]) : "localhost";
                int port = parts.Length > 1 ? int.Parse(parts[^1]) : 0;
                var client = new TcpClient();
                client.Connect(host, port);
                return (new GoTCPConn(client), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("Conn", "error")]
        public static (object?, object?) DialTimeout(string network, string address, long timeout)
        {
            return Dial(network, address);
        }

        [GoFunc]
        [return: GoReturn("*net.IPAddr", "error")]
        public static (GoIPAddr?, object?) ResolveIPAddr(string network, string address)
        {
            return (new GoIPAddr { IP = new Slice<byte>(new byte[] { 127, 0, 0, 1 }) }, null);
        }

        [GoFunc]
        [return: GoReturn("*TCPAddr", "error")]
        public static (object?, object?) ResolveTCPAddr(string network, string address)
        {
            return (new GoTCPAddr(), null);
        }

        [GoFunc]
        public static string JoinHostPort(string host, string port)
        {
            if (host.Contains(':'))
                return "[" + host + "]:" + port;
            return host + ":" + port;
        }

        [GoFunc]
        [return: GoReturn("string", "string", "error")]
        public static (string, string, object?) SplitHostPort(string hostport)
        {
            try
            {
                int lastColon = hostport.LastIndexOf(':');
                if (lastColon < 0)
                    return ("", "", "missing port in address");
                string host = hostport[..lastColon];
                string port = hostport[(lastColon + 1)..];
                if (host.StartsWith("[") && host.EndsWith("]"))
                    host = host[1..^1];
                return (host, port, null);
            }
            catch (Exception ex)
            {
                return ("", "", ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("IP")]
        public static Slice<byte> ParseIP(string s)
        {
            if (IPAddress.TryParse(s, out var addr))
                return new Slice<byte>(addr.GetAddressBytes());
            return default;
        }

        [GoFunc]
        [return: GoReturn("Conn", "Conn")]
        public static (object, object) Pipe()
        {
            var (connA, connB) = GoPipeConn.CreatePair();
            return (connA, connB);
        }

        [GoFunc]
        [return: GoReturn("[]string", "error")]
        public static (Slice<string>, object?) LookupHost(string host)
        {
            try
            {
                var entry = Dns.GetHostEntry(host);
                var addrs = new string[entry.AddressList.Length];
                for (int i = 0; i < entry.AddressList.Length; i++)
                    addrs[i] = entry.AddressList[i].ToString();
                return (new Slice<string>(addrs), null);
            }
            catch (Exception ex)
            {
                return (default, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("Listener", "error")]
        public static (object?, object?) FileListener(object? f)
        {
            return (null, "not supported");
        }

        // --- Constants ---

        [GoConst] public static readonly long IPv4len = 4;
        [GoConst] public static readonly long IPv6len = 16;

        [GoVar(Type = "IP")]
        public static readonly Slice<byte> IPv4zero = new Slice<byte>(new byte[] { 0, 0, 0, 0 });

        [GoVar] public static readonly object? IPv6zero = null;

        [GoFunc]
        [return: GoReturn("IPMask")]
        public static Slice<byte> CIDRMask(long ones, long bits)
        {
            return new Slice<byte>(new byte[bits / 8]);
        }

        [GoFunc]
        [return: GoReturn("*UDPConn", "error")]
        public static (object?, object?) DialUDP(string network, object? laddr, object? raddr)
        {
            return (new GoUDPConn(), null);
        }

        [GoFunc]
        [return: GoReturn("IPMask")]
        public static Slice<byte> IPv4Mask(byte a, byte b, byte c, byte d)
        {
            return new Slice<byte>(new byte[] { a, b, c, d });
        }

        [GoFunc]
        [return: GoReturn("[]Interface", "error")]
        public static (Slice<GoInterface>, object?) Interfaces()
        {
            return (new Slice<GoInterface>(Array.Empty<GoInterface>()), null);
        }

        [GoFunc]
        [return: GoReturn("[]IP", "error")]
        public static (Slice<Slice<byte>>, object?) LookupIP(string host)
        {
            try
            {
                var entry = Dns.GetHostEntry(host);
                var ips = new Slice<byte>[entry.AddressList.Length];
                for (int i = 0; i < entry.AddressList.Length; i++)
                    ips[i] = new Slice<byte>(entry.AddressList[i].GetAddressBytes());
                return (new Slice<Slice<byte>>(ips), null);
            }
            catch (Exception ex)
            {
                return (default, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("string", "[]*SRV", "error")]
        public static (string, Slice<GoSRV?>, object?) LookupSRV(string service, string proto, string name)
        {
            return ("", new Slice<GoSRV?>(Array.Empty<GoSRV?>()), null);
        }

        [GoFunc]
        [return: GoReturn("[]*MX", "error")]
        public static (Slice<GoMX?>, object?) LookupMX(string name)
        {
            return (new Slice<GoMX?>(Array.Empty<GoMX?>()), null);
        }

        [GoFunc]
        [return: GoReturn("IP", "*IPNet", "error")]
        public static (Slice<byte>, object?, object?) ParseCIDR(string s)
        {
            return (new Slice<byte>(new byte[] { 0, 0, 0, 0 }), new GoIPNet(), null);
        }

        [GoFunc]
        [return: GoReturn("HardwareAddr", "error")]
        public static (Slice<byte>, object?) ParseMAC(string s)
        {
            return (new Slice<byte>(Array.Empty<byte>()), null);
        }

        [GoFunc]
        [return: GoReturn("*UDPAddr", "error")]
        public static (object?, object?) ResolveUDPAddr(string network, string address)
        {
            return (new GoUDPAddr(), null);
        }

        [GoFunc]
        [return: GoReturn("*UnixAddr", "error")]
        public static (object?, object?) ResolveUnixAddr(string network, string address)
        {
            return (new GoUnixAddr(), null);
        }

        // --- Variables ---

        [GoVar(Type = "*net.Resolver")] public static readonly object? DefaultResolver = new GoResolver();

        [GoVar] public static readonly object? ErrClosed = "use of closed network connection";

        [GoType("named", Name = "Buffers", Package = "net", Underlying = "[][]byte")]
        public class GoBuffers
        {
            public Slice<Slice<byte>> Data;

            [GoMethod]
            [return: GoReturn("int64", "error")]
            public (long, object?) WriteTo(object? writer)
            {
                long total = 0;
                if (writer is Io.IGoWriter goWriter)
                {
                    for (int i = 0; i < Data.Len; i++)
                    {
                        var (bytesWritten, err) = goWriter.Write(Data[i]);
                        total += bytesWritten;
                        if (err != null && err.ToString() != "")
                        {
                            return (total, err);
                        }
                    }
                }
                return (total, null);
            }

            [GoMethod]
            [return: GoReturn("int", "error")]
            public (long, object?) Read(Slice<byte> p)
            {
                int offset = 0;
                for (int i = 0; i < Data.Len && offset < p.Len; i++)
                {
                    var chunk = Data[i];
                    int toCopy = System.Math.Min(chunk.Len, p.Len - offset);
                    for (int j = 0; j < toCopy; j++)
                    {
                        p[offset++] = chunk[j];
                    }
                }
                return (offset, null);
            }
        }

        [GoVar(Type = "func() ([]Addr, error)")]
        public static readonly object? InterfaceAddrs = null;

        [GoFunc]
        [return: GoReturn("*net.UDPConn", "error")]
        public static (object?, object?) ListenUDP(string network, object? laddr) => (null, null);

        [GoFunc]
        [return: GoReturn("IP")]
        public static object? IPv4(long a, long b, long c, long d)
        {
            var ip = new byte[16];
            ip[10] = 0xff;
            ip[11] = 0xff;
            ip[12] = (byte)a;
            ip[13] = (byte)b;
            ip[14] = (byte)c;
            ip[15] = (byte)d;
            return new Slice<byte>(ip);
        }

        [GoFunc]
        [return: GoReturn("*net.UnixConn", "error")]
        public static (object?, object?) DialUnix(string network, object? laddr, object? raddr) => (null, null);

        [GoFunc]
        [return: GoReturn("*net.UDPConn", "error")]
        public static (object?, object?) ListenMulticastUDP(string network, object? ifi, object? gaddr) => (null, null);

        [GoFunc]
        [return: GoReturn("*net.TCPListener", "error")]
        public static (object?, object?) ListenTCP(string network, object? laddr) => (null, null);

        [GoFunc]
        [return: GoReturn("*net.Interface", "error")]
        public static (object?, object?) InterfaceByName(string name) => (null, null);

        [GoFunc]
        [return: GoReturn("*net.Interface", "error")]
        public static (object?, object?) InterfaceByIndex(long index) => (null, null);

        [GoFunc]
        [return: GoReturn("*net.TCPConn", "error")]
        public static (GoTCPConn?, object?) DialTCP(string network, [GoParam("*net.TCPAddr")] GoTCPAddr? localAddr, [GoParam("*net.TCPAddr")] GoTCPAddr? remoteAddr)
        {
            try
            {
                if (remoteAddr == null)
                {
                    return (null, "net: DialTCP: missing remote address");
                }
                var remoteIp = new System.Net.IPAddress(remoteAddr.IP.AsSpan().ToArray());
                var client = new System.Net.Sockets.TcpClient();
                if (localAddr != null)
                {
                    var localIp = new System.Net.IPAddress(localAddr.IP.AsSpan().ToArray());
                    var localEndpoint = new System.Net.IPEndPoint(localIp, (int)localAddr.Port);
                    client.Client.Bind(localEndpoint);
                }
                client.Connect(remoteIp, (int)remoteAddr.Port);
                return (new GoTCPConn(client), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // net.Flags type constants
        [GoConst(Type = "net.Flags")] public static readonly long FlagUp = 1;
        [GoConst(Type = "net.Flags")] public static readonly long FlagBroadcast = 2;
        [GoConst(Type = "net.Flags")] public static readonly long FlagLoopback = 4;
        [GoConst(Type = "net.Flags")] public static readonly long FlagPointToPoint = 8;
        [GoConst(Type = "net.Flags")] public static readonly long FlagMulticast = 16;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) LookupPort(string network, string service)
        {
            if (int.TryParse(service, out int port))
            {
                return (port, null);
            }
            return service.ToLowerInvariant() switch
            {
                "http" => (80, null),
                "https" => (443, null),
                "ftp" => (21, null),
                "ssh" => (22, null),
                "telnet" => (23, null),
                "smtp" => (25, null),
                "dns" => (53, null),
                "pop3" => (110, null),
                "imap" => (143, null),
                "ldap" => (389, null),
                "mysql" => (3306, null),
                "postgresql" => (5432, null),
                "redis" => (6379, null),
                _ => (0, (object?)$"lookup {network}/{service}: unknown port"),
            };
        }
    }

    // net.Flags named type
    [GoType("named", Name = "Flags", Package = "net", Underlying = "uint")]
    public class GoFlags
    {
        public long Value;

        [GoMethod]
        public string String() => Value.ToString();
    }

    // net.AddrError struct
    [GoType("struct", Name = "AddrError", Package = "net")]
    public class GoAddrError
    {
        [GoField(Name = "Err")] public string Err = "";
        [GoField(Name = "Addr")] public string Addr = "";

        [GoMethod]
        [return: GoReturn("string")]
        public string Error() => Err + " " + Addr;
    }

    // net.UnknownNetworkError named type (underlying string)
    [GoType("named", Name = "UnknownNetworkError", Package = "net", Underlying = "string")]
    public class GoUnknownNetworkError
    {
        public string Value = "";

        [GoMethod]
        [return: GoReturn("string")]
        public string Error() => "unknown network " + Value;
    }
}
