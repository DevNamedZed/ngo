using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.Attr struct
    [GoType("struct", Name = "Attr", Package = "encoding/xml")]
    public struct GoAttr
    {
        [GoField(Name = "Name")] public GoName Name;
        [GoField(Name = "Value")] public string Value;
    }
}
