using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Goexperiment
{
    [GoPackage("internal/goexperiment")]
    public static class Package
    {
        [GoConst]
        public static readonly bool CoverageRedesign = true;
        [GoConst]
        public static readonly bool RangeFunc = false;
        [GoConst]
        public static readonly bool LoopVar = false;
        [GoConst]
        public static readonly bool AliasTypeParams = false;
        [GoConst]
        public static readonly bool AllocHeaders = false;
        [GoConst]
        public static readonly long AllocHeadersInt = 0;
        [GoConst]
        public static readonly bool CgoCheck2 = false;
        [GoConst]
        public static readonly long CgoCheck2Int = 0;
        [GoConst]
        public static readonly bool HeapMinimum512KiB = false;
        [GoConst]
        public static readonly long HeapMinimum512KiBInt = 0;
        [GoConst]
        public static readonly bool ExecTracer2 = false;
        [GoConst]
        public static readonly long ExecTracer2Int = 0;
    }
}
