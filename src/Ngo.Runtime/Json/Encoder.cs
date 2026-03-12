using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Json
{
    // json.Encoder struct
    [GoType("struct", Name = "Encoder", Package = "encoding/json")]
    public class Encoder
    {
        [GoMethod]
        [return: GoReturn("error")]
        public object? Encode(object? v) { return null; }
        [GoMethod]
        public void SetIndent(string prefix, string indent) { }
        [GoMethod]
        public void SetEscapeHTML(bool on) { }
    }
}
