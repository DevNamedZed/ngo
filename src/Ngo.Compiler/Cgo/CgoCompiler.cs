using System;
using System.Collections.Generic;
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
        private readonly CgoCompilerResolution _resolution;
        private readonly CgoProbeGenerator _probeGenerator;
        private readonly CgoProbeResultParser _probeResultParser;
        private readonly string _cacheDirectory;

        public CgoCompiler(string cacheDirectory, CCompilerDriver compilerDriver, CgoCompilerResolution resolution)
        {
            _compilerDriver = compilerDriver;
            _resolution = resolution;
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
            CCompilerInfo compiler = _resolution.Compiler;
            result.CompilerInfo = compiler;

            string currentOS = CCompilerDriver.GetCurrentOS();
            string cflags = preamble.GetCFlags(currentOS);
            string ldflags = preamble.GetLDFlags(currentOS);
            string cacheKey = ComputeCacheKey(preamble.CSource, cflags, ldflags, currentOS, compiler.Version);
            string cacheDir = Path.Combine(_cacheDirectory, "cgo", cacheKey);

            string cachedLibrary = Path.Combine(cacheDir, CCompilerDriver.GetStaticLibraryName(packageName));
            string cachedProbe = Path.Combine(cacheDir, "probe.json");

            if (File.Exists(cachedLibrary) && File.Exists(cachedProbe))
            {
                result.ProbeResult = _probeResultParser.Parse(File.ReadAllText(cachedProbe));
                result.NativeLibraryPath = cachedLibrary;
                result.LDFlags = ldflags;
                result.CacheHit = true;
                return result;
            }

            Directory.CreateDirectory(cacheDir);

            if (probeRequest.TypeSizes.Count > 0
                || probeRequest.FieldOffsets.Count > 0
                || probeRequest.EnumValues.Count > 0)
            {
                result.ProbeResult = RunProbe(preamble, probeRequest, cacheDir, packageName, cflags);
                SaveProbeResults(cachedProbe, result.ProbeResult);
            }
            else
            {
                result.ProbeResult = new CgoProbeResult();
            }

            if (preamble.HasCSource)
            {
                string cSourceFile = Path.Combine(cacheDir, $"cgo_{packageName}.c");
                File.WriteAllText(cSourceFile, preamble.CSource);

                string includeArgs = BuildIncludeArgs(preamble);
                result.NativeLibraryPath = _compilerDriver.CompileStaticLibrary(
                    cSourceFile, cacheDir, packageName, includeArgs, cflags);
                result.LDFlags = ldflags;
            }

            return result;
        }

        private CgoProbeResult RunProbe(
            CgoPreamble preamble, CgoProbeRequest request,
            string workDir, string packageName, string cflags)
        {
            string probeSource = _probeGenerator.GenerateExecutableProbe(preamble, request);
            string probeSourceFile = Path.Combine(workDir, $"probe_{packageName}.c");
            File.WriteAllText(probeSourceFile, probeSource);

            CCompilerInfo compiler = _resolution.Compiler;
            string probeExe = Path.Combine(workDir, "probe" + GetExecutableExtension());
            string includeArgs = BuildIncludeArgs(preamble);

            string compileArgs;
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                compileArgs = $"/Fe:\"{probeExe}\" \"{probeSourceFile}\" {includeArgs} {cflags}";
            }
            else
            {
                compileArgs = $"-o \"{probeExe}\" \"{probeSourceFile}\" {includeArgs} {cflags}";
            }

            CompilerProcessResult compileResult = ProcessRunner.Run(compiler.Path, compileArgs);
            if (!File.Exists(probeExe))
            {
                throw new CgoProbeCompileException(
                    $"cgo: probe compilation failed using {compiler.Kind} at \"{compiler.Path}\"",
                    compileResult.CombinedOutput);
            }

            CompilerProcessResult probeResult = ProcessRunner.Run(probeExe, string.Empty);
            if (string.IsNullOrEmpty(probeResult.StandardOutput))
            {
                throw new CgoProbeCompileException(
                    "cgo: probe executable produced no output",
                    probeResult.CombinedOutput);
            }

            return _probeResultParser.Parse(probeResult.StandardOutput);
        }

        /// <summary>
        /// Compile the anchor probe: the preamble plus sizeof references
        /// to every identifier collected by <see cref="CgoUsageCollector"/>,
        /// emitted with full debug info. The resulting object file carries
        /// DWARF (gcc/clang) or is paired with a PDB (MSVC) that the
        /// <c>ICgoSymbolSource</c> implementations read in a later stage.
        /// Throws <see cref="CgoCCompileException"/> on compile failure
        /// — there is no silent fallback. The object file is cached on
        /// a hash of the preamble + usage names + compiler identity.
        /// </summary>
        public CgoAnchorProbeBuildResult CompileAnchorProbe(
            CgoPreamble preamble, CgoUsageSet usageSet, string packageName)
        {
            CCompilerInfo compiler = _resolution.Compiler;
            string currentOS = CCompilerDriver.GetCurrentOS();
            string cflags = preamble.GetCFlags(currentOS);

            string cacheKey = ComputeAnchorCacheKey(preamble, usageSet, cflags, currentOS, compiler);
            string cacheDir = Path.Combine(_cacheDirectory, "cgo", cacheKey);
            Directory.CreateDirectory(cacheDir);

            string objectExtension = compiler.Kind == CCompilerKind.MSVC ? ".obj" : ".o";
            string objectFile = Path.Combine(cacheDir, $"anchor_{packageName}{objectExtension}");
            string? programDatabase = null;

            if (compiler.Kind == CCompilerKind.MSVC)
            {
                programDatabase = Path.Combine(cacheDir, $"anchor_{packageName}.pdb");
                if (File.Exists(objectFile) && File.Exists(programDatabase))
                {
                    return new CgoAnchorProbeBuildResult(objectFile, compiler, programDatabase);
                }
            }
            else if (File.Exists(objectFile))
            {
                return new CgoAnchorProbeBuildResult(objectFile, compiler, null);
            }

            string probeSource = _probeGenerator.GenerateAnchorProbe(preamble, usageSet, compiler.Kind);
            string probeSourceFile = Path.Combine(cacheDir, $"anchor_{packageName}.c");
            File.WriteAllText(probeSourceFile, probeSource);

            string includeArgs = BuildIncludeArgs(preamble);
            string compileArgs = BuildAnchorCompileArgs(
                compiler, probeSourceFile, objectFile, programDatabase, includeArgs, cflags);
            CompilerProcessResult compileResult = ProcessRunner.Run(compiler.Path, compileArgs);

            if (!File.Exists(objectFile))
            {
                PersistFailedAnchorProbe(cacheDir, probeSourceFile, compileResult);
                throw new CgoCCompileException(
                    $"cgo: anchor probe compilation failed using {compiler.Kind} at \"{compiler.Path}\"",
                    compileResult.CombinedOutput);
            }

            ClearFailedAnchorProbe(cacheDir);
            return new CgoAnchorProbeBuildResult(objectFile, compiler, programDatabase);
        }

        /// <summary>
        /// Compile the macro probe for the subset of C identifiers that
        /// the typeof anchor probe could not resolve to a concrete
        /// symbol — in practice these are <c>#define</c> constants that
        /// DWARF does not carry. The probe wraps each in an anonymous
        /// enum so the compiler evaluates the preprocessor expression
        /// and emits the result as a <see cref="DwarfTag.Enumerator"/>
        /// DIE; the DWARF reader harvests those enumerators and
        /// registers them as <see cref="CgoMacroConstantInfo"/> entries.
        /// Throws <see cref="CgoCCompileException"/> on compile failure
        /// because a macro that rejects integer evaluation could never
        /// appear as a <c>C.X</c> value from Go anyway. Cached on a key
        /// disjoint from the typeof anchor probe so the two runs never
        /// clash.
        /// </summary>
        public CgoAnchorProbeBuildResult CompileMacroProbe(
            CgoPreamble preamble, IReadOnlyList<string> macroNames, string packageName)
        {
            if (macroNames == null)
            {
                throw new ArgumentNullException(nameof(macroNames));
            }
            if (macroNames.Count == 0)
            {
                throw new ArgumentException(
                    "CompileMacroProbe requires at least one macro name; " +
                    "the caller should skip the probe entirely when there are none.",
                    nameof(macroNames));
            }

            CCompilerInfo compiler = _resolution.Compiler;
            string currentOS = CCompilerDriver.GetCurrentOS();
            string cflags = preamble.GetCFlags(currentOS);

            string cacheKey = ComputeMacroCacheKey(preamble, macroNames, cflags, currentOS, compiler);
            string cacheDir = Path.Combine(_cacheDirectory, "cgo", cacheKey);
            Directory.CreateDirectory(cacheDir);

            string objectExtension = compiler.Kind == CCompilerKind.MSVC ? ".obj" : ".o";
            string objectFile = Path.Combine(cacheDir, $"macro_{packageName}{objectExtension}");
            string? programDatabase = null;

            if (compiler.Kind == CCompilerKind.MSVC)
            {
                programDatabase = Path.Combine(cacheDir, $"macro_{packageName}.pdb");
                if (File.Exists(objectFile) && File.Exists(programDatabase))
                {
                    return new CgoAnchorProbeBuildResult(objectFile, compiler, programDatabase);
                }
            }
            else if (File.Exists(objectFile))
            {
                return new CgoAnchorProbeBuildResult(objectFile, compiler, null);
            }

            string probeSource = _probeGenerator.GenerateMacroProbe(preamble, macroNames, compiler.Kind);
            string probeSourceFile = Path.Combine(cacheDir, $"macro_{packageName}.c");
            File.WriteAllText(probeSourceFile, probeSource);

            string includeArgs = BuildIncludeArgs(preamble);
            string compileArgs = BuildMacroCompileArgs(
                compiler, probeSourceFile, objectFile, programDatabase, includeArgs, cflags);
            CompilerProcessResult compileResult = ProcessRunner.Run(compiler.Path, compileArgs);

            if (!File.Exists(objectFile))
            {
                PersistFailedMacroProbe(cacheDir, probeSourceFile, compileResult);
                throw new CgoCCompileException(
                    $"cgo: macro probe compilation failed using {compiler.Kind} at \"{compiler.Path}\"",
                    compileResult.CombinedOutput);
            }

            ClearFailedMacroProbe(cacheDir);
            return new CgoAnchorProbeBuildResult(objectFile, compiler, programDatabase);
        }

        private static string BuildMacroCompileArgs(
            CCompilerInfo compiler,
            string probeSourceFile,
            string objectFile,
            string? programDatabase,
            string includeArgs,
            string cflags)
        {
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                throw new System.NotSupportedException(
                    "cgo macro probe compilation for MSVC is not yet implemented.");
            }

            return $"-c -g -gdwarf-4 -o \"{objectFile}\" \"{probeSourceFile}\" {includeArgs} {cflags}";
        }

        private static void PersistFailedAnchorProbe(
            string cacheDir, string probeSourceFile, CompilerProcessResult compileResult)
        {
            string failedDir = Path.Combine(cacheDir, "failed");
            if (Directory.Exists(failedDir))
            {
                Directory.Delete(failedDir, recursive: true);
            }
            Directory.CreateDirectory(failedDir);
            if (File.Exists(probeSourceFile))
            {
                File.Copy(probeSourceFile, Path.Combine(failedDir, "probe.c"), overwrite: true);
            }
            File.WriteAllText(Path.Combine(failedDir, "stderr.txt"), compileResult.CombinedOutput ?? string.Empty);
        }

        private static void ClearFailedAnchorProbe(string cacheDir)
        {
            string failedDir = Path.Combine(cacheDir, "failed");
            if (Directory.Exists(failedDir))
            {
                Directory.Delete(failedDir, recursive: true);
            }
        }

        private static void PersistFailedMacroProbe(
            string cacheDir, string probeSourceFile, CompilerProcessResult compileResult)
        {
            string failedDir = Path.Combine(cacheDir, "failed");
            if (Directory.Exists(failedDir))
            {
                Directory.Delete(failedDir, recursive: true);
            }
            Directory.CreateDirectory(failedDir);
            if (File.Exists(probeSourceFile))
            {
                File.Copy(probeSourceFile, Path.Combine(failedDir, "probe.c"), overwrite: true);
            }
            File.WriteAllText(
                Path.Combine(failedDir, "stderr.txt"),
                compileResult.CombinedOutput ?? string.Empty);
        }

        private static void ClearFailedMacroProbe(string cacheDir)
        {
            string failedDir = Path.Combine(cacheDir, "failed");
            if (Directory.Exists(failedDir))
            {
                Directory.Delete(failedDir, recursive: true);
            }
        }

        private static string BuildAnchorCompileArgs(
            CCompilerInfo compiler,
            string probeSourceFile,
            string objectFile,
            string? programDatabase,
            string includeArgs,
            string cflags)
        {
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                string pdbArg = programDatabase != null ? $"/Fd\"{programDatabase}\" " : string.Empty;
                return $"/c /Z7 {pdbArg}/Fo\"{objectFile}\" \"{probeSourceFile}\" {includeArgs} {cflags}";
            }

            return $"-c -g -gdwarf-4 -o \"{objectFile}\" \"{probeSourceFile}\" {includeArgs} {cflags}";
        }

        /// <summary>
        /// Build the <c>-I &lt;dir&gt;</c> argument for the package source
        /// directory so that <c>#include "foo.h"</c> resolves to headers
        /// that ship with the Go package. MSVC accepts <c>-I</c> as well
        /// as <c>/I</c>, so a single form works across toolchains. The
        /// path is resolved to absolute at the time the compile command
        /// is built to avoid CWD-sensitivity when the child process
        /// inherits an unexpected working directory.
        /// </summary>
        private static string BuildIncludeArgs(CgoPreamble preamble)
        {
            string sourceDirectory = preamble.SourceDirectory;
            if (string.IsNullOrEmpty(sourceDirectory))
            {
                return string.Empty;
            }
            string absolutePath = Path.GetFullPath(sourceDirectory);
            return $"-I \"{absolutePath}\"";
        }

        private static string ComputeAnchorCacheKey(
            CgoPreamble preamble,
            CgoUsageSet usageSet,
            string cflags,
            string os,
            CCompilerInfo compiler)
        {
            var sb = new StringBuilder();
            sb.Append(preamble.CSource);
            sb.Append("\n---usage---\n");
            foreach (string name in usageSet.Names)
            {
                sb.Append(name);
                sb.Append('\n');
            }
            sb.Append("---cflags---\n");
            sb.Append(cflags);
            sb.Append("\n---includeDir---\n");
            sb.Append(string.IsNullOrEmpty(preamble.SourceDirectory)
                ? string.Empty
                : Path.GetFullPath(preamble.SourceDirectory));
            sb.Append("\n---os---\n");
            sb.Append(os);
            sb.Append("\n---compiler---\n");
            sb.Append(compiler.Kind);
            sb.Append('|');
            sb.Append(compiler.Version);
            sb.Append("\n---scheme---\n");
            sb.Append(CgoProbeGenerator.AnchorProbeSchemeVersion);

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            return "anchor_" + Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        }

        private static string ComputeMacroCacheKey(
            CgoPreamble preamble,
            IReadOnlyList<string> macroNames,
            string cflags,
            string os,
            CCompilerInfo compiler)
        {
            var sb = new StringBuilder();
            sb.Append(preamble.CSource);
            sb.Append("\n---macros---\n");
            foreach (string name in macroNames)
            {
                sb.Append(name);
                sb.Append('\n');
            }
            sb.Append("---cflags---\n");
            sb.Append(cflags);
            sb.Append("\n---includeDir---\n");
            sb.Append(string.IsNullOrEmpty(preamble.SourceDirectory)
                ? string.Empty
                : Path.GetFullPath(preamble.SourceDirectory));
            sb.Append("\n---os---\n");
            sb.Append(os);
            sb.Append("\n---compiler---\n");
            sb.Append(compiler.Kind);
            sb.Append('|');
            sb.Append(compiler.Version);
            sb.Append("\n---scheme---\n");
            sb.Append(CgoProbeGenerator.MacroProbeSchemeVersion);

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            return "macro_" + Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        }

        private static void SaveProbeResults(string path, CgoProbeResult? result)
        {
            if (result == null)
            {
                return;
            }

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

    }
}
