using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "ResponseController", Package = "net/http")]
    public class ResponseController
    {
        private readonly ResponseWriter _rw;

        public ResponseController(ResponseWriter rw) { _rw = rw; }

        [GoMethod]
        [return: GoReturn("net.Conn", "*bufio.ReadWriter", "error")]
        public (object, object, object?) Hijack() => (null!, null!, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetReadDeadline(object deadline) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetWriteDeadline(object deadline) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? EnableFullDuplex() => null;
    }
}
