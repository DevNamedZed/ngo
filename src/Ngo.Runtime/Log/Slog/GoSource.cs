using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    [GoType("struct", Name = "Source", Package = "log/slog")]
    public struct GoSource
    {
        [GoField(Name = "Function")]
        public string Function;

        [GoField(Name = "File")]
        public string File;

        [GoField(Name = "Line")]
        public long Line;
    }
}
