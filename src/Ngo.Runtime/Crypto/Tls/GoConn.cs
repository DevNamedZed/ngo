using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    // tls.Conn struct
    [GoType("struct", Name = "Conn", Package = "crypto/tls")]
    public class GoConn
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> b) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> b) => (0, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Handshake() => null;

        [GoMethod]
        [return: GoReturn("tls.ConnectionState")]
        public GoConnectionState ConnectionState() => new GoConnectionState();

        [GoMethod]
        [return: GoReturn("error")]
        public object? HandshakeContext([GoParam("context.Context")] object? ctx) => null;

        [GoMethod]
        [return: GoReturn("net.Conn")]
        public object? NetConn() => null;

        [GoMethod]
        [return: GoReturn("net.Addr")]
        public object? RemoteAddr() => null;

        [GoMethod]
        [return: GoReturn("net.Addr")]
        public object? LocalAddr() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetDeadline([GoParam("time.Time")] object? t) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetReadDeadline([GoParam("time.Time")] object? t) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetWriteDeadline([GoParam("time.Time")] object? t) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? CloseWrite() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? VerifyHostname(string host) => null;
    }
}
