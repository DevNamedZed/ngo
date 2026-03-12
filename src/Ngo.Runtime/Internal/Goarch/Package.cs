using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Goarch
{
    [GoPackage("internal/goarch")]
    public static class Package
    {
        [GoConst]
        public static readonly long PtrSize = System.IntPtr.Size;

        [GoConst]
        public static readonly string GOARCH = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "amd64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X86 => "386",
            _ => "unknown",
        };

        [GoConst] public static readonly long IsAmd64 = GOARCH == "amd64" ? 1 : 0;
        [GoConst] public static readonly long IsArm64 = GOARCH == "arm64" ? 1 : 0;
        [GoConst] public static readonly long Is386 = GOARCH == "386" ? 1 : 0;
        [GoConst] public static readonly long IsArm = GOARCH == "arm" ? 1 : 0;
        [GoConst] public static readonly long IsMips = 0;
        [GoConst] public static readonly long IsMipsle = 0;
        [GoConst] public static readonly long IsMips64le = 0;
        [GoConst] public static readonly long IsMips64 = 0;
        [GoConst] public static readonly long IsPpc64 = 0;
        [GoConst] public static readonly long IsPpc64le = 0;
        [GoConst] public static readonly long IsS390x = 0;
        [GoConst] public static readonly long IsWasm = 0;
        [GoConst] public static readonly long IsRiscv64 = 0;
        [GoConst] public static readonly long IsLoong64 = 0;

        // ArchFamilyType constants
        [GoConst] public static readonly long AMD64 = 0;
        [GoConst] public static readonly long ARM64 = 1;
        [GoConst] public static readonly long I386 = 2;
        [GoConst] public static readonly long ARM = 3;
        [GoConst] public static readonly long MIPS = 4;
        [GoConst] public static readonly long MIPS64 = 5;
        [GoConst] public static readonly long PPC64 = 6;
        [GoConst] public static readonly long RISCV64 = 7;
        [GoConst] public static readonly long S390X = 8;
        [GoConst] public static readonly long WASM = 9;
        [GoConst] public static readonly long LOONG64 = 10;

        [GoConst] public static readonly long ArchFamily = GOARCH switch
        {
            "amd64" => AMD64,
            "arm64" => ARM64,
            "386" => I386,
            _ => AMD64,
        };

        [GoConst] public static readonly bool BigEndian = false; // all supported .NET platforms are little-endian
        [GoConst] public static readonly long DefaultPhysPageSize = 4096;
        [GoConst] public static readonly long PCQuantum = 1;
        [GoConst] public static readonly long Int64Align = 8;
        [GoConst] public static readonly long MinFrameSize = 0;
        [GoConst] public static readonly long StackAlign = 8;
    }
}
