using System.Runtime.InteropServices;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Buildcfg
{
    /// <summary>
    /// Stub for internal/buildcfg — build configuration.
    /// </summary>
    [GoPackage("internal/buildcfg")]
    public static class Package
    {
        [GoVar]
        public static string GOARCH => RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "386",
            Architecture.Arm => "arm",
            _ => "amd64",
        };

        [GoVar]
        public static string GOOS => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin"
            : "linux";

        [GoVar]
        public static string GOROOT => System.Environment.GetEnvironmentVariable("GOROOT") ?? "";

        [GoVar]
        public static string GOEXPERIMENT => System.Environment.GetEnvironmentVariable("GOEXPERIMENT") ?? "";

        // func Check() — validates build config, no-op for ngo
        [GoFunc]
        public static void Check() { }

        [GoFunc]
        [return: GoReturn("[]string")]
        public static Slice<string> ToolTags() => new Slice<string>(new string[] { "goexperiment.unified" });

        // Experiment holds GOEXPERIMENT flags
        [GoType("struct", Name = "ExperimentFlags", Package = "internal/buildcfg")]
        public class GoExperimentFlags { }

        [GoVar]
        public static GoExperimentFlags Experiment => new GoExperimentFlags();
    }
}
