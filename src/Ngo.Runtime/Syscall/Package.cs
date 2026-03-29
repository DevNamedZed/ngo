using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Syscall
{
    [GoPackage("syscall")]
    public static class Package
    {
        private static readonly bool IsUnix = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Linux)
            || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.OSX);

        private static long PosixOnly(System.Func<uint> call)
        {
            if (IsUnix)
            {
                return call();
            }
            throw new GoPanicException("syscall: not available on this platform");
        }

        // File open flags
        [GoConst] public static readonly long O_RDONLY = 0;
        [GoConst] public static readonly long O_WRONLY = 1;
        [GoConst] public static readonly long O_RDWR = 2;
        [GoConst] public static readonly long O_APPEND = 1024;
        [GoConst] public static readonly long O_CREATE = 64;
        [GoConst] public static readonly long O_CREAT = 64;
        [GoConst] public static readonly long O_EXCL = 128;
        [GoConst] public static readonly long O_SYNC = 1052672;
        [GoConst] public static readonly long O_TRUNC = 512;
        [GoConst] public static readonly long O_NONBLOCK = 2048;
        [GoConst] public static readonly long O_CLOEXEC = 524288;
        [GoConst] public static readonly long O_DIRECTORY = 65536;
        [GoConst] public static readonly long O_NOFOLLOW = 131072;

        // File mode bits
        [GoConst] public static readonly long S_IFMT = 0xf000;
        [GoConst] public static readonly long S_IFBLK = 0x6000;
        [GoConst] public static readonly long S_IFCHR = 0x2000;
        [GoConst] public static readonly long S_IFDIR = 0x4000;
        [GoConst] public static readonly long S_IFIFO = 0x1000;
        [GoConst] public static readonly long S_IFLNK = 0xa000;
        [GoConst] public static readonly long S_IFREG = 0x8000;
        [GoConst] public static readonly long S_IFSOCK = 0xc000;
        [GoConst] public static readonly long S_ISUID = 0x800;
        [GoConst] public static readonly long S_ISGID = 0x400;
        [GoConst] public static readonly long S_ISVTX = 0x200;

        // Dirent type constants
        [GoConst] public static readonly long DT_BLK = 6;
        [GoConst] public static readonly long DT_CHR = 2;
        [GoConst] public static readonly long DT_DIR = 4;
        [GoConst] public static readonly long DT_FIFO = 1;
        [GoConst] public static readonly long DT_LNK = 10;
        [GoConst] public static readonly long DT_REG = 8;
        [GoConst] public static readonly long DT_SOCK = 12;
        [GoConst] public static readonly long DT_UNKNOWN = 0;

        // Socket constants
        [GoConst] public static readonly long AF_INET = 2;
        [GoConst] public static readonly long AF_INET6 = 10;
        [GoConst] public static readonly long AF_UNIX = 1;
        [GoConst] public static readonly long AF_UNSPEC = 0;
        [GoConst] public static readonly long SOCK_STREAM = 1;
        [GoConst] public static readonly long SOCK_DGRAM = 2;
        [GoConst] public static readonly long SOCK_RAW = 3;
        [GoConst] public static readonly long SOCK_SEQPACKET = 5;
        [GoConst] public static readonly long SOCK_NONBLOCK = 2048;
        [GoConst] public static readonly long SOCK_CLOEXEC = 524288;
        [GoConst] public static readonly long IPPROTO_TCP = 6;
        [GoConst] public static readonly long IPPROTO_UDP = 17;
        [GoConst] public static readonly long IPPROTO_IP = 0;
        [GoConst] public static readonly long IPPROTO_IPV6 = 41;
        [GoConst] public static readonly long SOL_SOCKET = 1;
        [GoConst] public static readonly long SO_REUSEADDR = 2;
        [GoConst] public static readonly long SO_REUSEPORT = 15;
        [GoConst] public static readonly long SO_KEEPALIVE = 9;
        [GoConst] public static readonly long SO_BROADCAST = 6;
        [GoConst] public static readonly long SO_LINGER = 13;
        [GoConst] public static readonly long SO_ERROR = 4;
        [GoConst] public static readonly long SO_TYPE = 3;
        [GoConst] public static readonly long SO_SNDBUF = 7;
        [GoConst] public static readonly long SO_RCVBUF = 8;
        [GoConst] public static readonly long TCP_NODELAY = 1;
        [GoConst] public static readonly long TCP_KEEPINTVL = 5;
        [GoConst] public static readonly long TCP_KEEPIDLE = 4;
        [GoConst] public static readonly long IP_TOS = 1;
        [GoConst] public static readonly long IP_TTL = 2;
        [GoConst] public static readonly long IPV6_V6ONLY = 26;
        [GoConst] public static readonly long IPV6_TCLASS = 67;
        [GoConst] public static readonly long IP_MULTICAST_TTL = 33;
        [GoConst] public static readonly long IP_MULTICAST_LOOP = 34;
        [GoConst] public static readonly long IPV6_MULTICAST_HOPS = 18;
        [GoConst] public static readonly long IPV6_MULTICAST_LOOP = 19;
        [GoConst] public static readonly long IP_ADD_MEMBERSHIP = 35;
        [GoConst] public static readonly long IP_DROP_MEMBERSHIP = 36;
        [GoConst] public static readonly long IPV6_JOIN_GROUP = 20;
        [GoConst] public static readonly long IPV6_LEAVE_GROUP = 21;
        [GoConst] public static readonly long IP_MULTICAST_IF = 32;
        [GoConst] public static readonly long SHUT_RD = 0;
        [GoConst] public static readonly long SHUT_WR = 1;
        [GoConst] public static readonly long SHUT_RDWR = 2;
        [GoConst] public static readonly long SizeofSockaddrInet4 = 16;
        [GoConst] public static readonly long SizeofSockaddrInet6 = 28;
        [GoConst] public static readonly long SizeofSockaddrAny = 112;
        [GoConst] public static readonly long SizeofSockaddrUnix = 110;
        [GoConst] public static readonly long SizeofLinger = 8;

        // Seek constants
        [GoConst] public static readonly long SEEK_SET = 0;
        [GoConst] public static readonly long SEEK_CUR = 1;
        [GoConst] public static readonly long SEEK_END = 2;

        // Stdin/Stdout/Stderr
        [GoConst] public static readonly long Stdin = 0;
        [GoConst] public static readonly long Stdout = 1;
        [GoConst] public static readonly long Stderr = 2;

        // Fcntl constants
        [GoConst] public static readonly long F_GETFL = 3;
        [GoConst] public static readonly long F_SETFL = 4;
        [GoConst] public static readonly long F_GETFD = 1;
        [GoConst] public static readonly long F_SETFD = 2;
        [GoConst] public static readonly long F_DUPFD_CLOEXEC = 1030;
        [GoConst] public static readonly long FD_CLOEXEC = 1;

        // Wait status
        [GoConst] public static readonly long WNOHANG = 1;
        [GoConst] public static readonly long WUNTRACED = 2;
        [GoConst] public static readonly long WEXITED = 4;
        [GoConst] public static readonly long WNOWAIT = 0x01000000;
        [GoConst] public static readonly long SYS_WAITID = 247;

        // Signals (typed as Signal)
        [GoConst(Type = "Signal")] public static readonly long SIGABRT = 6;
        [GoConst(Type = "Signal")] public static readonly long SIGALRM = 14;
        [GoConst(Type = "Signal")] public static readonly long SIGBUS = 7;
        [GoConst(Type = "Signal")] public static readonly long SIGCHLD = 17;
        [GoConst(Type = "Signal")] public static readonly long SIGCONT = 18;
        [GoConst(Type = "Signal")] public static readonly long SIGFPE = 8;
        [GoConst(Type = "Signal")] public static readonly long SIGHUP = 1;
        [GoConst(Type = "Signal")] public static readonly long SIGILL = 4;
        [GoConst(Type = "Signal")] public static readonly long SIGINT = 2;
        [GoConst(Type = "Signal")] public static readonly long SIGIO = 29;
        [GoConst(Type = "Signal")] public static readonly long SIGKILL = 9;
        [GoConst(Type = "Signal")] public static readonly long SIGPIPE = 13;
        [GoConst(Type = "Signal")] public static readonly long SIGPROF = 27;
        [GoConst(Type = "Signal")] public static readonly long SIGQUIT = 3;
        [GoConst(Type = "Signal")] public static readonly long SIGSEGV = 11;
        [GoConst(Type = "Signal")] public static readonly long SIGSTOP = 19;
        [GoConst(Type = "Signal")] public static readonly long SIGSYS = 31;
        [GoConst(Type = "Signal")] public static readonly long SIGTERM = 15;
        [GoConst(Type = "Signal")] public static readonly long SIGTRAP = 5;
        [GoConst(Type = "Signal")] public static readonly long SIGTSTP = 20;
        [GoConst(Type = "Signal")] public static readonly long SIGTTIN = 21;
        [GoConst(Type = "Signal")] public static readonly long SIGTTOU = 22;
        [GoConst(Type = "Signal")] public static readonly long SIGURG = 23;
        [GoConst(Type = "Signal")] public static readonly long SIGUSR1 = 10;
        [GoConst(Type = "Signal")] public static readonly long SIGUSR2 = 12;
        [GoConst(Type = "Signal")] public static readonly long SIGVTALRM = 26;
        [GoConst(Type = "Signal")] public static readonly long SIGWINCH = 28;
        [GoConst(Type = "Signal")] public static readonly long SIGXCPU = 24;
        [GoConst(Type = "Signal")] public static readonly long SIGXFSZ = 25;

        // Implementation flag
        [GoVar] public static readonly bool ImplementsGetwd = true;

        // Netlink constants
        [GoConst] public static readonly long NLMSG_DONE = 3;
        [GoConst] public static readonly long RTM_NEWLINK = 16;
        [GoConst] public static readonly long RTM_NEWADDR = 20;
        [GoConst] public static readonly long IFLA_ADDRESS = 1;
        [GoConst] public static readonly long IFLA_IFNAME = 3;
        [GoConst] public static readonly long IFLA_MTU = 4;
        [GoConst] public static readonly long IFA_LOCAL = 2;
        [GoConst] public static readonly long IFA_ADDRESS = 1;
        [GoConst] public static readonly long IFF_UP = 1;
        [GoConst] public static readonly long IFF_RUNNING = 64;
        [GoConst] public static readonly long IFF_BROADCAST = 2;
        [GoConst] public static readonly long IFF_LOOPBACK = 8;
        [GoConst] public static readonly long IFF_POINTOPOINT = 16;
        [GoConst] public static readonly long IFF_MULTICAST = 4096;
        [GoConst] public static readonly long MSG_CMSG_CLOEXEC = 0x40000000;
        [GoConst] public static readonly long SO_PROTOCOL = 38;
        [GoConst] public static readonly long IPV6_MULTICAST_IF = 17;
        [GoConst] public static readonly long RTM_GETLINK = 18;
        [GoConst] public static readonly long RTM_GETADDR = 22;
        [GoConst] public static readonly long RLIMIT_NOFILE = 7;
        [GoConst] public static readonly long SOMAXCONN = 4096;

        // Error variables (Errno values)
        [GoVar(Type = "Errno")] public static readonly object? EINVAL = 22L;
        [GoVar(Type = "Errno")] public static readonly object? ENOENT = 2L;
        [GoVar(Type = "Errno")] public static readonly object? EPERM = 1L;
        [GoVar(Type = "Errno")] public static readonly object? EEXIST = 17L;
        [GoVar(Type = "Errno")] public static readonly object? ENOTDIR = 20L;
        [GoVar(Type = "Errno")] public static readonly object? EISDIR = 21L;
        [GoVar(Type = "Errno")] public static readonly object? EACCES = 13L;
        [GoVar(Type = "Errno")] public static readonly object? EBADF = 9L;
        [GoVar(Type = "Errno")] public static readonly object? ENFILE = 23L;
        [GoVar(Type = "Errno")] public static readonly object? EMFILE = 24L;
        [GoVar(Type = "Errno")] public static readonly object? ENOSYS = 38L;
        [GoVar(Type = "Errno")] public static readonly object? EAGAIN = 11L;
        [GoVar(Type = "Errno")] public static readonly object? EWOULDBLOCK = 11L;
        [GoVar(Type = "Errno")] public static readonly object? ECONNREFUSED = 111L;
        [GoVar(Type = "Errno")] public static readonly object? ECONNRESET = 104L;
        [GoVar(Type = "Errno")] public static readonly object? ECONNABORTED = 103L;
        [GoVar(Type = "Errno")] public static readonly object? ETIMEDOUT = 110L;
        [GoVar(Type = "Errno")] public static readonly object? ENOBUFS = 105L;
        [GoVar(Type = "Errno")] public static readonly object? ENETUNREACH = 101L;
        [GoVar(Type = "Errno")] public static readonly object? EHOSTUNREACH = 113L;
        [GoVar(Type = "Errno")] public static readonly object? EAFNOSUPPORT = 97L;
        [GoVar(Type = "Errno")] public static readonly object? EADDRNOTAVAIL = 99L;
        [GoVar(Type = "Errno")] public static readonly object? EADDRINUSE = 98L;
        [GoVar(Type = "Errno")] public static readonly object? EINTR = 4L;
        [GoVar(Type = "Errno")] public static readonly object? EPIPE = 32L;
        [GoVar(Type = "Errno")] public static readonly object? EIO = 5L;
        [GoVar(Type = "Errno")] public static readonly object? ERANGE = 34L;
        [GoVar(Type = "Errno")] public static readonly object? ENAMETOOLONG = 36L;
        [GoVar(Type = "Errno")] public static readonly object? ELOOP = 40L;
        [GoVar(Type = "Errno")] public static readonly object? ENOTSOCK = 88L;
        [GoVar(Type = "Errno")] public static readonly object? EPROTONOSUPPORT = 93L;
        [GoVar(Type = "Errno")] public static readonly object? ENOPROTOOPT = 92L;
        [GoVar(Type = "Errno")] public static readonly object? ESRCH = 3L;
        [GoVar(Type = "Errno")] public static readonly object? ECHILD = 10L;
        [GoVar(Type = "Errno")] public static readonly object? ENOTCONN = 107L;
        [GoVar(Type = "Errno")] public static readonly object? EMSGSIZE = 90L;
        [GoVar(Type = "Errno")] public static readonly object? EOPNOTSUPP = 95L;
        [GoVar(Type = "Errno")] public static readonly object? ENOTSUP = 95L;
        [GoVar(Type = "Errno")] public static readonly object? EINPROGRESS = 115L;
        [GoVar(Type = "Errno")] public static readonly object? EALREADY = 114L;
        [GoVar(Type = "Errno")] public static readonly object? EISCONN = 106L;

        // ForkLock is a RWMutex used to synchronize fork/exec
        [GoVar(Type = "sync.RWMutex")] public static readonly object? ForkLock = new object();

        // Functions
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Close([GoParam("int")] long fd)
        {
            int result = LinuxSyscalls.close((int)fd);
            if (result == -1)
            {
                return ErrnoToError("close");
            }
            return null;
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Read([GoParam("int")] long fd, Slice<byte> p)
        {
            var buffer = new byte[p.Len];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int bytesRead = LinuxSyscalls.read((int)fd, handle.AddrOfPinnedObject(), (System.UIntPtr)buffer.Length);
                if (bytesRead == -1)
                {
                    return (0, ErrnoToError("read"));
                }
                for (int i = 0; i < bytesRead; i++)
                {
                    p[i] = buffer[i];
                }
                return (bytesRead, null);
            }
            finally
            {
                handle.Free();
            }
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Write([GoParam("int")] long fd, Slice<byte> p)
        {
            var buffer = new byte[p.Len];
            for (int i = 0; i < p.Len; i++)
            {
                buffer[i] = p[i];
            }
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int bytesWritten = LinuxSyscalls.write((int)fd, handle.AddrOfPinnedObject(), (System.UIntPtr)buffer.Length);
                if (bytesWritten == -1)
                {
                    return (0, ErrnoToError("write"));
                }
                return (bytesWritten, null);
            }
            finally
            {
                handle.Free();
            }
        }

        internal static string ErrnoToError(string syscallName)
        {
            int errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            return $"{syscallName}: errno {errno}";
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Setenv(string key, string value)
        {
            System.Environment.SetEnvironmentVariable(key, value);
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unsetenv(string key)
        {
            System.Environment.SetEnvironmentVariable(key, null);
            return null;
        }

        [GoFunc]
        [return: GoReturn("string", "bool")]
        public static (string, bool) Getenv(string key)
        {
            var val = System.Environment.GetEnvironmentVariable(key);
            return (val ?? "", val != null);
        }

        [GoFunc]
        [return: GoReturn("[]string")]
        public static Slice<string> Environ()
        {
            var env = System.Environment.GetEnvironmentVariables();
            var result = new string[env.Count];
            int i = 0;
            foreach (System.Collections.DictionaryEntry entry in env)
                result[i++] = $"{entry.Key}={entry.Value}";
            return new Slice<string>(result);
        }

        [GoFunc]
        public static void Clearenv()
        {
            foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                System.Environment.SetEnvironmentVariable((string)entry.Key, null);
            }
        }

        [GoFunc]
        public static long Getuid() => PosixOnly(LinuxSyscalls.getuid);

        [GoFunc]
        public static long Getgid() => PosixOnly(LinuxSyscalls.getgid);

        [GoFunc]
        public static long Geteuid() => PosixOnly(LinuxSyscalls.geteuid);

        [GoFunc]
        public static long Getegid() => PosixOnly(LinuxSyscalls.getegid);

        [GoFunc]
        public static long Getpid() => System.Environment.ProcessId;

        [GoFunc]
        public static long Getppid() => PosixOnly(() => (uint)LinuxSyscalls.getppid());

        [GoFunc]
        [return: GoReturn("[]int", "error")]
        public static (Slice<long>, object?) Getgroups() => (new Slice<long>(System.Array.Empty<long>()), null);

        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) Getwd() => (System.Environment.CurrentDirectory, null);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Socket([GoParam("int")] long domain, [GoParam("int")] long typ, [GoParam("int")] long proto)
        {
            if (!IsUnix)
            {
                return (0, "socket: not available on this platform");
            }
            int fd = LinuxSyscalls.socket((int)domain, (int)typ, (int)proto);
            if (fd == -1)
            {
                return (0, $"socket: errno {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }
            return (fd, null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fchmod([GoParam("int")] long fd, [GoParam("uint32")] long mode)
            => LinuxSyscalls.fchmod((int)fd, (uint)mode) == -1 ? ErrnoToError("fchmod") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fchown([GoParam("int")] long fd, [GoParam("int")] long uid, [GoParam("int")] long gid)
            => LinuxSyscalls.fchown((int)fd, (uint)uid, (uint)gid) == -1 ? ErrnoToError("fchown") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Ftruncate([GoParam("int")] long fd, long length)
            => LinuxSyscalls.ftruncate((int)fd, length) == -1 ? ErrnoToError("ftruncate") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chmod(string path, [GoParam("uint32")] long mode)
            => LinuxSyscalls.chmod(path, (uint)mode) == -1 ? ErrnoToError("chmod") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chown(string path, [GoParam("int")] long uid, [GoParam("int")] long gid)
            => LinuxSyscalls.chown(path, (uint)uid, (uint)gid) == -1 ? ErrnoToError("chown") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Lchown(string path, [GoParam("int")] long uid, [GoParam("int")] long gid)
            => LinuxSyscalls.chown(path, (uint)uid, (uint)gid) == -1 ? ErrnoToError("lchown") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Mkdir(string path, [GoParam("uint32")] long perm)
            => LinuxSyscalls.mkdir(path, (uint)perm) == -1 ? ErrnoToError("mkdir") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Rmdir(string path)
            => LinuxSyscalls.rmdir(path) == -1 ? ErrnoToError("rmdir") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unlink(string path)
            => LinuxSyscalls.unlink(path) == -1 ? ErrnoToError("unlink") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Rename(string oldpath, string newpath)
            => LinuxSyscalls.rename(oldpath, newpath) == -1 ? ErrnoToError("rename") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Link(string oldpath, string newpath)
            => LinuxSyscalls.link(oldpath, newpath) == -1 ? ErrnoToError("link") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Symlink(string oldname, string newname)
            => LinuxSyscalls.symlink(oldname, newname) == -1 ? ErrnoToError("symlink") : null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Readlink(string path, Slice<byte> buf)
        {
            var buffer = new byte[buf.Len];
            long bytesRead = LinuxSyscalls.readlink(path, buffer, (System.UIntPtr)buffer.Length);
            if (bytesRead == -1)
            {
                return (0, ErrnoToError("readlink"));
            }
            for (int i = 0; i < (int)bytesRead; i++)
            {
                buf[i] = buffer[i];
            }
            return (bytesRead, null);
        }

        [GoFunc]
        [return: GoReturn("int64", "error")]
        public static (long, object?) Seek([GoParam("int")] long fd, long offset, [GoParam("int")] long whence)
        {
            long result = LinuxSyscalls.lseek((int)fd, offset, (int)whence);
            if (result == -1)
            {
                return (0, ErrnoToError("lseek"));
            }
            return (result, null);
        }

        [GoFunc]
        [return: GoReturn("int", "int", "[]string")]
        public static (long, long, Slice<string>) ParseDirent(Slice<byte> buf, [GoParam("int")] long max, Slice<string> names)
        {
            // Linux dirent64: d_ino(8) + d_off(8) + d_reclen(2) + d_type(1) + d_name(256)
            var nameList = new System.Collections.Generic.List<string>();
            if (names.Len > 0)
            {
                for (int i = 0; i < names.Len; i++)
                {
                    nameList.Add(names[i]);
                }
            }
            int consumed = 0;
            int count = 0;
            int offset = 0;
            while (offset + 19 <= buf.Len && (max <= 0 || count < max))
            {
                int reclen = buf[offset + 16] | (buf[offset + 17] << 8);
                if (reclen < 19 || offset + reclen > buf.Len)
                {
                    break;
                }
                // d_name starts at offset+19
                int nameStart = offset + 19;
                int nameEnd = nameStart;
                while (nameEnd < offset + reclen && buf[nameEnd] != 0)
                {
                    nameEnd++;
                }
                var nameBytes = new byte[nameEnd - nameStart];
                for (int i = 0; i < nameBytes.Length; i++)
                {
                    nameBytes[i] = buf[nameStart + i];
                }
                string name = System.Text.Encoding.UTF8.GetString(nameBytes);
                if (name != "." && name != "..")
                {
                    nameList.Add(name);
                    count++;
                }
                consumed += reclen;
                offset += reclen;
            }
            return (consumed, count, new Slice<string>(nameList.ToArray()));
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) ReadDirent([GoParam("int")] long fd, Slice<byte> buf)
        {
            return Read(fd, buf);
        }

        [GoFunc]
        public static long Umask([GoParam("int")] long mask) => 0;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chdir(string path)
            => LinuxSyscalls.chdir(path) == -1 ? ErrnoToError("chdir") : null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Open(string path, [GoParam("int")] long mode, [GoParam("uint32")] long perm)
        {
            int fd = LinuxSyscalls.open(path, (int)mode, (uint)perm);
            if (fd == -1)
            {
                return (0, ErrnoToError("open"));
            }
            return (fd, null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Stat(string path, object? stat)
        {
            var nativeStat = new LinuxStat();
            int result = LinuxSyscalls.stat(path, ref nativeStat);
            if (result == -1)
            {
                return ErrnoToError("stat");
            }
            PopulateGoStat(stat, ref nativeStat);
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Lstat(string path, object? stat)
        {
            var nativeStat = new LinuxStat();
            int result = LinuxSyscalls.lstat(path, ref nativeStat);
            if (result == -1)
            {
                return ErrnoToError("lstat");
            }
            PopulateGoStat(stat, ref nativeStat);
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fstat([GoParam("int")] long fd, object? stat)
        {
            var nativeStat = new LinuxStat();
            int result = LinuxSyscalls.fstat((int)fd, ref nativeStat);
            if (result == -1)
            {
                return ErrnoToError("fstat");
            }
            PopulateGoStat(stat, ref nativeStat);
            return null;
        }

        private static unsafe Slice<byte> CopyUtsField(byte* field, int maxLen)
        {
            int len = 0;
            while (len < maxLen && field[len] != 0)
            {
                len++;
            }
            var bytes = new byte[len];
            for (int i = 0; i < len; i++)
            {
                bytes[i] = field[i];
            }
            return new Slice<byte>(bytes);
        }

        private static void PopulateGoStat(object? goStat, ref LinuxStat native)
        {
            if (goStat is GoStat_t st)
            {
                st.Dev = (long)native.Dev;
                st.Ino = (long)native.Ino;
                st.Nlink = (long)native.Nlink;
                st.Mode = (long)native.Mode;
                st.Uid = (long)native.Uid;
                st.Gid = (long)native.Gid;
                st.Rdev = (long)native.Rdev;
                st.Size = native.Size;
                st.Blksize = native.Blksize;
                st.Blocks = native.Blocks;
                st.Atim = new GoTimespec { Sec = native.Atim_sec, Nsec = native.Atim_nsec };
                st.Mtim = new GoTimespec { Sec = native.Mtim_sec, Nsec = native.Mtim_nsec };
                st.Ctim = new GoTimespec { Sec = native.Ctim_sec, Nsec = native.Ctim_nsec };
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Kill([GoParam("int")] long pid, object? sig)
        {
            int signalNum = 0;
            if (sig is long signalLong)
            {
                signalNum = (int)signalLong;
            }
            return LinuxSyscalls.kill((int)pid, signalNum) == -1 ? ErrnoToError("kill") : null;
        }

        [GoFunc]
        public static long Getpagesize() => 4096;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Getrusage([GoParam("int")] long who, [GoParam("*Rusage")] object? rusage)
        {
            var native = new LinuxRusage();
            int result = LinuxSyscalls.getrusage((int)who, ref native);
            if (result == -1)
            {
                return ErrnoToError("getrusage");
            }
            if (rusage is GoRusage goRusage)
            {
                goRusage.Utime = new GoTimeval { Sec = native.UserTime.Sec, Usec = native.UserTime.Usec };
                goRusage.Stime = new GoTimeval { Sec = native.SystemTime.Sec, Usec = native.SystemTime.Usec };
                goRusage.Maxrss = native.MaxRss;
            }
            return null;
        }

        [GoConst] public static readonly long RUSAGE_SELF = 0;
        [GoConst] public static readonly long RUSAGE_CHILDREN = -1;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Pipe(Slice<long> p)
        {
            var pipefd = new int[2];
            if (LinuxSyscalls.pipe(pipefd) == -1)
            {
                return ErrnoToError("pipe");
            }
            p[0] = pipefd[0];
            p[1] = pipefd[1];
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Pipe2(Slice<long> p, [GoParam("int")] long flags)
        {
            var pipefd = new int[2];
            if (LinuxSyscalls.pipe2(pipefd, (int)flags) == -1)
            {
                return ErrnoToError("pipe2");
            }
            p[0] = pipefd[0];
            p[1] = pipefd[1];
            return null;
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Dup([GoParam("int")] long oldfd) => (oldfd, null);

        [GoFunc]
        public static void Exit([GoParam("int")] long code) => System.Environment.Exit((int)code);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetNonblock([GoParam("int")] long fd, bool nonblocking)
        {
            int flags = LinuxSyscalls.fcntl((int)fd, 3, 0); // F_GETFL = 3
            if (flags == -1)
            {
                return ErrnoToError("fcntl(F_GETFL)");
            }
            if (nonblocking)
            {
                flags |= 0x800; // O_NONBLOCK
            }
            else
            {
                flags &= ~0x800;
            }
            if (LinuxSyscalls.fcntl((int)fd, 4, flags) == -1) // F_SETFL = 4
            {
                return ErrnoToError("fcntl(F_SETFL)");
            }
            return null;
        }

        [GoFunc]
        public static void CloseOnExec([GoParam("int")] long fd)
        {
            LinuxSyscalls.fcntl((int)fd, 2, 1); // F_SETFD=2, FD_CLOEXEC=1
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Truncate(string path, long length)
            => LinuxSyscalls.truncate(path, length) == -1 ? ErrnoToError("truncate") : null;

        [GoFunc]
        [return: GoReturn("Timespec")]
        public static GoTimespec NsecToTimespec(long nsec)
        {
            return new GoTimespec { Sec = nsec / 1000000000, Nsec = nsec % 1000000000 };
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? UtimesNano(string path, Slice<GoTimespec> ts)
        {
            if (ts.Len >= 2)
            {
                var times = new LinuxTimespec[2];
                times[0] = new LinuxTimespec { Sec = ts[0].Sec, Nsec = ts[0].Nsec };
                times[1] = new LinuxTimespec { Sec = ts[1].Sec, Nsec = ts[1].Nsec };
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(times, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    // utimensat(AT_FDCWD, path, times, 0)
                    int result = LinuxSyscalls.utimensat(-100, path, ref times[0], 0);
                    return result == -1 ? ErrnoToError("utimensat") : null;
                }
                finally
                {
                    handle.Free();
                }
            }
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Uname(object? buf)
        {
            var native = new LinuxUtsname();
            int result = LinuxSyscalls.uname(ref native);
            if (result == -1)
            {
                return ErrnoToError("uname");
            }
            if (buf is GoUtsname goUts)
            {
                unsafe
                {
                    goUts.Sysname = CopyUtsField(native.Sysname, 65);
                    goUts.Nodename = CopyUtsField(native.Nodename, 65);
                    goUts.Release = CopyUtsField(native.Release, 65);
                    goUts.Version = CopyUtsField(native.Version, 65);
                    goUts.Machine = CopyUtsField(native.Machine, 65);
                    goUts.Domainname = CopyUtsField(native.Domainname, 65);
                }
            }
            return null;
        }

        [GoFunc]
        [return: GoReturn("int", "uintptr", "Errno")]
        public static (long, long, object?) Syscall(long trap, long a1, long a2, long a3)
        {
            long result = LinuxSyscalls.syscall(trap, a1, a2, a3);
            if (result == -1)
            {
                int errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
                return (result, 0, (object?)(long)errno);
            }
            return (result, 0, null);
        }

        [GoFunc]
        [return: GoReturn("int", "uintptr", "Errno")]
        public static (long, long, object?) Syscall6(long trap, long a1, long a2, long a3, long a4, long a5, long a6)
        {
            long result = LinuxSyscalls.syscall6(trap, a1, a2, a3, a4, a5, a6);
            if (result == -1)
            {
                int errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
                return (result, 0, (object?)(long)errno);
            }
            return (result, 0, null);
        }

        [GoFunc]
        [return: GoReturn("int", "uintptr", "Errno")]
        public static (long, long, object?) RawSyscall(long trap, long a1, long a2, long a3)
            => Syscall(trap, a1, a2, a3);

        [GoFunc]
        [return: GoReturn("int", "uintptr", "Errno")]
        public static (long, long, object?) RawSyscall6(long trap, long a1, long a2, long a3, long a4, long a5, long a6)
            => Syscall6(trap, a1, a2, a3, a4, a5, a6);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Fcntl([GoParam("int")] long fd, [GoParam("int")] long cmd, [GoParam("int")] long arg)
        {
            int result = LinuxSyscalls.fcntl((int)fd, (int)cmd, (int)arg);
            if (result == -1)
            {
                return (0, ErrnoToError("fcntl"));
            }
            return (result, null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Bind([GoParam("int")] long fd, object? sa)
        {
            if (sa is GoSockaddrInet4 inet4)
            {
                var addr = new LinuxSockaddrIn
                {
                    Family = 2, // AF_INET
                    Port = (ushort)((inet4.Port >> 8) | ((inet4.Port & 0xff) << 8)), // htons
                    Addr = (uint)(inet4.Addr[0] | (inet4.Addr[1] << 8) | (inet4.Addr[2] << 16) | (inet4.Addr[3] << 24)),
                };
                return LinuxSyscalls.bind((int)fd, ref addr, 16) == -1 ? ErrnoToError("bind") : null;
            }
            return "bind: unsupported sockaddr type";
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Listen([GoParam("int")] long fd, [GoParam("int")] long backlog)
        {
            return LinuxSyscalls.listen((int)fd, (int)backlog) == -1 ? ErrnoToError("listen") : null;
        }

        [GoFunc]
        [return: GoReturn("int", "Sockaddr", "error")]
        public static (long, object?, object?) Accept([GoParam("int")] long fd)
        {
            int newfd = LinuxSyscalls.accept4((int)fd, System.IntPtr.Zero, System.IntPtr.Zero, 0);
            if (newfd == -1)
            {
                return (0, null, ErrnoToError("accept"));
            }
            return (newfd, null, null);
        }

        [GoFunc]
        [return: GoReturn("int", "Sockaddr", "error")]
        public static (long, object?, object?) Accept4([GoParam("int")] long fd, [GoParam("int")] long flags)
        {
            int newfd = LinuxSyscalls.accept4((int)fd, System.IntPtr.Zero, System.IntPtr.Zero, (int)flags);
            if (newfd == -1)
            {
                return (0, null, ErrnoToError("accept4"));
            }
            return (newfd, null, null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Connect([GoParam("int")] long fd, object? sa)
        {
            if (sa is GoSockaddrInet4 inet4)
            {
                var addr = new LinuxSockaddrIn
                {
                    Family = 2, // AF_INET
                    Port = (ushort)((inet4.Port >> 8) | ((inet4.Port & 0xff) << 8)),
                    Addr = (uint)(inet4.Addr[0] | (inet4.Addr[1] << 8) | (inet4.Addr[2] << 16) | (inet4.Addr[3] << 24)),
                };
                return LinuxSyscalls.connect((int)fd, ref addr, 16) == -1 ? ErrnoToError("connect") : null;
            }
            return "connect: unsupported sockaddr type";
        }

        [GoFunc]
        [return: GoReturn("Sockaddr", "error")]
        public static (object?, object?) Getsockname([GoParam("int")] long fd) => (null, null);

        [GoFunc]
        [return: GoReturn("Sockaddr", "error")]
        public static (object?, object?) Getpeername([GoParam("int")] long fd) => (null, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptInt([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, [GoParam("int")] long value)
        {
            var valueBytes = System.BitConverter.GetBytes((int)value);
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(valueBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int result = LinuxSyscalls.setsockopt((int)fd, (int)level, (int)opt, handle.AddrOfPinnedObject(), 4);
                return result == -1 ? ErrnoToError("setsockopt") : null;
            }
            finally
            {
                handle.Free();
            }
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) GetsockoptInt([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt)
        {
            var valueBytes = new byte[4];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(valueBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int optlen = 4;
                int result = LinuxSyscalls.getsockopt((int)fd, (int)level, (int)opt, handle.AddrOfPinnedObject(), ref optlen);
                if (result == -1)
                {
                    return (0, ErrnoToError("getsockopt"));
                }
                return (System.BitConverter.ToInt32(valueBytes, 0), null);
            }
            finally
            {
                handle.Free();
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptByte([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, byte value)
        {
            var valueBytes = new byte[] { value };
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(valueBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int result = LinuxSyscalls.setsockopt((int)fd, (int)level, (int)opt, handle.AddrOfPinnedObject(), 1);
                return result == -1 ? ErrnoToError("setsockopt") : null;
            }
            finally
            {
                handle.Free();
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptLinger([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, object? l)
        {
            if (l is GoLinger goLinger)
            {
                var native = new LinuxLinger { OnOff = (int)goLinger.Onoff, Linger = (int)goLinger.Linger };
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(native, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    return LinuxSyscalls.setsockopt((int)fd, (int)level, (int)opt, handle.AddrOfPinnedObject(), 8) == -1
                        ? ErrnoToError("setsockopt") : null;
                }
                finally
                {
                    handle.Free();
                }
            }
            return "setsockopt: invalid linger type";
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptIPMreq([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, object? mreq)
        {
            if (mreq is GoIPMreq goMreq)
            {
                var native = new LinuxIPMreq
                {
                    MultiAddr = (uint)(goMreq.Multiaddr[0] | (goMreq.Multiaddr[1] << 8) | (goMreq.Multiaddr[2] << 16) | (goMreq.Multiaddr[3] << 24)),
                    Interface = (uint)(goMreq.Interface[0] | (goMreq.Interface[1] << 8) | (goMreq.Interface[2] << 16) | (goMreq.Interface[3] << 24)),
                };
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(native, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    return LinuxSyscalls.setsockopt((int)fd, (int)level, (int)opt, handle.AddrOfPinnedObject(), 8) == -1
                        ? ErrnoToError("setsockopt") : null;
                }
                finally
                {
                    handle.Free();
                }
            }
            return "setsockopt: invalid ip_mreq type";
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptIPv6Mreq([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, object? mreq)
        {
            if (mreq is GoIPv6Mreq goMreq)
            {
                unsafe
                {
                    var native = new LinuxIPv6Mreq { Interface = (int)goMreq.Interface };
                    for (int i = 0; i < 16 && i < goMreq.Multiaddr.Len; i++)
                    {
                        native.MultiAddr[i] = goMreq.Multiaddr[i];
                    }
                    var handle = System.Runtime.InteropServices.GCHandle.Alloc(native, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        return LinuxSyscalls.setsockopt((int)fd, (int)level, (int)opt, handle.AddrOfPinnedObject(), 20) == -1
                            ? ErrnoToError("setsockopt") : null;
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
            }
            return "setsockopt: invalid ipv6_mreq type";
        }

        [GoFunc]
        [return: GoReturn("int", "syscall.Sockaddr", "error")]
        public static (long, object?, object?) Recvfrom([GoParam("int")] long fd, Slice<byte> p, [GoParam("int")] long flags)
        {
            var buffer = new byte[p.Len];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int bytesRead = LinuxSyscalls.recvfrom((int)fd, handle.AddrOfPinnedObject(), (System.UIntPtr)buffer.Length, (int)flags, System.IntPtr.Zero, System.IntPtr.Zero);
                if (bytesRead == -1)
                {
                    return (0, null, ErrnoToError("recvfrom"));
                }
                for (int i = 0; i < bytesRead; i++)
                {
                    p[i] = buffer[i];
                }
                return (bytesRead, null, null);
            }
            finally
            {
                handle.Free();
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Sendto([GoParam("int")] long fd, Slice<byte> p, [GoParam("int")] long flags, object? to)
        {
            var buffer = new byte[p.Len];
            for (int i = 0; i < p.Len; i++)
            {
                buffer[i] = p[i];
            }
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int bytesSent = LinuxSyscalls.sendto((int)fd, handle.AddrOfPinnedObject(), (System.UIntPtr)buffer.Length, (int)flags, System.IntPtr.Zero, 0);
                if (bytesSent == -1)
                {
                    return ErrnoToError("sendto");
                }
                return null;
            }
            finally
            {
                handle.Free();
            }
        }

        [GoFunc]
        [return: GoReturn("int", "int", "int", "Sockaddr", "error")]
        public static (long, long, long, object?, object?) Recvmsg([GoParam("int")] long fd, Slice<byte> p, Slice<byte> oob, [GoParam("int")] long flags)
        {
            var (bytesRead, _, readErr) = Recvfrom(fd, p, flags);
            return (bytesRead, 0, 0, null, readErr);
        }

        [GoFunc]
        [return: GoReturn("int", "int", "error")]
        public static (long, long, object?) SendmsgN([GoParam("int")] long fd, Slice<byte> p, Slice<byte> oob, object? to, [GoParam("int")] long flags)
        {
            var sendErr = Sendto(fd, p, flags, to);
            return (p.Len, 0, sendErr);
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Openat([GoParam("int")] long dirfd, string path, [GoParam("int")] long flags, [GoParam("uint32")] long mode)
        {
            int fd = LinuxSyscalls.openat((int)dirfd, path, (int)flags, (uint)mode);
            if (fd == -1)
            {
                return (0, ErrnoToError("openat"));
            }
            return (fd, null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fstatat([GoParam("int")] long dirfd, string path, object? stat, [GoParam("int")] long flags)
        {
            var nativeStat = new LinuxStat();
            int result = LinuxSyscalls.fstatat((int)dirfd, path, ref nativeStat, (int)flags);
            if (result == -1)
            {
                return ErrnoToError("fstatat");
            }
            PopulateGoStat(stat, ref nativeStat);
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unlinkat([GoParam("int")] long dirfd, string path, [GoParam("int")] long flags)
        {
            return LinuxSyscalls.unlinkat((int)dirfd, path, (int)flags) == -1 ? ErrnoToError("unlinkat") : null;
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Getdents([GoParam("int")] long fd, Slice<byte> buf)
        {
            return Read(fd, buf);
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Wait4([GoParam("int")] long pid, object? wstatus, [GoParam("int")] long options, object? rusage)
        {
            int status = 0;
            int result = LinuxSyscalls.waitpid((int)pid, ref status, (int)options);
            if (result == -1)
            {
                return (0, ErrnoToError("wait4"));
            }
            return (result, null);
        }

        [GoFunc]
        [return: GoReturn("int", "int", "error")]
        public static (long, long, object?) StartProcess(string argv0, Slice<string> argv, object? attr)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo(argv0)
                {
                    UseShellExecute = false,
                };
                for (int i = 1; i < argv.Len; i++)
                {
                    startInfo.ArgumentList.Add(argv[i]);
                }
                var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    return (0, 0, "failed to start process");
                }
                return (process.Id, 0, null);
            }
            catch (System.Exception ex)
            {
                return (0, 0, ex.Message);
            }
        }

        [GoFunc]
        public static long ByteSliceToString(Slice<byte> s)
        {
            // In Go, this converts a NUL-terminated byte slice to a string
            // Returns the length of the string (up to first NUL byte)
            for (int i = 0; i < s.Len; i++)
            {
                if (s[i] == 0)
                {
                    return i;
                }
            }
            return s.Len;
        }

        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) NetlinkRIB([GoParam("int")] long proto, [GoParam("int")] long family) => (default, null);

        [GoFunc]
        [return: GoReturn("[]NetlinkMessage", "error")]
        public static (Slice<object>, object?) ParseNetlinkMessage(Slice<byte> b) => (default, null);

        [GoFunc]
        [return: GoReturn("[]NetlinkRouteAttr", "error")]
        public static (Slice<object>, object?) ParseNetlinkRouteAttr(object? m) => (default, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Getrlimit([GoParam("int")] long resource, object? rlim)
        {
            var native = new LinuxRlimit();
            int result = LinuxSyscalls.getrlimit((int)resource, ref native);
            if (result == -1)
            {
                return ErrnoToError("getrlimit");
            }
            if (rlim is GoRlimit goRlimit)
            {
                goRlimit.Cur = (long)native.Cur;
                goRlimit.Max = (long)native.Max;
            }
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Setrlimit([GoParam("int")] long resource, object? rlim)
        {
            if (rlim is GoRlimit goRlimit)
            {
                var native = new LinuxRlimit { Cur = (ulong)goRlimit.Cur, Max = (ulong)goRlimit.Max };
                int result = LinuxSyscalls.setrlimit((int)resource, ref native);
                return result == -1 ? ErrnoToError("setrlimit") : null;
            }
            return "setrlimit: invalid rlimit type";
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) ForkExec(string argv0, Slice<string> argv, [GoParam("*ProcAttr")] object? attr)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo(argv0)
                {
                    UseShellExecute = false,
                };
                for (int i = 1; i < argv.Len; i++)
                {
                    startInfo.ArgumentList.Add(argv[i]);
                }
                var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                {
                    return (0, "failed to start process");
                }
                return (process.Id, null);
            }
            catch (System.Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Mkfifo(string path, [GoParam("uint32")] long mode)
            => LinuxSyscalls.mkfifo(path, (uint)mode) == -1 ? ErrnoToError("mkfifo") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Sysinfo([GoParam("*Sysinfo_t")] object? info)
        {
            if (info is GoSysinfo_t goSysinfo)
            {
                goSysinfo.Uptime = System.Environment.TickCount64 / 1000;
                goSysinfo.Totalram = (long)System.GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                goSysinfo.Freeram = goSysinfo.Totalram - System.GC.GetTotalMemory(false);
                goSysinfo.Procs = System.Environment.ProcessorCount;
            }
            return null;
        }

        // Ioctl constants
        [GoConst] public static readonly long SYS_IOCTL = 16;
        [GoConst] public static readonly long TIOCGWINSZ = 0x5413;
        [GoConst] public static readonly long TIOCSWINSZ = 0x5414;
        [GoConst] public static readonly long TIOCGPTN = 0x80045430;
        [GoConst] public static readonly long TIOCSPTLCK = 0x40045431;

        [GoConst] public static readonly long O_NOCTTY = 0x100;

        // File lock constants
        [GoConst] public static readonly long LOCK_SH = 1;
        [GoConst] public static readonly long LOCK_EX = 2;
        [GoConst] public static readonly long LOCK_NB = 4;
        [GoConst] public static readonly long LOCK_UN = 8;

        // Epoll constants
        [GoConst] public static readonly long EPOLLIN = 0x001;
        [GoConst] public static readonly long EPOLLOUT = 0x004;
        [GoConst] public static readonly long EPOLLRDHUP = 0x2000;
        [GoConst] public static readonly long EPOLLPRI = 0x002;
        [GoConst] public static readonly long EPOLLERR = 0x008;
        [GoConst] public static readonly long EPOLLHUP = 0x010;
        [GoConst] public static readonly long EPOLLONESHOT = 0x40000000;
        [GoConst] public static readonly long EPOLL_CTL_ADD = 1;
        [GoConst] public static readonly long EPOLL_CTL_DEL = 2;
        [GoConst] public static readonly long EPOLL_CTL_MOD = 3;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Flock(long fd, long how)
            => LinuxSyscalls.flock((int)fd, (int)how) == -1 ? ErrnoToError("flock") : null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) EpollCreate1(long flag)
        {
            int fd = LinuxSyscalls.epoll_create1((int)flag);
            if (fd == -1)
            {
                return (0, ErrnoToError("epoll_create1"));
            }
            return (fd, null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? EpollCtl(long epfd, long op, long fd, object? event_)
        {
            if (event_ is GoEpollEvent ev)
            {
                var nativeEvent = new LinuxEpollEvent { Events = (uint)ev.Events, Fd = (int)ev.Fd };
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(nativeEvent, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    int result = LinuxSyscalls.epoll_ctl((int)epfd, (int)op, (int)fd, handle.AddrOfPinnedObject());
                    return result == -1 ? ErrnoToError("epoll_ctl") : null;
                }
                finally
                {
                    handle.Free();
                }
            }
            int resultNull = LinuxSyscalls.epoll_ctl((int)epfd, (int)op, (int)fd, System.IntPtr.Zero);
            return resultNull == -1 ? ErrnoToError("epoll_ctl") : null;
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) EpollWait(long epfd, object? events, long msec)
        {
            // EpollWait needs an array of epoll_event structs — complex marshaling
            // For now, use the raw syscall with a small buffer
            var buffer = new LinuxEpollEvent[64];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int result = LinuxSyscalls.epoll_wait((int)epfd, handle.AddrOfPinnedObject(), buffer.Length, (int)msec);
                if (result == -1)
                {
                    return (0, ErrnoToError("epoll_wait"));
                }
                return (result, null);
            }
            finally
            {
                handle.Free();
            }
        }

        // Madvise constants
        [GoConst] public static readonly long MADV_RANDOM = 1;
        [GoConst] public static readonly long MADV_SEQUENTIAL = 2;
        [GoConst] public static readonly long MADV_WILLNEED = 3;

        // Terminal line discipline constants
        [GoConst] public static readonly long ISIG = 0x0001;
        [GoConst] public static readonly long ICRNL = 0x100;
        [GoConst] public static readonly long ICANON = 0x0002;
        [GoConst] public static readonly long ECHO = 0x0008;

        // UnixCredentials returns a socket control message encoding Ucred
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> UnixCredentials([GoParam("*Ucred")] GoUcred? ucred) => new Slice<byte>(System.Array.Empty<byte>());

        // Terminal ioctl constants
        [GoConst] public static readonly long TCGETS = 0x5401;
        [GoConst] public static readonly long TCSETS = 0x5402;

        // Errno constants
        [GoVar(Type = "Errno")] public static readonly object? EBADFD = 77L;

        // TCP constants
        [GoConst] public static readonly long TCP_DEFER_ACCEPT = 9;
        [GoConst] public static readonly long SOL_TCP = 6;

        // Syscall number constants
        [GoConst] public static readonly long SYS_WRITEV = 20;
        [GoConst] public static readonly long SYS_MADVISE = 28;
        [GoConst] public static readonly long SYS_EVENTFD2 = 290;

        // Message flags
        [GoConst] public static readonly long MSG_CTRUNC = 8;

        // Memory protection constants
        [GoConst] public static readonly long PROT_READ = 0x1;
        [GoConst] public static readonly long PROT_WRITE = 0x2;
        [GoConst] public static readonly long PROT_EXEC = 0x4;

        // Memory map constants
        [GoConst] public static readonly long MAP_SHARED = 0x01;
        [GoConst] public static readonly long MAP_PRIVATE = 0x02;
        [GoConst] public static readonly long MAP_ANONYMOUS = 0x20;

        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) Mmap(long fd, long offset, long length, long prot, long flags) => (default, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Munmap(Slice<byte> b)
        {
            // munmap requires the original pointer and length from mmap —
            // Slice<byte> doesn't track the mmap address. This is a real limitation.
            return null;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Statfs(string path, [GoParam("*syscall.Statfs_t")] object? stat)
        {
            // statfs struct is 120 bytes on x86_64
            var buffer = new byte[120];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int result = LinuxSyscalls.statfs(path, handle.AddrOfPinnedObject());
                if (result == -1)
                {
                    return ErrnoToError("statfs");
                }
                if (stat is GoStatfs_t goStatfs)
                {
                    goStatfs.Type = System.BitConverter.ToInt64(buffer, 0);
                    goStatfs.Bsize = System.BitConverter.ToInt64(buffer, 8);
                    goStatfs.Blocks = (long)System.BitConverter.ToUInt64(buffer, 16);
                    goStatfs.Bfree = (long)System.BitConverter.ToUInt64(buffer, 24);
                    goStatfs.Bavail = (long)System.BitConverter.ToUInt64(buffer, 32);
                    goStatfs.Files = (long)System.BitConverter.ToUInt64(buffer, 40);
                    goStatfs.Ffree = (long)System.BitConverter.ToUInt64(buffer, 48);
                }
                return null;
            }
            finally
            {
                handle.Free();
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fdatasync(long fd)
            => LinuxSyscalls.fdatasync((int)fd) == -1 ? ErrnoToError("fdatasync") : null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Madvise(Slice<byte> b, long advice)
        {
            var buffer = b.AsSpan().ToArray();
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                int result = LinuxSyscalls.madvise(handle.AddrOfPinnedObject(), (System.UIntPtr)buffer.Length, (int)advice);
                return result == -1 ? ErrnoToError("madvise") : null;
            }
            finally
            {
                handle.Free();
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fallocate(long fd, long mode, long off, long len)
            => LinuxSyscalls.fallocate((int)fd, (int)mode, off, len) == -1 ? ErrnoToError("fallocate") : null;

        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) UnixRights(params long[] fds) => (default, null);

        [GoFunc]
        [return: GoReturn("[]int", "error")]
        public static (Slice<long>, object?) ParseUnixRights(object? m) => (default, null);

        [GoFunc]
        [return: GoReturn("[]SocketControlMessage", "error")]
        public static (object?, object?) ParseSocketControlMessage(Slice<byte> b) => (null, null);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Sendfile([GoParam("int")] long outfd, [GoParam("int")] long infd, object? offset, [GoParam("int")] long count)
        {
            return (0, "syscall: Sendfile not supported");
        }
    }

    [GoType("struct", Name = "Termios", Package = "syscall")]
    public struct GoTermios
    {
        [GoField] public long Iflag;
        [GoField] public long Oflag;
        [GoField] public long Cflag;
        [GoField] public long Lflag;
    }

    // syscall.Ucred struct
    [GoType("struct", Name = "Ucred", Package = "syscall")]
    public class GoUcred
    {
        [GoField(Name = "Pid", Type = "int32")] public int Pid;
        [GoField(Name = "Uid", Type = "uint32")] public uint Uid;
        [GoField(Name = "Gid", Type = "uint32")] public uint Gid;
    }

    internal static class LinuxSyscalls
    {
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getuid")]
        internal static extern uint getuid();

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getgid")]
        internal static extern uint getgid();

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "geteuid")]
        internal static extern uint geteuid();

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getegid")]
        internal static extern uint getegid();

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getppid")]
        internal static extern int getppid();

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int socket(int domain, int type, int protocol);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int close(int fd);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int read(int fd, System.IntPtr buf, System.UIntPtr count);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int write(int fd, System.IntPtr buf, System.UIntPtr count);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern long lseek(int fd, long offset, int whence);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int open([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, int flags, uint mode);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int mkdir([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, uint mode);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int rmdir([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int unlink([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int rename([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string oldpath, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string newpath);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int link([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string oldpath, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string newpath);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int symlink([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string target, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string linkpath);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern long readlink([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, byte[] buf, System.UIntPtr bufsiz);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int chmod([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, uint mode);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int chown([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, uint owner, uint group);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fchmod(int fd, uint mode);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fchown(int fd, uint owner, uint group);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int ftruncate(int fd, long length);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int truncate([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, long length);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int chdir([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int pipe(int[] pipefd);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int pipe2(int[] pipefd, int flags);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int kill(int pid, int sig);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fsync(int fd);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fdatasync(int fd);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int flock(int fd, int operation);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fcntl(int fd, int cmd, int arg);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int listen(int sockfd, int backlog);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int setsockopt(int sockfd, int level, int optname, System.IntPtr optval, int optlen);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int getsockopt(int sockfd, int level, int optname, System.IntPtr optval, ref int optlen);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int shutdown(int sockfd, int how);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int openat(int dirfd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, int flags, uint mode);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int unlinkat(int dirfd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, int flags);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern long syscall(long number, long arg1, long arg2, long arg3);

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
        internal static extern long syscall6(long number, long arg1, long arg2, long arg3, long arg4, long arg5, long arg6);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int mkfifo([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, uint mode);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int epoll_create1(int flags);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fallocate(int fd, int mode, long offset, long len);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int waitpid(int pid, ref int status, int options);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int recvfrom(int sockfd, System.IntPtr buf, System.UIntPtr len, int flags, System.IntPtr srcAddr, System.IntPtr addrlen);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int sendto(int sockfd, System.IntPtr buf, System.UIntPtr len, int flags, System.IntPtr destAddr, int addrlen);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int epoll_ctl(int epfd, int op, int fd, System.IntPtr eventPtr);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int epoll_wait(int epfd, System.IntPtr events, int maxevents, int timeout);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int accept4(int sockfd, System.IntPtr addr, System.IntPtr addrlen, int flags);

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "__xstat", SetLastError = true)]
        internal static extern int stat_wrapper(int ver, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, ref LinuxStat buf);

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "__lxstat", SetLastError = true)]
        internal static extern int lstat_wrapper(int ver, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, ref LinuxStat buf);

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "__fxstat", SetLastError = true)]
        internal static extern int fstat_wrapper(int ver, int fd, ref LinuxStat buf);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int stat([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, ref LinuxStat buf);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int lstat([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, ref LinuxStat buf);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fstat(int fd, ref LinuxStat buf);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int bind(int sockfd, ref LinuxSockaddrIn addr, int addrlen);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int connect(int sockfd, ref LinuxSockaddrIn addr, int addrlen);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int getrusage(int who, ref LinuxRusage usage);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int getrlimit(int resource, ref LinuxRlimit rlim);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int setrlimit(int resource, ref LinuxRlimit rlim);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int uname(ref LinuxUtsname buf);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int utimensat(int dirfd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, ref LinuxTimespec times, int flags);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fstatat(int dirfd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, ref LinuxStat buf, int flags);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int statfs([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string path, System.IntPtr buf);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int poll(System.IntPtr fds, uint nfds, int timeout);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int madvise(System.IntPtr addr, System.UIntPtr length, int advice);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    internal struct LinuxEpollEvent
    {
        public uint Events;
        public int Fd;
        public int Pad;
    }

    // Linux x86_64 struct stat (144 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 144)]
    internal struct LinuxStat
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public ulong Dev;
        [System.Runtime.InteropServices.FieldOffset(8)] public ulong Ino;
        [System.Runtime.InteropServices.FieldOffset(16)] public ulong Nlink;
        [System.Runtime.InteropServices.FieldOffset(24)] public uint Mode;
        [System.Runtime.InteropServices.FieldOffset(28)] public uint Uid;
        [System.Runtime.InteropServices.FieldOffset(32)] public uint Gid;
        [System.Runtime.InteropServices.FieldOffset(40)] public ulong Rdev;
        [System.Runtime.InteropServices.FieldOffset(48)] public long Size;
        [System.Runtime.InteropServices.FieldOffset(56)] public long Blksize;
        [System.Runtime.InteropServices.FieldOffset(64)] public long Blocks;
        [System.Runtime.InteropServices.FieldOffset(72)] public long Atim_sec;
        [System.Runtime.InteropServices.FieldOffset(80)] public long Atim_nsec;
        [System.Runtime.InteropServices.FieldOffset(88)] public long Mtim_sec;
        [System.Runtime.InteropServices.FieldOffset(96)] public long Mtim_nsec;
        [System.Runtime.InteropServices.FieldOffset(104)] public long Ctim_sec;
        [System.Runtime.InteropServices.FieldOffset(112)] public long Ctim_nsec;
    }

    // Linux sockaddr_in (16 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 16)]
    internal struct LinuxSockaddrIn
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public ushort Family;
        [System.Runtime.InteropServices.FieldOffset(2)] public ushort Port;
        [System.Runtime.InteropServices.FieldOffset(4)] public uint Addr;
    }

    // Linux sockaddr_in6 (28 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal unsafe struct LinuxSockaddrIn6
    {
        public ushort Family;
        public ushort Port;
        public uint FlowInfo;
        public fixed byte Addr[16];
        public uint ScopeId;
    }

    // Linux linger (8 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LinuxLinger
    {
        public int OnOff;
        public int Linger;
    }

    // Linux ip_mreq (8 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LinuxIPMreq
    {
        public uint MultiAddr;
        public uint Interface;
    }

    // Linux ipv6_mreq (20 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LinuxIPv6Mreq
    {
        public unsafe fixed byte MultiAddr[16];
        public int Interface;
    }

    // Linux timespec (16 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LinuxTimespec
    {
        public long Sec;
        public long Nsec;
    }

    // Linux rusage (144 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LinuxRusage
    {
        public LinuxTimeval UserTime;
        public LinuxTimeval SystemTime;
        public long MaxRss;
        public long Ixrss;
        public long Idrss;
        public long Isrss;
        public long MinFlt;
        public long MajFlt;
        public long Nswap;
        public long InBlock;
        public long OutBlock;
        public long Msgsnd;
        public long Msgrcv;
        public long Nsignals;
        public long Nvcsw;
        public long Nivcsw;
    }

    // Linux timeval (16 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LinuxTimeval
    {
        public long Sec;
        public long Usec;
    }

    // Linux rlimit (16 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LinuxRlimit
    {
        public ulong Cur;
        public ulong Max;
    }

    // Linux utsname (390 bytes = 65*6)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
    internal unsafe struct LinuxUtsname
    {
        public fixed byte Sysname[65];
        public fixed byte Nodename[65];
        public fixed byte Release[65];
        public fixed byte Version[65];
        public fixed byte Machine[65];
        public fixed byte Domainname[65];
    }

    // Linux pollfd (8 bytes)
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LinuxPollfd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }
}
