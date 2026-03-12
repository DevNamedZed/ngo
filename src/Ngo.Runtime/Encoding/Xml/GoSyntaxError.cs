using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.SyntaxError struct
    [GoType("struct", Name = "SyntaxError", Package = "encoding/xml")]
    public class GoSyntaxError
    {
        [GoField(Name = "Msg")] public string Msg = "";
        [GoField(Name = "Line")] public long Line;

        [GoMethod]
        public string Error() => Msg;
    }
}
