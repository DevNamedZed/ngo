using System;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Thrown when the C compiler or linker fails while producing a
    /// build artifact (shared library, static library, object file,
    /// final link). The caller receives the verbatim compiler stderr
    /// so the user-facing diagnostic can show the underlying tool
    /// output without paraphrasing. Probe failures have their own
    /// exception type (<see cref="CgoProbeCompileException"/>) because
    /// the probe stage is recoverable only for different reasons.
    /// </summary>
    public sealed class CgoCCompileException : Exception
    {
        public CgoCCompileException(string message, string compilerOutput) : base(message)
        {
            CompilerOutput = compilerOutput ?? string.Empty;
        }

        /// <summary>
        /// Raw stdout/stderr from the compiler or linker. Surfaced
        /// verbatim in user-facing diagnostics.
        /// </summary>
        public string CompilerOutput { get; }
    }
}
