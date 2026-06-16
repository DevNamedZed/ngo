// -----------------------------------------------------------------------
// <copyright file="InterfaceTypeSymbol.cs" company="Ziad">
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
    public sealed class InterfaceTypeSymbol : TypeSymbol
    {
        public InterfaceTypeSymbol(string name, IReadOnlyList<MethodSymbol> methods, string? packagePath = null)
            : base(name, TypeKind.Interface, null, packagePath)
        {
            Methods = methods;
        }

        public new IReadOnlyList<MethodSymbol> Methods { get; private set; }

        public bool IsComparable { get; set; }

        public void SetMethods(IReadOnlyList<MethodSymbol> methods)
        {
            Methods = methods;
        }

        public new void AddMethod(MethodSymbol method)
        {
            if (Methods is List<MethodSymbol> list)
            {
                list.Add(method);
            }
            else
            {
                var newList = new List<MethodSymbol>(Methods);
                newList.Add(method);
                Methods = newList;
            }
        }

        public override MethodSymbol? LookupMethod(string name)
        {
            for (int i = 0; i < Methods.Count; i++)
            {
                if (Methods[i].Name == name)
                    return Methods[i];
            }

            return null;
        }
    }
}
