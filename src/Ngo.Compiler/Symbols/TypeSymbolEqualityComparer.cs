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
    /// <summary>
    /// Structural identity comparer for <see cref="TypeSymbol"/> that holds across .ngo
    /// archive boundaries, where a logical type is re-materialized as a new object instance.
    /// Reference equality is the fast path; otherwise:
    /// <list type="bullet">
    /// <item>structural types (pointer, slice, array, map, channel, function, instantiated,
    /// type parameter) are discriminated by their C# subclass and compared by recursing into
    /// their components;</item>
    /// <item>named types (struct, interface, plain named, builtin) are compared by
    /// (PackagePath, Name).</item>
    /// </list>
    /// It never inspects mutable bodies (a struct's fields, an interface's methods) or the
    /// mutable <see cref="TypeSymbol.TypeKind"/>, so a symbol used as a dictionary key stays
    /// stable. Structural recursion always bottoms out at a named type or builtin (Go has no
    /// infinitely-anonymous types), so no cycle guard is needed.
    /// </summary>
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

            switch (first)
            {
                case PointerTypeSymbol firstPointer:
                    return second is PointerTypeSymbol secondPointer
                        && Equals(firstPointer.ElementType, secondPointer.ElementType);
                case SliceTypeSymbol firstSlice:
                    return second is SliceTypeSymbol secondSlice
                        && Equals(firstSlice.ElementType, secondSlice.ElementType);
                case ArrayTypeSymbol firstArray:
                    return second is ArrayTypeSymbol secondArray
                        && firstArray.Length == secondArray.Length
                        && Equals(firstArray.ElementType, secondArray.ElementType);
                case MapTypeSymbol firstMap:
                    return second is MapTypeSymbol secondMap
                        && Equals(firstMap.KeyType, secondMap.KeyType)
                        && Equals(firstMap.ValueType, secondMap.ValueType);
                case ChannelTypeSymbol firstChannel:
                    return second is ChannelTypeSymbol secondChannel
                        && Equals(firstChannel.ElementType, secondChannel.ElementType);
                case FunctionTypeSymbol firstFunction:
                    return second is FunctionTypeSymbol secondFunction
                        && firstFunction.IsVariadic == secondFunction.IsVariadic
                        && SequenceEquals(firstFunction.ParameterTypes, secondFunction.ParameterTypes)
                        && SequenceEquals(firstFunction.ReturnTypes, secondFunction.ReturnTypes);
                case InstantiatedTypeSymbol firstInstantiated:
                    return second is InstantiatedTypeSymbol secondInstantiated
                        && Equals(firstInstantiated.GenericType, secondInstantiated.GenericType)
                        && SequenceEquals(firstInstantiated.TypeArguments, secondInstantiated.TypeArguments);
                case TypeParameterSymbol firstTypeParameter:
                    // Owner identity (K of Map vs K of Set) is deferred sub-decision D2; the
                    // model has no owner back-reference yet. (Name, Ordinal) is the interim key.
                    return second is TypeParameterSymbol secondTypeParameter
                        && firstTypeParameter.Ordinal == secondTypeParameter.Ordinal
                        && firstTypeParameter.Name == secondTypeParameter.Name;
                default:
                    // Named types: identity is (PackagePath, Name). A structural 'second' can
                    // never match here because its composed Name (e.g. "*Foo", "[]Foo") cannot
                    // equal a named type's Name.
                    return first.Name == second.Name
                        && first.PackagePath == second.PackagePath;
            }
        }

        public int GetHashCode(TypeSymbol symbol)
        {
            // Name is immutable and encodes the structural shape for structural types and the
            // type name for named types. PackagePath is deliberately excluded because it is
            // stamped after construction; including it would move a key to a different bucket.
            // Equals disambiguates same-named types from different packages.
            return symbol.Name.GetHashCode(StringComparison.Ordinal);
        }

        private bool SequenceEquals(IReadOnlyList<TypeSymbol> first, IReadOnlyList<TypeSymbol> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }
            for (int index = 0; index < first.Count; index++)
            {
                if (!Equals(first[index], second[index]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
