// -----------------------------------------------------------------------
// <copyright file="GoPackageResolver.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Language;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Resolves Go packages from source or .ngo cache.
    /// Per-compilation instance — holds the module resolver and DAG state.
    /// </summary>
    public sealed class GoPackageResolver : IPackageResolver
    {
        private readonly CompilationContext _ctx;
        private readonly GoModuleResolver _moduleResolver;
        private readonly Dictionary<string, PackageSymbol> _resolvedPackages = new();
        private readonly Dictionary<string, string> _resolvedDirs = new();
        private readonly HashSet<string> _discoveredPackages = new();
        // _isAnalyzingDag removed — per-package cycle detection via _beingResolved
        private readonly HashSet<string> _beingResolved = new();
        private readonly string? _goStdlibSrc;

        public GoPackageResolver(CompilationContext ctx, string projectRoot)
        {
            _ctx = ctx;
            _moduleResolver = new GoModuleResolver(ctx.Log);
            ProjectRoot = projectRoot;
            _moduleResolver.LoadGoMod(projectRoot);
            _goStdlibSrc = FindGoStdlibSource();
        }

        /// <summary>
        /// Returns true for packages that must use the C# runtime and cannot be
        /// compiled from Go source. These are packages whose Go source uses
        /// assembly stubs (.s files) or deeply internal runtime features that
        /// have no .NET equivalent at the source level.
        ///
        /// The RuntimeIntrinsics system handles the individual assembly-backed
        /// FUNCTIONS within these packages, but the package resolution itself
        /// must come from C# for the core runtime types.
        /// </summary>
        /// <summary>
        /// Packages that MUST use C# runtime and cannot compile from Go source.
        /// Only packages that use assembly stubs (.s files) with no pure-Go fallback
        /// or are fundamental .NET runtime bridges belong here.
        /// Pure Go internal packages should compile from Go source for exact type compatibility.
        /// </summary>
        private static bool IsRuntimeIntrinsicPackage(string importPath)
        {
            return importPath switch
            {
                // Core runtime — provides Slice<T>, Map, Channel, goroutine, defer/panic/recover
                "runtime" => true,

                // Compiler intrinsic
                "unsafe" => true,

                // CGo pseudo-package
                "C" => true,

                // Internal packages WITH assembly that need C# bridges
                "internal/bytealg" => true,    // has assembly + generics; C# stub handles both types
                "internal/cpu" => true,         // CPU feature detection via asm
                "internal/abi" => true,         // compiles from source but TypeMapper can't map its structs yet
                "internal/chacha8rand" => true, // ChaCha8 in asm
                "internal/reflectlite" => true, // needs runtime reflect bridge

                // Internal packages that bridge to .NET runtime
                "internal/poll" => true,        // I/O polling — needs .NET async
                "internal/syscall/unix" => true, // syscall bridge
                "internal/syscall/execenv" => true,

                // go/internal packages (Go toolchain internals, not in stdlib source tree)
                _ when importPath.StartsWith("go/internal/") => true,

                // Everything else compiles from Go source — including pure-Go internal packages:
                // internal/fmtsort, internal/itoa, internal/race, internal/godebug,
                // internal/goversion, internal/gover, internal/goroot, internal/safefilepath,
                // internal/singleflight, internal/testlog, internal/unsafeheader,
                // internal/oserror, internal/nettrace, internal/lazyregexp, internal/saferio,
                // internal/intern, internal/profile, internal/diff, internal/platform,
                // internal/bisect, internal/fuzz, internal/coverage/rtcov,
                // internal/goarch, internal/goos, internal/goexperiment, internal/godebugs
                _ => false,
            };
        }

        /// <summary>
        /// Find the Go stdlib source directory.
        /// Searches: GOROOT env, ~/.ngo/gosrc/go1.22.6/src, /usr/local/go/src
        /// </summary>
        private static string? FindGoStdlibSource()
        {
            // Check GOROOT environment variable
            var goroot = System.Environment.GetEnvironmentVariable("GOROOT");
            if (!string.IsNullOrEmpty(goroot))
            {
                var src = Path.Combine(goroot, "src");
                if (Directory.Exists(src) && Directory.Exists(Path.Combine(src, "fmt")))
                    return src;
            }

            // Check ngo's cached Go source (~/.ngo/gosrc/go1.22.6/src)
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var ngoCache = Path.Combine(home, ".ngo", "gosrc");
            if (Directory.Exists(ngoCache))
            {
                // Find the latest version
                foreach (var dir in Directory.GetDirectories(ngoCache).OrderByDescending(d => d))
                {
                    var src = Path.Combine(dir, "src");
                    if (Directory.Exists(src) && Directory.Exists(Path.Combine(src, "fmt")))
                        return src;
                }
            }

            // Check common system paths
            foreach (var candidate in new[] { "/usr/local/go/src", "/usr/lib/go/src", "/snap/go/current/src" })
            {
                if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "fmt")))
                    return candidate;
            }

            return null;
        }

        public string ProjectRoot { get; }

        /// <summary>
        /// Returns the source directory for a package that was resolved from source.
        /// Used by the emitter to re-compile dependencies into the host module.
        /// Returns null if the package was resolved from .ngo cache or not found.
        /// </summary>
        public string? GetSourceDir(string importPath)
        {
            _resolvedDirs.TryGetValue(importPath, out var dir);
            return dir;
        }

        /// <summary>
        /// Resolves cross-package type references during .ngo archive deserialization.
        /// Uses RuntimePackageResolver (symbol-level only, no CLR compilation).
        /// </summary>
        private TypeSymbol? CrossPkgResolver(string pkgName, string typeName)
        {
            // Look up the package by short name from RuntimePackageResolver
            var pkg = RuntimePackageResolver.Instance.ResolveByName(pkgName);
            if (pkg != null)
            {
                var sym = pkg.LookupExport(typeName);
                if (sym is TypeSymbol ts) return ts;
            }

            // Also check already-resolved Go packages
            foreach (var kvp in _resolvedPackages)
            {
                var lastSlash = kvp.Key.LastIndexOf('/');
                var shortName = lastSlash >= 0 ? kvp.Key.Substring(lastSlash + 1) : kvp.Key;
                if (shortName == pkgName)
                {
                    var sym = kvp.Value.LookupExport(typeName);
                    if (sym is TypeSymbol ts) return ts;
                }
            }

            return null;
        }

        public PackageSymbol? Resolve(string importPath)
        {
            if (_resolvedPackages.TryGetValue(importPath, out var cached))
                return cached;

            // Skip packages that must stay in C# runtime
            if (IsRuntimeIntrinsicPackage(importPath))
                return null;

            // Check .ngo cache on disk FIRST
            if (ProjectRoot != null)
            {
                var cacheDir = NgoArchive.GetCacheDir(ProjectRoot);
                var archivePath = NgoArchive.GetArchivePath(cacheDir, importPath);
                if (File.Exists(archivePath))
                {
                    var pkg = NgoArchive.ReadGoMetadata(archivePath, CrossPkgResolver);
                    if (pkg != null)
                    {
                        _resolvedPackages[importPath] = pkg;
                        return pkg;
                    }
                }
            }

            // Not cached — compile from source
            return ResolveFromSource(importPath);
        }

        public Type? ResolveClrType(string importPath, string typeName) => null;

        private PackageSymbol? ResolveFromSource(string importPath)
        {
            if (_beingResolved.Contains(importPath))
            {
                return null;
            }

            try
            {
                // Discover transitive imports — stores only directory paths, not parsed trees.
                // Trees are parsed and discarded during discovery; re-parsed when compiling.
                var pkgDirs = new Dictionary<string, string>();
                var deps = new Dictionary<string, List<string>>();
                DiscoverDependencies(importPath, pkgDirs, deps);

                if (!pkgDirs.ContainsKey(importPath))
                {
                    return null;
                }

                var order = TopologicalSort(pkgDirs.Keys, deps);

                _beingResolved.Add(importPath);
                // _isAnalyzingDag removed — using _beingResolved for per-package cycle detection
                try
                {
                    foreach (var pkg in order)
                    {
                        if (_resolvedPackages.ContainsKey(pkg))
                        {
                            continue;
                        }
                        // Only skip if the package is a runtime intrinsic (must use C#)
                        if (IsRuntimeIntrinsicPackage(pkg))
                        {
                            continue;
                        }
                        if (!pkgDirs.TryGetValue(pkg, out var dir))
                        {
                            continue;
                        }

                        // Parse fresh — each package's trees are held only during its compilation
                        var trees = ParseGoFilesInDir(dir);
                        if (trees.Count == 0)
                        {
                            continue;
                        }

                        AnalyzeAndCachePackage(pkg, trees);
                        _resolvedDirs[pkg] = dir;
                        // trees go out of scope here — GC can collect the AST
                    }
                }
                finally
                {
                    // _isAnalyzingDag removed
                    _beingResolved.Remove(importPath);
                }

                _resolvedPackages.TryGetValue(importPath, out var result);
                return result;
            }
            catch (OutOfMemoryException ex)
            {
                // _isAnalyzingDag removed
                _beingResolved.Remove(importPath);
                _ctx.Log.Error($"out of memory resolving '{importPath}': {ex.Message}");
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                return null;
            }
        }

        private void DiscoverDependencies(
            string importPath,
            Dictionary<string, string> pkgDirs,
            Dictionary<string, List<string>> deps)
        {
            var worklist = new Queue<string>();
            worklist.Enqueue(importPath);

            while (worklist.Count > 0)
            {
                var current = worklist.Dequeue();

                if (pkgDirs.ContainsKey(current) || _resolvedPackages.ContainsKey(current))
                {
                    continue;
                }
                // Skip only true runtime intrinsic packages that can't compile from Go source.
                // Everything else should be compiled from Go source when available.
                if (IsRuntimeIntrinsicPackage(current))
                {
                    continue;
                }
                // Check .ngo disk cache — read PackageSymbol + imports without parsing source
                if (ProjectRoot != null)
                {
                    var cacheDir = NgoArchive.GetCacheDir(ProjectRoot);
                    var archivePath = NgoArchive.GetArchivePath(cacheDir, current);
                    if (File.Exists(archivePath))
                    {
                        var pkg = NgoArchive.ReadGoMetadata(archivePath, CrossPkgResolver);
                        if (pkg != null)
                        {
                            _resolvedPackages[current] = pkg;
                            // Enqueue cached package's imports for transitive discovery
                            foreach (var imp in pkg.Imports)
                            {
                                worklist.Enqueue(imp);
                            }
                            continue;
                        }
                    }
                }
                // Only skip if already discovered AND successfully resolved from Go source
                if (_discoveredPackages.Contains(current))
                {
                    if (_resolvedPackages.ContainsKey(current))
                        continue;
                    // Previously discovered but not successfully compiled — try again
                }
                _discoveredPackages.Add(current);

                var dir = ResolvePackageDir(current);
                if (dir == null || !Directory.Exists(dir))
                {
                    continue;
                }

                // Parse only to extract imports — trees are discarded immediately.
                // They'll be re-parsed when the package is actually compiled.
                var imports = ExtractImports(dir);

                if (imports != null)
                {
                    pkgDirs[current] = dir;
                    deps[current] = imports;

                    foreach (var imp in imports)
                    {
                        worklist.Enqueue(imp);
                    }
                }
            }
        }

        /// <summary>
        /// Scans Go files in a directory to extract import paths without building a full CST.
        /// Uses line-by-line scanning for import declarations — no Parser, no SyntaxTree, no AST.
        /// Returns null if no valid Go files found.
        /// </summary>
        private static List<string>? ExtractImports(string dir)
        {
            var imports = new List<string>();
            bool hasFiles = false;

            foreach (var file in Directory.GetFiles(dir, "*.go"))
            {
                if (ShouldSkipGoFile(file))
                {
                    continue;
                }

                hasFiles = true;

                try
                {
                    ScanFileForImports(file, imports);
                }
                catch
                {
                    // Scan failures are non-fatal for dependency discovery
                }
            }

            return hasFiles ? imports : null;
        }

        /// <summary>
        /// Lightweight line-by-line scanner that extracts import paths from a Go source file.
        /// Handles both single imports (import "path") and grouped imports (import ( "path" )).
        /// Does NOT build a CST — avoids creating millions of syntax nodes per file.
        /// </summary>
        private static void ScanFileForImports(string filePath, List<string> imports)
        {
            using var reader = new StreamReader(filePath);
            bool pastPackage = false;
            bool inImportBlock = false;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.TrimStart();

                // Skip comments and blank lines
                if (trimmed.Length == 0 || trimmed.StartsWith("//"))
                {
                    continue;
                }

                // Wait for the package declaration first
                if (!pastPackage)
                {
                    if (trimmed.StartsWith("package "))
                    {
                        pastPackage = true;
                    }
                    continue;
                }

                if (inImportBlock)
                {
                    if (trimmed.StartsWith(")"))
                    {
                        inImportBlock = false;
                        continue;
                    }

                    // Inside import ( ... ) block: each line is [alias] "path"
                    var path = ExtractQuotedString(trimmed);
                    if (path != null)
                    {
                        imports.Add(path);
                    }
                    continue;
                }

                // import "path" or import ( ... )
                if (trimmed.StartsWith("import "))
                {
                    var rest = trimmed.Substring(7).TrimStart();
                    if (rest.StartsWith("("))
                    {
                        inImportBlock = true;
                        continue;
                    }

                    // Single import: import "path" or import alias "path"
                    var path = ExtractQuotedString(rest);
                    if (path != null)
                    {
                        imports.Add(path);
                    }
                    continue;
                }

                // Once we hit a non-import top-level declaration, stop scanning.
                // Go requires all imports before other declarations.
                if (trimmed.StartsWith("func ") || trimmed.StartsWith("type ") ||
                    trimmed.StartsWith("var ") || trimmed.StartsWith("const "))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Extracts the content of the first double-quoted string in the line.
        /// Returns null if no quoted string found.
        /// </summary>
        private static string? ExtractQuotedString(string text)
        {
            int start = text.IndexOf('"');
            if (start < 0)
            {
                return null;
            }
            int end = text.IndexOf('"', start + 1);
            if (end < 0)
            {
                return null;
            }
            var path = text.Substring(start + 1, end - start - 1);
            return path.Length > 0 ? path : null;
        }

        /// <summary>
        /// Parses all Go source files in a directory into SyntaxTrees.
        /// Used when actually compiling a package (not during discovery).
        /// </summary>
        private static List<SyntaxTree> ParseGoFilesInDir(string dir)
        {
            var trees = new List<SyntaxTree>();
            foreach (var file in Directory.GetFiles(dir, "*.go"))
            {
                if (ShouldSkipGoFile(file))
                {
                    continue;
                }

                try
                {
                    var source = File.ReadAllText(file);
                    trees.Add(SyntaxTree.Parse(source));
                }
                catch
                {
                    // Parse failures are non-fatal
                }
            }
            return trees;
        }

        private string? ResolvePackageDir(string importPath)
        {
            // Module-relative path: import path starts with the module name
            var moduleName = _moduleResolver.ModuleName;
            if (moduleName != null && (importPath == moduleName || importPath.StartsWith(moduleName + "/")))
            {
                var relativePath = importPath == moduleName
                    ? ""
                    : importPath.Substring(moduleName.Length + 1);
                var dir = Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                return Directory.Exists(dir) ? dir : null;
            }

            // Simple relative path (no dots in first segment — likely a local subdirectory or stdlib)
            if (!importPath.Contains('.'))
            {
                // Check project-local first
                var dir = Path.Combine(ProjectRoot, importPath.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(dir))
                    return dir;

                // Check Go stdlib source (GOROOT/src or ~/.ngo/gosrc/...)
                if (_goStdlibSrc != null)
                {
                    var stdlibDir = Path.Combine(_goStdlibSrc, importPath.Replace('/', Path.DirectorySeparatorChar));
                    if (Directory.Exists(stdlibDir))
                        return stdlibDir;
                }
            }

            // Check vendor directory (common in Go projects)
            var vendorDir = Path.Combine(ProjectRoot, "vendor", importPath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(vendorDir))
                return vendorDir;

            // External module: find via go.mod requirements
            var match = _moduleResolver.FindModule(importPath);
            if (match != null)
                return _moduleResolver.ResolvePackageDir(importPath, match.Module, match.Version);

            return null;
        }

        private void AnalyzeAndCachePackage(string importPath, List<SyntaxTree> trees)
        {
            AnalysisResult result;
            try
            {
                // Pass compilation context so nested imports resolve through the resolver chain
                result = SemanticAnalyzer.Analyze(trees, _ctx);
                if (result.HasErrors)
                {
                    _ctx.Log.Debug($"analysis of '{importPath}' has {result.Errors.Count(e => e.Severity == Ngo.Compiler.ErrorSeverity.Error)} errors");
                }
            }
            catch (Exception ex)
            {
                _ctx.Log.Warn($"analysis failed for '{importPath}': {ex.Message}");
                return;
            }

            var pkgName = result.Root.Package.Symbol.Name;
            var pkg = new PackageSymbol(pkgName, importPath);

            foreach (var func in result.Root.Functions)
            {
                if (func.Symbol.Name.Length > 0 && char.IsUpper(func.Symbol.Name[0]))
                    pkg.AddExport(func.Symbol);
            }

            foreach (var typeDecl in result.Root.Types)
            {
                if (typeDecl.Symbol.Name.Length > 0 && char.IsUpper(typeDecl.Symbol.Name[0]))
                {
                    typeDecl.Symbol.PackagePath = importPath;
                    pkg.AddExport(typeDecl.Symbol);
                }
            }
            // Types with methods from compiled Go source

            foreach (var constDecl in result.Root.Constants)
            {
                if (constDecl.Symbol.Name.Length > 0 && char.IsUpper(constDecl.Symbol.Name[0]))
                    pkg.AddExport(constDecl.Symbol);
            }

            foreach (var varDecl in result.Root.Variables)
            {
                if (varDecl.Symbol.Name.Length > 0 && char.IsUpper(varDecl.Symbol.Name[0]))
                    pkg.AddExport(varDecl.Symbol);
            }

            // Collect import paths so they're stored in the .ngo archive.
            // This allows dependency discovery from cache without re-parsing source.
            var importPaths = new List<string>();
            foreach (var imp in result.Root.Imports)
            {
                if (!string.IsNullOrEmpty(imp.Path))
                {
                    importPaths.Add(imp.Path);
                }
            }
            pkg.SetImports(importPaths);

            result.Root.Package.Symbol.CopyExportsFrom(pkg);

            _resolvedPackages[importPath] = pkg;

            // Write .ngo archive — all 3 sections via NgoModuleBuilder (zero DynamicAssembly).
            // NgoModuleBuilder captures IL as pure data; no System.Reflection.Emit allocation.
            // AST is discarded after this method returns.
            try
            {
                var cacheDir = NgoArchive.GetCacheDir(ProjectRoot);
                var archivePath = NgoArchive.GetArchivePath(cacheDir, importPath);
                ILSerializer.WriteArchive(archivePath, pkg, importPath, result, _ctx);
                _ctx.Log.Debug($"wrote archive for '{importPath}'");
            }
            catch (Exception ex)
            {
                _ctx.Log.Warn($"archive write failed for '{importPath}': {ex.Message}");
            }
        }

        // Current target platform — used for file filtering
        private static readonly string _targetOS = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows) ? "windows"
            : System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX) ? "darwin" : "linux";
        private static readonly string _targetArch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "amd64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X86 => "386",
            System.Runtime.InteropServices.Architecture.Arm => "arm",
            _ => "amd64",
        };

        private static readonly HashSet<string> _allOS = new()
        {
            "linux", "windows", "darwin", "freebsd", "openbsd", "netbsd",
            "solaris", "plan9", "aix", "ios", "js", "wasip1", "android",
            "illumos", "dragonfly", "hurd",
        };
        private static readonly HashSet<string> _allArch = new()
        {
            "amd64", "386", "arm", "arm64", "mips", "mips64", "mipsle",
            "mips64le", "ppc64", "ppc64le", "riscv64", "s390x", "wasm", "loong64",
        };

        public static bool ShouldSkipGoFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                return true;

            // Parse platform suffixes from filename: name_GOOS.go, name_GOARCH.go, name_GOOS_GOARCH.go
            var nameWithoutGo = fileName.Substring(0, fileName.Length - 3); // strip ".go"
            var parts = nameWithoutGo.Split('_');
            if (parts.Length >= 2)
            {
                var last = parts[parts.Length - 1];
                var secondLast = parts.Length >= 3 ? parts[parts.Length - 2] : null;

                // name_GOARCH.go — skip if arch doesn't match
                if (_allArch.Contains(last) && last != _targetArch)
                    return true;

                // name_GOOS.go — skip if OS doesn't match
                if (_allOS.Contains(last) && last != _targetOS)
                    return true;

                // name_GOOS_GOARCH.go — skip if either doesn't match
                if (secondLast != null && _allOS.Contains(secondLast) && _allArch.Contains(last))
                {
                    if (secondLast != _targetOS || last != _targetArch)
                        return true;
                }
            }

            // Read only the first ~20 lines for build tag checks — not the whole file
            using (var reader = new StreamReader(filePath))
            {
                for (int i = 0; i < 20; i++)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    line = line.Trim();
                    if (line.StartsWith("package "))
                    {
                        break;
                    }
                    if (line.StartsWith("//go:build ") || line.StartsWith("// +build "))
                    {
                        var tag = line.StartsWith("//go:build ") ? line.Substring(11).Trim() : line.Substring(10).Trim();

                        // Skip files explicitly marked ignore
                        if (tag == "ignore")
                            return true;

                        // Skip CGo-required files when not in CGo mode
                        // (files with "cgo" build tag without negation)
                        if (tag == "cgo" || tag.StartsWith("cgo ") || tag.StartsWith("cgo,")
                            || tag.Contains("&& cgo") || tag.Contains("cgo &&"))
                            return true;

                        // Skip files that require specific platforms we're not on
                        var excludedPlatforms = new[] { "windows", "darwin", "freebsd", "openbsd", "netbsd",
                            "solaris", "plan9", "aix", "ios", "js", "wasip1", "android", "illumos",
                            "dragonfly", "hurd", "386", "arm", "arm64", "mips", "mips64", "mipsle",
                            "mips64le", "ppc64", "ppc64le", "riscv64", "s390x", "wasm", "loong64",
                            "boringcrypto" };

                        // If the build tag is JUST an excluded platform name, skip
                        foreach (var pt in excludedPlatforms)
                        {
                            if (tag == pt)
                                return true;
                        }

                        // If the tag is "!linux" or "!amd64", skip (negation of our platform)
                        if (tag == "!linux" || tag == "!amd64")
                            return true;

                        // Complex expressions: skip if tag requires a platform we don't support
                        // e.g., "//go:build windows || darwin" — skip if neither matches
                        if (tag.Contains("||") && !tag.Contains("linux") && !tag.Contains("unix"))
                        {
                            bool anyMatch = false;
                            foreach (var part in tag.Split(new[] { "||" }, StringSplitOptions.None))
                            {
                                var p = part.Trim().TrimStart('(').TrimEnd(')').Trim();
                                if (p == "linux" || p == "amd64" || p == "unix" || p.StartsWith("go1."))
                                    anyMatch = true;
                            }
                            if (!anyMatch)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static List<string> TopologicalSort(
            IEnumerable<string> packages,
            Dictionary<string, List<string>> deps)
        {
            var inDegree = new Dictionary<string, int>();
            var pkgSet = new HashSet<string>(packages);

            foreach (var pkg in pkgSet)
                inDegree[pkg] = 0;

            foreach (var (pkg, pkgDeps) in deps)
            {
                if (!pkgSet.Contains(pkg)) continue;
                foreach (var dep in pkgDeps)
                {
                    if (pkgSet.Contains(dep) && inDegree.ContainsKey(dep))
                    {
                    }
                }
            }

            // Build adjacency: dep → dependents
            var adj = new Dictionary<string, List<string>>();
            foreach (var pkg in pkgSet)
                adj[pkg] = new List<string>();

            foreach (var (pkg, pkgDeps) in deps)
            {
                if (!pkgSet.Contains(pkg)) continue;
                foreach (var dep in pkgDeps)
                {
                    if (pkgSet.Contains(dep))
                    {
                        if (!inDegree.ContainsKey(pkg))
                            inDegree[pkg] = 0;
                        inDegree[pkg]++;
                        if (!adj.ContainsKey(dep))
                            adj[dep] = new List<string>();
                        adj[dep].Add(pkg);
                    }
                }
            }

            var queue = new Queue<string>();
            foreach (var (pkg, degree) in inDegree)
            {
                if (degree == 0)
                    queue.Enqueue(pkg);
            }

            var result = new List<string>();
            while (queue.Count > 0)
            {
                var pkg = queue.Dequeue();
                result.Add(pkg);

                if (adj.TryGetValue(pkg, out var dependents))
                {
                    foreach (var dep in dependents)
                    {
                        inDegree[dep]--;
                        if (inDegree[dep] == 0)
                            queue.Enqueue(dep);
                    }
                }
            }

            // Add any remaining (cycles) — append them at end
            foreach (var pkg in pkgSet)
            {
                if (!result.Contains(pkg))
                    result.Add(pkg);
            }

            return result;
        }
    }
}
