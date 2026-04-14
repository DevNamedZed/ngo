using System;
using System.Collections.Generic;
using System.Text;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Thrown when C compiler resolution fails. Carries the full list of
    /// attempts so callers can surface a complete diagnostic instead of
    /// a bare "not found" message. Callers format the trace via
    /// <see cref="FormatDiagnostic"/>.
    /// </summary>
    public sealed class CgoCompilerNotFoundException : Exception
    {
        public CgoCompilerNotFoundException(
            string message,
            IReadOnlyList<CgoCompilerResolutionAttempt> attempts)
            : base(message)
        {
            Attempts = attempts;
        }

        public IReadOnlyList<CgoCompilerResolutionAttempt> Attempts { get; }

        /// <summary>
        /// Produce a multi-line diagnostic: the exception message, the
        /// attempt trace, and a closing line with remediation hints.
        /// </summary>
        public string FormatDiagnostic()
        {
            var builder = new StringBuilder();
            builder.AppendLine(Message);
            builder.AppendLine("Compiler resolution trace:");
            foreach (var attempt in Attempts)
            {
                string tag = SourceTag(attempt.Source);
                builder.AppendLine($"  [{tag}] \"{attempt.Probed}\" — {attempt.Outcome}");
            }
            builder.Append("Remedy: pass --cc <path>, set the CC environment variable, or install gcc/clang.");
            return builder.ToString();
        }

        private static string SourceTag(CgoCompilerSource source)
        {
            switch (source)
            {
                case CgoCompilerSource.CliFlag:
                {
                    return "--cc";
                }
                case CgoCompilerSource.Environment:
                {
                    return "CC env";
                }
                case CgoCompilerSource.AutoDetect:
                {
                    return "auto-detect";
                }
                default:
                {
                    return source.ToString();
                }
            }
        }
    }
}
