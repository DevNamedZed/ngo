// -----------------------------------------------------------------------
// <copyright file="ILVerifier.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ILVerify;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Verifies the IL of a persisted assembly using ILVerify.
    /// </summary>
    public static class ILVerifier
    {
        /// <summary>
        /// Verifies the IL of the assembly at the given path.
        /// Returns a list of verification error messages (empty = valid).
        /// </summary>
        public static IReadOnlyList<string> Verify(string assemblyPath)
        {
            var fullPath = Path.GetFullPath(assemblyPath);
            var assemblyDir = Path.GetDirectoryName(fullPath)!;

            // Locate .NET shared framework assemblies
            var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

            var resolver = new AssemblyResolver(runtimeDir, assemblyDir);
            var verifier = new Verifier(resolver);
            verifier.SetSystemModuleName(new AssemblyNameInfo("System.Runtime"));

            var errors = new List<string>();

            using var peStream = File.OpenRead(fullPath);
            using var peReader = new PEReader(peStream);

            var verificationResults = verifier.Verify(peReader);
            foreach (var result in verificationResults)
            {
                errors.Add(result.Message);
            }

            return errors;
        }

        private sealed class AssemblyResolver : IResolver
        {
            private readonly Dictionary<string, string> _assemblyPaths = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, PEReader> _cache = new(StringComparer.OrdinalIgnoreCase);

            public AssemblyResolver(string runtimeDir, string assemblyDir)
            {
                // Index all DLLs in the .NET shared framework directory
                foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
                {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    _assemblyPaths[name] = dll;
                }

                // Index DLLs in the target assembly's directory (includes Ngo.Runtime.dll)
                if (!string.Equals(runtimeDir, assemblyDir, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var dll in Directory.GetFiles(assemblyDir, "*.dll"))
                    {
                        var name = Path.GetFileNameWithoutExtension(dll);
                        _assemblyPaths[name] = dll;
                    }
                }
            }

            public PEReader ResolveAssembly(AssemblyNameInfo assemblyName)
            {
                return Resolve(assemblyName.Name!);
            }

            public PEReader ResolveModule(AssemblyNameInfo referencingModule, string fileName)
            {
                return Resolve(Path.GetFileNameWithoutExtension(fileName));
            }

            private PEReader Resolve(string simpleName)
            {
                if (_cache.TryGetValue(simpleName, out var cached))
                    return cached;

                if (_assemblyPaths.TryGetValue(simpleName, out var path))
                {
                    var reader = new PEReader(File.OpenRead(path));
                    _cache[simpleName] = reader;
                    return reader;
                }

                throw new FileNotFoundException($"Could not resolve assembly: {simpleName}");
            }
        }
    }
}
