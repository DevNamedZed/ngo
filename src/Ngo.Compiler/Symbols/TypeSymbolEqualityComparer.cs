// -----------------------------------------------------------------------
// <copyright file="TypeSymbolEqualityComparer.cs" company="Ziad">
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
    public sealed class TypeSymbolEqualityComparer : IEqualityComparer<TypeSymbol>
    {
        public static readonly TypeSymbolEqualityComparer Instance = new();

        public bool Equals(TypeSymbol? first, TypeSymbol? second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }
            if (first is null || second is null)
            {
                return false;
            }
            if (first.TypeKind != second.TypeKind)
            {
                return false;
            }
            if (first.Name != second.Name)
            {
                return false;
            }
            if (first.PackagePath != second.PackagePath)
            {
                return false;
            }
            return true;
        }

        public int GetHashCode(TypeSymbol symbol)
        {
            return HashCode.Combine(symbol.Name, symbol.TypeKind, symbol.PackagePath);
        }
    }
}
