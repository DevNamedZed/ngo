using System;
using System.Security.Cryptography;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Syscall.Unix
{
    [GoPackage("internal/syscall/unix")]
    public static class Package
    {
        // Constants
        [GoConst] public static readonly long UTIME_OMIT = -1;
        [GoConst] public static readonly long AT_REMOVEDIR = 0x200;
        [GoConst] public static readonly long AT_SYMLINK_NOFOLLOW = 0x100;
        [GoConst] public static readonly long AT_FDCWD = -100;
        [GoConst] public static readonly long X_OK = 1;
        [GoConst] public static readonly long W_OK = 2;
        [GoConst] public static readonly long R_OK = 4;

        [GoVar(Type = "Errno")]
        public static readonly object? NoFollowErrno = 40L;

        [GoFunc]
        [return: GoReturn("uintptr", "error")]
        public static (long, object?) PidFDOpen([GoParam("int")] long pid, [GoParam("int")] long flags)
        {
            int result = LinuxUnixSyscalls.pidfd_open((int)pid, (uint)flags);
            if (result == -1)
            {
                int errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
                return (~(long)0, (long)errno);
            }
            return ((long)result, null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? PidFDSendSignal([GoParam("uintptr")] long pidfd, [GoParam("syscall.Signal")] long signal)
        {
            int result = LinuxUnixSyscalls.pidfd_send_signal((int)pidfd, (int)signal, System.IntPtr.Zero, 0);
            if (result == -1)
            {
                int errno = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
                return (long)errno;
            }
            return null;
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) GetRandom(Slice<byte> p, long flags)
        {
            try
            {
                var buf = new byte[p.Len];
                RandomNumberGenerator.Fill(buf);
                for (int i = 0; i < buf.Length; i++)
                    p[i] = buf[i];
                return (p.Len, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Fcntl([GoParam("int")] long fd, [GoParam("int")] long cmd, [GoParam("int")] long arg)
        {
            int result = LinuxUnixSyscalls.fcntl((int)fd, (int)cmd, (int)arg);
            if (result == -1)
            {
                return (0, $"fcntl: errno {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }
            return (result, null);
        }

        [GoFunc]
        public static bool HasNonblockFlag([GoParam("int")] long flag)
        {
            return (flag & 0x800) != 0;
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unlinkat([GoParam("int")] long dirfd, string path, [GoParam("int")] long flags)
        {
            try
            {
                System.IO.File.Delete(path);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long, object?) Openat([GoParam("int")] long dirfd, string path, [GoParam("int")] long flags, [GoParam("uint32")] long perm)
        {
            int fd = LinuxUnixSyscalls.openat((int)dirfd, path, (int)flags, (uint)perm);
            if (fd == -1)
            {
                return (-1, $"openat: errno {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
            }
            return (fd, null);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Eaccess(string path, [GoParam("uint32")] long mode)
        {
            try
            {
                if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
                    return null;
                return "no such file or directory";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Fstatat([GoParam("int")] long dirfd, string path, object stat, [GoParam("int")] long flags)
        {
            int result = LinuxUnixSyscalls.fstatat((int)dirfd, path, System.IntPtr.Zero, (int)flags);
            if (result == -1)
            {
                return $"fstatat: errno {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}";
            }
            return null;
        }

        // KernelVersion returns major and minor kernel version numbers.
        [GoFunc]
        [return: GoReturn("int", "int")]
        public static (long, long) KernelVersion()
        {
            // Return a reasonable default Linux kernel version
            try
            {
                var release = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
                // Try to parse "X.Y" from the OS description
                var parts = release.Split(new[] { ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 &&
                    long.TryParse(parts[0], out var major) &&
                    long.TryParse(parts[1], out var minor))
                {
                    return (major, minor);
                }
            }
            catch (System.IO.IOException)
            {
            }
            catch (FormatException)
            {
            }
            return (5, 15); // default fallback
        }
    }

    [GoType("struct", Name = "SiginfoChild", Package = "internal/syscall/unix")]
    public class GoSiginfoChild
    {
        [GoField] public int Signo;
        [GoField] public int Errno;
        [GoField] public int Code;
        [GoField] public int Pid;
        [GoField] public uint Uid;
        [GoField] public int Status;

        [GoMethod]
        public long WaitStatus()
        {
            const int core = 0x80;
            const int stopped = 0x7f;
            const int continued = 0xffff;

            switch (Code)
            {
                case 1: // CLD_EXITED
                    return (long)(Status << 8);
                case 3: // CLD_DUMPED
                    return (long)(Status | core);
                case 2: // CLD_KILLED
                    return (long)Status;
                case 4: // CLD_TRAPPED
                case 5: // CLD_STOPPED
                    return (long)((Status << 8) | stopped);
                case 6: // CLD_CONTINUED
                    return continued;
                default:
                    return 0;
            }
        }
    }

    internal static class LinuxUnixSyscalls
    {
        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fcntl(int fd, int cmd, int arg);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int openat(int dirfd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, int flags, uint mode);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern int fstatat(int dirfd, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPUTF8Str)] string pathname, System.IntPtr statbuf, int flags);

        [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
        internal static extern long syscall(long number, long arg1, long arg2, long arg3);

        internal static int pidfd_open(int pid, uint flags)
        {
            return (int)syscall(434, pid, flags, 0);
        }

        internal static int pidfd_send_signal(int pidfd, int sig, System.IntPtr info, int flags)
        {
            return (int)syscall(424, pidfd, sig, 0);
        }
    }
}
