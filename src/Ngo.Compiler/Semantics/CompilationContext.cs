// -----------------------------------------------------------------------
// <copyright file="CompilationContext.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public sealed class CompilationContext
    {
        private readonly IPackageResolver[] _resolvers;
        private readonly Dictionary<string, PackageSymbol> _resolved = new();

        // Well-known package aliases: old import paths that map to stdlib packages
        private static readonly Dictionary<string, string> PackageAliases = new()
        {
            ["golang.org/x/net/context"] = "context",
        };

        public CompilationContext(string? projectRoot, ICompilerLog? log = null)
        {
            ProjectRoot = projectRoot;
            Log = log ?? NullLog.Instance;

            var resolvers = new List<IPackageResolver>();
            // Go source takes priority — compile from Go source first.
            // Fall back to C# runtime only for packages that can't compile from Go
            // (runtime intrinsics, internal/* packages, assembly-backed functions).
            if (projectRoot != null)
            {
                resolvers.Add(new GoPackageResolver(this, projectRoot));
            }
            resolvers.Add(RuntimePackageResolver.Instance);
            _resolvers = resolvers.ToArray();
        }

        public string? ProjectRoot { get; }

        public ICompilerLog Log { get; }

        public PackageSymbol? ResolvePackage(string importPath)
        {
            // Resolve well-known package aliases (e.g. golang.org/x/net/context -> context)
            if (PackageAliases.TryGetValue(importPath, out var aliased))
            {
                importPath = aliased;
            }

            if (_resolved.TryGetValue(importPath, out var cached))
            {
                return cached;
            }

            for (int i = 0; i < _resolvers.Length; i++)
            {
                var pkg = _resolvers[i].Resolve(importPath);
                if (pkg != null)
                {
                    // Cache results from GoPackageResolver (index 0).
                    // Don't cache RuntimePackageResolver results (index 1+)
                    // so GoPackageResolver gets another chance on future calls.
                    if (i == 0)
                    {
                        _resolved[importPath] = pkg;
                    }
                    return pkg;
                }
            }
            return null;
        }

        public Type? ResolveClrType(string importPath, string typeName)
        {
            foreach (var resolver in _resolvers)
            {
                var type = resolver.ResolveClrType(importPath, typeName);
                if (type != null)
                    return type;
            }
            return null;
        }

        /// <summary>
        /// Returns the source directory for a Go package resolved from source.
        /// Used by the emitter to re-compile dependencies into the host module.
        /// </summary>
        public string? GetSourceDir(string importPath)
        {
            foreach (var resolver in _resolvers)
            {
                if (resolver is GoPackageResolver goResolver)
                {
                    var dir = goResolver.GetSourceDir(importPath);
                    if (dir != null)
                    {
                        return dir;
                    }
                }
            }
            return null;
        }

        public static string GetDefaultPackageName(string importPath)
        {
            var lastSlash = importPath.LastIndexOf('/');
            return lastSlash >= 0 ? importPath.Substring(lastSlash + 1) : importPath;
        }

        /// <summary>
        /// The CGo preamble extracted from import "C", if any.
        /// Stored here so the emitter can access it for C compilation and P/Invoke generation.
        /// </summary>
        public Cgo.CgoPreamble? CgoPreamble { get; private set; }

        /// <summary>
        /// The compiled CGo result (native library path, probe results).
        /// </summary>
        public Cgo.CgoCompilationResult? CgoResult { get; set; }

        /// <summary>
        /// The C pseudo-package symbol, if import "C" was used.
        /// Stored so the emitter can match FunctionSymbols to P/Invoke methods.
        /// </summary>
        public Symbols.PackageSymbol? CgoPackage { get; set; }

        /// <summary>
        /// C function info extracted from the preamble.
        /// </summary>
        public List<Cgo.CgoFunctionInfo>? CgoFunctions { get; set; }

        /// <summary>
        /// C struct info extracted from the preamble.
        /// </summary>
        public List<Cgo.CgoStructInfo>? CgoStructs { get; set; }

        /// <summary>
        /// Go function name → C export name from //export directives.
        /// </summary>
        public Dictionary<string, string>? CgoExports { get; set; }

        public void SetCgoPreamble(Cgo.CgoPreamble preamble)
        {
            CgoPreamble = preamble;
        }
    }
}
