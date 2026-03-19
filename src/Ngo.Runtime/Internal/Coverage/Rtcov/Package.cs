using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Coverage.Rtcov
{
    [GoPackage("internal/coverage/rtcov")]
    public static class Package
    {
        [GoConst] public const long NotHardCoded = 0;

        [GoVar] public static Slice<GoCovMetaBlob> Meta = default;
        [GoVar] public static Slice<GoCovCounterBlob> Counters = default;
    }

    [GoType("struct", Name = "CovMetaBlob", Package = "internal/coverage/rtcov")]
    public class GoCovMetaBlob
    {
        [GoField(Name = "P")] public long P;
        [GoField(Name = "Len")] public long Len;
        [GoField(Name = "Hash")] public Slice<byte> Hash;
        [GoField(Name = "PkgPath")] public string PkgPath = "";
        [GoField(Name = "PkgID")] public long PkgID;
        [GoField(Name = "CounterMode")] public long CounterMode;
        [GoField(Name = "CounterGranularity")] public long CounterGranularity;
    }

    [GoType("struct", Name = "CovCounterBlob", Package = "internal/coverage/rtcov")]
    public class GoCovCounterBlob
    {
        [GoField(Name = "Counters")] public long Counters;
        [GoField(Name = "Len")] public long Len;
    }
}
