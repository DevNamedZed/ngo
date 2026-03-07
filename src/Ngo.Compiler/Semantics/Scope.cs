// -----------------------------------------------------------------------
// <copyright file="Scope.cs" company="Ziad">
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

using System.Collections.Generic;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public sealed class Scope
    {
        private readonly Dictionary<string, Symbol> _symbols = new();

        public Scope(string name, Scope? parent)
        {
            Name = name;
            Parent = parent;
        }

        public string Name { get; }

        public Scope? Parent { get; }

        public bool TryDeclare(Symbol symbol)
        {
            return _symbols.TryAdd(symbol.Name, symbol);
        }

        public Symbol? Lookup(string name)
        {
            if (_symbols.TryGetValue(name, out var symbol))
            {
                return symbol;
            }

            return Parent?.Lookup(name);
        }

        public Symbol? LookupLocal(string name)
        {
            return _symbols.TryGetValue(name, out var symbol) ? symbol : null;
        }

        public void Replace(string name, Symbol symbol)
        {
            _symbols[name] = symbol;
        }

        public IEnumerable<Symbol> DeclaredSymbols => _symbols.Values;
    }
}
