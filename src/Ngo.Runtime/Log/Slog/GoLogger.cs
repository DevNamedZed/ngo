using System;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Log.Slog
{
    // slog.Logger struct
    [GoType("struct", Name = "Logger", Package = "log/slog")]
    public class GoLogger
    {
        private readonly Package.IHandler? _handler;

        public GoLogger() : this(null) { }

        public GoLogger(Package.IHandler? handler)
        {
            _handler = handler;
        }

        [GoMethod(IsVariadic = true)]
        public void Info(string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(Package.LevelInfo, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        public void Warn(string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(Package.LevelWarn, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        public void Error(string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(Package.LevelError, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        public void Debug(string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(Package.LevelDebug, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        public void InfoContext(object? ctx, string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(Package.LevelInfo, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        public void WarnContext(object? ctx, string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(Package.LevelWarn, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        public void ErrorContext(object? ctx, string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(Package.LevelError, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        public void DebugContext(object? ctx, string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(Package.LevelDebug, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        public void Log(object? ctx, [GoParam("slog.Level")] long level, string msg, [GoParam("any")] params object[] args)
        {
            LogImpl(level, msg, args);
        }

        [GoMethod(IsVariadic = true)]
        [return: GoReturn("*slog.Logger")]
        public GoLogger With([GoParam("any")] params object[] args)
        {
            if (_handler == null)
            {
                return new GoLogger(Package.DefaultHandler);
            }
            var attrs = ArgsToAttrs(args);
            var newHandler = _handler.WithAttrs(new Slice<GoAttr>(attrs)) as Package.IHandler;
            return new GoLogger(newHandler ?? _handler);
        }

        [GoMethod]
        [return: GoReturn("*slog.Logger")]
        public GoLogger WithGroup(string name)
        {
            var h = _handler ?? Package.DefaultHandler;
            var newHandler = h.WithGroup(name) as Package.IHandler;
            return new GoLogger(newHandler ?? h);
        }

        [GoMethod]
        public bool Enabled(object? ctx, [GoParam("slog.Level")] long level)
        {
            var h = _handler ?? Package.DefaultHandler;
            return h.Enabled(ctx, level);
        }

        [GoMethod]
        [return: GoReturn("slog.Handler")]
        public object? Handler() => _handler;

        [GoMethod(IsVariadic = true)]
        public void LogAttrs(object? ctx, [GoParam("slog.Level")] long level, string msg, [GoParam("slog.Attr")] params GoAttr[] attrs)
        {
            var h = _handler ?? Package.DefaultHandler;
            if (!h.Enabled(ctx, level))
            {
                return;
            }
            var rec = new GoRecord { Message = msg, Level = new GoLevel { Value = level } };
            rec.AddAttrs(attrs);
            h.Handle(ctx, rec);
        }

        private void LogImpl(long level, string msg, object[] args)
        {
            var h = _handler ?? Package.DefaultHandler;
            if (!h.Enabled(null, level))
            {
                return;
            }
            var rec = new GoRecord { Message = msg, Level = new GoLevel { Value = level } };
            var attrs = ArgsToAttrs(args);
            if (attrs.Length > 0)
            {
                rec.AddAttrs(attrs);
            }
            h.Handle(null, rec);
        }

        internal static GoAttr[] ArgsToAttrs(object[] args)
        {
            var list = new System.Collections.Generic.List<GoAttr>();
            int i = 0;
            while (i < args.Length)
            {
                if (args[i] is GoAttr attr)
                {
                    list.Add(attr);
                    i++;
                }
                else if (args[i] is string key && i + 1 < args.Length)
                {
                    list.Add(new GoAttr { Key = key, Value = new GoValue(args[i + 1]) });
                    i += 2;
                }
                else
                {
                    // Skip malformed args
                    i++;
                }
            }
            return list.ToArray();
        }
    }
}
