using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.JSONHandler struct
    [GoType("struct", Name = "JSONHandler", Package = "log/slog")]
    public class GoJSONHandler : Package.IHandler
    {
        public bool Enabled(object? ctx, long level) => false;
        public object? Handle(object? ctx, object? r) => null;
        public object? WithAttrs(Slice<GoAttr> attrs) => new GoJSONHandler();
        public object? WithGroup(string name) => new GoJSONHandler();
    }
}
