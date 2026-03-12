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

        public GoPackageResolver(CompilationContext ctx, string projectRoot)
        {
            _ctx = ctx;
            _moduleResolver = new GoModuleResolver(ctx.Log);
            ProjectRoot = projectRoot;
            _moduleResolver.LoadGoMod(projectRoot);
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
            if (_isAnalyzingDag)
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

                _isAnalyzingDag = true;
                try
                {
                    foreach (var pkg in order)
                    {
                        if (_resolvedPackages.ContainsKey(pkg))
                        {
                            continue;
                        }
                        // Skip packages already resolved by other resolvers in the chain
                        if (_ctx.ResolvePackage(pkg) != null)
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
                    _isAnalyzingDag = false;
                }

                _resolvedPackages.TryGetValue(importPath, out var result);
                return result;
            }
            catch (OutOfMemoryException ex)
            {
                _isAnalyzingDag = false;
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
                // Skip packages already resolved by other resolvers
                if (RuntimePackageResolver.Instance.Resolve(current) != null)
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
                if (_discoveredPackages.Contains(current))
                {
                    continue;
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

            // Simple relative path (no dots in first segment — likely a local subdirectory)
            if (!importPath.Contains('.'))
            {
                var dir = Path.Combine(ProjectRoot, importPath.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(dir))
                    return dir;
            }

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

        public static bool ShouldSkipGoFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                return true;

            var platformSuffixes = new[]
            {
                "_windows.go", "_darwin.go", "_freebsd.go", "_openbsd.go", "_netbsd.go",
                "_solaris.go", "_plan9.go", "_aix.go", "_ios.go", "_js.go", "_wasip1.go",
                "_android.go", "_illumos.go", "_dragonfly.go", "_hurd.go",
                "_386.go", "_arm.go", "_arm64.go", "_mips.go", "_mips64.go",
                "_mipsle.go", "_mips64le.go", "_ppc64.go", "_ppc64le.go",
                "_riscv64.go", "_s390x.go", "_wasm.go", "_loong64.go",
            };
            foreach (var suffix in platformSuffixes)
            {
                if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
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
                        var platformTags = new[] { "windows", "darwin", "freebsd", "openbsd", "netbsd",
                            "solaris", "plan9", "aix", "ios", "js", "wasip1", "android", "illumos",
                            "dragonfly", "hurd", "386", "arm", "arm64", "mips", "mips64", "mipsle",
                            "mips64le", "ppc64", "ppc64le", "riscv64", "s390x", "wasm", "loong64",
                            "boringcrypto" };
                        foreach (var pt in platformTags)
                        {
                            if (tag == pt || tag.StartsWith(pt + " ") || tag.StartsWith(pt + ","))
                            {
                                return true;
                            }
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
