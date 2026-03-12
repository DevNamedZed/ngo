using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "ProtocolError", Package = "net/http")]
    public class ProtocolError
    {
        [GoField(Name = "ErrorString")] public string ErrorString { get; set; } = "";

        [GoMethod]
        [return: GoReturn("string")]
        public string Error() => ErrorString;
    }
}
