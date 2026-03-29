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

        [GoMethod]
        [return: GoReturn("error")]
        public object? Init(string net, bool pollable)
        {
            IsStream = (net == "tcp" || net == "tcp4" || net == "tcp6" || net == "unix" || net == "unixpacket");
            ZeroReadIsEOF = IsStream;
            isFile = (net == "file");
            if (pollable && Sysfd >= 0)
            {
                Ngo.Runtime.Syscall.Package.SetNonblock(Sysfd, true);
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close()
        {
            return Ngo.Runtime.Syscall.Package.Close(Sysfd);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> p)
        {
            return Ngo.Runtime.Syscall.Package.Read(Sysfd, p);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Pread(Slice<byte> b, long off)
        {
            var (currentPos, seekErr) = Ngo.Runtime.Syscall.Package.Seek(Sysfd, 0, 1);
            if (seekErr != null)
            {
                return (0, seekErr);
            }
            var (_, seekErr2) = Ngo.Runtime.Syscall.Package.Seek(Sysfd, off, 0);
            if (seekErr2 != null)
            {
                return (0, seekErr2);
            }
            var (bytesRead, readErr) = Ngo.Runtime.Syscall.Package.Read(Sysfd, b);
            Ngo.Runtime.Syscall.Package.Seek(Sysfd, currentPos, 0);
            return (bytesRead, readErr);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> p)
        {
            return Ngo.Runtime.Syscall.Package.Write(Sysfd, p);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Pwrite(Slice<byte> b, long off)
        {
            var (currentPos, seekErr) = Ngo.Runtime.Syscall.Package.Seek(Sysfd, 0, 1);
            if (seekErr != null)
            {
                return (0, seekErr);
            }
            var (_, seekErr2) = Ngo.Runtime.Syscall.Package.Seek(Sysfd, off, 0);
            if (seekErr2 != null)
            {
                return (0, seekErr2);
            }
            var (bytesWritten, writeErr) = Ngo.Runtime.Syscall.Package.Write(Sysfd, b);
            Ngo.Runtime.Syscall.Package.Seek(Sysfd, currentPos, 0);
            return (bytesWritten, writeErr);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadDirent(Slice<byte> buf)
        {
            return Ngo.Runtime.Syscall.Package.Read(Sysfd, buf);
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) Seek(long offset, [GoParam("int")] long whence)
        {
            return Ngo.Runtime.Syscall.Package.Seek(Sysfd, offset, whence);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fchmod([GoParam("uint32")] long mode)
        {
            return Ngo.Runtime.Syscall.Package.Fchmod(Sysfd, mode);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fchown([GoParam("int")] long uid, [GoParam("int")] long gid)
        {
            return Ngo.Runtime.Syscall.Package.Fchown(Sysfd, uid, gid);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Ftruncate(long size)
        {
            return Ngo.Runtime.Syscall.Package.Ftruncate(Sysfd, size);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fsync()
        {
            return Ngo.Runtime.Syscall.LinuxSyscalls.fsync((int)Sysfd) == -1
                ? Ngo.Runtime.Syscall.Package.ErrnoToError("fsync") : null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fchdir()
        {
            return Ngo.Runtime.Syscall.LinuxSyscalls.chdir($"/proc/self/fd/{Sysfd}") == -1
                ? Ngo.Runtime.Syscall.Package.ErrnoToError("fchdir") : null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Fstat(object? s)
        {
            return Ngo.Runtime.Syscall.Package.Fstat(Sysfd, s);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetDeadline([GoParam("time.Time")] object t)
        {
            SetReadDeadline(t);
            SetWriteDeadline(t);
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetReadDeadline([GoParam("time.Time")] object t)
        {
            if (t is Ngo.Runtime.Time.GoTimeValue timeVal)
            {
                var duration = timeVal.Sub(Ngo.Runtime.Time.GoTime.Now());
                int timeoutMs = duration > 0 ? (int)(duration / 1_000_000) : 1;
                Ngo.Runtime.Syscall.Package.SetsockoptInt(Sysfd, 1, 20, timeoutMs); // SOL_SOCKET=1, SO_RCVTIMEO=20
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetWriteDeadline([GoParam("time.Time")] object t)
        {
            if (t is Ngo.Runtime.Time.GoTimeValue timeVal)
            {
                var duration = timeVal.Sub(Ngo.Runtime.Time.GoTime.Now());
                int timeoutMs = duration > 0 ? (int)(duration / 1_000_000) : 1;
                Ngo.Runtime.Syscall.Package.SetsockoptInt(Sysfd, 1, 21, timeoutMs); // SOL_SOCKET=1, SO_SNDTIMEO=21
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetBlocking()
        {
            return Ngo.Runtime.Syscall.Package.SetNonblock(Sysfd, false);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? RawControl(object? f)
        {
            if (f is System.Action<long> controlFn)
            {
                controlFn(Sysfd);
            }
            else if (f is System.Delegate del)
            {
                del.DynamicInvoke(Sysfd);
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? RawRead(object? f)
        {
            if (f is System.Func<long, bool> readFn)
            {
                while (!readFn(Sysfd)) { }
            }
            else if (f is System.Delegate del)
            {
                del.DynamicInvoke(Sysfd);
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? RawWrite(object? f)
        {
            if (f is System.Func<long, bool> writeFn)
            {
                while (!writeFn(Sysfd)) { }
            }
            else if (f is System.Delegate del)
            {
                del.DynamicInvoke(Sysfd);
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Shutdown([GoParam("int")] long how)
        {
            return Ngo.Runtime.Syscall.LinuxSyscalls.shutdown((int)Sysfd, (int)how) == -1
                ? Ngo.Runtime.Syscall.Package.ErrnoToError("shutdown") : null;
        }

        [GoMethod]
        [return: GoReturn("int", "syscall.Sockaddr", "error")]
        public (long, object?, object?) ReadFrom(Slice<byte> p)
        {
            var (bytesRead, err) = Read(p);
            return (bytesRead, null, err);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadFromInet4(Slice<byte> p, object? sa) => Read(p);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadFromInet6(Slice<byte> p, object? sa) => Read(p);

        [GoMethod]
        [return: GoReturn("int", "int", "int", "syscall.Sockaddr", "error")]
        public (long, long, long, object?, object?) ReadMsg(Slice<byte> p, Slice<byte> oob, [GoParam("int")] long flags)
        {
            var (bytesRead, err) = Read(p);
            return (bytesRead, 0, 0, null, err);
        }

        [GoMethod]
        [return: GoReturn("int", "int", "int", "error")]
        public (long, long, long, object?) ReadMsgInet4(Slice<byte> p, Slice<byte> oob, [GoParam("int")] long flags, object? sa)
        {
            var (bytesRead, err) = Read(p);
            return (bytesRead, 0, 0, err);
        }

        [GoMethod]
        [return: GoReturn("int", "int", "int", "error")]
        public (long, long, long, object?) ReadMsgInet6(Slice<byte> p, Slice<byte> oob, [GoParam("int")] long flags, object? sa)
        {
            var (bytesRead, err) = Read(p);
            return (bytesRead, 0, 0, err);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteTo(Slice<byte> p, object? sa) => Write(p);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteToInet4(Slice<byte> p, object? sa) => Write(p);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteToInet6(Slice<byte> p, object? sa) => Write(p);

        [GoMethod]
        [return: GoReturn("int", "int", "error")]
        public (long, long, object?) WriteMsg(Slice<byte> p, Slice<byte> oob, object? sa)
        {
            var (bytesWritten, err) = Write(p);
            return (bytesWritten, 0, err);
        }

        [GoMethod]
        [return: GoReturn("int", "int", "error")]
        public (long, long, object?) WriteMsgInet4(Slice<byte> p, Slice<byte> oob, object? sa)
        {
            var (bytesWritten, err) = Write(p);
            return (bytesWritten, 0, err);
        }

        [GoMethod]
        [return: GoReturn("int", "int", "error")]
        public (long, long, object?) WriteMsgInet6(Slice<byte> p, Slice<byte> oob, object? sa)
        {
            var (bytesWritten, err) = Write(p);
            return (bytesWritten, 0, err);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptInt([GoParam("int")] long level, [GoParam("int")] long name, [GoParam("int")] long value)
        {
            return Ngo.Runtime.Syscall.Package.SetsockoptInt(Sysfd, level, name, value);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) GetsockoptInt([GoParam("int")] long level, [GoParam("int")] long name)
        {
            return Ngo.Runtime.Syscall.Package.GetsockoptInt(Sysfd, level, name);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptIPMreq([GoParam("int")] long level, [GoParam("int")] long name, object? mreq)
        {
            return Ngo.Runtime.Syscall.Package.SetsockoptIPMreq(Sysfd, level, name, mreq);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptIPMreqn([GoParam("int")] long level, [GoParam("int")] long name, object? mreq)
        {
            return Ngo.Runtime.Syscall.Package.SetsockoptIPMreq(Sysfd, level, name, mreq);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptIPv6Mreq([GoParam("int")] long level, [GoParam("int")] long name, object? mreq)
        {
            return Ngo.Runtime.Syscall.Package.SetsockoptIPv6Mreq(Sysfd, level, name, mreq);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? SetsockoptLinger([GoParam("int")] long level, [GoParam("int")] long name, object? l)
        {
            return Ngo.Runtime.Syscall.Package.SetsockoptLinger(Sysfd, level, name, l);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? WaitWrite()
        {
            var pollfd = new Ngo.Runtime.Syscall.LinuxPollfd
            {
                Fd = (int)Sysfd,
                Events = 4, // POLLOUT
                Revents = 0,
            };
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pollfd, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int result = Ngo.Runtime.Syscall.LinuxSyscalls.poll(handle.AddrOfPinnedObject(), 1, -1);
                if (result == -1)
                {
                    return Ngo.Runtime.Syscall.Package.ErrnoToError("poll");
                }
                return null;
            }
            finally
            {
                handle.Free();
            }
        }

        [GoMethod]
        [return: GoReturn("int", "syscall.Sockaddr", "string", "error")]
        public (long, object?, string, object?) Accept()
        {
            int newfd = Ngo.Runtime.Syscall.LinuxSyscalls.accept4((int)Sysfd, System.IntPtr.Zero, System.IntPtr.Zero, 0x80000); // SOCK_CLOEXEC
            if (newfd == -1)
            {
                return (0, null, "", Ngo.Runtime.Syscall.Package.ErrnoToError("accept4"));
            }
            return (newfd, null, "", null);
        }

        [GoMethod]
        [return: GoReturn("int", "string", "error")]
        public (long, string, object?) Dup()
        {
            int newfd = Ngo.Runtime.Syscall.LinuxSyscalls.fcntl((int)Sysfd, 0, 0); // F_DUPFD = 0
            if (newfd == -1)
            {
                return (0, "", Ngo.Runtime.Syscall.Package.ErrnoToError("dup"));
            }
            return (newfd, "", null);
        }

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) Writev(object? v) => Write(default);

        [GoField] public GoSysFile SysFile;
    }
}
