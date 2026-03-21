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
        private bool _isAnalyzingDag;
        private readonly HashSet<string> _beingResolved = new();
        private readonly string? _goStdlibSrc;

        public GoPackageResolver(CompilationContext ctx, string projectRoot)
        {
            _ctx = ctx;
            _moduleResolver = new GoModuleResolver(ctx.Log);
            ProjectRoot = projectRoot;
            _moduleResolver.LoadGoMod(projectRoot);
            _moduleResolver.LoadAllTransitiveDependencies();
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
        /// Packages where Go source is PREFERRED over the C# runtime stub,
        /// even when a C# stub exists. These packages have incomplete C# stubs
        /// and need Go source compilation for full type information.
        /// </summary>
        private static bool PreferGoSource(string importPath)
        {
            return importPath switch
            {
                // internal/abi has complete Go source with all struct types needed by reflect.
                // The C# stub only has constants + basic struct shells.
                "internal/abi" => true,

                // Pure Go internal packages with no assembly dependencies
                "internal/fmtsort" => true,
                "internal/itoa" => true,
                "internal/gover" => true,
                "internal/goversion" => true,

                _ => false,
            };
        }

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

                // reflect — needs C# runtime bridge for TypeOf, ValueOf, DeepEqual, Kind consts
                "reflect" => true,

                // Compiler intrinsic
                "unsafe" => true,

                // CGo pseudo-package
                "C" => true,

                // Internal packages WITH assembly that need C# bridges
                "internal/bytealg" => true,    // has assembly + generics; C# stub handles both types
                "internal/cpu" => true,         // CPU feature detection via asm
                // internal/abi: prefer Go source (has all struct types for reflect)
                // but fall through — it's handled by PreferGoSource()
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
        private TypeSymbol? CrossPkgResolver(string pkgIdentifier, string typeName)
        {
            // Full import path (new format): exact lookup by import path
            if (pkgIdentifier.Contains('/') || pkgIdentifier.Contains('.'))
            {
                if (_resolvedPackages.TryGetValue(pkgIdentifier, out var exactPkg))
                {
                    var sym = exactPkg.LookupExport(typeName);
                    if (sym is TypeSymbol ts) return ts;
                }

                var runtimePkg = RuntimePackageResolver.Instance.Resolve(pkgIdentifier);
                if (runtimePkg != null)
                {
                    var sym = runtimePkg.LookupExport(typeName);
                    if (sym is TypeSymbol ts) return ts;
                }
            }

            // Short name (legacy format): search by package short name
            foreach (var kvp in _resolvedPackages)
            {
                var shortName = CompilationContext.GetDefaultPackageName(kvp.Key);
                if (shortName == pkgIdentifier)
                {
                    var sym = kvp.Value.LookupExport(typeName);
                    if (sym is TypeSymbol ts) return ts;
                }
            }

            var runtimeByName = RuntimePackageResolver.Instance.ResolveByName(pkgIdentifier);
            if (runtimeByName != null)
            {
                var sym = runtimeByName.LookupExport(typeName);
                if (sym is TypeSymbol ts) return ts;
            }

            return null;
        }

        private int _resolveDepth;
        private const int MaxResolveDepth = 50;

        public PackageSymbol? Resolve(string importPath)
        {
            if (_resolvedPackages.TryGetValue(importPath, out var cached))
            {
                return cached;
            }

            // Prevent stack overflow from deeply recursive dependency chains
            if (_resolveDepth >= MaxResolveDepth)
            {
                _ctx.Log.Warn($"dependency depth limit reached for '{importPath}'");
                return null;
            }

            // Skip packages that must stay in C# runtime
            if (IsRuntimeIntrinsicPackage(importPath))
                return null;

            // Resolve from source or .ngo cache (via topological DAG ordering)
            _resolveDepth++;
            try
            {
                return ResolveFromSource(importPath);
            }
            finally
            {
                _resolveDepth--;
            }
        }

        public Type? ResolveClrType(string importPath, string typeName) => null;

        private PackageSymbol? ResolveFromSource(string importPath)
        {
            if (_isAnalyzingDag || _beingResolved.Contains(importPath))
            {
                    return null;
            }

            try
            {
                // Discover transitive imports — stores only directory paths, not parsed trees.
                // Trees are parsed and discarded during discovery; re-parsed when compiling.
                var pkgDirs = new Dictionary<string, string>();
                var deps = new Dictionary<string, List<string>>();
                var cachedArchives = new Dictionary<string, string>();
                DiscoverDependencies(importPath, pkgDirs, deps, cachedArchives);

                if (!pkgDirs.ContainsKey(importPath) && !cachedArchives.ContainsKey(importPath))
                {
                    return null;
                }

                // Topological sort ALL discovered packages (both source and cached)
                var allPkgs = new HashSet<string>(pkgDirs.Keys);
                foreach (var k in cachedArchives.Keys)
                {
                    allPkgs.Add(k);
                }
                var order = TopologicalSort(allPkgs, deps);

                _beingResolved.Add(importPath);
                _isAnalyzingDag = true;
                try
                {
                    // Process ALL packages in topological order.
                    // Read from .ngo cache if available, compile from source if not.
                    // Cross-package types are fully qualified (import_path:TypeName)
                    // so they resolve unambiguously regardless of load order.
                    foreach (var pkg in order)
                    {
                        if (_resolvedPackages.ContainsKey(pkg))
                        {
                            continue;
                        }
                        if (RuntimePackageResolver.Instance.Resolve(pkg) != null
                            && !PreferGoSource(pkg))
                        {
                            continue;
                        }

                        // Try reading from .ngo cache first
                        if (cachedArchives.TryGetValue(pkg, out var archivePath))
                        {
                            var cachedPkg = NgoArchive.ReadGoMetadata(archivePath, CrossPkgResolver);
                            if (cachedPkg != null)
                            {
                                _resolvedPackages[pkg] = cachedPkg;
                                continue;
                            }
                        }

                        // No cache — compile from source
                        if (!pkgDirs.TryGetValue(pkg, out var dir))
                        {
                            continue;
                        }

                        var trees = ParseGoFilesInDir(dir);
                        if (trees.Count == 0)
                        {
                            continue;
                        }

                        _resolvedDirs[pkg] = dir;
                        AnalyzeAndCachePackage(pkg, trees);
                    }
                }
                finally
                {
                    _isAnalyzingDag = false;
                    _beingResolved.Remove(importPath);
                }

                _resolvedPackages.TryGetValue(importPath, out var result);
                return result;
            }
            catch (OutOfMemoryException ex)
            {
                _isAnalyzingDag = false;
                _beingResolved.Remove(importPath);
                _ctx.Log.Error($"out of memory resolving '{importPath}': {ex.Message}");
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                return null;
            }
        }

        private void DiscoverDependencies(
            string importPath,
            Dictionary<string, string> pkgDirs,
            Dictionary<string, List<string>> deps,
            Dictionary<string, string> cachedArchives)
        {
            var worklist = new Queue<string>();
            worklist.Enqueue(importPath);

            while (worklist.Count > 0)
            {
                var current = worklist.Dequeue();

                if (pkgDirs.ContainsKey(current) || cachedArchives.ContainsKey(current)
                    || _resolvedPackages.ContainsKey(current))
                {
                    continue;
                }
                // Skip runtime intrinsic packages, UNLESS they prefer Go source
                if (IsRuntimeIntrinsicPackage(current) && !PreferGoSource(current))
                {
                    continue;
                }
                // Check .ngo disk cache — read ONLY import list for dependency discovery.
                // Full archive read happens later in topological order.
                if (ProjectRoot != null)
                {
                    var discoverDir = ResolvePackageDir(current);
                    if (discoverDir != null)
                    {
                        _resolvedDirs[current] = discoverDir;
                    }
                    var cacheDir = NgoArchive.GetCacheDir(ProjectRoot);
                    var archivePath = NgoArchive.GetArchivePath(cacheDir, current, discoverDir);
                    if (!File.Exists(archivePath) && discoverDir != null)
                    {
                        archivePath = NgoArchive.GetArchivePath(cacheDir, current);
                    }
                    if (File.Exists(archivePath))
                    {
                        // Read with null crossPkgResolver — we only need the import list
                        var cachedPkg = NgoArchive.ReadGoMetadata(archivePath, null);
                        if (cachedPkg != null)
                        {
                            cachedArchives[current] = archivePath;
                            deps[current] = new List<string>(cachedPkg.Imports);
                            foreach (var imp in cachedPkg.Imports)
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
                var moduleRoot = _moduleResolver.ModuleRoot ?? ProjectRoot;
                var relativePath = importPath == moduleName
                    ? ""
                    : importPath.Substring(moduleName.Length + 1);
                var dir = Path.Combine(moduleRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(dir))
                {
                    return dir;
                }
                // Sub-path doesn't exist — it might be a separate module with the same prefix
                // (e.g., go.opentelemetry.io/otel/trace is a separate module from go.opentelemetry.io/otel).
                // Fall through to external module resolution.
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
            {
                var dir = _moduleResolver.ResolvePackageDir(importPath, match.Module, match.Version);
                if (dir != null)
                {
                    return dir;
                }
            }

            // Last resort: check if the import path is a sub-package of a cached module.
            var cached = _moduleResolver.FindInCache(importPath);
            if (cached != null)
            {
                return cached;
            }

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
                    var errorCount = result.Errors.Count(e => e.Severity == ErrorSeverity.Error);
                    if (errorCount > 0)
                    {
                        _ctx.Log.Warn($"analysis of '{importPath}' has {errorCount} errors:");
                        foreach (var err in result.Errors.Where(e => e.Severity == ErrorSeverity.Error).Take(5))
                            _ctx.Log.Warn($"  {err.Code}: {err.Message}");
                    }
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

            // Also add methods to their receiver types — methods are declared
            // separately from types in Go, and AddMethod may not have been called
            // on the exported type symbol if it's a different instance.
            foreach (var methodDecl in result.Root.Methods)
            {
                var receiverType = methodDecl.Symbol.ReceiverType;
                if (receiverType is PointerTypeSymbol ptrRecv)
                {
                    receiverType = ptrRecv.ElementType;
                }
                if (receiverType != null && receiverType.Name.Length > 0
                    && char.IsUpper(receiverType.Name[0]))
                {
                    // Find the exported type and add the method if not already present
                    var exported = pkg.LookupExport(receiverType.Name);
                    if (exported is TypeSymbol exportedType
                        && exportedType.LookupMethod(methodDecl.Symbol.Name) == null)
                    {
                        exportedType.AddMethod(methodDecl.Symbol);
                    }
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
                {
                    if (varDecl.Symbol is LocalSymbol local)
                    {
                        pkg.AddExport(new PackageVarSymbol(local.Name, local.Type));
                    }
                    else
                    {
                        pkg.AddExport(varDecl.Symbol);
                    }
                }
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

            // Write .ngo archive to disk for caching.
            try
            {
                var cacheDir = NgoArchive.GetCacheDir(ProjectRoot);
                _resolvedDirs.TryGetValue(importPath, out var writeSourceDir);
                var archivePath = NgoArchive.GetArchivePath(cacheDir, importPath, writeSourceDir);
                if (_isAnalyzingDag)
                {
                    NgoArchive.Write(archivePath, pkg, importPath);
                }
                else
                {
                    ILSerializer.WriteArchive(archivePath, pkg, importPath, result, _ctx);
                }
            }
            catch (Exception ex)
            {
                _ctx.Log.Warn($"archive write failed for '{importPath}': {ex.Message}");
                _ctx.Log.Warn(ex.StackTrace ?? "");
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
            "illumos", "dragonfly", "hurd", "zos",
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

            // Read build tags from the first ~20 lines.
            // If //go:build is present, it is authoritative and // +build lines are ignored.
            string? goBuildExpr = null;
            var oldBuildTags = new List<string>();

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
                    if (line.StartsWith("//go:build "))
                    {
                        goBuildExpr = line.Substring(11).Trim();
                    }
                    else if (line.StartsWith("// +build ") || line.StartsWith("//+build "))
                    {
                        var tagStart = line.IndexOf("+build ") + 7;
                        oldBuildTags.Add(line.Substring(tagStart).Trim());
                    }
                }
            }

            // Evaluate: //go:build takes precedence over // +build
            if (goBuildExpr != null)
            {
                if (goBuildExpr == "ignore")
                {
                    return true;
                }
                return !EvalBuildExpression(goBuildExpr);
            }

            // Old-style: each // +build line must be satisfied (AND across lines)
            foreach (var tag in oldBuildTags)
            {
                if (tag == "ignore")
                {
                    return true;
                }
                if (!EvalBuildExpression(tag))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EvalBuildExpression(string expr)
        {
            // Handle old-style "// +build" with space-separated OR groups and comma-separated AND
            if (!expr.Contains("||") && !expr.Contains("&&") && !expr.Contains("("))
            {
                var orGroups = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bool anyGroupSatisfied = false;
                foreach (var group in orGroups)
                {
                    var andTerms = group.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    bool groupSatisfied = true;
                    foreach (var term in andTerms)
                    {
                        if (!EvalBuildTerm(term.Trim()))
                        {
                            groupSatisfied = false;
                            break;
                        }
                    }
                    if (groupSatisfied)
                    {
                        anyGroupSatisfied = true;
                        break;
                    }
                }
                return anyGroupSatisfied;
            }

            // New-style "//go:build" expression with ||, &&, !, ()
            int pos = 0;
            return ParseBuildOr(expr, ref pos);
        }

        private static bool ParseBuildOr(string expr, ref int pos)
        {
            bool result = ParseBuildAnd(expr, ref pos);
            while (true)
            {
                SkipBuildSpaces(expr, ref pos);
                if (pos + 1 < expr.Length && expr[pos] == '|' && expr[pos + 1] == '|')
                {
                    pos += 2;
                    bool right = ParseBuildAnd(expr, ref pos);
                    result = result || right;
                }
                else
                {
                    break;
                }
            }
            return result;
        }

        private static bool ParseBuildAnd(string expr, ref int pos)
        {
            bool result = ParseBuildUnary(expr, ref pos);
            while (true)
            {
                SkipBuildSpaces(expr, ref pos);
                if (pos + 1 < expr.Length && expr[pos] == '&' && expr[pos + 1] == '&')
                {
                    pos += 2;
                    bool right = ParseBuildUnary(expr, ref pos);
                    result = result && right;
                }
                else
                {
                    break;
                }
            }
            return result;
        }

        private static bool ParseBuildUnary(string expr, ref int pos)
        {
            SkipBuildSpaces(expr, ref pos);
            if (pos < expr.Length && expr[pos] == '!')
            {
                pos++;
                return !ParseBuildUnary(expr, ref pos);
            }
            if (pos < expr.Length && expr[pos] == '(')
            {
                pos++;
                bool result = ParseBuildOr(expr, ref pos);
                SkipBuildSpaces(expr, ref pos);
                if (pos < expr.Length && expr[pos] == ')')
                {
                    pos++;
                }
                return result;
            }
            int start = pos;
            while (pos < expr.Length && expr[pos] != ' ' && expr[pos] != ')'
                   && expr[pos] != '&' && expr[pos] != '|' && expr[pos] != '!')
            {
                pos++;
            }
            string term = expr.Substring(start, pos - start);
            return EvalBuildTerm(term);
        }

        private static void SkipBuildSpaces(string expr, ref int pos)
        {
            while (pos < expr.Length && expr[pos] == ' ')
            {
                pos++;
            }
        }

        private static bool EvalBuildTerm(string term)
        {
            if (string.IsNullOrEmpty(term))
            {
                return false;
            }

            // Handle negation
            if (term.StartsWith("!"))
            {
                return !EvalBuildTerm(term.Substring(1));
            }

            // Active tags: our target platform and compiler features
            if (term is "linux" or "amd64" or "unix")
            {
                return true;
            }

            // We can't compile assembly, so noasm/purego/safe are active
            if (term is "gc" or "noasm" or "purego" or "safe" or "disableunsafe")
            {
                return true;
            }

            // Go version constraints: go1.X is active if X <= our target version
            if (term.StartsWith("go1.") && int.TryParse(term.AsSpan(4), out int version))
            {
                return version <= 22;
            }

            // Everything else (other OS/arch, cgo, gccgo, etc.) is not active
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
