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
            if (first.Name != second.Name)
            {
                return false;
            }

            if (first is MethodSymbol firstMethod && second is MethodSymbol secondMethod)
            {
                if (firstMethod.ReceiverType?.Name != secondMethod.ReceiverType?.Name)
                {
                    return false;
                }
                if (firstMethod.ReceiverType?.PackagePath != secondMethod.ReceiverType?.PackagePath)
                {
                    return false;
                }
            }

            if (first is FunctionSymbol firstFunction && second is FunctionSymbol secondFunction)
            {
                if (firstFunction.PackageName != secondFunction.PackageName)
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(Symbol symbol)
        {
            if (symbol is MethodSymbol method)
            {
                return HashCode.Combine(symbol.Name, symbol.Kind, method.ReceiverType?.Name);
            }
            if (symbol is FunctionSymbol function)
            {
                return HashCode.Combine(symbol.Name, symbol.Kind, function.PackageName);
            }
            return HashCode.Combine(symbol.Name, symbol.Kind);
        }
    }
}
