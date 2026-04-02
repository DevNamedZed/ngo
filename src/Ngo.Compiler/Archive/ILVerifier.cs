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

namespace Ngo.Compiler.Archive
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

            // Ngo.Runtime.dll may be in a different directory than the assembly under test
            var ngoRuntimeDir = Path.GetDirectoryName(typeof(Ngo.Runtime.Slice<>).Assembly.Location)!;

            var resolver = new AssemblyResolver(runtimeDir, assemblyDir, ngoRuntimeDir);
            var verifier = new Verifier(resolver);
            verifier.SetSystemModuleName(new AssemblyNameInfo("System.Runtime"));

            var errors = new List<string>();

            using var peStream = File.OpenRead(fullPath);
            using var peReader = new PEReader(peStream);

            var verificationResults = verifier.Verify(peReader);
            foreach (var result in verificationResults)
            {
                var methodInfo = "unknown";
                var metadataReader = peReader.GetMetadataReader();
                if (!result.Method.IsNil)
                {
                    var methodDef = metadataReader.GetMethodDefinition(result.Method);
                    var methodName = metadataReader.GetString(methodDef.Name);
                    var declaringType = metadataReader.GetTypeDefinition(methodDef.GetDeclaringType());
                    var typeName = metadataReader.GetString(declaringType.Name);
                    methodInfo = $"{typeName}.{methodName}";
                }
                var details = $"[{methodInfo}] {result.Code}: {result.Message}";
                if (result.ErrorArguments != null)
                {
                    foreach (var arg in result.ErrorArguments)
                    {
                        details += $" [{arg.Name}={arg.Value}]";
                    }
                }
                errors.Add(details);
            }

            return errors;
        }

        private sealed class AssemblyResolver : IResolver
        {
            private readonly Dictionary<string, string> _assemblyPaths = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, PEReader> _cache = new(StringComparer.OrdinalIgnoreCase);

            public AssemblyResolver(string runtimeDir, string assemblyDir, string ngoRuntimeDir)
            {
                // Index all DLLs in the .NET shared framework directory
                foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
                {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    _assemblyPaths[name] = dll;
                }

                // Index DLLs in the target assembly's directory
                IndexDirectory(assemblyDir);

                // Index the Ngo.Runtime directory so ILVerify can resolve runtime types
                IndexDirectory(ngoRuntimeDir);
            }

            private void IndexDirectory(string directory)
            {
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return;
                }
                foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    if (!_assemblyPaths.ContainsKey(name))
                    {
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
