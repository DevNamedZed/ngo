using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Orchestrates the full CGo compilation pipeline:
    /// 1. Extract preamble
    /// 2. Generate and compile probe to extract type info
    /// 3. Compile C code to shared/static library
    /// 4. Cache results for incremental builds
    ///
    /// This follows Go's real cgo approach: the C compiler is used as an oracle
    /// for type information. We never parse C ourselves.
    /// </summary>
    public class CgoCompiler
    {
        private readonly CCompilerDriver _compilerDriver;
        private readonly CgoProbeGenerator _probeGenerator;
        private readonly CgoProbeResultParser _probeResultParser;
        private readonly string _cacheDirectory;

        public CgoCompiler(string cacheDirectory)
        {
            _compilerDriver = new CCompilerDriver();
            _probeGenerator = new CgoProbeGenerator();
            _probeResultParser = new CgoProbeResultParser();
            _cacheDirectory = cacheDirectory;
        }

        /// <summary>
        /// Run the full CGo pipeline for a package.
        /// Returns the probe results (type info) and the path to the compiled native library.
        /// </summary>
        public CgoCompilationResult Compile(CgoPreamble preamble, CgoProbeRequest probeRequest, string packageName)
        {
            var result = new CgoCompilationResult();

            // Step 0: Detect C compiler
            var compiler = _compilerDriver.Detect();
            if (compiler == null)
            {
                result.Error = _compilerDriver.GetType().Name + ": " +
                    "C compiler not found. Set the CC environment variable or install gcc/clang.";
                return result;
            }
            result.CompilerInfo = compiler;

            // Step 1: Check cache
            string currentOS = CCompilerDriver.GetCurrentOS();
            string cflags = preamble.GetCFlags(currentOS);
            string ldflags = preamble.GetLDFlags(currentOS);
            string cacheKey = ComputeCacheKey(preamble.CSource, cflags, ldflags, currentOS, compiler.Version);
            string cacheDir = Path.Combine(_cacheDirectory, "cgo", cacheKey);

            string cachedLibrary = Path.Combine(cacheDir, CCompilerDriver.GetStaticLibraryName(packageName));
            string cachedProbe = Path.Combine(cacheDir, "probe.json");

            if (File.Exists(cachedLibrary) && File.Exists(cachedProbe))
            {
                // Cache hit — read cached probe results
                result.ProbeResult = _probeResultParser.Parse(File.ReadAllText(cachedProbe));
                result.NativeLibraryPath = cachedLibrary;
                result.LDFlags = ldflags;
                result.CacheHit = true;
                return result;
            }

            // Step 2: Create temp/cache directory
            Directory.CreateDirectory(cacheDir);

            // Step 3: Run probe to extract type information
            if (probeRequest.TypeSizes.Count > 0 || probeRequest.FieldOffsets.Count > 0 ||
                probeRequest.EnumValues.Count > 0)
            {
                var probeResult = RunProbe(preamble, probeRequest, cacheDir, packageName, cflags);
                if (probeResult.error != null)
                {
                    // Probe failure is non-fatal — proceed with default type sizes
                    result.ProbeResult = new CgoProbeResult();
                }
                else
                {
                    result.ProbeResult = probeResult.result;
                }

                // Cache probe results
                SaveProbeResults(cachedProbe, probeResult.result);
            }
            else
            {
                result.ProbeResult = new CgoProbeResult();
            }

            // Step 4: Compile preamble C code to static library (.a/.lib)
            // Static libs are cached per-package and linked together at final build time.
            if (preamble.HasCSource)
            {
                string cSourceFile = Path.Combine(cacheDir, $"cgo_{packageName}.c");
                File.WriteAllText(cSourceFile, preamble.CSource);

                var (libraryPath, compileError) = _compilerDriver.CompileStaticLibrary(
                    cSourceFile, cacheDir, packageName, cflags);

                if (compileError != null)
                {
                    result.Error = compileError;
                    return result;
                }

                result.NativeLibraryPath = libraryPath;
                result.LDFlags = ldflags;
            }

            return result;
        }

        private (CgoProbeResult? result, string? error) RunProbe(
            CgoPreamble preamble, CgoProbeRequest request,
            string workDir, string packageName, string cflags)
        {
            // Generate executable probe (compile-and-run approach)
            string probeSource = _probeGenerator.GenerateExecutableProbe(preamble, request);
            string probeSourceFile = Path.Combine(workDir, $"probe_{packageName}.c");
            File.WriteAllText(probeSourceFile, probeSource);

            // Compile probe to executable
            var compiler = _compilerDriver.Detect()!;
            string probeExe = Path.Combine(workDir, "probe" + GetExecutableExtension());

            string compileArgs;
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                compileArgs = $"/Fe:\"{probeExe}\" \"{probeSourceFile}\" {cflags}";
            }
            else
            {
                compileArgs = $"-o \"{probeExe}\" \"{probeSourceFile}\" {cflags}";
            }

            var (_, compileErrors) = RunProcess(compiler.Path, compileArgs);
            if (!File.Exists(probeExe))
            {
                return (null, $"cgo: probe compilation failed:\n{compileErrors}");
            }

            // Run probe executable
            var (probeOutput, probeErrors) = RunProcess(probeExe, "");
            if (string.IsNullOrEmpty(probeOutput))
            {
                return (null, $"cgo: probe execution failed:\n{probeErrors}");
            }

            // Parse results
            var result = _probeResultParser.Parse(probeOutput);
            return (result, null);
        }

        private static void SaveProbeResults(string path, CgoProbeResult? result)
        {
            if (result == null)
            {
                return;
            }

            // Simple key=value format matching probe output
            var sb = new StringBuilder();
            foreach (var kv in result.TypeSizes)
            {
                sb.AppendLine($"sizeof_{kv.Key}={kv.Value}");
            }
            foreach (var kv in result.TypeAlignments)
            {
                sb.AppendLine($"alignof_{kv.Key}={kv.Value}");
            }
            foreach (var kv in result.FieldOffsets)
            {
                sb.AppendLine($"offsetof_{kv.Key}={kv.Value}");
            }
            foreach (var kv in result.FieldSizes)
            {
                sb.AppendLine($"fieldsizeof_{kv.Key}={kv.Value}");
            }
            foreach (var kv in result.EnumValues)
            {
                sb.AppendLine($"enum_{kv.Key}={kv.Value}");
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string ComputeCacheKey(string cSource, string cflags, string ldflags, string os, string compilerVersion)
        {
            string input = $"{cSource}\n---\n{cflags}\n{ldflags}\n{os}\n{compilerVersion}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        }

        private static string GetExecutableExtension()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return ".exe";
            }
            return "";
        }

        private static (string stdout, string stderr) RunProcess(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return ("", $"Failed to start {fileName}");
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);

            return (stdout, stderr);
        }
    }
}
