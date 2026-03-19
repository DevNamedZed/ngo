// -----------------------------------------------------------------------
// <copyright file="FunctionSymbol.cs" company="Ziad">
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
    public sealed class FunctionSymbol : Symbol
    {
        public FunctionSymbol(string name, IReadOnlyList<ParameterSymbol> parameters, TypeSymbol returnType)
            : this(name, parameters,
                  returnType == BuiltinTypes.Void
                      ? Array.Empty<TypeSymbol>()
                      : new[] { returnType })
        {
        }

        public FunctionSymbol(string name, IReadOnlyList<ParameterSymbol> parameters,
            IReadOnlyList<TypeSymbol> returnTypes, bool isVariadic = false,
            string? packageName = null)
            : this(name, Array.Empty<TypeParameterSymbol>(), parameters, returnTypes,
                  isVariadic, packageName)
        {
        }

        public FunctionSymbol(string name, IReadOnlyList<TypeParameterSymbol> typeParameters,
            IReadOnlyList<ParameterSymbol> parameters, IReadOnlyList<TypeSymbol> returnTypes,
            bool isVariadic = false, string? packageName = null)
            : base(name, SymbolKind.Function)
        {
            TypeParameters = typeParameters;
            Parameters = parameters;
            ReturnTypes = returnTypes;
            IsVariadic = isVariadic;
            PackageName = packageName;
        }

        public IReadOnlyList<TypeParameterSymbol> TypeParameters { get; }

        public IReadOnlyList<ParameterSymbol> Parameters { get; }

        public IReadOnlyList<TypeSymbol> ReturnTypes { get; }

        public bool IsVariadic { get; }

        public string? PackageName { get; }

        /// <summary>
        /// If this function has a //go:linkname directive, this is the target
        /// (e.g., "runtime.semacquire"). Used to map assembly-backed functions
        /// to their .NET intrinsic implementations.
        /// </summary>
        public string? LinkName { get; set; }

        public TypeSymbol ReturnType =>
            ReturnTypes.Count > 0 ? ReturnTypes[0] : BuiltinTypes.Void;

        public bool IsGeneric => TypeParameters.Count > 0;
    }
}
