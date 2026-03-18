using System;
using System.Net;
using System.Net.Sockets;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "UDPConn", Package = "net")]
    public class GoUDPConn : IGoNetConn, IGoNetPacketConn
    {
        private readonly UdpClient? _client;
        private IPEndPoint? _remoteEndPoint;

        public GoUDPConn() { }

        internal GoUDPConn(UdpClient client)
        {
            _client = client;
        }

        internal GoUDPConn(UdpClient client, IPEndPoint remoteEndPoint)
        {
            _client = client;
            _remoteEndPoint = remoteEndPoint;
        }

        public (int, string) Read(Slice<byte> b)
        {
            if (_client == null)
            {
                return (0, "udp: not connected");
            }
            try
            {
                var ep = _remoteEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
                var data = _client.Receive(ref ep);
                int count = global::System.Math.Min(data.Length, b.Len);
                for (int i = 0; i < count; i++)
                {
                    b[i] = data[i];
                }
                return (count, null!);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        public (int, string) Write(Slice<byte> b)
        {
            if (_client == null)
            {
                return (0, "udp: not connected");
            }
            try
            {
                var buf = new byte[b.Len];
                for (int i = 0; i < b.Len; i++)
                {
                    buf[i] = b[i];
                }
                int sent = _client.Send(buf, buf.Length);
                return (sent, null!);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        public string Close()
        {
            try
            {
                _client?.Close();
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public IGoNetAddr LocalAddr()
        {
            if (_client?.Client?.LocalEndPoint is IPEndPoint ep)
            {
                return new GoUDPAddr { IP = new Slice<byte>(ep.Address.GetAddressBytes()), Port = ep.Port };
            }
            return new GoUDPAddr();
        }

        public IGoNetAddr RemoteAddr()
        {
            if (_remoteEndPoint != null)
            {
                return new GoUDPAddr { IP = new Slice<byte>(_remoteEndPoint.Address.GetAddressBytes()), Port = _remoteEndPoint.Port };
            }
            return new GoUDPAddr();
        }

        public string SetDeadline(object t) => null!;
        public string SetReadDeadline(object t) => null!;
        public string SetWriteDeadline(object t) => null!;

        // PacketConn methods
        [GoMethod]
        [return: GoReturn("int", "net.Addr", "error")]
        public (int, object?, object?) ReadFrom(Slice<byte> b)
        {
            if (_client == null)
            {
                return (0, null, (object?)"udp: not connected");
            }
            try
            {
                var ep = new IPEndPoint(IPAddress.Any, 0);
                var data = _client.Receive(ref ep);
                int count = global::System.Math.Min(data.Length, b.Len);
                for (int i = 0; i < count; i++)
                {
                    b[i] = data[i];
                }
                var addr = new GoUDPAddr { IP = new Slice<byte>(ep.Address.GetAddressBytes()), Port = ep.Port };
                return (count, addr, null);
            }
            catch (Exception ex)
            {
                return (0, null, (object?)ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) WriteTo(Slice<byte> b, object addr)
        {
            if (_client == null)
            {
                return (0, "udp: not connected");
            }
            try
            {
                var buf = new byte[b.Len];
                for (int i = 0; i < b.Len; i++)
                {
                    buf[i] = b[i];
                }

                if (addr is GoUDPAddr udpAddr)
                {
                    var ipBytes = new byte[udpAddr.IP.Len];
                    for (int i = 0; i < udpAddr.IP.Len; i++)
                    {
                        ipBytes[i] = udpAddr.IP[i];
                    }
                    var ep = new IPEndPoint(new IPAddress(ipBytes), (int)udpAddr.Port);
                    int sent = _client.Send(buf, buf.Length, ep);
                    return (sent, null!);
                }

                return (0, "udp: invalid address type");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("int", "net.Addr", "error")]
        public (int, object?, object?) ReadFromUDP(Slice<byte> b) => ReadFrom(b);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) WriteToUDP(Slice<byte> b, [GoParam("*net.UDPAddr")] GoUDPAddr? addr)
        {
            return WriteTo(b, addr);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetReadBuffer(long bytes)
        {
            if (_client?.Client != null)
            {
                _client.Client.ReceiveBufferSize = (int)bytes;
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetWriteBuffer(long bytes)
        {
            if (_client?.Client != null)
            {
                _client.Client.SendBufferSize = (int)bytes;
            }
            return null;
        }
    }
}
