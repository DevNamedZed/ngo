namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// User-supplied overrides for the CGo toolchain. Mirrors the Go CLI
    /// surface — <c>go build</c> reads env vars; ngo additionally accepts
    /// <c>--cc</c> on the command line for per-invocation overrides.
    /// </summary>
    public sealed class CgoOptions
    {
        public static CgoOptions Empty { get; } = new CgoOptions();

        /// <summary>
        /// Path or name of the C compiler to use. When set, wins over the
        /// <c>CC</c> environment variable and platform auto-detection. If
        /// the path cannot be resolved to a working compiler, resolution
        /// hard-fails; ngo never silently falls through to a different
        /// compiler after an explicit override.
        /// </summary>
        public string? CCOverride { get; init; }
    }
}
