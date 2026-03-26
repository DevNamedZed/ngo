using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.ProcInst struct
    [GoType("struct", Name = "ProcInst", Package = "encoding/xml")]
    public struct GoProcInst
    {
        [GoField(Name = "Target")] public string Target;
        [GoField(Name = "Inst")] public Slice<byte> Inst;

        [GoMethod]
        public GoProcInst Copy()
        {
            var instCopy = new byte[Inst.Len];
            for (int i = 0; i < Inst.Len; i++)
            {
                instCopy[i] = Inst[i];
            }
            return new GoProcInst { Target = Target, Inst = new Slice<byte>(instCopy) };
        }
    }
}
