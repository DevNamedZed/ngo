// -----------------------------------------------------------------------
// <copyright file="InstantiatedTypeSymbol.cs" company="Ziad">
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
using System.Linq;

namespace Ngo.Compiler.Symbols
{
    public sealed class InstantiatedTypeSymbol : TypeSymbol
    {
        public InstantiatedTypeSymbol(TypeSymbol genericType, IReadOnlyList<TypeSymbol> typeArguments)
            : base(BuildName(genericType, typeArguments), genericType.TypeKind, genericType)
        {
            GenericType = genericType;
            TypeArguments = typeArguments;
        }

        public TypeSymbol GenericType { get; }

        public IReadOnlyList<TypeSymbol> TypeArguments { get; }

        private static string BuildName(TypeSymbol genericType, IReadOnlyList<TypeSymbol> typeArguments)
        {
            return genericType.Name + "[" + string.Join(", ", typeArguments.Select(a => a.Name)) + "]";
        }
    }
}
