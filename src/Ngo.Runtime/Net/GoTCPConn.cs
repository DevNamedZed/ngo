using System;
using System.Net;
using System.Net.Sockets;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "TCPConn", Package = "net")]
    public class GoTCPConn : IGoNetConn
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        public GoTCPConn() { }

        public GoTCPConn(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
        }

        public (int, string) Read(Slice<byte> b)
        {
            try
            {
                var arr = new byte[b.Len];
                int n = _stream?.Read(arr, 0, arr.Length) ?? 0;
                for (int i = 0; i < n; i++)
                {
                    b[i] = arr[i];
                }
                if (n == 0)
                {
                    return (0, "EOF");
                }
                return (n, null!);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        public (int, string) Write(Slice<byte> b)
        {
            try
            {
                var arr = new byte[b.Len];
                for (int i = 0; i < b.Len; i++)
                {
                    arr[i] = b[i];
                }
                _stream?.Write(arr, 0, arr.Length);
                return (b.Len, null!);
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
                _stream?.Close();
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
                return new GoTCPAddr { IP = new Slice<byte>(ep.Address.GetAddressBytes()), Port = ep.Port };
            }
            return new GoTCPAddr();
        }

        public IGoNetAddr RemoteAddr()
        {
            if (_client?.Client?.RemoteEndPoint is IPEndPoint ep)
            {
                return new GoTCPAddr { IP = new Slice<byte>(ep.Address.GetAddressBytes()), Port = ep.Port };
            }
            return new GoTCPAddr();
        }

        public string SetDeadline(object t)
        {
            if (_client?.Client != null)
            {
                int timeoutMs = ExtractTimeoutMs(t);
                if (timeoutMs > 0)
                {
                    _client.Client.SendTimeout = timeoutMs;
                    _client.Client.ReceiveTimeout = timeoutMs;
                }
            }
            return null!;
        }

        public string SetReadDeadline(object t)
        {
            if (_client?.Client != null)
            {
                int timeoutMs = ExtractTimeoutMs(t);
                if (timeoutMs > 0)
                {
                    _client.Client.ReceiveTimeout = timeoutMs;
                }
            }
            return null!;
        }

        public string SetWriteDeadline(object t)
        {
            if (_client?.Client != null)
            {
                int timeoutMs = ExtractTimeoutMs(t);
                if (timeoutMs > 0)
                {
                    _client.Client.SendTimeout = timeoutMs;
                }
            }
            return null!;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetKeepAlive(bool keepalive)
        {
            if (_client?.Client != null)
            {
                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, keepalive);
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetKeepAlivePeriod(long d)
        {
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetNoDelay(bool noDelay)
        {
            if (_client != null)
            {
                _client.NoDelay = noDelay;
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetLinger(long sec)
        {
            if (_client?.Client != null)
            {
                _client.Client.LingerState = new LingerOption(sec >= 0, (int)sec);
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) ReadFrom(object? r)
        {
            if (r is IGoReader reader && _stream != null)
            {
                long total = 0;
                var buf = new byte[32768];
                while (true)
                {
                    var slice = new Slice<byte>(buf);
                    var (n, err) = reader.Read(slice);
                    if (n > 0)
                    {
                        _stream.Write(buf, 0, n);
                        total += n;
                    }
                    if (err != null)
                    {
                        break;
                    }
                }
                return (total, null);
            }
            return (0, "net: invalid reader");
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? CloseWrite()
        {
            try
            {
                _client?.Client?.Shutdown(SocketShutdown.Send);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? CloseRead()
        {
            try
            {
                _client?.Client?.Shutdown(SocketShutdown.Receive);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("net.Conn")]
        public object? SyscallConn() => null;

        private static int ExtractTimeoutMs(object t)
        {
            // time.Time represented as GoTimeValue
            if (t is Time.GoTimeValue tv)
            {
                var now = DateTimeOffset.UtcNow;
                var deadline = tv.Value;
                var duration = deadline - now;
                if (duration.TotalMilliseconds > 0)
                {
                    return (int)duration.TotalMilliseconds;
                }
            }
            return 0;
        }
    }
}
