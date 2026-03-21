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
using Ngo.Compiler.Semantics;
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
        private readonly CompilationContext _compilationContext;
        private readonly HashSet<TypeSymbol> _inProgress = new();

        public TypeMapper(CompilationContext compilationContext)
        {
            _compilationContext = compilationContext;
        }

        public void Register(TypeSymbol symbol, Type type)
        {
            _typeCache[symbol] = type;
        }

        public Type Map(TypeSymbol symbol)
        {
            if (_typeCache.TryGetValue(symbol, out var cached))
                return cached;

            if (!_inProgress.Add(symbol))
            {
                // Recursive type (e.g., type lexFn func(*lexer) lexFn)
                // Break the cycle by returning object for self-referencing types
                return typeof(object);
            }

            try
            {
                var result = MapCore(symbol);
                _typeCache[symbol] = result;
                return result;
            }
            finally
            {
                _inProgress.Remove(symbol);
            }
        }

        private Type MapCore(TypeSymbol symbol)
        {
            // Instantiated generic type: Stack[int] → Stack<int>
            if (symbol is InstantiatedTypeSymbol inst)
            {
                var genericType = Map(inst.GenericType);
                if (!genericType.IsGenericTypeDefinition)
                    throw new InvalidOperationException(
                        $"Cannot instantiate '{inst.GenericType.Name}' (mapped to {genericType}) — it is not a generic type definition.");

                var typeArgs = new Type[inst.TypeArguments.Count];
                for (int i = 0; i < inst.TypeArguments.Count; i++)
                {
                    typeArgs[i] = Map(inst.TypeArguments[i]);
                }

                return genericType.MakeGenericType(typeArgs);
            }

            // Named type with underlying type — map through the underlying type
            if (symbol.UnderlyingType != null && symbol.GetType() == typeof(TypeSymbol))
            {
                return Map(symbol.UnderlyingType);
            }

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
                case TypeKind.UntypedRune:
                    return typeof(int);

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
                    var elemType = Map(ptrType.ElementType);
                    // Reference types (classes) don't need Ptr<T> wrapping
                    if (!elemType.IsValueType && elemType is not TypeBuilder && elemType is not GenericTypeParameterBuilder)
                        return elemType;
                    return typeof(Ptr<>).MakeGenericType(elemType);

                case TypeKind.Struct:
                    // Anonymous empty struct: struct{} → ValueTuple
                    if (symbol.Name is "struct{}" or "struct" && symbol is StructTypeSymbol sts && sts.Fields.Count == 0)
                    {
                        return typeof(ValueTuple);
                    }

                    // unsafe.Pointer → long
                    if (symbol.Name == "Pointer" && symbol.PackagePath == "unsafe")
                    {
                        return typeof(long);
                    }

                    // Resolve via [GoType] annotations on runtime types
                    if (symbol.PackagePath != null)
                    {
                        var resolved = _compilationContext.ResolveClrType(symbol.PackagePath, symbol.Name);
                        if (resolved != null)
                        {
                            return resolved;
                        }
                    }

                    // Struct without CLR type — return object as fallback
                    // This happens for dependency structs not yet linked via EmitDependencyFromSource
                    return typeof(object);

                case TypeKind.Interface:
                    var ifaceType = (InterfaceTypeSymbol)symbol;

                    // Empty interface{} → object
                    if (ifaceType.Methods.Count == 0)
                    {
                        return typeof(object);
                    }

                    // Resolve via [GoType] annotations on runtime types
                    if (symbol.PackagePath != null)
                    {
                        var resolved = _compilationContext.ResolveClrType(symbol.PackagePath, symbol.Name);
                        if (resolved != null)
                        {
                            return resolved;
                        }
                    }

                    // Check if we've already registered a CLR type for a different
                    // InterfaceTypeSymbol instance with the same name (symbol identity issue)
                    foreach (var kv in _typeCache)
                    {
                        if (kv.Key is InterfaceTypeSymbol cachedIface
                            && cachedIface.Name == symbol.Name
                            && kv.Value != typeof(object))
                        {
                            return kv.Value;
                        }
                    }

                    // Unresolved named interface → object (runtime dispatch)
                    return typeof(object);

                case TypeKind.Channel:
                    var chanType = (ChannelTypeSymbol)symbol;
                    return typeof(Channel<>).MakeGenericType(Map(chanType.ElementType));

                case TypeKind.Function:
                    return MapFunctionType((FunctionTypeSymbol)symbol);

                case TypeKind.UntypedNil:
                    return typeof(object);

                case TypeKind.TypeParameter:
                    // Must have been registered via DefineGenericParameters
                    throw new InvalidOperationException(
                        $"Type parameter '{symbol.Name}' was not registered. " +
                        "Generic type parameters must be registered before use.");

                case TypeKind.Error:
                    // Unresolved types — return object as fallback
                    return typeof(object);

                default:
                    // Unknown type kind — return object instead of crashing
                    return typeof(object);
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
            {
                clrTypes[i] = Map(types[i]);
                // System.Void cannot be a generic type argument — replace with object
                if (clrTypes[i] == typeof(void))
                {
                    clrTypes[i] = typeof(object);
                }
            }

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
