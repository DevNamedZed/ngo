// -----------------------------------------------------------------------
// <copyright file="MethodEmitContext.cs" company="Ziad">
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
    /// Method-level emit context. Created when entering a function, method, or lambda body.
    /// Holds the method's generic parameter bindings and per-method state.
    /// Resolves generic params by walking: method → parent type → module mapper.
    /// </summary>
    internal sealed class MethodEmitContext
    {
        public TypeEmitContext ParentType { get; }
        public ModuleEmitContext Module => ParentType.Module;
        public IMethodBuilder MethodBuilder { get; }
        public CilWriter IL { get; }

        private readonly Dictionary<TypeParameterSymbol, Type> _genericParameters;
        private readonly Type[] _methodGenericParamTypes;
        private readonly Type[] _typeGenericParamTypes;

        public MethodEmitContext(TypeEmitContext parentType, IMethodBuilder methodBuilder,
            IReadOnlyList<TypeParameterSymbol>? methodTypeParameters = null,
            Type[]? methodGenericParams = null)
        {
            ParentType = parentType;
            MethodBuilder = methodBuilder;
            IL = methodBuilder.GetILWriter();
            _genericParameters = new Dictionary<TypeParameterSymbol, Type>();

            if (methodTypeParameters != null && methodGenericParams != null)
            {
                _methodGenericParamTypes = methodGenericParams;
                for (int i = 0; i < methodTypeParameters.Count && i < methodGenericParams.Length; i++)
                {
                    _genericParameters[methodTypeParameters[i]] = methodGenericParams[i];
                }
            }
            else
            {
                _methodGenericParamTypes = Type.EmptyTypes;
            }

            _typeGenericParamTypes = parentType.GetGenericParamTypes();
        }

        public Type Resolve(TypeSymbol symbol)
        {
            // Method-level params first
            if (symbol is TypeParameterSymbol typeParam)
            {
                if (_genericParameters.TryGetValue(typeParam, out var methodResolved))
                {
                    return methodResolved;
                }
            }
            // Type-level params next
            return ParentType.Resolve(symbol);
        }

        /// <summary>
        /// Checks if a CLR Type is one of this method's generic parameters.
        /// Returns the index if found, -1 otherwise.
        /// </summary>
        public int FindMethodGenericParamIndex(Type type)
        {
            for (int i = 0; i < _methodGenericParamTypes.Length; i++)
            {
                if (_methodGenericParamTypes[i] == type)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Checks if a CLR Type is one of the parent type's generic parameters.
        /// Returns the index if found, -1 otherwise.
        /// </summary>
        public int FindTypeGenericParamIndex(Type type)
        {
            for (int i = 0; i < _typeGenericParamTypes.Length; i++)
            {
                if (_typeGenericParamTypes[i] == type)
                {
                    return i;
                }
            }
            return -1;
        }

        public int MethodGenericParamCount => _methodGenericParamTypes.Length;
        public int TypeGenericParamCount => _typeGenericParamTypes.Length;

        /// <summary>
        /// The TypeParameterSymbols bound to this method's generic params.
        /// Used by closures/lambdas to create child contexts with the same symbols.
        /// </summary>
        public IReadOnlyDictionary<TypeParameterSymbol, Type> GenericParameters => _genericParameters;

        /// <summary>
        /// Creates a child TypeEmitContext for a closure type inside this method.
        /// The closure type's generic params are bound to the SAME TypeParameterSymbols
        /// as this method's params, so resolution chains correctly.
        /// </summary>
        public TypeEmitContext CreateClosureTypeContext(ITypeBuilder closureBuilder,
            Type[] closureGenericParams)
        {
            // Bind the enclosing method's TypeParameterSymbols to the closure type's generic params
            var closureTypeParams = new List<TypeParameterSymbol>();
            var closureTypes = new List<Type>();
            foreach (var (symbol, _) in _genericParameters)
            {
                if (closureTypeParams.Count < closureGenericParams.Length)
                {
                    closureTypeParams.Add(symbol);
                    closureTypes.Add(closureGenericParams[closureTypeParams.Count - 1]);
                }
            }
            return new TypeEmitContext(Module, closureBuilder, closureTypeParams, closureTypes.ToArray());
        }

        /// <summary>
        /// Creates a child MethodEmitContext for a lambda method inside this method.
        /// The lambda method's generic params are bound to the SAME TypeParameterSymbols
        /// as this method's params.
        /// </summary>
        public MethodEmitContext CreateLambdaContext(TypeEmitContext parentType,
            IMethodBuilder lambdaBuilder, Type[] lambdaGenericParams)
        {
            var lambdaTypeParams = new List<TypeParameterSymbol>();
            var lambdaTypes = new List<Type>();
            foreach (var (symbol, _) in _genericParameters)
            {
                if (lambdaTypeParams.Count < lambdaGenericParams.Length)
                {
                    lambdaTypeParams.Add(symbol);
                    lambdaTypes.Add(lambdaGenericParams[lambdaTypeParams.Count - 1]);
                }
            }
            return new MethodEmitContext(parentType, lambdaBuilder, lambdaTypeParams, lambdaTypes.ToArray());
        }

    }
}
