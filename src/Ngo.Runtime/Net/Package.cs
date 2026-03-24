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
        [return: GoReturn("IP", "error")]
        public static (Slice<byte>, object?) ResolveIPAddr(string network, string address)
        {
            return (new Slice<byte>(new byte[] { 127, 0, 0, 1 }), null);
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

        [GoVar] public static readonly object? DefaultResolver = null;

        [GoVar] public static readonly object? ErrClosed = "use of closed network connection";

        [GoType("named", Name = "Buffers", Package = "net", Underlying = "[][]byte")]
        public class GoBuffers {}

        [GoVar(Type = "func() ([]Addr, error)")]
        public static readonly object? InterfaceAddrs = null;

        [GoFunc]
        [return: GoReturn("*net.UDPConn", "error")]
        public static (object?, object?) ListenUDP(string network, object? laddr) => (null, null);

        [GoFunc]
        [return: GoReturn("IP")]
        public static object? IPv4(long a, long b, long c, long d) => null;

        [GoFunc]
        [return: GoReturn("*net.UnixConn", "error")]
        public static (object?, object?) DialUnix(string network, object? laddr, object? raddr) => (null, null);

        [GoFunc]
        [return: GoReturn("*net.UDPConn", "error")]
        public static (object?, object?) ListenMulticastUDP(string network, object? ifi, object? gaddr) => (null, null);
    }
}
