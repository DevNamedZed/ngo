using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os.Signal
{
    /// <summary>
    /// Runtime support for Go's os/signal package.
    /// </summary>
    [GoPackage("os/signal")]
    public static class Package
    {
        [GoFunc(IsVariadic = true)]
        public static void Notify(object c, params object[] sig)
        {
            // Stub: signal notification is not supported in .NET runtime
        }

        [GoFunc]
        public static void Stop(object c)
        {
            // Stub
        }

        [GoFunc(IsVariadic = true)]
        public static void Reset(params object[] sig)
        {
            // Stub
        }

        [GoFunc(IsVariadic = true)]
        public static void Ignore(params object[] sig)
        {
            // Stub
        }

        [GoFunc(IsVariadic = true)]
        public static (object, object) NotifyContext(object parent, params object[] signals)
        {
            // Stub: returns (context, cancelFunc)
            throw new NotImplementedException("os/signal.NotifyContext not yet implemented");
        }
    }
}
