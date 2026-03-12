using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.Encoder struct
    [GoType("struct", Name = "Encoder", Package = "encoding/xml")]
    public class GoEncoder
    {
        [GoMethod]
        [return: GoReturn("error")]
        public object? Encode(object? v) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? EncodeElement(object? v, GoStartElement start) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? EncodeToken([GoParam("xml.Token")] object? t) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush() => null;

        [GoMethod]
        public void Indent(string prefix, string indent) { }
    }
}
