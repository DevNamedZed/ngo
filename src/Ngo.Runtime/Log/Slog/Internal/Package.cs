using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog.Internal
{
    [GoPackage("log/slog/internal")]
    public static class Package
    {
        // If IgnorePC is true, do not invoke runtime.Callers to get the pc.
        // This is for benchmarks only.
        [GoVar(Type = "bool")]
        public static bool IgnorePC = false;
    }
}
