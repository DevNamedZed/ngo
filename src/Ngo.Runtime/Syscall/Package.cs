using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Syscall
{
    [GoPackage("syscall")]
    public static class Package
    {
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
        [return: GoReturn("int", "error")]
        public static (long, object?) Close([GoParam("int")] long fd) => (0, null);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Read([GoParam("int")] long fd, Slice<byte> p) => (0, null);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Write([GoParam("int")] long fd, Slice<byte> p) => (0, null);

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
            // Not easily doable in .NET, stub
        }

        [GoFunc]
        public static long Getuid() => 0;
        [GoFunc]
        public static long Getgid() => 0;
        [GoFunc]
        public static long Geteuid() => 0;
        [GoFunc]
        public static long Getegid() => 0;
        [GoFunc]
        public static long Getpid() => System.Environment.ProcessId;
        [GoFunc]
        public static long Getppid() => 0;

        [GoFunc]
        [return: GoReturn("[]int", "error")]
        public static (Slice<long>, object?) Getgroups() => (new Slice<long>(System.Array.Empty<long>()), null);

        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, object?) Getwd() => (System.Environment.CurrentDirectory, null);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Socket([GoParam("int")] long domain, [GoParam("int")] long typ, [GoParam("int")] long proto) => (0, "not supported");

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fchmod([GoParam("int")] long fd, [GoParam("uint32")] long mode) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fchown([GoParam("int")] long fd, [GoParam("int")] long uid, [GoParam("int")] long gid) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Ftruncate([GoParam("int")] long fd, long length) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chmod(string path, [GoParam("uint32")] long mode) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chown(string path, [GoParam("int")] long uid, [GoParam("int")] long gid) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Lchown(string path, [GoParam("int")] long uid, [GoParam("int")] long gid) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Mkdir(string path, [GoParam("uint32")] long perm) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Rmdir(string path) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unlink(string path) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Rename(string oldpath, string newpath) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Link(string oldpath, string newpath) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Symlink(string oldname, string newname) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Readlink(string path, Slice<byte> buf) => (0, null);

        [GoFunc]
        [return: GoReturn("int64", "error")]
        public static (long, object?) Seek([GoParam("int")] long fd, long offset, [GoParam("int")] long whence) => (0, null);

        [GoFunc]
        [return: GoReturn("int", "int", "[]string")]
        public static (long, long, Slice<string>) ParseDirent(Slice<byte> buf, [GoParam("int")] long max, Slice<string> names) => (0, 0, names);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) ReadDirent([GoParam("int")] long fd, Slice<byte> buf) => (0, null);

        [GoFunc]
        public static long Umask([GoParam("int")] long mask) => 0;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Chdir(string path) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Open(string path, [GoParam("int")] long mode, [GoParam("uint32")] long perm) => (0, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Stat(string path, object? stat) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Lstat(string path, object? stat) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fstat([GoParam("int")] long fd, object? stat) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Kill([GoParam("int")] long pid, object? sig) => null;

        [GoFunc]
        public static long Getpagesize() => 4096;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Getrusage([GoParam("int")] long who, [GoParam("*Rusage")] object? rusage) => null;

        [GoConst] public static readonly long RUSAGE_SELF = 0;
        [GoConst] public static readonly long RUSAGE_CHILDREN = -1;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Pipe(Slice<long> p) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Pipe2(Slice<long> p, [GoParam("int")] long flags) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Dup([GoParam("int")] long oldfd) => (oldfd, null);

        [GoFunc]
        public static void Exit([GoParam("int")] long code) => System.Environment.Exit((int)code);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetNonblock([GoParam("int")] long fd, bool nonblocking) => null;

        [GoFunc]
        public static void CloseOnExec([GoParam("int")] long fd) { }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Truncate(string path, long length) => null;

        [GoFunc]
        [return: GoReturn("Timespec")]
        public static GoTimespec NsecToTimespec(long nsec)
        {
            return new GoTimespec { Sec = nsec / 1000000000, Nsec = nsec % 1000000000 };
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? UtimesNano(string path, Slice<GoTimespec> ts) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Uname(object? buf) => null;

        [GoFunc]
        [return: GoReturn("int", "uintptr", "Errno")]
        public static (long, long, object?) Syscall(long trap, long a1, long a2, long a3) => (0, 0, null);

        [GoFunc]
        [return: GoReturn("int", "uintptr", "Errno")]
        public static (long, long, object?) Syscall6(long trap, long a1, long a2, long a3, long a4, long a5, long a6) => (0, 0, null);

        [GoFunc]
        [return: GoReturn("int", "uintptr", "Errno")]
        public static (long, long, object?) RawSyscall(long trap, long a1, long a2, long a3) => (0, 0, null);

        [GoFunc]
        [return: GoReturn("int", "uintptr", "Errno")]
        public static (long, long, object?) RawSyscall6(long trap, long a1, long a2, long a3, long a4, long a5, long a6) => (0, 0, null);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Fcntl([GoParam("int")] long fd, [GoParam("int")] long cmd, [GoParam("int")] long arg) => (0, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Bind([GoParam("int")] long fd, object? sa) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Listen([GoParam("int")] long fd, [GoParam("int")] long backlog) => null;

        [GoFunc]
        [return: GoReturn("int", "Sockaddr", "error")]
        public static (long, object?, object?) Accept([GoParam("int")] long fd) => (0, null, null);

        [GoFunc]
        [return: GoReturn("int", "Sockaddr", "error")]
        public static (long, object?, object?) Accept4([GoParam("int")] long fd, [GoParam("int")] long flags) => (0, null, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Connect([GoParam("int")] long fd, object? sa) => null;

        [GoFunc]
        [return: GoReturn("Sockaddr", "error")]
        public static (object?, object?) Getsockname([GoParam("int")] long fd) => (null, null);

        [GoFunc]
        [return: GoReturn("Sockaddr", "error")]
        public static (object?, object?) Getpeername([GoParam("int")] long fd) => (null, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptInt([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, [GoParam("int")] long value) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) GetsockoptInt([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt) => (0, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptByte([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, byte value) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptLinger([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, object? l) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptIPMreq([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, object? mreq) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? SetsockoptIPv6Mreq([GoParam("int")] long fd, [GoParam("int")] long level, [GoParam("int")] long opt, object? mreq) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Recvfrom([GoParam("int")] long fd, Slice<byte> p, [GoParam("int")] long flags) => (0, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Sendto([GoParam("int")] long fd, Slice<byte> p, [GoParam("int")] long flags, object? to) => null;

        [GoFunc]
        [return: GoReturn("int", "int", "int", "Sockaddr", "error")]
        public static (long, long, long, object?, object?) Recvmsg([GoParam("int")] long fd, Slice<byte> p, Slice<byte> oob, [GoParam("int")] long flags) => (0, 0, 0, null, null);

        [GoFunc]
        [return: GoReturn("int", "int", "error")]
        public static (long, long, object?) SendmsgN([GoParam("int")] long fd, Slice<byte> p, Slice<byte> oob, object? to, [GoParam("int")] long flags) => (0, 0, null);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Openat([GoParam("int")] long dirfd, string path, [GoParam("int")] long flags, [GoParam("uint32")] long mode) => (0, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fstatat([GoParam("int")] long dirfd, string path, object? stat, [GoParam("int")] long flags) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unlinkat([GoParam("int")] long dirfd, string path, [GoParam("int")] long flags) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Getdents([GoParam("int")] long fd, Slice<byte> buf) => (0, null);

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Wait4([GoParam("int")] long pid, object? wstatus, [GoParam("int")] long options, object? rusage) => (0, null);

        [GoFunc]
        [return: GoReturn("int", "int", "error")]
        public static (long, long, object?) StartProcess(string argv0, Slice<string> argv, object? attr) => (0, 0, "not supported");

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
        public static object? Getrlimit([GoParam("int")] long resource, object? rlim) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Setrlimit([GoParam("int")] long resource, object? rlim) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) ForkExec(string argv0, Slice<string> argv, [GoParam("*ProcAttr")] object? attr) => (0, "not supported");

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Mkfifo(string path, [GoParam("uint32")] long mode) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Sysinfo([GoParam("*Sysinfo_t")] object? info) => null;

        // Ioctl constants
        [GoConst] public static readonly long SYS_IOCTL = 16;
        [GoConst] public static readonly long TIOCGWINSZ = 0x5413;
        [GoConst] public static readonly long TIOCSWINSZ = 0x5414;
        [GoConst] public static readonly long TIOCGPTN = 0x80045430;
        [GoConst] public static readonly long TIOCSPTLCK = 0x40045431;

        [GoConst] public static readonly long O_NOCTTY = 0x100;

        // Epoll constants
        [GoConst] public static readonly long EPOLLIN = 0x001;
        [GoConst] public static readonly long EPOLLOUT = 0x004;
        [GoConst] public static readonly long EPOLLRDHUP = 0x2000;
        [GoConst] public static readonly long EPOLLPRI = 0x002;
        [GoConst] public static readonly long EPOLLERR = 0x008;
        [GoConst] public static readonly long EPOLLHUP = 0x010;
        [GoConst] public static readonly long EPOLL_CTL_ADD = 1;
        [GoConst] public static readonly long EPOLL_CTL_DEL = 2;
        [GoConst] public static readonly long EPOLL_CTL_MOD = 3;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Flock(long fd, long how) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) EpollCreate1(long flag) => (0, null);

        [GoFunc]
        [return: GoReturn("error")]
        public static object? EpollCtl(long epfd, long op, long fd, object? event_) => null;

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) EpollWait(long epfd, object? events, long msec) => (0, null);

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
        public static object? Munmap(Slice<byte> b) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Statfs(string path, [GoParam("*syscall.Statfs_t")] object? stat) => null;
    }
}
