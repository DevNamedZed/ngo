using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.UnsupportedTypeError struct
    [GoType("struct", Name = "UnsupportedTypeError", Package = "encoding/xml")]
    public class GoUnsupportedTypeError
    {
        [GoField(Name = "Type")] public object? Type;

        [GoMethod]
        public string Error() => "xml: unsupported type";
    }
}
