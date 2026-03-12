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
        private readonly GoModuleResolver _moduleResolver = new();
        private readonly Dictionary<string, PackageSymbol> _resolvedPackages = new();
        private readonly HashSet<string> _discoveredPackages = new();
        private bool _isAnalyzingDag;

        public GoPackageResolver(CompilationContext ctx, string projectRoot)
        {
            _ctx = ctx;
            ProjectRoot = projectRoot;
            _moduleResolver.LoadGoMod(projectRoot);
        }

        public string ProjectRoot { get; }

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

            // Always resolve from source — this ensures dependencies are compiled
            // in topological order so cross-package types are fully resolved.
            // The .ngo cache is only used by the precompiler pipeline where
            // dependencies are explicitly pre-loaded.
            return ResolveFromSource(importPath);
        }

        public Type? ResolveClrType(string importPath, string typeName) => null;

        private PackageSymbol? ResolveFromSource(string importPath)
        {
            if (_isAnalyzingDag)
                return null;

            try
            {
                var pkgTrees = new Dictionary<string, List<SyntaxTree>>();
                var deps = new Dictionary<string, List<string>>();
                DiscoverDependencies(importPath, pkgTrees, deps);

                if (!pkgTrees.ContainsKey(importPath))
                    return null;

                var order = TopologicalSort(pkgTrees.Keys, deps);

                _isAnalyzingDag = true;
                try
                {
                    foreach (var pkg in order)
                    {
                        if (_resolvedPackages.ContainsKey(pkg))
                            continue;
                        // Skip packages already resolved by other resolvers in the chain
                        if (_ctx.ResolvePackage(pkg) != null)
                            continue;
                        if (!pkgTrees.TryGetValue(pkg, out var trees) || trees.Count == 0)
                            continue;

                        AnalyzeAndCachePackage(pkg, trees);
                    }
                }
                finally
                {
                    _isAnalyzingDag = false;
                }

                _resolvedPackages.TryGetValue(importPath, out var result);
                return result;
            }
            catch (OutOfMemoryException)
            {
                _isAnalyzingDag = false;
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                return null;
            }
        }

        private void DiscoverDependencies(
            string importPath,
            Dictionary<string, List<SyntaxTree>> pkgTrees,
            Dictionary<string, List<string>> deps)
        {
            var worklist = new Queue<string>();
            worklist.Enqueue(importPath);

            while (worklist.Count > 0)
            {
                var current = worklist.Dequeue();

                if (pkgTrees.ContainsKey(current) || _resolvedPackages.ContainsKey(current))
                    continue;
                // Skip packages already resolved by other resolvers
                if (RuntimePackageResolver.Instance.Resolve(current) != null)
                    continue;
                if (_discoveredPackages.Contains(current))
                    continue;
                _discoveredPackages.Add(current);

                var dir = ResolvePackageDir(current);
                if (dir == null || !Directory.Exists(dir))
                    continue;

                var trees = new List<SyntaxTree>();
                var imports = new List<string>();

                foreach (var file in Directory.GetFiles(dir, "*.go"))
                {
                    if (ShouldSkipGoFile(file))
                        continue;

                    try
                    {
                        var source = File.ReadAllText(file);
                        var tree = SyntaxTree.Parse(source);
                        trees.Add(tree);

                        foreach (var imp in tree.Root.Imports)
                        {
                            foreach (var spec in imp.Specs)
                            {
                                var path = spec.Path.Text.Trim('"');
                                if (!string.IsNullOrEmpty(path))
                                    imports.Add(path);
                            }
                        }
                    }
                    catch
                    {
                        // Parse failures are non-fatal for dependency discovery
                    }
                }

                if (trees.Count > 0)
                {
                    pkgTrees[current] = trees;
                    deps[current] = imports;

                    foreach (var imp in imports)
                        worklist.Enqueue(imp);
                }
            }
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
                return _moduleResolver.ResolvePackageDir(importPath, match.Value.module, match.Value.version);

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
            catch
            {
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

            result.Root.Package.Symbol.CopyExportsFrom(pkg);

            _resolvedPackages[importPath] = pkg;

            // Write .ngo archive (all sections) — only for error-free packages
            if (!result.HasErrors)
            {
                try
                {
                    var cacheDir = NgoArchive.GetCacheDir(ProjectRoot);
                    var archivePath = NgoArchive.GetArchivePath(cacheDir, importPath);
                    ILSerializer.WriteArchive(archivePath, pkg, importPath, result, _ctx);
                }
                catch (Exception ex)
                {
                    System.Console.Error.WriteLine($"[ngo] WriteArchive failed for {importPath}: {ex.GetType().Name}: {ex.Message}");
                    // Cache write failure is non-fatal — write Section 1 only
                    try
                    {
                        var cacheDir = NgoArchive.GetCacheDir(ProjectRoot);
                        var archivePath = NgoArchive.GetArchivePath(cacheDir, importPath);
                        NgoArchive.Write(archivePath, pkg, importPath);
                    }
                    catch { }
                }
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

            var source = File.ReadAllText(filePath);
            var lines = source.Split('\n');
            for (int i = 0; i < Math.Min(lines.Length, 20); i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("package "))
                    break;
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
                            return true;
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
