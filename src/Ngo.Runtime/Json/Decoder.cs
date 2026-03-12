using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Json
{
    // json.Decoder struct
    [GoType("struct", Name = "Decoder", Package = "encoding/json")]
    public class Decoder
    {
        [GoMethod]
        [return: GoReturn("error")]
        public object? Decode(object? v) { return null; }
        [GoMethod]
        public bool More() { return false; }
        [GoMethod]
        public void DisallowUnknownFields() { }
        [GoMethod]
        public void UseNumber() { }
        [GoMethod]
        public long InputOffset() { return 0; }
        [GoMethod]
        public (object?, object?) Token() { return (null, null); }
        [GoMethod]
        public object? Buffered() { return null; }
    }
}
