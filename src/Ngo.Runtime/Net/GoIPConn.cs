using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "IPConn", Package = "net")]
    public class GoIPConn : IGoNetConn
    {
        public GoIPConn() { }

        public (long, string) Read(Slice<byte> b)
        {
            return (0, "ip: not connected");
        }

        public (long, string) Write(Slice<byte> b)
        {
            return (0, "ip: not connected");
        }

        public string Close()
        {
            return null!;
        }

        public IGoNetAddr LocalAddr()
        {
            return new GoIPAddr();
        }

        public IGoNetAddr RemoteAddr()
        {
            return new GoIPAddr();
        }

        public string SetDeadline(object t)
        {
            return null!;
        }

        public string SetReadDeadline(object t)
        {
            return null!;
        }

        public string SetWriteDeadline(object t)
        {
            return null!;
        }

        [GoMethod]
        [return: GoReturn("syscall.RawConn", "error")]
        public (object?, object?) SyscallConn()
        {
            return (null, (object?)"net: no underlying socket");
        }

        [GoMethod]
        [return: GoReturn("int", "int", "int", "*net.IPAddr", "error")]
        public (long, long, long, GoIPAddr?, object?) ReadMsgIP(Slice<byte> b, Slice<byte> oob)
        {
            return (0, 0, 0, null, (object?)"not supported");
        }
    }
}
