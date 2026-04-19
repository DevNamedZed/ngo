using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Cpu
{
    [GoPackage("internal/cpu")]
    public static class Package
    {
        [GoVar(Type = "cpu.x86")]
        public static readonly GoX86 X86 = new GoX86();

        [GoVar(Type = "cpu.arm")]
        public static readonly GoARM ARM = new GoARM();

        [GoVar(Type = "cpu.arm64")]
        public static readonly GoARM64 ARM64 = new GoARM64();

        [GoVar(Type = "cpu.s390x")]
        public static readonly GoS390X S390X = new GoS390X();

        [GoConst]
        public static readonly long CacheLinePadSize = 64;

        [GoVar(Type = "uintptr")]
        public static readonly nuint CacheLineSize = 64;

        [GoVar]
        public static bool DebugOptions;

        [GoVar(Type = "cpu.mips64x")]
        public static readonly GoMIPS64X MIPS64X = new GoMIPS64X();

        [GoFunc]
        public static void Initialize(string env)
        {
            if (string.IsNullOrEmpty(env))
            {
                return;
            }
            foreach (var feature in env.Split(','))
            {
                var parts = feature.Split('=');
                if (parts.Length == 2 && parts[1] == "off")
                {
                    var name = parts[0].Trim();
                    var field = typeof(GoX86).GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        field.SetValue(X86, false);
                    }
                }
            }
        }

        [GoFunc]
        [return: GoReturn("string")]
        public static string Name()
        {
            return "";
        }
    }

    [GoType("struct", Name = "CacheLinePad", Package = "internal/cpu")]
    public struct GoCacheLinePad
    {
        // Padding struct — [CacheLinePadSize]byte (64 bytes)
    }

    [GoType("struct", Name = "mips64x", Package = "internal/cpu")]
    public struct GoMIPS64X
    {
        [GoField(Name = "HasMSA")] public bool HasMSA;
    }

    [GoType("struct", Name = "x86", Package = "internal/cpu")]
    public struct GoX86
    {
        [GoField(Name = "HasAES")] public bool HasAES;
        [GoField(Name = "HasADX")] public bool HasADX;
        [GoField(Name = "HasAVX")] public bool HasAVX;
        [GoField(Name = "HasAVX2")] public bool HasAVX2;
        [GoField(Name = "HasAVX512F")] public bool HasAVX512F;
        [GoField(Name = "HasAVX512BW")] public bool HasAVX512BW;
        [GoField(Name = "HasAVX512VL")] public bool HasAVX512VL;
        [GoField(Name = "HasBMI1")] public bool HasBMI1;
        [GoField(Name = "HasBMI2")] public bool HasBMI2;
        [GoField(Name = "HasERMS")] public bool HasERMS;
        [GoField(Name = "HasFMA")] public bool HasFMA;
        [GoField(Name = "HasOSXSAVE")] public bool HasOSXSAVE;
        [GoField(Name = "HasPCLMULQDQ")] public bool HasPCLMULQDQ;
        [GoField(Name = "HasPOPCNT")] public bool HasPOPCNT;
        [GoField(Name = "HasRDTSCP")] public bool HasRDTSCP;
        [GoField(Name = "HasSHA")] public bool HasSHA;
        [GoField(Name = "HasSSE3")] public bool HasSSE3;
        [GoField(Name = "HasSSSE3")] public bool HasSSSE3;
        [GoField(Name = "HasSSE41")] public bool HasSSE41;
        [GoField(Name = "HasSSE42")] public bool HasSSE42;
    }

    [GoType("struct", Name = "arm", Package = "internal/cpu")]
    public struct GoARM
    {
        [GoField(Name = "HasVFPv4")] public bool HasVFPv4;
        [GoField(Name = "HasIDIVA")] public bool HasIDIVA;
        [GoField(Name = "HasV7Atomics")] public bool HasV7Atomics;
    }

    [GoType("struct", Name = "s390x", Package = "internal/cpu")]
    public struct GoS390X
    {
        [GoField(Name = "HasAES")] public bool HasAES;
        [GoField(Name = "HasAESCBC")] public bool HasAESCBC;
        [GoField(Name = "HasAESCTR")] public bool HasAESCTR;
        [GoField(Name = "HasAESGCM")] public bool HasAESGCM;
        [GoField(Name = "HasGHASH")] public bool HasGHASH;
        [GoField(Name = "HasSHA1")] public bool HasSHA1;
        [GoField(Name = "HasSHA256")] public bool HasSHA256;
        [GoField(Name = "HasSHA512")] public bool HasSHA512;
        [GoField(Name = "HasSHA3")] public bool HasSHA3;
        [GoField(Name = "HasVX")] public bool HasVX;
        [GoField(Name = "HasVXE")] public bool HasVXE;
        [GoField(Name = "HasKDSA")] public bool HasKDSA;
        [GoField(Name = "HasECDSA")] public bool HasECDSA;
        [GoField(Name = "HasEDDSA")] public bool HasEDDSA;
    }

    [GoType("struct", Name = "arm64", Package = "internal/cpu")]
    public struct GoARM64
    {
        [GoField(Name = "HasAES")] public bool HasAES;
        [GoField(Name = "HasPMULL")] public bool HasPMULL;
        [GoField(Name = "HasSHA1")] public bool HasSHA1;
        [GoField(Name = "HasSHA2")] public bool HasSHA2;
        [GoField(Name = "HasSHA512")] public bool HasSHA512;
        [GoField(Name = "HasCRC32")] public bool HasCRC32;
        [GoField(Name = "HasATOMICS")] public bool HasATOMICS;
        [GoField(Name = "HasCPUID")] public bool HasCPUID;
        [GoField(Name = "IsNeoverseV1")] public bool IsNeoverseV1;
        [GoField(Name = "IsNeoverseV2")] public bool IsNeoverseV2;
    }
}
