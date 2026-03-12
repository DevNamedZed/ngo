using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.CharData type (named []byte)
    [GoType("named", Name = "CharData", Package = "encoding/xml", Underlying = "[]byte")]
    public struct GoCharData
    {
        public Slice<byte> Value;
    }
}
