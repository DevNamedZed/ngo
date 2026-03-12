using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.StartElement struct
    [GoType("struct", Name = "StartElement", Package = "encoding/xml")]
    public struct GoStartElement
    {
        [GoField(Name = "Name")] public GoName Name;
        [GoField(Name = "Attr")] public Slice<GoAttr> Attr;
    }
}
