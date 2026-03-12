using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Poll
{
    [GoType("struct", Name = "FD", Package = "internal/poll")]
    public class GoFD
    {
        [GoField] public long Sysfd;
        [GoField] public bool IsStream;
        [GoField] public bool ZeroReadIsEOF;
        [GoField] public bool isFile;

        // Methods needed by os package
        [GoMethod]
        [return: GoReturn("error")]
        public object? Init(string net, bool pollable) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> p) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Pread(Slice<byte> b, long off) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> p) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Pwrite(Slice<byte> b, long off) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadDirent(Slice<byte> buf) => (0, null);

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) Seek(long offset, [GoParam("int")] long whence) => (0, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fchmod([GoParam("uint32")] long mode) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fchown([GoParam("int")] long uid, [GoParam("int")] long gid) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Ftruncate(long size) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fsync() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fchdir() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fstat(object? s) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetDeadline([GoParam("time.Time")] object t) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetReadDeadline([GoParam("time.Time")] object t) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetWriteDeadline([GoParam("time.Time")] object t) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetBlocking() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? RawControl(object? f) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? RawRead(object? f) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? RawWrite(object? f) => null;

        // Methods needed by net package
        [GoMethod]
        [return: GoReturn("error")]
        public object? Shutdown([GoParam("int")] long how) => null;

        [GoMethod]
        [return: GoReturn("int", "syscall.Sockaddr", "error")]
        public (long, object?, object?) ReadFrom(Slice<byte> p) => (0, null, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadFromInet4(Slice<byte> p, object? sa) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadFromInet6(Slice<byte> p, object? sa) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "int", "int", "syscall.Sockaddr", "error")]
        public (long, long, long, object?, object?) ReadMsg(Slice<byte> p, Slice<byte> oob, [GoParam("int")] long flags) => (0, 0, 0, null, null);

        [GoMethod]
        [return: GoReturn("int", "int", "int", "error")]
        public (long, long, long, object?) ReadMsgInet4(Slice<byte> p, Slice<byte> oob, [GoParam("int")] long flags, object? sa) => (0, 0, 0, null);

        [GoMethod]
        [return: GoReturn("int", "int", "int", "error")]
        public (long, long, long, object?) ReadMsgInet6(Slice<byte> p, Slice<byte> oob, [GoParam("int")] long flags, object? sa) => (0, 0, 0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteTo(Slice<byte> p, object? sa) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteToInet4(Slice<byte> p, object? sa) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteToInet6(Slice<byte> p, object? sa) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "int", "error")]
        public (long, long, object?) WriteMsg(Slice<byte> p, Slice<byte> oob, object? sa) => (0, 0, null);

        [GoMethod]
        [return: GoReturn("int", "int", "error")]
        public (long, long, object?) WriteMsgInet4(Slice<byte> p, Slice<byte> oob, object? sa) => (0, 0, null);

        [GoMethod]
        [return: GoReturn("int", "int", "error")]
        public (long, long, object?) WriteMsgInet6(Slice<byte> p, Slice<byte> oob, object? sa) => (0, 0, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptInt([GoParam("int")] long level, [GoParam("int")] long name, [GoParam("int")] long value) => null;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) GetsockoptInt([GoParam("int")] long level, [GoParam("int")] long name) => (0, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptIPMreq([GoParam("int")] long level, [GoParam("int")] long name, object? mreq) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptIPMreqn([GoParam("int")] long level, [GoParam("int")] long name, object? mreq) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptIPv6Mreq([GoParam("int")] long level, [GoParam("int")] long name, object? mreq) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptLinger([GoParam("int")] long level, [GoParam("int")] long name, object? l) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? WaitWrite() => null;

        [GoMethod]
        [return: GoReturn("int", "syscall.Sockaddr", "string", "error")]
        public (long, object?, string, object?) Accept() => (0, null, "", null);

        [GoMethod]
        [return: GoReturn("int", "string", "error")]
        public (long, string, object?) Dup() => (0, "", null);

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) Writev(object? v) => (0, null);

        // Embedded SysFile
        [GoField] public GoSysFile SysFile;
    }
}
