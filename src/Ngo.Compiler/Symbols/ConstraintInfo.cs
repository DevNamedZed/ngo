// -----------------------------------------------------------------------
// <copyright file="ConstraintInfo.cs" company="Ziad">
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
    public sealed class ConstraintInfo
    {
        public static readonly ConstraintInfo Any = new("any",
            Array.Empty<MethodSymbol>(), Array.Empty<TypeElement>(), isComparable: false);

        public static readonly ConstraintInfo Comparable = new("comparable",
            Array.Empty<MethodSymbol>(), Array.Empty<TypeElement>(), isComparable: true);

        public ConstraintInfo(string name, IReadOnlyList<MethodSymbol> methods,
            IReadOnlyList<TypeElement> typeElements, bool isComparable)
        {
            Name = name;
            Methods = methods;
            TypeElements = typeElements;
            IsComparable = isComparable;
        }

        public string Name { get; }

        public IReadOnlyList<MethodSymbol> Methods { get; }

        public IReadOnlyList<TypeElement> TypeElements { get; }

        public bool IsComparable { get; }
    }

    public sealed class TypeElement
    {
        public TypeElement(TypeSymbol type, bool isTilde)
        {
            Type = type;
            IsTilde = isTilde;
        }

        public TypeSymbol Type { get; }

        public bool IsTilde { get; }
    }
}
