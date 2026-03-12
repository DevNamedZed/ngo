using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.RuntimeDebug
{
    /// <summary>
    /// Runtime support for Go's runtime/debug package.
    /// </summary>
    [GoPackage("runtime/debug")]
    public static class Package
    {
        [GoFunc]
        public static Slice<byte> Stack()
        {
            var trace = Environment.StackTrace ?? "";
            var bytes = System.Text.Encoding.UTF8.GetBytes(trace);
            return new Slice<byte>(bytes);
        }

        [GoFunc]
        public static void PrintStack()
        {
            Console.Error.Write(System.Text.Encoding.UTF8.GetString(Stack().AsSpan()));
        }

        [GoFunc]
        public static long SetGCPercent(long percent)
        {
            // .NET GC doesn't support percent-based tuning directly
            return 100;
        }

        [GoFunc]
        public static void FreeOSMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [GoFunc]
        public static void SetTraceback(string level)
        {
            // no-op in .NET runtime
        }

        [GoFunc]
        public static (GoBuildInfo, bool) ParseBuildInfo(string data)
        {
            return (new GoBuildInfo { GoVersion = "go1.22.6", Path = "", Main = (object)null! }, true);
        }

        [GoFunc]
        public static (GoBuildInfo, bool) ReadBuildInfo()
        {
            var info = new GoBuildInfo
            {
                GoVersion = "go1.22.6",
                Path = "",
                Main = (object)null!,
            };
            return (info, true);
        }
    }

    [GoType("struct", Name = "BuildInfo", Package = "runtime/debug")]
    public class GoBuildInfo
    {
        [GoField]
        public string GoVersion;

        [GoField]
        public string Path;

        [GoField]
        public object Main;
    }
}
