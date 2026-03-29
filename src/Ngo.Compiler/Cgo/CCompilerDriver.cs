using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Detects, invokes, and manages the system C compiler.
    /// Follows Go's detection order: CGO_ENABLED → CC env var → platform defaults.
    /// </summary>
    public class CCompilerDriver
    {
        private CCompilerInfo? _cachedCompiler;

        /// <summary>
        /// Detect the C compiler available on the current system.
        /// Returns null if no compiler is found.
        /// </summary>
        public CCompilerInfo? Detect()
        {
            if (_cachedCompiler != null)
            {
                return _cachedCompiler;
            }

            // Check CGO_ENABLED
            string? cgoEnabled = Environment.GetEnvironmentVariable("CGO_ENABLED");
            if (cgoEnabled == "0")
            {
                return null;
            }

            // Check CC environment variable (same as Go)
            string? ccEnv = Environment.GetEnvironmentVariable("CC");
            if (!string.IsNullOrEmpty(ccEnv))
            {
                var info = TryCompiler(ccEnv);
                if (info != null)
                {
                    _cachedCompiler = info;
                    return info;
                }
            }

            // Platform-specific detection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _cachedCompiler = DetectWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _cachedCompiler = DetectMacOS();
            }
            else
            {
                _cachedCompiler = DetectLinux();
            }

            return _cachedCompiler;
        }

        /// <summary>
        /// Preprocess a C source file (gcc -E equivalent).
        /// Returns the preprocessed output.
        /// </summary>
        public (string output, string errors) Preprocess(string sourceFile, string cflags)
        {
            var compiler = Detect() ?? throw new InvalidOperationException(GetMissingCompilerMessage());
            string args = $"-E {cflags} \"{sourceFile}\"";
            return RunCompiler(compiler.Path, args);
        }

        /// <summary>
        /// Compile a C source file to a shared library.
        /// Returns the path to the compiled library.
        /// </summary>
        public (string? libraryPath, string? error) CompileSharedLibrary(
            string sourceFile, string outputDir, string packageName, string cflags, string ldflags)
        {
            var compiler = Detect() ?? throw new InvalidOperationException(GetMissingCompilerMessage());

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

            var (_, errors) = RunCompiler(compiler.Path, args);
            if (!string.IsNullOrEmpty(errors) && !File.Exists(outputPath))
            {
                return (null, $"cgo: C compilation failed:\n{errors}");
            }

            return (outputPath, null);
        }

        /// <summary>
        /// Compile a C source file to a static library (for AOT single-binary).
        /// </summary>
        public (string? libraryPath, string? error) CompileStaticLibrary(
            string sourceFile, string outputDir, string packageName, string cflags)
        {
            var compiler = Detect() ?? throw new InvalidOperationException(GetMissingCompilerMessage());

            string objectFile = Path.Combine(outputDir, $"cgo_{packageName}.o");
            string libName = GetStaticLibraryName(packageName);
            string outputPath = Path.Combine(outputDir, libName);

            // Step 1: Compile to object file
            string compileArgs;
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                objectFile = Path.Combine(outputDir, $"cgo_{packageName}.obj");
                compileArgs = $"/c /Fo\"{objectFile}\" \"{sourceFile}\" {cflags}";
            }
            else
            {
                string picFlag = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "" : "-fPIC";
                compileArgs = $"-c {picFlag} -o \"{objectFile}\" \"{sourceFile}\" {cflags}";
            }

            var (_, compileErrors) = RunCompiler(compiler.Path, compileArgs);
            if (!File.Exists(objectFile))
            {
                return (null, $"cgo: C compilation failed:\n{compileErrors}");
            }

            // Step 2: Create static library
            if (compiler.Kind == CCompilerKind.MSVC)
            {
                string libExe = Path.Combine(Path.GetDirectoryName(compiler.Path) ?? "", "lib.exe");
                RunCompiler(libExe, $"/OUT:\"{outputPath}\" \"{objectFile}\"");
            }
            else
            {
                RunCompiler("ar", $"rcs \"{outputPath}\" \"{objectFile}\"");
            }

            if (!File.Exists(outputPath))
            {
                return (null, "cgo: failed to create static library");
            }

            return (outputPath, null);
        }

        /// <summary>
        /// Link one or more static libraries (.a/.lib) into a single shared library.
        /// This is called at final build time to produce the runtime native library.
        /// </summary>
        public (string? libraryPath, string? error) LinkStaticLibraries(
            IReadOnlyList<string> staticLibPaths, string outputDir, string outputName, string ldflags)
        {
            var compiler = Detect() ?? throw new InvalidOperationException(GetMissingCompilerMessage());

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
                // Wrap static libs in --whole-archive so all symbols are included
                string picFlag = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "" : "-fPIC";
                string wholeArchiveStart = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "-Wl,-force_load"
                    : "-Wl,--whole-archive";
                string wholeArchiveEnd = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? ""
                    : "-Wl,--no-whole-archive";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // macOS: -force_load takes one lib at a time
                    var forceLoadArgs = string.Join(" ", staticLibPaths.Select(p => $"-Wl,-force_load,\"{p}\""));
                    args = $"-shared {picFlag} -o \"{outputPath}\" {forceLoadArgs} {ldflags}";
                }
                else
                {
                    args = $"-shared {picFlag} -o \"{outputPath}\" {wholeArchiveStart} {libArgs} {wholeArchiveEnd} {ldflags}";
                }
            }

            var (_, errors) = RunCompiler(compiler.Path, args);
            if (!string.IsNullOrEmpty(errors) && !File.Exists(outputPath))
            {
                return (null, $"cgo: link failed:\n{errors}");
            }

            return (outputPath, null);
        }

        /// <summary>
        /// Compile a probe file to extract type information.
        /// Returns the path to the compiled object file.
        /// </summary>
        public (string? objectPath, string? error) CompileProbe(string sourceFile, string outputDir, string cflags)
        {
            var compiler = Detect() ?? throw new InvalidOperationException(GetMissingCompilerMessage());

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

            var (_, errors) = RunCompiler(compiler.Path, args);
            if (!File.Exists(objectFile))
            {
                return (null, $"cgo: probe compilation failed:\n{errors}");
            }

            return (objectFile, null);
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

        private CCompilerInfo? DetectWindows()
        {
            // 1. Try cl.exe via vswhere
            var clPath = TryFindMSVC();
            if (clPath != null)
            {
                return clPath;
            }

            // 2. Try clang
            var clang = TryCompiler("clang");
            if (clang != null)
            {
                return clang;
            }

            // 3. Try gcc (MinGW)
            var gcc = TryCompiler("gcc");
            if (gcc != null)
            {
                return gcc;
            }

            return null;
        }

        private CCompilerInfo? DetectLinux()
        {
            // 1. Try cc (system default)
            var cc = TryCompiler("cc");
            if (cc != null)
            {
                return cc;
            }

            // 2. Try gcc
            var gcc = TryCompiler("gcc");
            if (gcc != null)
            {
                return gcc;
            }

            // 3. Try clang
            var clang = TryCompiler("clang");
            if (clang != null)
            {
                return clang;
            }

            return null;
        }

        private CCompilerInfo? DetectMacOS()
        {
            // 1. Try cc (usually Xcode clang)
            var cc = TryCompiler("cc");
            if (cc != null)
            {
                return cc;
            }

            // 2. Try clang
            var clang = TryCompiler("clang");
            if (clang != null)
            {
                return clang;
            }

            // 3. Try gcc (may be clang alias)
            var gcc = TryCompiler("gcc");
            if (gcc != null)
            {
                return gcc;
            }

            return null;
        }

        private CCompilerInfo? TryCompiler(string name)
        {
            try
            {
                var (output, _) = RunCompiler(name, "--version");
                if (string.IsNullOrEmpty(output))
                {
                    return null;
                }

                CCompilerKind kind;
                if (output.Contains("clang", StringComparison.OrdinalIgnoreCase))
                {
                    kind = CCompilerKind.Clang;
                }
                else if (output.Contains("gcc", StringComparison.OrdinalIgnoreCase) ||
                         output.Contains("Free Software Foundation", StringComparison.OrdinalIgnoreCase))
                {
                    kind = CCompilerKind.GCC;
                }
                else
                {
                    kind = CCompilerKind.GCC; // Default assumption
                }

                string version = ExtractVersion(output);
                return new CCompilerInfo(name, kind, version);
            }
            catch
            {
                return null;
            }
        }

        private CCompilerInfo? TryFindMSVC()
        {
            try
            {
                // Try vswhere to locate cl.exe
                var (output, _) = RunProcess("vswhere",
                    "-latest -find VC\\Tools\\MSVC\\**\\bin\\Hostx64\\x64\\cl.exe");
                if (!string.IsNullOrEmpty(output))
                {
                    string clPath = output.Trim().Split('\n')[0].Trim();
                    if (File.Exists(clPath))
                    {
                        return new CCompilerInfo(clPath, CCompilerKind.MSVC, "");
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }

            // Fallback: try cl.exe directly (may work if VS developer prompt is active)
            try
            {
                var (output, _) = RunProcess("cl.exe", "");
                if (output.Contains("Microsoft"))
                {
                    return new CCompilerInfo("cl.exe", CCompilerKind.MSVC, "");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }

            return null;
        }

        private static (string stdout, string stderr) RunCompiler(string compiler, string arguments)
        {
            return RunProcess(compiler, arguments);
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
            process.WaitForExit(30000); // 30 second timeout

            return (stdout, stderr);
        }

        private static string ExtractVersion(string output)
        {
            // Extract first line as version
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
            return cc != null && cc.Contains("gcc", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMissingCompilerMessage()
        {
            string platform = GetCurrentOS();
            string installHint = platform switch
            {
                "windows" => "Install Visual Studio Build Tools, or MinGW-w64, or LLVM/clang",
                "darwin" => "Run: xcode-select --install",
                "linux" => "Run: sudo apt install gcc (Debian/Ubuntu) or sudo dnf install gcc (Fedora)",
                _ => "Install a C compiler (gcc, clang, or MSVC)",
            };
            return $"cgo: C compiler not found. {installHint}, or set the CC environment variable.";
        }
    }

    // CCompilerInfo and CCompilerKind moved to CgoModels.cs
}
