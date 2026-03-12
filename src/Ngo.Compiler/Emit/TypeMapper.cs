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
using Ngo.Runtime.GoRuntimePkg;
using Ngo.Runtime.Context;
using Ngo.Runtime.Csv;
using Ngo.Runtime.Io;
using Ngo.Runtime.Os;
using Ngo.Runtime.Reflect;
using Ngo.Runtime.Strings;
using Ngo.Runtime.Testing;
using Ngo.Runtime.Time;

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
                return typeof(object); // break recursive type cycles

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
                    // Try fully-qualified CLR type resolution via CompilationContext
                    if (symbol.PackagePath != null)
                    {
                        var resolved = _compilationContext.ResolveClrType(symbol.PackagePath, symbol.Name);
                        if (resolved != null)
                            return resolved;
                    }
                    // Hardcoded fallbacks — temporary, removed as packages get [GoType] annotations
                    if (symbol.Name == "WaitGroup") return typeof(Ngo.Runtime.Sync.WaitGroup);
                    if (symbol.Name == "Mutex") return typeof(Ngo.Runtime.Sync.Mutex);
                    if (symbol.Name == "Once") return typeof(Ngo.Runtime.Sync.Once);
                    if (symbol.Name == "RWMutex") return typeof(Ngo.Runtime.Sync.RWMutex);
                    if (symbol.Name == "Builder") return typeof(Ngo.Runtime.Strings.Builder);
                    if (symbol.Name == "Buffer") return typeof(Ngo.Runtime.Bytes.Buffer);
                    if (symbol.Name == "Replacer") return typeof(Ngo.Runtime.Strings.Replacer);
                    if (symbol.Name == "Context") return typeof(GoContext);
                    if (symbol.Name == "DirEntry") return typeof(GoDirEntry);
                    if (symbol.Name == "File") return typeof(GoFile);
                    if (symbol.Name == "FileInfo") return typeof(GoFileInfo);
                    if (symbol.Name == "Time") return typeof(GoTimeValue);
                    if (symbol.Name == "Location") return typeof(object); // time.Location stub
                    if (symbol.Name == "Timer") return typeof(object); // time.Timer stub
                    if (symbol.Name == "Ticker") return typeof(object); // time.Ticker stub
                    if (symbol.Name == "Request") return typeof(object); // http.Request stub
                    if (symbol.Name == "Server") return typeof(object); // httptest.Server stub
                    if (symbol.Name == "ResponseRecorder") return typeof(object); // httptest stub
                    if (symbol.Name == "BuildInfo") return typeof(object); // runtime/debug stub
                    if (symbol.Name == "URL") return typeof(object); // net/url stub
                    // Regexp: discovered from [GoType] via RuntimePackageResolver
                    if (symbol.Name == "Scanner") return typeof(Ngo.Runtime.Bufio.Scanner);
                    if (symbol.Name == "Reader")
                    {
                        var st = (StructTypeSymbol)symbol;
                        if (st.LookupMethod("ReadAll") != null) return typeof(Ngo.Runtime.Csv.Reader);
                        if (st.LookupMethod("Seek") != null) return typeof(Ngo.Runtime.Strings.Reader);
                        return typeof(Ngo.Runtime.Bufio.Reader);
                    }
                    if (symbol.Name == "Writer")
                    {
                        var st = (StructTypeSymbol)symbol;
                        if (st.LookupMethod("WriteAll") != null) return typeof(Ngo.Runtime.Csv.Writer);
                        return typeof(Ngo.Runtime.Bufio.Writer);
                    }
                    if (symbol.Name == "Map") return typeof(Ngo.Runtime.Sync.Map);
                    if (symbol.Name == "T") return typeof(Ngo.Runtime.Testing.T); // testing.T
                    if (symbol.Name == "Encoding") return typeof(Ngo.Runtime.Base64.Encoding);
                    if (symbol.Name == "Hash") return typeof(Ngo.Runtime.Sha256.Hash);
                    if (symbol.Name == "Response") return typeof(Ngo.Runtime.Http.Response);
                    if (symbol.Name == "Body") return typeof(Ngo.Runtime.Http.ResponseBody);
                    if (symbol.Name == "Type" && IsReflectType(symbol)) return typeof(GoReflectType);
                    if (symbol.Name == "Value" && IsReflectValue(symbol)) return typeof(GoReflectValue);
                    if (symbol.Name == "StructField" && IsReflectStructField(symbol)) return typeof(GoReflectStructField);
                    if (symbol.Name == "Func" && IsRuntimeFunc(symbol)) return typeof(GoRuntimeFunc);
                    if (symbol.Name == "Pointer" && symbol is StructTypeSymbol pts && pts.Fields.Count == 0 && pts.Methods.Count == 0) return typeof(long); // unsafe.Pointer
                    if (symbol.Name == "FileInfo") return typeof(object); // os.FileInfo stub
                    if (symbol.Name is "struct{}" or "struct" && symbol is StructTypeSymbol sts && sts.Fields.Count == 0) return typeof(ValueTuple); // struct{} → ValueTuple
                    // Opaque struct types from registry packages — no full .NET runtime type yet
                    if (symbol.Name == "Rand") return typeof(object); // math/rand.Rand
                    if (symbol.Name == "Int" && symbol is StructTypeSymbol intSt && intSt.LookupMethod("Add") != null) return typeof(object); // math/big.Int
                    if (symbol.Name == "Int64") return typeof(object); // sync/atomic.Int64 or expvar.Int
                    if (symbol.Name == "Float") return typeof(object); // math/big.Float
                    if (symbol.Name == "Rat") return typeof(object); // math/big.Rat
                    if (symbol.Name == "RangeTable") return typeof(object); // unicode.RangeTable
                    if (symbol.Name == "Range16") return typeof(object); // unicode.Range16
                    if (symbol.Name == "Range32") return typeof(object); // unicode.Range32
                    if (symbol.Name == "CaseRange") return typeof(object); // unicode.CaseRange
                    if (symbol.Name == "SpecialCase") return typeof(object); // unicode.SpecialCase
                    if (symbol.Name == "SectionReader") return typeof(object); // io.SectionReader
                    if (symbol.Name == "LimitedReader") return typeof(object); // io.LimitedReader
                    if (symbol.Name == "RGBA") return typeof(object); // image/color.RGBA
                    if (symbol.Name == "RGBA64") return typeof(object); // image/color.RGBA64
                    if (symbol.Name == "NRGBA") return typeof(object); // image/color.NRGBA
                    if (symbol.Name == "NRGBA64") return typeof(object); // image/color.NRGBA64
                    if (symbol.Name == "Alpha") return typeof(object); // image/color.Alpha
                    if (symbol.Name == "Alpha16") return typeof(object); // image/color.Alpha16
                    if (symbol.Name == "Gray") return typeof(object); // image/color.Gray
                    if (symbol.Name == "Gray16") return typeof(object); // image/color.Gray16
                    if (symbol.Name == "YCbCr") return typeof(object); // image/color.YCbCr
                    if (symbol.Name == "NYCbCrA") return typeof(object); // image/color.NYCbCrA
                    if (symbol.Name == "CMYK") return typeof(object); // image/color.CMYK
                    if (symbol.Name == "Palette") return typeof(object); // image/color.Palette
                    if (symbol.Name == "Logger") return typeof(object); // log.Logger
                    if (symbol.Name == "Tree") return typeof(object); // text/template/parse.Tree
                    if (symbol.Name == "RawValue") return typeof(object); // encoding/asn1.RawValue
                    if (symbol.Name == "CurveParams") return typeof(object); // crypto/elliptic.CurveParams
                    if (symbol.Name == "CommonType") return typeof(object); // encoding/gob.CommonType
                    if (symbol.Name is "Encoder" or "encoder") return typeof(object); // encoding/xml.Encoder etc
                    if (symbol.Name is "Decoder" or "decoder") return typeof(object); // encoding/json.Decoder etc
                    if (symbol.Name == "Rectangle") return typeof(object); // image.Rectangle
                    if (symbol.Name == "Point") return typeof(object); // image.Point
                    if (symbol.Name == "Uniform") return typeof(object); // image.Uniform
                    if (symbol.Name == "Image") return typeof(object); // image types
                    if (symbol.Name == "Config") return typeof(object); // image.Config
                    throw new NotSupportedException(
                        $"Struct type '{symbol.Name}' (package: {symbol.PackagePath ?? "unknown"}) " +
                        "was not registered in the type mapper. " +
                        "Runtime types need [GoType] attributes. User types need TypeBuilder registration.");

                case TypeKind.Interface:
                    var ifaceType = (InterfaceTypeSymbol)symbol;
                    if (ifaceType.Methods.Count == 0)
                        return typeof(object); // empty interface{} → object
                    if (symbol.Name == "Context") return typeof(GoContext);
                    if (symbol.Name == "Type" && ifaceType.LookupMethod("Kind") != null) return typeof(GoReflectType);
                    // Named non-empty interfaces → object (runtime dispatch)
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

        private static bool IsReflectType(TypeSymbol symbol)
        {
            if (symbol is not StructTypeSymbol st) return false;
            return st.LookupMethod("Kind") != null && st.LookupMethod("NumField") != null;
        }

        private static bool IsReflectValue(TypeSymbol symbol)
        {
            if (symbol is not StructTypeSymbol st) return false;
            return st.LookupMethod("Kind") != null && st.LookupMethod("Interface") != null;
        }

        private static bool IsRuntimeFunc(TypeSymbol symbol)
        {
            if (symbol is not StructTypeSymbol st) return false;
            return st.LookupMethod("Name") != null && st.LookupMethod("FileLine") != null;
        }

        private static bool IsReflectStructField(TypeSymbol symbol)
        {
            if (symbol is not StructTypeSymbol st) return false;
            return st.Fields.Count >= 4 && st.Fields[0].Name == "Name" && st.Fields[1].Name == "Type";
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
