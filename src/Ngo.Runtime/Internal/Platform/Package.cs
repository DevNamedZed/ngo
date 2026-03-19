using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Platform
{
    /// <summary>
    /// Stub for internal/platform and internal/syslist — platform utilities.
    /// Used by go/build.
    /// </summary>
    [GoPackage("internal/platform")]
    public static class Package
    {
        // func BuildModeSupported(compiler, buildmode, goos, goarch string) bool
        [GoFunc]
        public static bool BuildModeSupported(string compiler, string buildmode, string goos, string goarch) => true;

        // func InternalLinkPIESupported(goos, goarch string) bool
        [GoFunc]
        public static bool InternalLinkPIESupported(string goos, string goarch) => false;

        // func MustLinkExternal(goos, goarch string, withCgo bool) bool
        [GoFunc]
        public static bool MustLinkExternal(string goos, string goarch, bool withCgo) => withCgo;

        // func CGOSupported(goos, goarch string) bool
        [GoFunc]
        public static bool CGOSupported(string goos, string goarch) => true;

        // CgoSupported — alternate casing used by go/build
        [GoFunc]
        public static bool CgoSupported(string goos, string goarch) => true;

        // func DefaultPIE(goos, goarch string, isRace bool) bool
        [GoFunc]
        public static bool DefaultPIE(string goos, string goarch, bool isRace) => false;

        // func ExecutableHasDWARF(goos, goarch string) bool
        [GoFunc]
        public static bool ExecutableHasDWARF(string goos, string goarch) => true;

        // func FirstClass() []OSArch — known supported platform pairs
        [GoFunc]
        [return: GoReturn("[]internal/platform.OSArch")]
        public static Slice<GoOSArch> FirstClass()
        {
            return new Slice<GoOSArch>(new[]
            {
                new GoOSArch { GOOS = "linux", GOARCH = "amd64" },
                new GoOSArch { GOOS = "linux", GOARCH = "arm64" },
                new GoOSArch { GOOS = "darwin", GOARCH = "amd64" },
                new GoOSArch { GOOS = "darwin", GOARCH = "arm64" },
                new GoOSArch { GOOS = "windows", GOARCH = "amd64" },
            });
        }

        // func Broken(goos, goarch string) bool
        [GoFunc]
        public static bool Broken(string goos, string goarch) => false;

        // func RaceDetectorSupported(goos, goarch string) bool
        [GoFunc]
        public static bool RaceDetectorSupported(string goos, string goarch) => false;

        // func FuzzSupported(goos, goarch string) bool
        [GoFunc]
        public static bool FuzzSupported(string goos, string goarch) => false;

        // func FuzzInstrumented(goos, goarch string) bool
        [GoFunc]
        public static bool FuzzInstrumented(string goos, string goarch) => false;

        // func MSanSupported(goos, goarch string) bool
        [GoFunc]
        public static bool MSanSupported(string goos, string goarch) => false;

        // func ASanSupported(goos, goarch string) bool
        [GoFunc]
        public static bool ASanSupported(string goos, string goarch) => false;
    }

    [GoType("struct", Name = "OSArch", Package = "internal/platform")]
    public class GoOSArch
    {
        [GoField(Name = "GOOS")]
        public string GOOS { get; set; } = "";

        [GoField(Name = "GOARCH")]
        public string GOARCH { get; set; } = "";

        [GoMethod]
        public string String() => $"{GOOS}/{GOARCH}";
    }
}
