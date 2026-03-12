using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Json
{
    // json.Marshaler interface
    [GoType("interface", Name = "Marshaler", Package = "encoding/json")]
    public interface Marshaler
    {
        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        (Slice<byte>, object?) MarshalJSON();
    }
}
