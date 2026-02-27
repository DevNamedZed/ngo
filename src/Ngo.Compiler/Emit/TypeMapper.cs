// -----------------------------------------------------------------------
// <copyright file="TypeMapper.cs" company="Ziad">
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
using System.Reflection.Emit;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Maps Go type symbols to .NET System.Type instances.
    /// </summary>
    internal sealed class TypeMapper
    {
        private readonly Dictionary<TypeSymbol, Type> _typeCache = new();

        public void Register(TypeSymbol symbol, Type type)
        {
            _typeCache[symbol] = type;
        }

        public Type Map(TypeSymbol symbol)
        {
            if (_typeCache.TryGetValue(symbol, out var cached))
                return cached;

            var result = MapCore(symbol);
            _typeCache[symbol] = result;
            return result;
        }

        private Type MapCore(TypeSymbol symbol)
        {
            switch (symbol.TypeKind)
            {
                case TypeKind.Bool:
                case TypeKind.UntypedBool:
                    return typeof(bool);

                case TypeKind.Int:
                    return typeof(long);
                case TypeKind.Int8:
                    return typeof(sbyte);
                case TypeKind.Int16:
                    return typeof(short);
                case TypeKind.Int32:
                    return typeof(int);
                case TypeKind.Int64:
                    return typeof(long);
                case TypeKind.UntypedInt:
                    return typeof(long);

                case TypeKind.Uint:
                    return typeof(ulong);
                case TypeKind.Uint8:
                    return typeof(byte);
                case TypeKind.Uint16:
                    return typeof(ushort);
                case TypeKind.Uint32:
                    return typeof(uint);
                case TypeKind.Uint64:
                    return typeof(ulong);
                case TypeKind.Uintptr:
                    return typeof(nuint);

                case TypeKind.Float32:
                    return typeof(float);
                case TypeKind.Float64:
                case TypeKind.UntypedFloat:
                    return typeof(double);

                case TypeKind.Complex64:
                case TypeKind.Complex128:
                case TypeKind.UntypedComplex:
                    return typeof(System.Numerics.Complex);

                case TypeKind.String:
                case TypeKind.UntypedString:
                    return typeof(string);

                case TypeKind.Void:
                    return typeof(void);

                case TypeKind.Slice:
                    var sliceType = (SliceTypeSymbol)symbol;
                    return typeof(Slice<>).MakeGenericType(Map(sliceType.ElementType));

                case TypeKind.Array:
                    var arrayType = (ArrayTypeSymbol)symbol;
                    return Map(arrayType.ElementType).MakeArrayType();

                case TypeKind.Map:
                    var mapType = (MapTypeSymbol)symbol;
                    return typeof(Map<,>).MakeGenericType(Map(mapType.KeyType), Map(mapType.ValueType));

                case TypeKind.Pointer:
                    var ptrType = (PointerTypeSymbol)symbol;
                    return typeof(Ptr<>).MakeGenericType(Map(ptrType.ElementType));

                case TypeKind.Struct:
                    // Runtime types map to pre-existing .NET classes
                    if (symbol.Name == "WaitGroup") return typeof(WaitGroup);
                    if (symbol.Name == "Mutex") return typeof(Mutex);
                    if (symbol.Name == "File") return typeof(GoFile);
                    if (symbol.Name == "Regexp") return typeof(GoRegexpObj);
                    if (symbol.Name == "Scanner") return typeof(GoScanner);
                    if (symbol.Name == "Reader") return typeof(GoBufferedReader);
                    if (symbol.Name == "Writer") return typeof(GoBufferedWriter);
                    // Should have been registered via DeclarationEmitter
                    throw new InvalidOperationException(
                        $"Type '{symbol.Name}' was not registered. User-defined types must be registered before use.");

                case TypeKind.Interface:
                    var ifaceType = (InterfaceTypeSymbol)symbol;
                    if (ifaceType.Methods.Count == 0)
                        return typeof(object); // empty interface{} → object
                    // Named interfaces should have been registered via DeclarationEmitter
                    throw new InvalidOperationException(
                        $"Type '{symbol.Name}' was not registered. User-defined types must be registered before use.");

                case TypeKind.Channel:
                    var chanType = (ChannelTypeSymbol)symbol;
                    return typeof(Channel<>).MakeGenericType(Map(chanType.ElementType));

                case TypeKind.Function:
                    return MapFunctionType((FunctionTypeSymbol)symbol);

                case TypeKind.UntypedNil:
                    return typeof(object);

                default:
                    throw new NotSupportedException($"Unsupported type kind: {symbol.TypeKind}");
            }
        }

        public Type MapReturnType(IReadOnlyList<TypeSymbol> returnTypes)
        {
            if (returnTypes.Count == 0)
                return typeof(void);
            if (returnTypes.Count == 1)
                return Map(returnTypes[0]);
            return MakeTupleType(returnTypes);
        }

        private Type MakeTupleType(IReadOnlyList<TypeSymbol> types)
        {
            var clrTypes = new Type[types.Count];
            for (int i = 0; i < types.Count; i++)
                clrTypes[i] = Map(types[i]);

            return MakeValueTupleType(clrTypes);
        }

        internal static Type MakeValueTupleType(Type[] types)
        {
            return types.Length switch
            {
                1 => typeof(ValueTuple<>).MakeGenericType(types),
                2 => typeof(ValueTuple<,>).MakeGenericType(types),
                3 => typeof(ValueTuple<,,>).MakeGenericType(types),
                4 => typeof(ValueTuple<,,,>).MakeGenericType(types),
                5 => typeof(ValueTuple<,,,,>).MakeGenericType(types),
                6 => typeof(ValueTuple<,,,,,>).MakeGenericType(types),
                7 => typeof(ValueTuple<,,,,,,>).MakeGenericType(types),
                _ => MakeNestedValueTupleType(types),
            };
        }

        private static Type MakeNestedValueTupleType(Type[] types)
        {
            // .NET nested ValueTuple: ValueTuple<T1..T7, ValueTuple<T8...>>
            var first7 = new Type[7];
            Array.Copy(types, first7, 7);

            var rest = new Type[types.Length - 7];
            Array.Copy(types, 7, rest, 0, rest.Length);

            var restType = MakeValueTupleType(rest);
            var allTypes = new Type[8];
            Array.Copy(first7, allTypes, 7);
            allTypes[7] = restType;

            return typeof(ValueTuple<,,,,,,,>).MakeGenericType(allTypes);
        }

        private Type MapFunctionType(FunctionTypeSymbol funcType)
        {
            var paramCount = funcType.ParameterTypes.Count;
            var hasReturn = funcType.ReturnTypes.Count > 0;

            if (!hasReturn)
            {
                if (paramCount == 0) return typeof(Action);

                var paramTypes = new Type[paramCount];
                for (int i = 0; i < paramCount; i++)
                    paramTypes[i] = Map(funcType.ParameterTypes[i]);

                return paramCount switch
                {
                    1 => typeof(Action<>).MakeGenericType(paramTypes),
                    2 => typeof(Action<,>).MakeGenericType(paramTypes),
                    3 => typeof(Action<,,>).MakeGenericType(paramTypes),
                    4 => typeof(Action<,,,>).MakeGenericType(paramTypes),
                    5 => typeof(Action<,,,,>).MakeGenericType(paramTypes),
                    6 => typeof(Action<,,,,,>).MakeGenericType(paramTypes),
                    7 => typeof(Action<,,,,,,>).MakeGenericType(paramTypes),
                    8 => typeof(Action<,,,,,,,>).MakeGenericType(paramTypes),
                    9 => typeof(Action<,,,,,,,,>).MakeGenericType(paramTypes),
                    10 => typeof(Action<,,,,,,,,,>).MakeGenericType(paramTypes),
                    11 => typeof(Action<,,,,,,,,,,>).MakeGenericType(paramTypes),
                    12 => typeof(Action<,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    13 => typeof(Action<,,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    14 => typeof(Action<,,,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    15 => typeof(Action<,,,,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    16 => typeof(Action<,,,,,,,,,,,,,,,>).MakeGenericType(paramTypes),
                    _ => throw new NotSupportedException($"Action with {paramCount} params not supported"),
                };
            }

            var returnType = MapReturnType(funcType.ReturnTypes);
            var allTypes = new Type[paramCount + 1];
            for (int i = 0; i < paramCount; i++)
                allTypes[i] = Map(funcType.ParameterTypes[i]);
            allTypes[paramCount] = returnType;

            return (paramCount + 1) switch
            {
                1 => typeof(Func<>).MakeGenericType(allTypes),
                2 => typeof(Func<,>).MakeGenericType(allTypes),
                3 => typeof(Func<,,>).MakeGenericType(allTypes),
                4 => typeof(Func<,,,>).MakeGenericType(allTypes),
                5 => typeof(Func<,,,,>).MakeGenericType(allTypes),
                6 => typeof(Func<,,,,,>).MakeGenericType(allTypes),
                7 => typeof(Func<,,,,,,>).MakeGenericType(allTypes),
                8 => typeof(Func<,,,,,,,>).MakeGenericType(allTypes),
                9 => typeof(Func<,,,,,,,,>).MakeGenericType(allTypes),
                10 => typeof(Func<,,,,,,,,,>).MakeGenericType(allTypes),
                11 => typeof(Func<,,,,,,,,,,>).MakeGenericType(allTypes),
                12 => typeof(Func<,,,,,,,,,,,>).MakeGenericType(allTypes),
                13 => typeof(Func<,,,,,,,,,,,,>).MakeGenericType(allTypes),
                14 => typeof(Func<,,,,,,,,,,,,,>).MakeGenericType(allTypes),
                15 => typeof(Func<,,,,,,,,,,,,,,>).MakeGenericType(allTypes),
                16 => typeof(Func<,,,,,,,,,,,,,,,>).MakeGenericType(allTypes),
                17 => typeof(Func<,,,,,,,,,,,,,,,,>).MakeGenericType(allTypes),
                _ => throw new NotSupportedException($"Func with {paramCount} params not supported"),
            };
        }
    }
}
