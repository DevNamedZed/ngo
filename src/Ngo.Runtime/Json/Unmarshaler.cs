using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Json
{
    // json.Unmarshaler interface
    [GoType("interface", Name = "Unmarshaler", Package = "encoding/json")]
    public interface Unmarshaler
    {
        [GoMethod]
        [return: GoReturn("error")]
        object? UnmarshalJSON(Slice<byte> data);
    }
}
