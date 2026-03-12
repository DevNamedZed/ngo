using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.Attr struct
    [GoType("struct", Name = "Attr", Package = "log/slog")]
    public struct GoAttr
    {
        [GoField(Name = "Key")]
        public string Key;

        [GoField(Name = "Value")]
        public GoValue Value;
    }
}
