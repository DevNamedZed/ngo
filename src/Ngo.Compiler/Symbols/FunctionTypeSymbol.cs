// -----------------------------------------------------------------------
// <copyright file="FunctionTypeSymbol.cs" company="Ziad">
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
    public sealed class FunctionTypeSymbol : TypeSymbol
    {
        public FunctionTypeSymbol(IReadOnlyList<TypeSymbol> parameterTypes, IReadOnlyList<TypeSymbol> returnTypes,
            bool isVariadic = false)
            : base(BuildName(parameterTypes, returnTypes), TypeKind.Function, null)
        {
            ParameterTypes = parameterTypes;
            ReturnTypes = returnTypes;
            IsVariadic = isVariadic;
        }

        public IReadOnlyList<TypeSymbol> ParameterTypes { get; }

        public IReadOnlyList<TypeSymbol> ReturnTypes { get; }

        public bool IsVariadic { get; }

        private static string BuildName(IReadOnlyList<TypeSymbol> parameterTypes, IReadOnlyList<TypeSymbol> returnTypes)
        {
            var paramNames = new string[parameterTypes.Count];
            for (int i = 0; i < parameterTypes.Count; i++)
            {
                paramNames[i] = parameterTypes[i].Name;
            }

            var result = "func(" + string.Join(", ", paramNames) + ")";

            if (returnTypes.Count == 1)
            {
                result += " " + returnTypes[0].Name;
            }
            else if (returnTypes.Count > 1)
            {
                var returnNames = new string[returnTypes.Count];
                for (int i = 0; i < returnTypes.Count; i++)
                {
                    returnNames[i] = returnTypes[i].Name;
                }
                result += " (" + string.Join(", ", returnNames) + ")";
            }

            return result;
        }
    }
}
