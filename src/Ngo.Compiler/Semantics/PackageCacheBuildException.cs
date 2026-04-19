using System;
using System.Collections.Generic;

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Thrown when a transitive package cannot be turned into a valid cache
    /// archive — either source analysis produced errors, or the archive
    /// writer threw. Callers must treat this as a hard build failure for the
    /// package being compiled, not a recoverable warning.
    /// </summary>
    public sealed class PackageCacheBuildException : Exception
    {
        public PackageCacheBuildException(string importPath, string reason, IReadOnlyList<CompileError> analysisErrors)
            : base($"cannot build cache archive for '{importPath}': {reason}")
        {
            ImportPath = importPath;
            Reason = reason;
            AnalysisErrors = analysisErrors;
        }

        public PackageCacheBuildException(string importPath, string reason, Exception inner)
            : base($"cannot build cache archive for '{importPath}': {reason}", inner)
        {
            ImportPath = importPath;
            Reason = reason;
            AnalysisErrors = Array.Empty<CompileError>();
        }

        public string ImportPath { get; }

        public string Reason { get; }

        public IReadOnlyList<CompileError> AnalysisErrors { get; }
    }
}
