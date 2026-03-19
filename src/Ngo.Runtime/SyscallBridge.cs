using System;
using System.Runtime.InteropServices;

namespace Ngo.Runtime
{
    /// <summary>
    /// Bridges Go's syscall.Syscall/RawSyscall to the system's libc via P/Invoke.
    /// No custom C code — calls the operating system's existing libc directly.
    ///
    /// Go's syscall package calls Syscall(trap, a1, a2, a3) with a syscall number.
    /// We dispatch based on the trap number to the corresponding libc function.
    /// </summary>
    public static class SyscallBridge
    {
        // P/Invoke to libc functions
        private const string Libc = "libc";

        [DllImport(Libc, SetLastError = true)] static extern long read(int fd, IntPtr buf, long count);
        [DllImport(Libc, SetLastError = true)] static extern long write(int fd, IntPtr buf, long count);
        [DllImport(Libc, SetLastError = true)] static extern int open([MarshalAs(UnmanagedType.LPStr)] string path, int flags, int mode);
        [DllImport(Libc, SetLastError = true)] static extern int close(int fd);
        [DllImport(Libc, SetLastError = true)] static extern int stat([MarshalAs(UnmanagedType.LPStr)] string path, IntPtr buf);
        [DllImport(Libc, SetLastError = true)] static extern int fstat(int fd, IntPtr buf);
        [DllImport(Libc, SetLastError = true)] static extern int lstat([MarshalAs(UnmanagedType.LPStr)] string path, IntPtr buf);
        [DllImport(Libc, SetLastError = true)] static extern long lseek(int fd, long offset, int whence);
        [DllImport(Libc, SetLastError = true)] static extern IntPtr mmap(IntPtr addr, long length, int prot, int flags, int fd, long offset);
        [DllImport(Libc, SetLastError = true)] static extern int munmap(IntPtr addr, long length);
        [DllImport(Libc, SetLastError = true)] static extern int ioctl(int fd, long request, long arg);
        [DllImport(Libc, SetLastError = true)] static extern int pipe(IntPtr pipefd);
        [DllImport(Libc, SetLastError = true)] static extern int dup(int oldfd);
        [DllImport(Libc, SetLastError = true)] static extern int dup2(int oldfd, int newfd);
        [DllImport(Libc, SetLastError = true)] static extern int socket(int domain, int type, int protocol);
        [DllImport(Libc, SetLastError = true)] static extern int connect(int sockfd, IntPtr addr, int addrlen);
        [DllImport(Libc, SetLastError = true)] static extern int bind(int sockfd, IntPtr addr, int addrlen);
        [DllImport(Libc, SetLastError = true)] static extern int listen(int sockfd, int backlog);
        [DllImport(Libc, SetLastError = true)] static extern int accept(int sockfd, IntPtr addr, IntPtr addrlen);
        [DllImport(Libc, SetLastError = true)] static extern int accept4(int sockfd, IntPtr addr, IntPtr addrlen, int flags);
        [DllImport(Libc, SetLastError = true)] static extern int getpid();
        [DllImport(Libc, SetLastError = true)] static extern int getppid();
        [DllImport(Libc, SetLastError = true)] static extern int getuid();
        [DllImport(Libc, SetLastError = true)] static extern int getgid();
        [DllImport(Libc, SetLastError = true)] static extern int geteuid();
        [DllImport(Libc, SetLastError = true)] static extern int getegid();
        [DllImport(Libc, SetLastError = true)] static extern int kill(int pid, int sig);
        [DllImport(Libc, SetLastError = true)] static extern int fcntl(int fd, int cmd, long arg);
        [DllImport(Libc, SetLastError = true)] static extern int ftruncate(int fd, long length);
        [DllImport(Libc, SetLastError = true)] static extern IntPtr getcwd(IntPtr buf, long size);
        [DllImport(Libc, SetLastError = true)] static extern int chdir([MarshalAs(UnmanagedType.LPStr)] string path);
        [DllImport(Libc, SetLastError = true)] static extern int rename([MarshalAs(UnmanagedType.LPStr)] string oldp, [MarshalAs(UnmanagedType.LPStr)] string newp);
        [DllImport(Libc, SetLastError = true)] static extern int mkdir([MarshalAs(UnmanagedType.LPStr)] string path, int mode);
        [DllImport(Libc, SetLastError = true)] static extern int rmdir([MarshalAs(UnmanagedType.LPStr)] string path);
        [DllImport(Libc, SetLastError = true)] static extern int unlink([MarshalAs(UnmanagedType.LPStr)] string path);
        [DllImport(Libc, SetLastError = true)] static extern int link([MarshalAs(UnmanagedType.LPStr)] string oldp, [MarshalAs(UnmanagedType.LPStr)] string newp);
        [DllImport(Libc, SetLastError = true)] static extern int symlink([MarshalAs(UnmanagedType.LPStr)] string target, [MarshalAs(UnmanagedType.LPStr)] string linkpath);
        [DllImport(Libc, SetLastError = true)] static extern long readlink([MarshalAs(UnmanagedType.LPStr)] string path, IntPtr buf, long bufsiz);
        [DllImport(Libc, SetLastError = true)] static extern int chmod([MarshalAs(UnmanagedType.LPStr)] string path, int mode);
        [DllImport(Libc, SetLastError = true)] static extern int chown([MarshalAs(UnmanagedType.LPStr)] string path, int owner, int group);
        [DllImport(Libc, SetLastError = true)] static extern int pipe2(IntPtr pipefd, int flags);
        [DllImport(Libc, SetLastError = true)] static extern long sendto(int sockfd, IntPtr buf, long len, int flags, IntPtr dest_addr, int addrlen);
        [DllImport(Libc, SetLastError = true)] static extern long recvfrom(int sockfd, IntPtr buf, long len, int flags, IntPtr src_addr, IntPtr addrlen);
        [DllImport(Libc, SetLastError = true)] static extern int setsockopt(int sockfd, int level, int optname, IntPtr optval, int optlen);
        [DllImport(Libc, SetLastError = true)] static extern int getsockopt(int sockfd, int level, int optname, IntPtr optval, IntPtr optlen);
        [DllImport(Libc, SetLastError = true)] static extern int getsockname(int sockfd, IntPtr addr, IntPtr addrlen);
        [DllImport(Libc, SetLastError = true)] static extern int getpeername(int sockfd, IntPtr addr, IntPtr addrlen);
        [DllImport(Libc, SetLastError = true)] static extern int poll(IntPtr fds, long nfds, int timeout);
        [DllImport(Libc, SetLastError = true)] static extern int openat(int dirfd, [MarshalAs(UnmanagedType.LPStr)] string pathname, int flags, int mode);
        [DllImport(Libc, SetLastError = true)] static extern int unlinkat(int dirfd, [MarshalAs(UnmanagedType.LPStr)] string pathname, int flags);
        [DllImport(Libc, SetLastError = true)] static extern long fork();
        [DllImport(Libc, SetLastError = true)] static extern int execve([MarshalAs(UnmanagedType.LPStr)] string filename, IntPtr argv, IntPtr envp);
        [DllImport(Libc, SetLastError = true)] static extern int wait4(int pid, IntPtr wstatus, int options, IntPtr rusage);

        // Linux syscall numbers (amd64)
        const int SYS_READ = 0, SYS_WRITE = 1, SYS_OPEN = 2, SYS_CLOSE = 3;
        const int SYS_STAT = 4, SYS_FSTAT = 5, SYS_LSTAT = 6, SYS_POLL = 7;
        const int SYS_LSEEK = 8, SYS_MMAP = 9, SYS_MUNMAP = 11, SYS_IOCTL = 16;
        const int SYS_PIPE = 22, SYS_DUP = 32, SYS_DUP2 = 33, SYS_GETPID = 39;
        const int SYS_SOCKET = 41, SYS_CONNECT = 42, SYS_ACCEPT = 43;
        const int SYS_SENDTO = 44, SYS_RECVFROM = 45;
        const int SYS_BIND = 49, SYS_LISTEN = 50;
        const int SYS_GETSOCKNAME = 51, SYS_GETPEERNAME = 52;
        const int SYS_SETSOCKOPT = 54, SYS_GETSOCKOPT = 55;
        const int SYS_FORK = 57, SYS_EXECVE = 59, SYS_EXIT = 60, SYS_WAIT4 = 61;
        const int SYS_KILL = 62, SYS_FCNTL = 72, SYS_FTRUNCATE = 77;
        const int SYS_GETCWD = 79, SYS_CHDIR = 80, SYS_RENAME = 82;
        const int SYS_MKDIR = 83, SYS_RMDIR = 84, SYS_LINK = 86, SYS_UNLINK = 87;
        const int SYS_SYMLINK = 88, SYS_READLINK = 89, SYS_CHMOD = 90, SYS_CHOWN = 92;
        const int SYS_GETUID = 102, SYS_GETGID = 104;
        const int SYS_GETEUID = 107, SYS_GETEGID = 108, SYS_GETPPID = 110;
        const int SYS_OPENAT = 257, SYS_UNLINKAT = 263;
        const int SYS_ACCEPT4 = 288, SYS_PIPE2 = 293;

        /// <summary>
        /// Dispatch a 3-argument syscall to the corresponding libc function.
        /// Returns (r1, r2, errno) matching Go's Syscall signature.
        /// </summary>
        public static (long, long, long) Syscall3(long trap, long a1, long a2, long a3)
        {
            long r;
            try
            {
                r = trap switch
                {
                    SYS_READ => read((int)a1, (IntPtr)a2, a3),
                    SYS_WRITE => write((int)a1, (IntPtr)a2, a3),
                    SYS_OPEN => open(Marshal.PtrToStringAnsi((IntPtr)a1)!, (int)a2, (int)a3),
                    SYS_CLOSE => close((int)a1),
                    SYS_STAT => stat(Marshal.PtrToStringAnsi((IntPtr)a1)!, (IntPtr)a2),
                    SYS_FSTAT => fstat((int)a1, (IntPtr)a2),
                    SYS_LSTAT => lstat(Marshal.PtrToStringAnsi((IntPtr)a1)!, (IntPtr)a2),
                    SYS_POLL => poll((IntPtr)a1, a2, (int)a3),
                    SYS_LSEEK => lseek((int)a1, a2, (int)a3),
                    SYS_MUNMAP => munmap((IntPtr)a1, a2),
                    SYS_IOCTL => ioctl((int)a1, a2, a3),
                    SYS_PIPE => pipe((IntPtr)a1),
                    SYS_DUP => dup((int)a1),
                    SYS_DUP2 => dup2((int)a1, (int)a2),
                    SYS_GETPID => getpid(),
                    SYS_SOCKET => socket((int)a1, (int)a2, (int)a3),
                    SYS_CONNECT => connect((int)a1, (IntPtr)a2, (int)a3),
                    SYS_ACCEPT => accept((int)a1, (IntPtr)a2, (IntPtr)a3),
                    SYS_BIND => bind((int)a1, (IntPtr)a2, (int)a3),
                    SYS_LISTEN => listen((int)a1, (int)a2),
                    SYS_GETSOCKNAME => getsockname((int)a1, (IntPtr)a2, (IntPtr)a3),
                    SYS_GETPEERNAME => getpeername((int)a1, (IntPtr)a2, (IntPtr)a3),
                    SYS_FORK => fork(),
                    SYS_EXIT => ExitNoReturn((int)a1),
                    SYS_KILL => kill((int)a1, (int)a2),
                    SYS_FCNTL => fcntl((int)a1, (int)a2, a3),
                    SYS_FTRUNCATE => ftruncate((int)a1, a2),
                    SYS_GETCWD => (long)getcwd((IntPtr)a1, a2),
                    SYS_CHDIR => chdir(Marshal.PtrToStringAnsi((IntPtr)a1)!),
                    SYS_RENAME => rename(Marshal.PtrToStringAnsi((IntPtr)a1)!, Marshal.PtrToStringAnsi((IntPtr)a2)!),
                    SYS_MKDIR => mkdir(Marshal.PtrToStringAnsi((IntPtr)a1)!, (int)a2),
                    SYS_RMDIR => rmdir(Marshal.PtrToStringAnsi((IntPtr)a1)!),
                    SYS_LINK => link(Marshal.PtrToStringAnsi((IntPtr)a1)!, Marshal.PtrToStringAnsi((IntPtr)a2)!),
                    SYS_UNLINK => unlink(Marshal.PtrToStringAnsi((IntPtr)a1)!),
                    SYS_SYMLINK => symlink(Marshal.PtrToStringAnsi((IntPtr)a1)!, Marshal.PtrToStringAnsi((IntPtr)a2)!),
                    SYS_READLINK => readlink(Marshal.PtrToStringAnsi((IntPtr)a1)!, (IntPtr)a2, a3),
                    SYS_CHMOD => chmod(Marshal.PtrToStringAnsi((IntPtr)a1)!, (int)a2),
                    SYS_CHOWN => chown(Marshal.PtrToStringAnsi((IntPtr)a1)!, (int)a2, (int)a3),
                    SYS_GETUID => getuid(),
                    SYS_GETGID => getgid(),
                    SYS_GETEUID => geteuid(),
                    SYS_GETEGID => getegid(),
                    SYS_GETPPID => getppid(),
                    SYS_PIPE2 => pipe2((IntPtr)a1, (int)a2),
                    SYS_OPENAT => openat((int)a1, Marshal.PtrToStringAnsi((IntPtr)a2)!, (int)a3, 0),
                    SYS_UNLINKAT => unlinkat((int)a1, Marshal.PtrToStringAnsi((IntPtr)a2)!, (int)a3),
                    _ => -1, // unsupported syscall
                };
            }
            catch
            {
                return (-1, 0, 38); // ENOSYS
            }

            if (r < 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                return (-1, 0, errno == 0 ? 38 : errno);
            }
            return (r, 0, 0);
        }

        /// <summary>
        /// Dispatch a 6-argument syscall to the corresponding libc function.
        /// </summary>
        public static (long, long, long) Syscall6(long trap, long a1, long a2, long a3,
            long a4, long a5, long a6)
        {
            long r;
            try
            {
                r = trap switch
                {
                    SYS_MMAP => (long)mmap((IntPtr)a1, a2, (int)a3, (int)a4, (int)a5, a6),
                    SYS_SENDTO => sendto((int)a1, (IntPtr)a2, a3, (int)a4, (IntPtr)a5, (int)a6),
                    SYS_RECVFROM => recvfrom((int)a1, (IntPtr)a2, a3, (int)a4, (IntPtr)a5, (IntPtr)a6),
                    SYS_SETSOCKOPT => setsockopt((int)a1, (int)a2, (int)a3, (IntPtr)a4, (int)a5),
                    SYS_GETSOCKOPT => getsockopt((int)a1, (int)a2, (int)a3, (IntPtr)a4, (IntPtr)a5),
                    SYS_WAIT4 => wait4((int)a1, (IntPtr)a2, (int)a3, (IntPtr)a4),
                    SYS_ACCEPT4 => accept4((int)a1, (IntPtr)a2, (IntPtr)a3, (int)a4),
                    SYS_OPENAT => openat((int)a1, Marshal.PtrToStringAnsi((IntPtr)a2)!, (int)a3, (int)a4),
                    _ => Syscall3(trap, a1, a2, a3).Item1, // fall through to 3-arg
                };
            }
            catch
            {
                return (-1, 0, 38);
            }

            if (r < 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                return (-1, 0, errno == 0 ? 38 : errno);
            }
            return (r, 0, 0);
        }

        private static long ExitNoReturn(int code)
        {
            Environment.Exit(code);
            return 0; // unreachable
        }
    }
}
