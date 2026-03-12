using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.Level type (named int)
    [GoType("named", Name = "Level", Package = "log/slog", Underlying = "int")]
    public struct GoLevel
    {
        public long Value;

        [GoMethod]
        public string String() => Value.ToString();
    }
}
