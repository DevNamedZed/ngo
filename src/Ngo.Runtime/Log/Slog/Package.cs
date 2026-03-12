using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    [GoPackage("log/slog")]
    public static class Package
    {
        // Key constants for built-in handlers
        [GoConst(Type = "string")]
        public const string TimeKey = "time";

        [GoConst(Type = "string")]
        public const string LevelKey = "level";

        [GoConst(Type = "string")]
        public const string MessageKey = "msg";

        [GoConst(Type = "string")]
        public const string SourceKey = "source";

        // Level constants
        [GoConst(Type = "slog.Level")]
        public const long LevelDebug = -4;

        [GoConst(Type = "slog.Level")]
        public const long LevelInfo = 0;

        [GoConst(Type = "slog.Level")]
        public const long LevelWarn = 4;

        [GoConst(Type = "slog.Level")]
        public const long LevelError = 8;

        // slog.Info(msg string, args ...any)
        [GoFunc(IsVariadic = true)]
        public static void Info(string msg, [GoParam("any")] params object[] args) { }

        // slog.Warn(msg string, args ...any)
        [GoFunc(IsVariadic = true)]
        public static void Warn(string msg, [GoParam("any")] params object[] args) { }

        // slog.Error(msg string, args ...any)
        [GoFunc(IsVariadic = true)]
        public static void Error(string msg, [GoParam("any")] params object[] args) { }

        // slog.Debug(msg string, args ...any)
        [GoFunc(IsVariadic = true)]
        public static void Debug(string msg, [GoParam("any")] params object[] args) { }

        // slog.Log(ctx context.Context, level Level, msg string, args ...any)
        [GoFunc(IsVariadic = true)]
        public static void Log(object? ctx, [GoParam("slog.Level")] long level, string msg, [GoParam("any")] params object[] args) { }

        // slog.With(args ...any) *Logger
        [GoFunc(IsVariadic = true)]
        [return: GoReturn("*slog.Logger")]
        public static GoLogger With([GoParam("any")] params object[] args) => new GoLogger();

        // slog.New(h Handler) *Logger
        [GoFunc]
        [return: GoReturn("*slog.Logger")]
        public static GoLogger New([GoParam("slog.Handler")] object? h) => new GoLogger();

        // slog.Default() *Logger
        [GoFunc]
        [return: GoReturn("*slog.Logger")]
        public static GoLogger Default() => new GoLogger();

        // slog.SetDefault(l *Logger)
        [GoFunc]
        public static void SetDefault([GoParam("*slog.Logger")] GoLogger? l) { }

        // slog.NewTextHandler(w io.Writer, opts *HandlerOptions) *TextHandler
        [GoFunc]
        [return: GoReturn("*slog.TextHandler")]
        public static GoTextHandler NewTextHandler([GoParam("io.Writer")] object? w, [GoParam("*slog.HandlerOptions")] object? opts) => new GoTextHandler();

        // slog.NewJSONHandler(w io.Writer, opts *HandlerOptions) *JSONHandler
        [GoFunc]
        [return: GoReturn("*slog.JSONHandler")]
        public static GoJSONHandler NewJSONHandler([GoParam("io.Writer")] object? w, [GoParam("*slog.HandlerOptions")] object? opts) => new GoJSONHandler();

        // Attr constructor functions
        [GoFunc]
        [return: GoReturn("slog.Attr")]
        public static GoAttr String(string key, string value) => new GoAttr();

        [GoFunc]
        [return: GoReturn("slog.Attr")]
        public static GoAttr Int(string key, [GoParam("int")] long value) => new GoAttr();

        [GoFunc]
        [return: GoReturn("slog.Attr")]
        public static GoAttr Int64(string key, long value) => new GoAttr();

        [GoFunc]
        [return: GoReturn("slog.Attr")]
        public static GoAttr Bool(string key, bool value) => new GoAttr();

        [GoFunc]
        [return: GoReturn("slog.Attr")]
        public static GoAttr Any(string key, object? value) => new GoAttr();

        [GoFunc]
        [return: GoReturn("slog.Attr")]
        public static GoAttr Float64(string key, double value) => new GoAttr();

        [GoFunc]
        [return: GoReturn("slog.Attr")]
        public static GoAttr Duration(string key, [GoParam("time.Duration")] long value) => new GoAttr();

        [GoFunc]
        [return: GoReturn("slog.Attr")]
        public static GoAttr Time(string key, [GoParam("time.Time")] object? value) => new GoAttr();

        [GoFunc(IsVariadic = true)]
        [return: GoReturn("slog.Attr")]
        public static GoAttr Group(string key, [GoParam("any")] params object[] args) => new GoAttr();

        [GoFunc]
        [return: GoReturn("slog.Value")]
        public static GoValue AnyValue([GoParam("any")] object? v) => new GoValue();

        [GoFunc]
        [return: GoReturn("slog.Value")]
        public static GoValue StringValue(string value) => new GoValue();

        [GoFunc]
        [return: GoReturn("slog.Value")]
        public static GoValue IntValue(long v) => new GoValue();

        [GoFunc]
        [return: GoReturn("slog.Value")]
        public static GoValue Float64Value(double v) => new GoValue();

        [GoFunc]
        [return: GoReturn("slog.Value")]
        public static GoValue BoolValue(bool v) => new GoValue();

        [GoFunc]
        [return: GoReturn("slog.Value")]
        public static GoValue TimeValue([GoParam("time.Time")] object? v) => new GoValue();

        [GoFunc]
        [return: GoReturn("slog.Value")]
        public static GoValue DurationValue([GoParam("time.Duration")] long v) => new GoValue();

        [GoFunc]
        [return: GoReturn("slog.Value")]
        public static GoValue GroupValue([GoParam("slog.Attr")] params GoAttr[] as_) => new GoValue();

        // slog.NewRecord(time time.Time, level Level, msg string, pc uintptr) Record
        [GoFunc]
        [return: GoReturn("slog.Record")]
        public static GoRecord NewRecord([GoParam("time.Time")] object? time, [GoParam("slog.Level")] long level, string msg, long pc) => new GoRecord();

        // Kind constants
        [GoConst(Type = "slog.Kind")]
        public const long KindGroup = 4;

        // slog.Handler interface
        [GoType("interface", Name = "Handler", Package = "log/slog")]
        public interface IHandler
        {
            [GoMethod]
            bool Enabled(object? ctx, [GoParam("slog.Level")] long level);

            [GoMethod]
            [return: GoReturn("error")]
            object? Handle(object? ctx, [GoParam("slog.Record")] object? r);

            [GoMethod]
            [return: GoReturn("slog.Handler")]
            object? WithAttrs(Slice<GoAttr> attrs);

            [GoMethod]
            [return: GoReturn("slog.Handler")]
            object? WithGroup(string name);
        }

        // slog.Leveler interface
        [GoType("interface", Name = "Leveler", Package = "log/slog")]
        public interface ILeveler
        {
            [GoMethod]
            [return: GoReturn("slog.Level")]
            long Level();
        }

        // slog.LogValuer interface
        [GoType("interface", Name = "LogValuer", Package = "log/slog")]
        public interface ILogValuer
        {
            [GoMethod]
            [return: GoReturn("slog.Value")]
            GoValue LogValue();
        }
    }
}
