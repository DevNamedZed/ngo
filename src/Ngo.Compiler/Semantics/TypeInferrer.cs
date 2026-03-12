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

            // Use constraints to infer remaining type params.
            // E.g., for [P *T, T any], if P=*Signature, infer T=Signature from constraint P *T.
            bool progress = true;
            while (progress)
            {
                progress = false;
                for (int i = 0; i < inferred.Length; i++)
                {
                    if (inferred[i] != null && typeParams[i].Constraint.TypeElements.Count == 1)
                    {
                        var constraintType = typeParams[i].Constraint.TypeElements[0].Type;
                        Unify(constraintType, inferred[i], typeParams, inferred);
                    }
                }

                // Check if we made progress (new inferences)
                for (int i = 0; i < inferred.Length; i++)
                {
                    if (inferred[i] == null)
                    {
                        // Try to infer from constraints of already-inferred params
                        for (int j = 0; j < inferred.Length; j++)
                        {
                            if (j != i && inferred[j] != null && typeParams[j].Constraint.TypeElements.Count == 1)
                            {
                                var before = inferred[i];
                                Unify(typeParams[j].Constraint.TypeElements[0].Type, inferred[j], typeParams, inferred);
                                if (inferred[i] != null && inferred[i] != before)
                                    progress = true;
                            }
                        }
                    }
                }

                // Prevent infinite loop
                bool allInferred = true;
                for (int i = 0; i < inferred.Length; i++)
                {
                    if (inferred[i] == null) { allInferred = false; break; }
                }
                if (allInferred) break;
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

            // Resolve named types to their underlying structural types for matching
            var resolvedArg = argType.Resolved();
            if (resolvedArg == argType && argType.UnderlyingType != null)
                resolvedArg = argType.UnderlyingType;

            // For type parameter arguments with structural constraints (e.g., S ~[]E),
            // extract the structural type so we can unify against it.
            if (resolvedArg is TypeParameterSymbol argTp && argTp.Constraint.TypeElements.Count > 0)
            {
                var structural = TypeChecker.GetConstraintStructuralType(argTp);
                if (structural != null)
                    resolvedArg = structural;
            }

            if (paramType is SliceTypeSymbol paramSlice && resolvedArg is SliceTypeSymbol argSlice)
            {
                Unify(paramSlice.ElementType, argSlice.ElementType, typeParams, inferred);
                return;
            }

            if (paramType is ArrayTypeSymbol paramArr && resolvedArg is ArrayTypeSymbol argArr)
            {
                Unify(paramArr.ElementType, argArr.ElementType, typeParams, inferred);
                return;
            }

            if (paramType is MapTypeSymbol paramMap && resolvedArg is MapTypeSymbol argMap)
            {
                Unify(paramMap.KeyType, argMap.KeyType, typeParams, inferred);
                Unify(paramMap.ValueType, argMap.ValueType, typeParams, inferred);
                return;
            }

            if (paramType is PointerTypeSymbol paramPtr && resolvedArg is PointerTypeSymbol argPtr)
            {
                Unify(paramPtr.ElementType, argPtr.ElementType, typeParams, inferred);
                return;
            }

            if (paramType is ChannelTypeSymbol paramChan && resolvedArg is ChannelTypeSymbol argChan)
            {
                Unify(paramChan.ElementType, argChan.ElementType, typeParams, inferred);
                return;
            }

            // Check InstantiatedTypeSymbol on the original argType, NOT resolvedArg,
            // because Resolved() unwraps InstantiatedTypeSymbol to its GenericType
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

            // Function type: unify parameter types and return types
            if (paramType is FunctionTypeSymbol paramFunc && resolvedArg is FunctionTypeSymbol argFunc)
            {
                int pCount = paramFunc.ParameterTypes.Count < argFunc.ParameterTypes.Count
                    ? paramFunc.ParameterTypes.Count
                    : argFunc.ParameterTypes.Count;
                for (int i = 0; i < pCount; i++)
                {
                    Unify(paramFunc.ParameterTypes[i], argFunc.ParameterTypes[i], typeParams, inferred);
                }

                int rCount = paramFunc.ReturnTypes.Count < argFunc.ReturnTypes.Count
                    ? paramFunc.ReturnTypes.Count
                    : argFunc.ReturnTypes.Count;
                for (int i = 0; i < rCount; i++)
                {
                    Unify(paramFunc.ReturnTypes[i], argFunc.ReturnTypes[i], typeParams, inferred);
                }
            }
        }
    }
}
