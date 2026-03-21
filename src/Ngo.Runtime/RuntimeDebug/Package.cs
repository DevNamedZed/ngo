using System;
using System.Reflection;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.RuntimeDebug
{
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
            // No-op: .NET runtime manages stack traces differently
        }

        [GoFunc]
        public static long SetMaxStack(long bytes)
        {
            // .NET manages stack sizes per-thread, not globally
            return 1024 * 1024; // 1MB default
        }

        [GoFunc]
        [return: GoReturn("int", "int")]
        public static (long, long) SetMaxThreads(long threads)
        {
            // .NET ThreadPool manages this
            return (threads, 0);
        }

        [GoFunc]
        public static bool SetPanicOnFault(bool enabled)
        {
            return false;
        }

        [GoFunc]
        public static long SetMemoryLimit(long limit)
        {
            // GC.RefreshMemoryLimit exists in .NET 8+ but is complex
            return limit;
        }

        [GoFunc]
        [return: GoReturn("*BuildInfo", "bool")]
        public static (GoBuildInfo, bool) ParseBuildInfo(string data)
        {
            return (new GoBuildInfo { GoVersion = "go1.22.6", Path = "", Main = new GoModule() }, true);
        }

        [GoFunc]
        [return: GoReturn("*BuildInfo", "bool")]
        public static (GoBuildInfo, bool) ReadBuildInfo()
        {
            var entry = Assembly.GetEntryAssembly();
            var info = new GoBuildInfo
            {
                GoVersion = "go1.22.6",
                Path = entry?.GetName().Name ?? "",
                Main = new GoModule
                {
                    Path = entry?.GetName().Name ?? "",
                    Version = entry?.GetName().Version?.ToString() ?? "(devel)",
                },
                Deps = new Slice<GoModule>(Array.Empty<GoModule>()),
                Settings = new Slice<GoBuildSetting>(Array.Empty<GoBuildSetting>()),
            };
            return (info, true);
        }
    }

    [GoType("struct", Name = "BuildInfo", Package = "runtime/debug")]
    public class GoBuildInfo
    {
        [GoField(Name = "GoVersion")]
        public string GoVersion = "";

        [GoField(Name = "Path")]
        public string Path = "";

        [GoField(Name = "Main")]
        public object Main = null!;

        [GoField(Name = "Deps")]
        public Slice<GoModule> Deps;

        [GoField(Name = "Settings")]
        public Slice<GoBuildSetting> Settings;
    }

    [GoType("struct", Name = "Module", Package = "runtime/debug")]
    public class GoModule
    {
        [GoField(Name = "Path")]
        public string Path = "";

        [GoField(Name = "Version")]
        public string Version = "";

        [GoField(Name = "Sum")]
        public string Sum = "";

        [GoField(Name = "Replace")]
        public GoModule? Replace;
    }

    [GoType("struct", Name = "BuildSetting", Package = "runtime/debug")]
    public class GoBuildSetting
    {
        [GoField(Name = "Key")]
        public string Key = "";

        [GoField(Name = "Value")]
        public string Value = "";
    }

    [GoType("struct", Name = "GCStats", Package = "runtime/debug")]
    public class GoGCStats
    {
        [GoField(Name = "LastGC")]
        public object LastGC = null!; // time.Time

        [GoField(Name = "NumGC")]
        public long NumGC;

        [GoField(Name = "PauseTotal")]
        public long PauseTotal;
    }
}
