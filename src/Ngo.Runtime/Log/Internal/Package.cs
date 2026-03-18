using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Internal
{
    [GoPackage("log/internal")]
    public static class Package
    {
        // DefaultOutput holds a function which calls the default log.Logger's
        // output function. It allows slog.defaultHandler to call into an
        // unexported function of the log package.
        [GoVar(Type = "func(uintptr, []byte) error")]
        public static Func<nuint, Slice<byte>, object?>? DefaultOutput = (calldepth, buf) =>
        {
            var text = System.Text.Encoding.UTF8.GetString(buf.AsSpan());
            Console.Error.Write(text);
            return null;
        };
    }
}
