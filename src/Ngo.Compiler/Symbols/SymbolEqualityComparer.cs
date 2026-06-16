// -----------------------------------------------------------------------
// <copyright file="SymbolEqualityComparer.cs" company="Ziad">
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
    /// <summary>
    /// Structural identity comparer for any <see cref="Symbol"/> that holds across .ngo
    /// archive boundaries. Reference equality is the fast path; type symbols delegate to
    /// <see cref="TypeSymbolEqualityComparer"/>; methods are identified by
    /// (Name, receiver identity, pointer-receiver); functions by (Name, package). Like the
    /// type comparer, it relies only on immutable identity fields so a symbol stays stable as
    /// a dictionary key.
    /// </summary>
    public sealed class SymbolEqualityComparer : IEqualityComparer<Symbol>
    {
        public static readonly SymbolEqualityComparer Instance = new();

        public bool Equals(Symbol? first, Symbol? second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }
            if (first is null || second is null)
            {
                return false;
            }
            if (first.Kind != second.Kind)
            {
                return false;
            }

            if (first is TypeSymbol firstType)
            {
                return second is TypeSymbol secondType
                    && TypeSymbolEqualityComparer.Instance.Equals(firstType, secondType);
            }

            if (first.Name != second.Name)
            {
                return false;
            }

            if (first is MethodSymbol firstMethod && second is MethodSymbol secondMethod)
            {
                return firstMethod.IsPointerReceiver == secondMethod.IsPointerReceiver
                    && TypeSymbolEqualityComparer.Instance.Equals(
                        firstMethod.ReceiverType, secondMethod.ReceiverType);
            }

            if (first is FunctionSymbol firstFunction && second is FunctionSymbol secondFunction)
            {
                return firstFunction.PackageName == secondFunction.PackageName;
            }

            return true;
        }

        public int GetHashCode(Symbol symbol)
        {
            if (symbol is TypeSymbol type)
            {
                return TypeSymbolEqualityComparer.Instance.GetHashCode(type);
            }
            if (symbol is MethodSymbol method)
            {
                return HashCode.Combine(symbol.Name, symbol.Kind,
                    TypeSymbolEqualityComparer.Instance.GetHashCode(method.ReceiverType));
            }
            if (symbol is FunctionSymbol function)
            {
                return HashCode.Combine(symbol.Name, symbol.Kind, function.PackageName);
            }
            return HashCode.Combine(symbol.Name, symbol.Kind);
        }
    }
}
