// -----------------------------------------------------------------------
// <copyright file="TypeSymbol.cs" company="Ziad">
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
    public class TypeSymbol : Symbol
    {
        public static readonly TypeSymbol Error = new TypeSymbol("$$error", TypeKind.Error, null);

        private List<MethodSymbol>? _methods;

        public TypeSymbol(string name, TypeKind typeKind, TypeSymbol? underlyingType)
            : base(name, SymbolKind.Type)
        {
            TypeKind = typeKind;
            UnderlyingType = underlyingType;
        }

        public TypeKind TypeKind { get; set; }

        public TypeSymbol? UnderlyingType { get; set; }

        public IReadOnlyList<TypeParameterSymbol> TypeParameters { get; private set; }
            = Array.Empty<TypeParameterSymbol>();

        public bool IsGeneric => TypeParameters.Count > 0;

        public void SetTypeParameters(IReadOnlyList<TypeParameterSymbol> typeParameters)
        {
            TypeParameters = typeParameters;
        }

        public IReadOnlyList<MethodSymbol> Methods =>
            (IReadOnlyList<MethodSymbol>?)_methods ?? Array.Empty<MethodSymbol>();

        public void AddMethod(MethodSymbol method)
        {
            _methods ??= new List<MethodSymbol>();
            _methods.Add(method);
        }

        public virtual MethodSymbol? LookupMethod(string name)
        {
            if (_methods != null)
            {
                for (int i = 0; i < _methods.Count; i++)
                {
                    if (_methods[i].Name == name)
                        return _methods[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the underlying concrete type, unwrapping named type definitions.
        /// For example, if this is "type StackTrace []Frame", returns the SliceTypeSymbol.
        /// For concrete types (SliceTypeSymbol, StructTypeSymbol, etc.) returns this.
        /// </summary>
        public virtual TypeSymbol Resolved()
        {
            if (UnderlyingType != null && GetType() == typeof(TypeSymbol))
                return UnderlyingType;
            return this;
        }

        public override string ToString() => Name;
    }
}
