using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Race
{
    /// <summary>
    /// internal/race — race detector stubs. All no-ops since .NET doesn't have Go's race detector.
    /// </summary>
    [GoPackage("internal/race")]
    public static class Package
    {
        [GoConst] public const bool Enabled = false;

        [GoFunc] public static void Acquire(object? addr) { }
        [GoFunc] public static void Release(object? addr) { }
        [GoFunc] public static void ReleaseMerge(object? addr) { }
        [GoFunc] public static void Read(object? addr) { }
        [GoFunc] public static void Write(object? addr) { }
        [GoFunc] public static void ReadRange(object? addr, [GoParam("int")] long len) { }
        [GoFunc] public static void WriteRange(object? addr, [GoParam("int")] long len) { }
        [GoFunc] public static void Disable() { }
        [GoFunc] public static void Enable() { }
        [GoFunc] [return: GoReturn("int")] public static long Errors() => 0;
    }
}
