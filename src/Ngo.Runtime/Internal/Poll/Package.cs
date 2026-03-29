using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Poll
{
    [GoPackage("internal/poll")]
    public static class Package
    {
        // CloseFunc is used to hook the close call.
        [GoVar(Type = "func(int) error")]
        public static readonly object? CloseFunc = null;

        // AcceptFunc is used to hook the accept call.
        [GoVar(Type = "func(int) (int, syscall.Sockaddr, error)")]
        public static readonly object? AcceptFunc = null;

        // Error variables
        [GoVar(Type = "error")]
        public static readonly object ErrNetClosing = "use of closed network connection";
        [GoVar(Type = "error")]
        public static readonly object ErrFileClosing = "use of closed file";
        [GoVar(Type = "error")]
        public static readonly object ErrNoDeadline = "file type does not support deadline";
        [GoVar(Type = "error")]
        public static readonly object ErrDeadlineExceeded = "i/o timeout";
        [GoVar(Type = "error")]
        public static readonly object ErrNotPollable = "not pollable";

        [GoFunc]
        public static bool IsPollDescriptor([GoParam("uintptr")] long fd)
        {
            return false;
        }

        [GoFunc]
        [return: GoReturn("int64", "bool", "error")]
        public static (long, bool, object?) CopyFileRange(GoFD dst, GoFD src, long remain)
        {
            // Return (0, false, null) to indicate syscall not handled — Go uses userspace fallback
            return (0, false, null);
        }

        [GoFunc]
        [return: GoReturn("int64", "bool", "string", "error")]
        public static (long, bool, string, object?) Splice(GoFD dst, GoFD src, long remain)
        {
            // Return false to indicate splice not handled — Go uses userspace fallback
            return (0, false, "", null);
        }

        [GoFunc]
        [return: GoReturn("int64", "error", "bool")]
        public static (long, object?, bool) SendFile(GoFD dstFD, [GoParam("int")] long src, long remain)
        {
            // Return false to indicate sendfile not handled — Go uses userspace fallback
            return (0, null, false);
        }

        [GoFunc]
        [return: GoReturn("int", "string", "error")]
        public static (long, string, object?) DupCloseOnExec([GoParam("int")] long fd)
        {
            return (fd, "", null);
        }
    }
}
