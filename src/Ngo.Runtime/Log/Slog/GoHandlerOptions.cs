using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.HandlerOptions struct
    [GoType("struct", Name = "HandlerOptions", Package = "log/slog")]
    public class GoHandlerOptions
    {
        [GoField(Name = "AddSource")]
        public bool AddSource;

        [GoField(Name = "Level")]
        public object? Level;

        [GoField(Name = "ReplaceAttr")]
        public object? ReplaceAttr;
    }
}
