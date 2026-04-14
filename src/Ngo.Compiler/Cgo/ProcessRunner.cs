using System;
using System.Diagnostics;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Shared process-launch helper for the cgo pipeline. Returns a
    /// typed <see cref="CompilerProcessResult"/> so callers never have
    /// to juggle anonymous tuples. Any launch failure (Win32Exception
    /// for a missing binary, <see cref="InvalidOperationException"/>
    /// if <see cref="Process.Start(ProcessStartInfo)"/> returns null)
    /// propagates to the caller so diagnostics can record it.
    /// </summary>
    internal static class ProcessRunner
    {
        private const int DefaultWaitForExitMilliseconds = 30000;

        public static CompilerProcessResult Run(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Process.Start returned null for \"{fileName}\"");
            }

            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit(DefaultWaitForExitMilliseconds);

            return new CompilerProcessResult(standardOutput, standardError, process.ExitCode);
        }
    }
}
