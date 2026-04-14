using System;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Thrown when the probe — the synthesised C program used to extract
    /// type information — fails to compile or execute. Carries the raw
    /// compiler output so diagnostics can show the user exactly why the
    /// probe rejected their preamble.
    /// </summary>
    public sealed class CgoProbeCompileException : Exception
    {
        public CgoProbeCompileException(string message, string compilerOutput) : base(message)
        {
            CompilerOutput = compilerOutput;
        }

        /// <summary>
        /// Raw stdout/stderr from the compiler or probe executable, in
        /// whichever form the caller produced it. Surfaced verbatim in
        /// the user-facing diagnostic.
        /// </summary>
        public string CompilerOutput { get; }
    }
}
