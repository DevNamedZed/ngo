using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.Logger struct
    [GoType("struct", Name = "Logger", Package = "log/slog")]
    public class GoLogger
    {
        [GoMethod(IsVariadic = true)]
        public void Info(string msg, [GoParam("any")] params object[] args) { }

        [GoMethod(IsVariadic = true)]
        public void Warn(string msg, [GoParam("any")] params object[] args) { }

        [GoMethod(IsVariadic = true)]
        public void Error(string msg, [GoParam("any")] params object[] args) { }

        [GoMethod(IsVariadic = true)]
        public void Debug(string msg, [GoParam("any")] params object[] args) { }

        [GoMethod(IsVariadic = true)]
        public void Log(object? ctx, [GoParam("slog.Level")] long level, string msg, [GoParam("any")] params object[] args) { }

        [GoMethod(IsVariadic = true)]
        [return: GoReturn("*slog.Logger")]
        public GoLogger With([GoParam("any")] params object[] args) => new GoLogger();

        [GoMethod]
        [return: GoReturn("*slog.Logger")]
        public GoLogger WithGroup(string name) => new GoLogger();

        [GoMethod]
        public bool Enabled(object? ctx, [GoParam("slog.Level")] long level) => false;

        [GoMethod]
        [return: GoReturn("slog.Handler")]
        public object? Handler() => null;
    }
}
