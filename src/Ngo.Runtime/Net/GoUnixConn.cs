using System;
using System.Net.Sockets;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "UnixConn", Package = "net")]
    public class GoUnixConn : IGoNetConn
    {
        private Socket? _socket;

        public GoUnixConn() { }

        internal GoUnixConn(Socket socket)
        {
            _socket = socket;
        }

        public (int, string) Read(Slice<byte> b)
        {
            if (_socket == null)
            {
                return (0, "unix: not connected");
            }
            try
            {
                var buf = new byte[b.Len];
                int n = _socket.Receive(buf);
                for (int i = 0; i < n; i++)
                {
                    b[i] = buf[i];
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
            if (_socket == null)
            {
                return (0, "unix: not connected");
            }
            try
            {
                var buf = new byte[b.Len];
                for (int i = 0; i < b.Len; i++)
                {
                    buf[i] = b[i];
                }
                int n = _socket.Send(buf);
                return (n, null!);
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
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public IGoNetAddr LocalAddr()
        {
            if (_socket?.LocalEndPoint is UnixDomainSocketEndPoint ep)
            {
                return new GoUnixAddr { Name = ep.ToString(), Net = "unix" };
            }
            return new GoUnixAddr();
        }

        public IGoNetAddr RemoteAddr()
        {
            if (_socket?.RemoteEndPoint is UnixDomainSocketEndPoint ep)
            {
                return new GoUnixAddr { Name = ep.ToString(), Net = "unix" };
            }
            return new GoUnixAddr();
        }

        public string SetDeadline(object t) => null!;
        public string SetReadDeadline(object t) => null!;
        public string SetWriteDeadline(object t) => null!;

        [GoMethod]
        [return: GoReturn("error")]
        public object? CloseRead()
        {
            try
            {
                _socket?.Shutdown(SocketShutdown.Receive);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? CloseWrite()
        {
            try
            {
                _socket?.Shutdown(SocketShutdown.Send);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Dial a Unix domain socket.
        /// </summary>
        internal static (GoUnixConn?, string?) Dial(string address)
        {
            try
            {
                var endpoint = new UnixDomainSocketEndPoint(address);
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                socket.Connect(endpoint);
                return (new GoUnixConn(socket), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Listen on a Unix domain socket.
        /// </summary>
        internal static (GoUnixListener?, string?) Listen(string address)
        {
            try
            {
                var endpoint = new UnixDomainSocketEndPoint(address);
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                socket.Bind(endpoint);
                socket.Listen(128);
                return (new GoUnixListener(socket, address), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }

    internal class GoUnixListener : IGoNetListener
    {
        private readonly Socket _socket;
        private readonly string _address;

        public GoUnixListener(Socket socket, string address)
        {
            _socket = socket;
            _address = address;
        }

        public (object?, object?) Accept()
        {
            try
            {
                var client = _socket.Accept();
                return (new GoUnixConn(client), null!);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public string Close()
        {
            try
            {
                _socket.Close();
                // Clean up the socket file
                if (System.IO.File.Exists(_address))
                {
                    System.IO.File.Delete(_address);
                }
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public IGoNetAddr Addr()
        {
            return new GoUnixAddr { Name = _address, Net = "unix" };
        }
    }
}
