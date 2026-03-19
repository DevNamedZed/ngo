using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Testlog
{
    /// <summary>
    /// internal/testlog — test logging support used by os and flag packages.
    /// </summary>
    [GoPackage("internal/testlog")]
    public static class Package
    {
        [GoType("interface", Name = "Interface", Package = "internal/testlog")]
        public interface IInterface
        {
            [GoMethod] void Stat(string name);
            [GoMethod] void Open(string name);
            [GoMethod] void Chdir(string name);
            [GoMethod] void Getenv(string name);
        }

        [GoFunc]
        public static void SetLogger([GoParam("internal/testlog.Interface")] object? impl) { }

        [GoFunc]
        [return: GoReturn("internal/testlog.Interface")]
        public static object? Logger() => null;

        [GoFunc]
        public static void Stat(string name) { }

        [GoFunc]
        public static void Open(string name) { }

        [GoFunc]
        public static void Getenv(string key) { }

        [GoFunc]
        public static bool PanicOnExit0() => false;

        [GoFunc]
        public static void SetPanicOnExit0(bool v) { }
    }
}
