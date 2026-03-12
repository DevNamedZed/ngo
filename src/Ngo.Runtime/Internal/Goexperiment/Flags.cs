using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Goexperiment
{
    [GoType("struct", Name = "Flags", Package = "internal/goexperiment")]
    public class Flags
    {
        [GoField(Name = "RangeFunc")]
        public bool RangeFunc { get; set; }
        [GoField(Name = "AliasTypeParams")]
        public bool AliasTypeParams { get; set; }
        [GoField(Name = "CoverageRedesign")]
        public bool CoverageRedesign { get; set; }
        [GoField(Name = "LoopVar")]
        public bool LoopVar { get; set; }
    }
}
