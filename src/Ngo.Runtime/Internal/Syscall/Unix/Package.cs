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
            return (0, "not supported");
        }

        [GoFunc]
        public static bool HasNonblockFlag([GoParam("int")] long flag)
        {
            return false;
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
            return (-1, "not supported");
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
            return "not supported";
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
            catch { }
            return (5, 15); // default fallback
        }
    }
}
