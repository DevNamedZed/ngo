// -----------------------------------------------------------------------
// <copyright file="TypeEmitContext.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Type-level emit context. Created when entering a struct, interface, closure, or wrapper type.
    /// Holds the type's generic parameter bindings.
    /// </summary>
    internal sealed class TypeEmitContext
    {
        public ModuleEmitContext Module { get; }
        public ITypeBuilder TypeBuilder { get; }
        private readonly Dictionary<TypeParameterSymbol, Type> _genericParameters;

        public TypeEmitContext(ModuleEmitContext module, ITypeBuilder typeBuilder,
            IReadOnlyList<TypeParameterSymbol>? typeParameters = null,
            Type[]? genericParams = null)
        {
            Module = module;
            TypeBuilder = typeBuilder;
            _genericParameters = new Dictionary<TypeParameterSymbol, Type>();

            if (typeParameters != null && genericParams != null)
            {
                for (int i = 0; i < typeParameters.Count && i < genericParams.Length; i++)
                {
                    _genericParameters[typeParameters[i]] = genericParams[i];
                }
            }
        }

        public IReadOnlyDictionary<TypeParameterSymbol, Type> GenericParameters => _genericParameters;

        public bool TryResolveGenericParam(TypeParameterSymbol symbol, out Type type)
        {
            return _genericParameters.TryGetValue(symbol, out type);
        }

        public Type[] GetGenericParamTypes()
        {
            var result = new Type[_genericParameters.Count];
            int index = 0;
            foreach (var entry in _genericParameters)
            {
                result[index++] = entry.Value;
            }
            return result;
        }

        public Type Resolve(TypeSymbol symbol)
        {
            if (symbol is TypeParameterSymbol typeParam && _genericParameters.TryGetValue(typeParam, out var resolved))
            {
                return resolved;
            }
            return Module.Resolve(symbol);
        }

        public MethodEmitContext CreateMethodContext(IMethodBuilder methodBuilder,
            IReadOnlyList<TypeParameterSymbol>? methodTypeParameters = null,
            Type[]? methodGenericParams = null)
        {
            return new MethodEmitContext(this, methodBuilder, methodTypeParameters, methodGenericParams);
        }
    }
}
