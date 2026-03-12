using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.Comment type (named []byte)
    [GoType("named", Name = "Comment", Package = "encoding/xml", Underlying = "[]byte")]
    public struct GoComment
    {
        public Slice<byte> Value;
    }
}
