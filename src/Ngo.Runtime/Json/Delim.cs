using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Json
{
    // json.Delim type
    [GoType("named", Name = "Delim", Package = "encoding/json", Underlying = "rune")]
    public class Delim
    {
        [GoMethod] public string String() => "";
    }
}
