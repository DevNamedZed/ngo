// -----------------------------------------------------------------------
// <copyright file="RuntimeTypeCatalog.cs" company="Ziad">
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
using System.Reflection;
using Ngo.Runtime.Discovery;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// An owned, read-only index over a runtime assembly (Ngo.Runtime.dll). Built once from the
    /// assembly's immutable <c>[GoPackage]</c>/<c>[GoType]</c> metadata; every lookup is a dictionary
    /// hit, never a scan. Replaces the three redundant runtime-type indexes — the static
    /// <see cref="Semantics.RuntimePackageResolver"/> clr-type map, <c>BuiltinEmitter._packageTypes</c>,
    /// and <c>ILLinker</c>'s ad-hoc <c>_runtimeAssembly.GetTypes()</c>/<c>GetType()</c> scans.
    /// See <c>spec/A1-RUNTIME-CATALOG.md</c>.
    /// </summary>
    internal sealed class RuntimeTypeCatalog
    {
        private readonly Dictionary<string, List<Type>> _byGoTypeName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> _byClrFullName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Type>> _byClrShortName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> _packageClassByImportPath = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> _packageClassByShortName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> _byImportPathAndName = new(StringComparer.Ordinal);

        public RuntimeTypeCatalog(Assembly runtimeAssembly)
        {
            foreach (var type in GetLoadableTypes(runtimeAssembly))
            {
                if (type.FullName != null)
                {
                    _byClrFullName[type.FullName] = type;
                }

                IndexShortName(type.Name, type);
                // Also index a generic type (Foo`1) under its bare name (Foo) so a lookup by the Go
                // name resolves the .NET arity-suffixed type without a scan.
                int backtick = type.Name.IndexOf('`');
                if (backtick > 0)
                {
                    IndexShortName(type.Name.Substring(0, backtick), type);
                }

                var goType = type.GetCustomAttribute<GoTypeAttribute>();
                if (goType?.Name != null)
                {
                    if (!_byGoTypeName.TryGetValue(goType.Name, out var sameGoName))
                    {
                        sameGoName = new List<Type>();
                        _byGoTypeName[goType.Name] = sameGoName;
                    }
                    sameGoName.Add(type);
                }

                var goPackage = type.GetCustomAttribute<GoPackageAttribute>();
                if (goPackage != null)
                {
                    _packageClassByImportPath[goPackage.ImportPath] = type;
                    _byImportPathAndName[ImportPathKey(goPackage.ImportPath, type.Name)] = type;

                    int lastSlash = goPackage.ImportPath.LastIndexOf('/');
                    var shortName = lastSlash >= 0 ? goPackage.ImportPath.Substring(lastSlash + 1) : goPackage.ImportPath;
                    if (!_packageClassByShortName.ContainsKey(shortName))
                    {
                        _packageClassByShortName[shortName] = type;
                    }
                }
            }
        }

        /// <summary>The first runtime type carrying <c>[GoType(Name = goTypeName)]</c>, or null.</summary>
        public Type? ResolveByGoTypeName(string goTypeName) =>
            _byGoTypeName.TryGetValue(goTypeName, out var types) && types.Count > 0 ? types[0] : null;

        /// <summary>
        /// The first runtime type carrying <c>[GoType(Name = goTypeName)]</c> whose <c>Package</c>
        /// matches <paramref name="importPath"/>, or whose namespace starts with
        /// <paramref name="namespacePrefix"/> (checked per type in declaration order, matching the
        /// linker's old scan). Null if none.
        /// </summary>
        public Type? ResolveByGoTypeNameInPackageOrNamespace(string goTypeName, string? importPath, string? namespacePrefix)
        {
            if (_byGoTypeName.TryGetValue(goTypeName, out var types))
            {
                foreach (var type in types)
                {
                    var goType = type.GetCustomAttribute<GoTypeAttribute>();
                    if (importPath != null && goType?.Package == importPath)
                    {
                        return type;
                    }
                    if (namespacePrefix != null && type.Namespace != null
                        && type.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }
            return null;
        }

        /// <summary>The runtime type with the given CLR <see cref="Type.FullName"/>, or null.</summary>
        public Type? ResolveByClrFullName(string fullName) =>
            _byClrFullName.TryGetValue(fullName, out var type) ? type : null;

        /// <summary>The package class carrying <c>[GoPackage(importPath)]</c>, or null.</summary>
        public Type? ResolvePackageClass(string importPath) =>
            _packageClassByImportPath.TryGetValue(importPath, out var type) ? type : null;

        /// <summary>
        /// The package class for <paramref name="packageName"/> — matched as an import path, then as a
        /// package short name (the last import-path segment, first-wins). Null if neither matches.
        /// </summary>
        public Type? ResolvePackageClassByNameOrImportPath(string packageName)
        {
            if (_packageClassByImportPath.TryGetValue(packageName, out var byImportPath))
            {
                return byImportPath;
            }
            return _packageClassByShortName.TryGetValue(packageName, out var byShortName) ? byShortName : null;
        }

        /// <summary>
        /// A runtime type whose declaring <c>[GoPackage]</c> import path matches and whose CLR short
        /// name is <paramref name="typeName"/> (or its generic form <c>typeName`N</c>), or null.
        /// </summary>
        public Type? ResolveByGoPackageAndName(string importPath, string typeName)
        {
            if (_byImportPathAndName.TryGetValue(ImportPathKey(importPath, typeName), out var exact))
            {
                return exact;
            }
            return MatchShortName(typeName, candidate =>
            {
                var goPackage = candidate.GetCustomAttribute<GoPackageAttribute>();
                return goPackage != null && goPackage.ImportPath == importPath;
            });
        }

        /// <summary>
        /// A runtime type with CLR short name <paramref name="shortName"/> whose namespace starts with
        /// <paramref name="namespacePrefix"/>, or null.
        /// </summary>
        public Type? ResolveByShortNameInNamespace(string shortName, string namespacePrefix) =>
            MatchShortName(shortName, candidate =>
                candidate.Namespace != null
                && candidate.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal));

        // Assembly.GetTypes() throws ReflectionTypeLoadException if any type fails to load; index the
        // ones that did load (the same set the runtime emit path successfully uses).
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loaded = new List<Type>();
                foreach (var type in ex.Types)
                {
                    if (type != null)
                    {
                        loaded.Add(type);
                    }
                }
                return loaded;
            }
        }

        private void IndexShortName(string shortName, Type type)
        {
            if (!_byClrShortName.TryGetValue(shortName, out var sameShortName))
            {
                sameShortName = new List<Type>();
                _byClrShortName[shortName] = sameShortName;
            }
            sameShortName.Add(type);
        }

        private Type? MatchShortName(string shortName, Func<Type, bool> predicate)
        {
            if (_byClrShortName.TryGetValue(shortName, out var candidates))
            {
                foreach (var candidate in candidates)
                {
                    if (predicate(candidate))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        private static string ImportPathKey(string importPath, string name) =>
            importPath + " :: " + name;
    }
}
