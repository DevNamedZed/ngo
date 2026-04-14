using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Detects, invokes, and manages the system C compiler. Resolution
    /// precedence follows Go, with an ngo-specific <c>--cc</c> CLI flag
    /// on top: <c>--cc</c> &gt; <c>CC</c> env var &gt; platform-default
    /// auto-detection. When <c>CGO_ENABLED=0</c> is set explicitly, cgo
    /// is disabled regardless of compiler availability (matching Go).
    /// Failed resolution produces <see cref="CgoCompilerNotFoundException"/>
    /// or <see cref="CgoDisabledException"/>; there is no silent fallback.
    /// </summary>
    public class CCompilerDriver
    {
        private CgoCompilerResolution? _cachedResolution;
        private CgoOptions? _cachedOptionsKey;

        /// <summary>
        /// Resolve the C compiler according to the given options and the
        /// current process environment. Throws on failure; never returns
        /// with an unusable compiler. Results are cached per options
        /// instance so repeated resolution within one compile does not
        /// re-launch the compiler.
        /// </summary>
        public CgoCompilerResolution Resolve(CgoOptions? options = null, CgoEnvironment? environment = null)
        {
            CgoOptions effectiveOptions = options ?? CgoOptions.Empty;
            CgoEnvironment effectiveEnvironment = environment ?? CgoEnvironment.Load();

            if (_cachedResolution != null && ReferenceEquals(_cachedOptionsKey, effectiveOptions))
            {
                return _cachedResolution;
            }

            if (effectiveEnvironment.CgoEnabled == "0")
            {
                throw new CgoDisabledException(
                    "cgo: CGO_ENABLED=0 explicitly disables cgo. Unset the variable or set CGO_ENABLED=1 to compile code that uses `import \"C\"`.");
            }

            var attempts = new List<CgoCompilerResolutionAttempt>();

            if (!string.IsNullOrEmpty(effectiveOptions.CCOverride))
            {
                if (TryCompiler(effectiveOptions.CCOverride, CgoCompilerSource.CliFlag, attempts, out var viaCli))
                {
                    var resolution = new CgoCompilerResolution(viaCli, CgoCompilerSource.CliFlag);
                    _cachedResolution = resolution;
                    _cachedOptionsKey = effectiveOptions;
                    return resolution;
                }

                throw new CgoCompilerNotFoundException(
                    $"cgo: --cc \"{effectiveOptions.CCOverride}\" is not a working C compiler.",
                    attempts);
            }

            if (!string.IsNullOrEmpty(effectiveEnvironment.CC))
            {
                if (TryCompiler(effectiveEnvironment.CC, CgoCompilerSource.Environment, attempts, out var viaEnv))
                {
                    var resolution = new CgoCompilerResolution(viaEnv, CgoCompilerSource.Environment);
                    _cachedResolution = resolution;
                    _cachedOptionsKey = effectiveOptions;
                    return resolution;
                }

                throw new CgoCompilerNotFoundException(
                    $"cgo: CC=\"{effectiveEnvironment.CC}\" is not a working C compiler.",
                    attempts);
            }

            var autoCandidates = GetAutoDetectCandidates();
            foreach (var candidate in autoCandidates)
            {
                if (TryCompiler(candidate, CgoCompilerSource.AutoDetect, attempts, out var viaAuto))
                {
                    var resolution = new CgoCompilerResolution(viaAuto, CgoCompilerSource.AutoDetect);
                    _cachedResolution = resolution;
                    _cachedOptionsKey = effectiveOptions;
                    return resolution;
                }
            }

            throw new CgoCompilerNotFoundException(
                "cgo: no C compiler found.",
                attempts);
        }

        /// <summary>
        /// Detect the C compiler using default options. Equivalent to
        /// <c>Resolve(CgoOptions.Empty).Compiler</c>; throws on failure.
        /// </summary>
        public CCompilerInfo Detect()
        {
            return Resolve(CgoOptions.Empty).Compiler;
        }

        /// <summary>
        /// Preprocess a C source file (gcc -E equivalent). Returns the
        /// preprocessed source text on success. Throws
        /// <see cref="CgoCCompileException"/> if the preprocessor exits
        /// non-zero.
        /// </summary>
        public string Preprocess(string sourceFile, string cflags)
        {
            CCompilerInfo compiler = Detect();
            string args = $"-E {cflags} \"{sourceFile}\"";
            CompilerProcessResult result = ProcessRunner.Run(compiler.Path, args);
            if (!result.Succeeded)
            {
                throw new CgoCCompileException(
                    $"cgo: preprocessor \"{compiler.Path}\" exited with code {result.ExitCode}",
                    result.CombinedOutput);
            }
            return result.StandardOutput;
        }

        /// <summary>
        /// Compile a C source file to a shared library. Returns the
        /// absolute path of the produced library. Throws
        /// <see cref="CgoCCompileException"/> if the compiler fails.
        /// </summary>
        public string CompileSharedLibrary(
            string sourceFile, string outputDir, string packageName, string cflags, string ldflags)
        {
            CCompilerInfo compiler = Detect();

            string libName = GetSharedLibraryName(packageName);
            string outputPath = Path.Combine(outputDir, libName);

            string args;
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                args = $"/LD /Fe:\"{outputPath}\" \"{sourceFile}\" {cflags} link {ldflags}";
            }
            else
            {
                string picFlag = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "" : "-fPIC";
                args = $"-shared {picFlag} -o \"{outputPath}\" \"{sourceFile}\" {cflags} {ldflags}";
            }

            CompilerProcessResult result = ProcessRunner.Run(compiler.Path, args);
            if (!File.Exists(outputPath))
            {
                throw new CgoCCompileException(
                    $"cgo: shared library compilation failed using {compiler.Kind} at \"{compiler.Path}\"",
                    result.CombinedOutput);
            }
            return outputPath;
        }

        /// <summary>
        /// Compile a C source file to a static library (for AOT
        /// single-binary). Returns the absolute path of the produced
        /// library. Throws <see cref="CgoCCompileException"/> if either
        /// the compile or archive step fails. <paramref name="includeArgs"/>
        /// carries the <c>-I &lt;dir&gt;</c> switch for the package
        /// source directory so that <c>#include "foo.h"</c> inside the
        /// preamble resolves against package-local headers; pass an
        /// empty string when the preamble uses only standard headers.
        /// </summary>
        public string CompileStaticLibrary(
            string sourceFile, string outputDir, string packageName, string includeArgs, string cflags)
        {
            CCompilerInfo compiler = Detect();

            string objectFile = Path.Combine(outputDir, $"cgo_{packageName}.o");
            string libName = GetStaticLibraryName(packageName);
            string outputPath = Path.Combine(outputDir, libName);

            string compileArgs;
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                objectFile = Path.Combine(outputDir, $"cgo_{packageName}.obj");
                compileArgs = $"/c /Fo\"{objectFile}\" \"{sourceFile}\" {includeArgs} {cflags}";
            }
            else
            {
                string picFlag = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "" : "-fPIC";
                compileArgs = $"-c {picFlag} -o \"{objectFile}\" \"{sourceFile}\" {includeArgs} {cflags}";
            }

            CompilerProcessResult compileResult = ProcessRunner.Run(compiler.Path, compileArgs);
            if (!File.Exists(objectFile))
            {
                throw new CgoCCompileException(
                    $"cgo: static library compile stage failed using {compiler.Kind} at \"{compiler.Path}\"",
                    compileResult.CombinedOutput);
            }

            CompilerProcessResult archiveResult;
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                string libExe = Path.Combine(Path.GetDirectoryName(compiler.Path) ?? string.Empty, "lib.exe");
                archiveResult = ProcessRunner.Run(libExe, $"/OUT:\"{outputPath}\" \"{objectFile}\"");
            }
            else
            {
                archiveResult = ProcessRunner.Run("ar", $"rcs \"{outputPath}\" \"{objectFile}\"");
            }

            if (!File.Exists(outputPath))
            {
                throw new CgoCCompileException(
                    "cgo: failed to create static library (archive step produced no output)",
                    archiveResult.CombinedOutput);
            }

            return outputPath;
        }

        /// <summary>
        /// Link one or more static libraries (.a/.lib) into a single
        /// shared library. Called at final build time to produce the
        /// runtime native library. Returns the absolute path of the
        /// produced library. Throws <see cref="CgoCCompileException"/>
        /// on link failure.
        /// </summary>
        public string LinkStaticLibraries(
            IReadOnlyList<string> staticLibPaths, string outputDir, string outputName, string ldflags)
        {
            CCompilerInfo compiler = Detect();

            string libName = GetSharedLibraryName(outputName);
            string outputPath = Path.Combine(outputDir, libName);

            string libArgs = string.Join(" ", staticLibPaths.Select(p => $"\"{p}\""));

            string args;
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                args = $"/LD /Fe:\"{outputPath}\" {libArgs} link {ldflags}";
            }
            else
            {
                string picFlag = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "" : "-fPIC";
                string wholeArchiveStart = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "-Wl,-force_load"
                    : "-Wl,--whole-archive";
                string wholeArchiveEnd = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? string.Empty
                    : "-Wl,--no-whole-archive";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string forceLoadArgs = string.Join(" ", staticLibPaths.Select(p => $"-Wl,-force_load,\"{p}\""));
                    args = $"-shared {picFlag} -o \"{outputPath}\" {forceLoadArgs} {ldflags}";
                }
                else
                {
                    args = $"-shared {picFlag} -o \"{outputPath}\" {wholeArchiveStart} {libArgs} {wholeArchiveEnd} {ldflags}";
                }
            }

            CompilerProcessResult result = ProcessRunner.Run(compiler.Path, args);
            if (!File.Exists(outputPath))
            {
                throw new CgoCCompileException(
                    $"cgo: link failed using {compiler.Kind} at \"{compiler.Path}\"",
                    result.CombinedOutput);
            }
            return outputPath;
        }

        /// <summary>
        /// Compile a probe source file to an object file for symbol
        /// extraction. Returns the absolute path of the produced
        /// object. Throws <see cref="CgoCCompileException"/> if the
        /// compile step fails.
        /// </summary>
        public string CompileProbe(string sourceFile, string outputDir, string cflags)
        {
            CCompilerInfo compiler = Detect();

            string objectFile = Path.Combine(outputDir, "probe.o");
            string args;
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                objectFile = Path.Combine(outputDir, "probe.obj");
                args = $"/c /Fo\"{objectFile}\" \"{sourceFile}\" {cflags}";
            }
            else
            {
                args = $"-c -g -o \"{objectFile}\" \"{sourceFile}\" {cflags}";
            }

            CompilerProcessResult result = ProcessRunner.Run(compiler.Path, args);
            if (!File.Exists(objectFile))
            {
                throw new CgoCCompileException(
                    $"cgo: probe object compilation failed using {compiler.Kind} at \"{compiler.Path}\"",
                    result.CombinedOutput);
            }
            return objectFile;
        }

        /// <summary>
        /// Get the current OS identifier for #cgo directive filtering.
        /// Matches Go's GOOS values.
        /// </summary>
        public static string GetCurrentOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "windows";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "darwin";
            }
            return "linux";
        }

        /// <summary>
        /// Get the platform-appropriate shared library file name.
        /// </summary>
        public static string GetSharedLibraryName(string packageName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"cgo_{packageName}.dll";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"libcgo_{packageName}.dylib";
            }
            return $"libcgo_{packageName}.so";
        }

        /// <summary>
        /// Get the platform-appropriate static library file name.
        /// </summary>
        public static string GetStaticLibraryName(string packageName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsMinGW())
            {
                return $"cgo_{packageName}.lib";
            }
            return $"libcgo_{packageName}.a";
        }

        private static IReadOnlyList<string> GetAutoDetectCandidates()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new[] { "cl.exe", "clang-cl.exe", "clang.exe", "gcc.exe" };
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new[] { "cc", "clang", "gcc" };
            }
            return new[] { "cc", "gcc", "clang" };
        }

        private bool TryCompiler(
            string commandOrPath,
            CgoCompilerSource source,
            List<CgoCompilerResolutionAttempt> attempts,
            out CCompilerInfo compiler)
        {
            compiler = null!;

            if (IsMSVCCommand(commandOrPath))
            {
                if (TryMSVC(commandOrPath, source, attempts, out var msvc))
                {
                    compiler = msvc;
                    return true;
                }
                return false;
            }

            string versionOutput;
            try
            {
                CompilerProcessResult probe = ProcessRunner.Run(commandOrPath, "--version");
                versionOutput = !string.IsNullOrEmpty(probe.StandardOutput)
                    ? probe.StandardOutput
                    : probe.StandardError;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                attempts.Add(new CgoCompilerResolutionAttempt(
                    source, commandOrPath, $"process launch failed: {ex.Message}", succeeded: false));
                return false;
            }
            catch (InvalidOperationException ex)
            {
                attempts.Add(new CgoCompilerResolutionAttempt(
                    source, commandOrPath, $"process launch failed: {ex.Message}", succeeded: false));
                return false;
            }

            if (string.IsNullOrEmpty(versionOutput))
            {
                attempts.Add(new CgoCompilerResolutionAttempt(
                    source, commandOrPath, "--version produced no output", succeeded: false));
                return false;
            }

            CCompilerKind kind = DetectKind(versionOutput);
            string version = ExtractVersion(versionOutput);
            compiler = new CCompilerInfo(commandOrPath, kind, version);
            attempts.Add(new CgoCompilerResolutionAttempt(
                source, commandOrPath, $"OK {kind} {version}", succeeded: true));
            return true;
        }

        private bool TryMSVC(
            string commandOrPath,
            CgoCompilerSource source,
            List<CgoCompilerResolutionAttempt> attempts,
            out CCompilerInfo compiler)
        {
            compiler = null!;

            if (source == CgoCompilerSource.AutoDetect && string.Equals(commandOrPath, "cl.exe", StringComparison.OrdinalIgnoreCase))
            {
                string? vswherePath = LocateMSVCViaVswhere();
                if (vswherePath != null)
                {
                    compiler = new CCompilerInfo(vswherePath, CCompilerKind.MSVC, "");
                    attempts.Add(new CgoCompilerResolutionAttempt(
                        source, vswherePath, "OK MSVC (located via vswhere)", succeeded: true));
                    return true;
                }
            }

            try
            {
                CompilerProcessResult probe = ProcessRunner.Run(commandOrPath, string.Empty);
                string combined = probe.CombinedOutput;
                if (combined.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                {
                    compiler = new CCompilerInfo(commandOrPath, CCompilerKind.MSVC, string.Empty);
                    attempts.Add(new CgoCompilerResolutionAttempt(
                        source, commandOrPath, "OK MSVC", succeeded: true));
                    return true;
                }

                attempts.Add(new CgoCompilerResolutionAttempt(
                    source, commandOrPath, "banner did not identify as MSVC", succeeded: false));
                return false;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                attempts.Add(new CgoCompilerResolutionAttempt(
                    source, commandOrPath, $"process launch failed: {ex.Message}", succeeded: false));
                return false;
            }
            catch (InvalidOperationException ex)
            {
                attempts.Add(new CgoCompilerResolutionAttempt(
                    source, commandOrPath, $"process launch failed: {ex.Message}", succeeded: false));
                return false;
            }
        }

        private static bool IsMSVCCommand(string commandOrPath)
        {
            string name = Path.GetFileName(commandOrPath);
            return string.Equals(name, "cl.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "cl", StringComparison.OrdinalIgnoreCase);
        }

        private static string? LocateMSVCViaVswhere()
        {
            try
            {
                CompilerProcessResult probe = ProcessRunner.Run("vswhere",
                    "-latest -find VC\\Tools\\MSVC\\**\\bin\\Hostx64\\x64\\cl.exe");
                if (string.IsNullOrEmpty(probe.StandardOutput))
                {
                    return null;
                }

                string clPath = probe.StandardOutput.Trim().Split('\n')[0].Trim();
                if (File.Exists(clPath))
                {
                    return clPath;
                }
                return null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static CCompilerKind DetectKind(string versionOutput)
        {
            if (versionOutput.Contains("clang", StringComparison.OrdinalIgnoreCase))
            {
                return CCompilerKind.Clang;
            }
            if (versionOutput.Contains("gcc", StringComparison.OrdinalIgnoreCase)
                || versionOutput.Contains("Free Software Foundation", StringComparison.OrdinalIgnoreCase))
            {
                return CCompilerKind.GCC;
            }
            return CCompilerKind.GCC;
        }

        private static string ExtractVersion(string output)
        {
            int newline = output.IndexOf('\n');
            if (newline > 0)
            {
                return output.Substring(0, newline).Trim();
            }
            return output.Trim();
        }

        private static bool IsMinGW()
        {
            string? cc = Environment.GetEnvironmentVariable("CC");
            if (cc == null)
            {
                return false;
            }
            return cc.Contains("gcc", StringComparison.OrdinalIgnoreCase);
        }
    }
}
