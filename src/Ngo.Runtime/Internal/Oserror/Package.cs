using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Oserror
{
    [GoPackage("internal/oserror")]
    public static class Package
    {
        [GoVar(Type = "error")]
        public static readonly object ErrInvalid = new Exception("invalid argument");

        [GoVar(Type = "error")]
        public static readonly object ErrPermission = new Exception("permission denied");

        [GoVar(Type = "error")]
        public static readonly object ErrExist = new Exception("file already exists");

        [GoVar(Type = "error")]
        public static readonly object ErrNotExist = new Exception("file does not exist");

        [GoVar(Type = "error")]
        public static readonly object ErrClosed = new Exception("file already closed");
    }
}
