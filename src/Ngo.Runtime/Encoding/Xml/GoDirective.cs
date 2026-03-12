using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.Directive type (named []byte)
    [GoType("named", Name = "Directive", Package = "encoding/xml", Underlying = "[]byte")]
    public struct GoDirective
    {
        public Slice<byte> Value;
    }
}
