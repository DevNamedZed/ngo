using System;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Captured output from a compiler or auxiliary tool process. Used
    /// internally by <see cref="CCompilerDriver"/> so helpers can return
    /// stdout, stderr, and exit status as a single typed value rather
    /// than an anonymous tuple.
    /// </summary>
    public sealed class CompilerProcessResult
    {
        public CompilerProcessResult(string standardOutput, string standardError, int exitCode)
        {
            StandardOutput = standardOutput ?? throw new ArgumentNullException(nameof(standardOutput));
            StandardError = standardError ?? throw new ArgumentNullException(nameof(standardError));
            ExitCode = exitCode;
        }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public int ExitCode { get; }

        public bool Succeeded
        {
            get { return ExitCode == 0; }
        }

        /// <summary>
        /// Best-effort combined output for diagnostics: stdout if non-empty,
        /// otherwise stderr. Compilers write errors to stderr on Unix but
        /// sometimes to stdout on Windows, so both channels need handling.
        /// </summary>
        public string CombinedOutput
        {
            get
            {
                if (!string.IsNullOrEmpty(StandardOutput) && !string.IsNullOrEmpty(StandardError))
                {
                    return StandardOutput + "\n" + StandardError;
                }
                if (!string.IsNullOrEmpty(StandardOutput))
                {
                    return StandardOutput;
                }
                return StandardError;
            }
        }
    }
}
