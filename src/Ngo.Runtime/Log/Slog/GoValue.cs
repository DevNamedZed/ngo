using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.Value struct
    [GoType("struct", Name = "Value", Package = "log/slog")]
    public struct GoValue
    {
        [GoMethod]
        public string String() => "";
    }
}
