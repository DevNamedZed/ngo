using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.Name struct
    [GoType("struct", Name = "Name", Package = "encoding/xml")]
    public struct GoName
    {
        [GoField(Name = "Space")] public string Space;
        [GoField(Name = "Local")] public string Local;
    }
}
