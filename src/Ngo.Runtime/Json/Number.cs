using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Json
{
    // json.Number type
    [GoType("named", Name = "Number", Package = "encoding/json", Underlying = "string")]
    public class Number
    {
        private readonly string _value;
        public Number() { _value = "0"; }
        public Number(string v) { _value = v; }
        [GoMethod] public string String() => _value;
        [GoMethod] public (long, object?) Int64() { return long.TryParse(_value, out var v) ? (v, (object?)null) : (0, "invalid number"); }
        [GoMethod] public (double, object?) Float64() { return double.TryParse(_value, out var v) ? (v, (object?)null) : (0, "invalid number"); }
    }
}
