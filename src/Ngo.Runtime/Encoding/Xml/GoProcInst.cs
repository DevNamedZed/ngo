using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.ProcInst struct
    [GoType("struct", Name = "ProcInst", Package = "encoding/xml")]
    public struct GoProcInst
    {
        [GoField(Name = "Target")] public string Target;
        [GoField(Name = "Inst")] public Slice<byte> Inst;
    }
}
