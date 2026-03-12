using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.EndElement struct
    [GoType("struct", Name = "EndElement", Package = "encoding/xml")]
    public struct GoEndElement
    {
        [GoField(Name = "Name")] public GoName Name;
    }
}
