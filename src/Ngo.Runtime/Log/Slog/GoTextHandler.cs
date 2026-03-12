using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.TextHandler struct
    [GoType("struct", Name = "TextHandler", Package = "log/slog")]
    public class GoTextHandler : Package.IHandler
    {
        public bool Enabled(object? ctx, long level) => false;
        public object? Handle(object? ctx, object? r) => null;
        public object? WithAttrs(Slice<GoAttr> attrs) => new GoTextHandler();
        public object? WithGroup(string name) => new GoTextHandler();
    }
}
