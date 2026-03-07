// -----------------------------------------------------------------------
// <copyright file="TypeSubstituter.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public static class TypeSubstituter
    {
        public static TypeSymbol Substitute(
            TypeSymbol type,
            IReadOnlyList<TypeParameterSymbol> typeParams,
            IReadOnlyList<TypeSymbol> typeArgs)
        {
            if (typeParams.Count == 0)
            {
                return type;
            }

            if (type is TypeParameterSymbol tps)
            {
                for (int i = 0; i < typeParams.Count; i++)
                {
                    if (typeParams[i] == tps
                        || (typeParams[i].Name == tps.Name && typeParams[i].Ordinal == tps.Ordinal))
                    {
                        return typeArgs[i];
                    }
                }

                return type;
            }

            if (type is SliceTypeSymbol slice)
            {
                var elem = Substitute(slice.ElementType, typeParams, typeArgs);
                return elem == slice.ElementType ? type : new SliceTypeSymbol(elem);
            }

            if (type is ArrayTypeSymbol array)
            {
                var elem = Substitute(array.ElementType, typeParams, typeArgs);
                return elem == array.ElementType ? type : new ArrayTypeSymbol(elem, array.Length);
            }

            if (type is MapTypeSymbol map)
            {
                var key = Substitute(map.KeyType, typeParams, typeArgs);
                var val = Substitute(map.ValueType, typeParams, typeArgs);
                return key == map.KeyType && val == map.ValueType
                    ? type
                    : new MapTypeSymbol(key, val);
            }

            if (type is PointerTypeSymbol ptr)
            {
                var elem = Substitute(ptr.ElementType, typeParams, typeArgs);
                return elem == ptr.ElementType ? type : new PointerTypeSymbol(elem);
            }

            if (type is ChannelTypeSymbol chan)
            {
                var elem = Substitute(chan.ElementType, typeParams, typeArgs);
                return elem == chan.ElementType ? type : new ChannelTypeSymbol(elem);
            }

            if (type is FunctionTypeSymbol funcType)
            {
                var paramTypes = SubstituteTypes(funcType.ParameterTypes, typeParams, typeArgs);
                var returnTypes = SubstituteTypes(funcType.ReturnTypes, typeParams, typeArgs);
                return new FunctionTypeSymbol(paramTypes, returnTypes);
            }

            if (type is InstantiatedTypeSymbol inst)
            {
                var args = SubstituteTypes(inst.TypeArguments, typeParams, typeArgs);
                return new InstantiatedTypeSymbol(inst.GenericType, args);
            }

            return type;
        }

        public static IReadOnlyList<TypeSymbol> SubstituteTypes(
            IReadOnlyList<TypeSymbol> types,
            IReadOnlyList<TypeParameterSymbol> typeParams,
            IReadOnlyList<TypeSymbol> typeArgs)
        {
            if (typeParams.Count == 0)
            {
                return types;
            }

            var result = new List<TypeSymbol>(types.Count);
            bool changed = false;
            for (int i = 0; i < types.Count; i++)
            {
                var substituted = Substitute(types[i], typeParams, typeArgs);
                result.Add(substituted);
                if (substituted != types[i])
                {
                    changed = true;
                }
            }

            return changed ? result : types;
        }

        public static IReadOnlyList<ParameterSymbol> SubstituteParams(
            IReadOnlyList<ParameterSymbol> parameters,
            IReadOnlyList<TypeParameterSymbol> typeParams,
            IReadOnlyList<TypeSymbol> typeArgs)
        {
            if (typeParams.Count == 0)
            {
                return parameters;
            }

            var result = new List<ParameterSymbol>(parameters.Count);
            bool changed = false;
            for (int i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];
                var substituted = Substitute(param.Type, typeParams, typeArgs);
                if (substituted != param.Type)
                {
                    result.Add(new ParameterSymbol(param.Name, substituted, param.Ordinal));
                    changed = true;
                }
                else
                {
                    result.Add(param);
                }
            }

            return changed ? result : parameters;
        }
    }
}
