namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// One step in the C compiler resolution trace. Each attempt names
    /// its source, what was probed, and the outcome (a version string if
    /// the probe succeeded, otherwise the reason it failed). Exposed on
    /// <see cref="CgoCompilerNotFoundException"/> so diagnostics can show
    /// the complete resolution path instead of a bare "not found".
    /// </summary>
    public sealed class CgoCompilerResolutionAttempt
    {
        public CgoCompilerResolutionAttempt(CgoCompilerSource source, string probed, string outcome, bool succeeded)
        {
            Source = source;
            Probed = probed;
            Outcome = outcome;
            Succeeded = succeeded;
        }

        public CgoCompilerSource Source { get; }

        /// <summary>The path or command name that was invoked.</summary>
        public string Probed { get; }

        /// <summary>
        /// Free-form description of the outcome — a version string on
        /// success, or the reason for failure (missing file, bad output,
        /// process launch error, etc.).
        /// </summary>
        public string Outcome { get; }

        public bool Succeeded { get; }
    }
}
