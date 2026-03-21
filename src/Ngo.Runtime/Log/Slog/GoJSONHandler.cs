using System;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Log.Slog
{
    // slog.JSONHandler struct
    [GoType("struct", Name = "JSONHandler", Package = "log/slog")]
    public class GoJSONHandler : Package.IHandler
    {
        private readonly IGoWriter? _writer;
        private readonly GoHandlerOptions? _opts;
        private readonly string _groupPrefix;
        private readonly GoAttr[]? _preformatted;

        public GoJSONHandler() : this(null, null) { }

        public GoJSONHandler(IGoWriter? w, GoHandlerOptions? opts)
        {
            _writer = w;
            _opts = opts;
            _groupPrefix = "";
            _preformatted = null;
        }

        private GoJSONHandler(IGoWriter? w, GoHandlerOptions? opts, string groupPrefix, GoAttr[]? preformatted)
        {
            _writer = w;
            _opts = opts;
            _groupPrefix = groupPrefix;
            _preformatted = preformatted;
        }

        [GoMethod]
        public bool Enabled(object? ctx, [GoParam("slog.Level")] long level)
        {
            long minLevel = Package.LevelInfo;
            if (_opts?.Level != null)
            {
                if (_opts.Level is long l)
                {
                    minLevel = l;
                }
            }
            return level >= minLevel;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Handle(object? ctx, [GoParam("slog.Record")] object? r)
        {
            if (_writer == null || r is not GoRecord rec)
            {
                return null;
            }

            var sb = new StringBuilder();
            sb.Append("{\"time\":\"");
            sb.Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            sb.Append("\",\"level\":\"");
            sb.Append(GoTextHandler.LevelString(rec.Level.Value));
            sb.Append("\",\"msg\":");
            sb.Append(JsonEscape(rec.Message ?? ""));

            if (_preformatted != null)
            {
                foreach (var attr in _preformatted)
                {
                    AppendAttr(sb, _groupPrefix, attr);
                }
            }

            rec.Attrs(attr => { AppendAttr(sb, _groupPrefix, attr); return true; });

            sb.Append("}\n");

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            _writer.Write(new Slice<byte>(bytes));
            return null;
        }

        [GoMethod]
        [return: GoReturn("slog.Handler")]
        public object? WithAttrs(Slice<GoAttr> attrs)
        {
            var arr = new GoAttr[attrs.Len];
            for (int i = 0; i < attrs.Len; i++)
            {
                arr[i] = attrs[i];
            }
            var existing = _preformatted ?? Array.Empty<GoAttr>();
            var combined = new GoAttr[existing.Length + arr.Length];
            existing.CopyTo(combined, 0);
            arr.CopyTo(combined, existing.Length);
            return new GoJSONHandler(_writer, _opts, _groupPrefix, combined);
        }

        [GoMethod]
        [return: GoReturn("slog.Handler")]
        public object? WithGroup(string name)
        {
            var prefix = string.IsNullOrEmpty(_groupPrefix) ? name + "." : _groupPrefix + name + ".";
            return new GoJSONHandler(_writer, _opts, prefix, _preformatted);
        }

        private static void AppendAttr(StringBuilder sb, string prefix, GoAttr attr)
        {
            if (string.IsNullOrEmpty(attr.Key))
            {
                return;
            }
            sb.Append(',');
            sb.Append(JsonEscape(prefix + attr.Key));
            sb.Append(':');
            sb.Append(JsonEscape(attr.Value.String()));
        }

        private static string JsonEscape(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
        }
    }
}
