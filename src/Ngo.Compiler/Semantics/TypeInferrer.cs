// -----------------------------------------------------------------------
// <copyright file="TypeInferrer.cs" company="Ziad">
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
using Ngo.Compiler.Ast;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public static class TypeInferrer
    {
        public static IReadOnlyList<TypeSymbol>? InferTypeArguments(
            FunctionSymbol generic,
            IReadOnlyList<Expression> arguments)
        {
            var typeParams = generic.TypeParameters;
            var inferred = new TypeSymbol?[typeParams.Count];

            int paramCount = generic.Parameters.Count;
            int argCount = arguments.Count;
            int matchCount = paramCount < argCount ? paramCount : argCount;

            for (int i = 0; i < matchCount; i++)
            {
                var paramType = generic.Parameters[i].Type;
                var argType = arguments[i].Type;

                if (argType == TypeSymbol.Error)
                {
                    continue;
                }

                // Default untyped types before unification
                if (TypeChecker.IsUntyped(argType))
                {
                    argType = TypeChecker.DefaultType(argType);
                }

                Unify(paramType, argType, typeParams, inferred);
            }

            // Check all type params were inferred
            for (int i = 0; i < inferred.Length; i++)
            {
                if (inferred[i] == null)
                {
                    return null;
                }
            }

            var result = new TypeSymbol[inferred.Length];
            for (int i = 0; i < inferred.Length; i++)
            {
                result[i] = inferred[i]!;
            }

            return result;
        }

        private static void Unify(
            TypeSymbol paramType,
            TypeSymbol argType,
            IReadOnlyList<TypeParameterSymbol> typeParams,
            TypeSymbol?[] inferred)
        {
            if (paramType is TypeParameterSymbol tps)
            {
                for (int i = 0; i < typeParams.Count; i++)
                {
                    if (typeParams[i] == tps)
                    {
                        if (inferred[i] == null)
                        {
                            inferred[i] = argType;
                        }

                        return;
                    }
                }

                return;
            }

            if (paramType is SliceTypeSymbol paramSlice && argType is SliceTypeSymbol argSlice)
            {
                Unify(paramSlice.ElementType, argSlice.ElementType, typeParams, inferred);
                return;
            }

            if (paramType is ArrayTypeSymbol paramArr && argType is ArrayTypeSymbol argArr)
            {
                Unify(paramArr.ElementType, argArr.ElementType, typeParams, inferred);
                return;
            }

            if (paramType is MapTypeSymbol paramMap && argType is MapTypeSymbol argMap)
            {
                Unify(paramMap.KeyType, argMap.KeyType, typeParams, inferred);
                Unify(paramMap.ValueType, argMap.ValueType, typeParams, inferred);
                return;
            }

            if (paramType is PointerTypeSymbol paramPtr && argType is PointerTypeSymbol argPtr)
            {
                Unify(paramPtr.ElementType, argPtr.ElementType, typeParams, inferred);
                return;
            }

            if (paramType is ChannelTypeSymbol paramChan && argType is ChannelTypeSymbol argChan)
            {
                Unify(paramChan.ElementType, argChan.ElementType, typeParams, inferred);
                return;
            }

            if (paramType is InstantiatedTypeSymbol paramInst && argType is InstantiatedTypeSymbol argInst)
            {
                int count = paramInst.TypeArguments.Count < argInst.TypeArguments.Count
                    ? paramInst.TypeArguments.Count
                    : argInst.TypeArguments.Count;
                for (int i = 0; i < count; i++)
                {
                    Unify(paramInst.TypeArguments[i], argInst.TypeArguments[i], typeParams, inferred);
                }
            }
        }
    }
}
