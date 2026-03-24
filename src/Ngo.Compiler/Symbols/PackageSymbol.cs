// -----------------------------------------------------------------------
// <copyright file="PackageSymbol.cs" company="Ziad">
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

namespace Ngo.Compiler.Symbols
{
    public sealed class PackageSymbol : Symbol
    {
        private Dictionary<string, Symbol>? _exports;
        private List<string>? _imports;
        private List<PackageSymbol>? _alternates;

        public PackageSymbol(string name)
            : base(name, SymbolKind.Package)
        {
            ImportPath = "";
        }

        public PackageSymbol(string name, string importPath)
            : base(name, SymbolKind.Package)
        {
            ImportPath = importPath;
        }

        public string ImportPath { get; }

        public void AddAlternate(PackageSymbol alternate)
        {
            _alternates ??= new List<PackageSymbol>();
            _alternates.Add(alternate);
        }

        /// <summary>
        /// The import paths this package depends on.
        /// Stored in .ngo archives so dependency discovery doesn't require re-parsing source.
        /// </summary>
        public IReadOnlyList<string> Imports => (IReadOnlyList<string>?)_imports ?? Array.Empty<string>();

        public void SetImports(IReadOnlyList<string> imports)
        {
            _imports = new List<string>(imports);
        }

        public IReadOnlyDictionary<string, Symbol> Exports =>
            (IReadOnlyDictionary<string, Symbol>?)_exports
            ?? (IReadOnlyDictionary<string, Symbol>)new Dictionary<string, Symbol>();

        public void AddExport(Symbol symbol)
        {
            _exports ??= new Dictionary<string, Symbol>();
            _exports[symbol.Name] = symbol;
        }

        public void RemoveExport(string name)
        {
            _exports?.Remove(name);
        }

        public Symbol? LookupExport(string name)
        {
            Symbol? result = null;
            if (_exports != null && _exports.TryGetValue(name, out var symbol))
            {
                result = symbol;
            }

            // Check alternate packages (from same-name imports across different files).
            // If both primary and an alternate have the same export, prefer the one
            // with more methods — Go source packages have complete method sets while
            // runtime stubs may be incomplete.
            if (_alternates != null)
            {
                foreach (var alt in _alternates)
                {
                    if (alt._exports != null && alt._exports.TryGetValue(name, out var altSymbol))
                    {
                        if (result == null)
                        {
                            result = altSymbol;
                        }
                        else if (altSymbol is TypeSymbol altType && result is TypeSymbol resultType
                            && altType.Methods.Count > resultType.Methods.Count)
                        {
                            result = altSymbol;
                        }
                    }
                }
            }

            return result;
        }

        public IReadOnlyDictionary<string, Symbol> AllExports
        {
            get
            {
                if (_alternates == null || _alternates.Count == 0)
                {
                    return Exports;
                }
                var merged = new Dictionary<string, Symbol>();
                if (_exports != null)
                {
                    foreach (var kvp in _exports)
                    {
                        merged[kvp.Key] = kvp.Value;
                    }
                }
                foreach (var alt in _alternates)
                {
                    foreach (var kvp in alt.Exports)
                    {
                        if (!merged.ContainsKey(kvp.Key))
                        {
                            merged[kvp.Key] = kvp.Value;
                        }
                    }
                }
                return merged;
            }
        }

        public void CopyExportsFrom(PackageSymbol other)
        {
            foreach (var export in other.Exports)
            {
                AddExport(export.Value);
            }
        }
    }
}
