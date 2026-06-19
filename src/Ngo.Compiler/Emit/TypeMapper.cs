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

        // Secondary index of FINALIZED types (post-CreateType), keyed by structural value
        // identity so a symbol re-materialized across an .ngo boundary resolves to the same CLR
        // type. Separate from _typeCache (reference-keyed, holds in-progress TypeBuilders) to
        // avoid the phase collision noted in spec/archive/HACK-AUDIT.md.
        private readonly Dictionary<TypeSymbol, Type> _finalizedTypeIndex
            = new(TypeSymbolEqualityComparer.Instance);
        private readonly CompilationContext _compilationContext;
        private readonly HashSet<TypeSymbol> _inProgress = new();
        private EmitContext? _emitContext;
        private readonly List<Builder.ITypeBuilder> _pendingTypeCreations = new();
        private System.Reflection.Emit.ModuleBuilder? _ngoInlineArrayModule;

        public TypeMapper(CompilationContext compilationContext)
        {
            _compilationContext = compilationContext;
        }

        internal void SetEmitContext(EmitContext ctx)
        {
            _emitContext = ctx;
        }

        internal void RegisterSourceCompiledType(string importPath, string typeName, Type clrType)
        {
            _compilationContext.RegisterSourceCompiledType(importPath, typeName, clrType);
        }

        internal void CreatePendingTypes()
        {
            foreach (var tb in _pendingTypeCreations)
            {
                tb.CreateType();
            }
            _pendingTypeCreations.Clear();
        }

        internal void PromoteTypeBuilders()
        {
            foreach (var kvp in _typeCache)
            {
                if (kvp.Value is TypeBuilder typeBuilder && typeBuilder.IsCreated())
                {
                    var finalized = typeBuilder.CreateType()!;
                    _typeCache[kvp.Key] = finalized;
                    _finalizedTypeIndex[kvp.Key] = finalized;
                }
            }
        }

        public void Register(TypeSymbol symbol, Type type)
        {
            if (type != null)
            {
                _typeCache[symbol] = type;
                if (type is not TypeBuilder)
                {
                    _finalizedTypeIndex[symbol] = type;
                }
            }
        }

        private static string? GetSafeFullName(Type type)
        {
            try
            {
                return type.FullName;
            }
            catch (ArgumentException)
            {
                var ns = type.Namespace;
                return string.IsNullOrEmpty(ns) ? type.Name : ns + "." + type.Name;
            }
            catch (NotSupportedException)
            {
                var ns = type.Namespace;
                return string.IsNullOrEmpty(ns) ? type.Name : ns + "." + type.Name;
            }
        }

        private static bool ContainsGenericParameter(Type type)
        {
            if (type.IsGenericParameter)
            {
                return true;
            }
            if (type is Builder.NgoGenericParameterType)
            {
                return true;
            }
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    if (ContainsGenericParameter(arg))
                    {
                        return true;
                    }
                }
            }
            if (type.IsArray || type.IsByRef || type.IsPointer)
            {
                var elementType = type.GetElementType();
                if (elementType != null)
                {
                    return ContainsGenericParameter(elementType);
                }
            }
            return false;
        }

        public Type Map(TypeSymbol symbol)
        {
            if (symbol == null)
            {
                throw new ArgumentNullException(nameof(symbol), "TypeMapper: cannot map null type symbol");
            }

            if (_typeCache.TryGetValue(symbol, out var cached))
            {
                return cached;
            }
            // Cross-boundary fallback: a symbol re-materialized from an .ngo archive is a
            // different instance, so the reference-keyed lookup above misses. Resolve it by
            // structural value identity against finalized types.
            if (_finalizedTypeIndex.TryGetValue(symbol, out var finalizedByValue))
            {
                return finalizedByValue;
            }

            if (!_inProgress.Add(symbol))
            {
                // Recursive type (e.g., type lexFn func(*lexer) lexFn)
                // Break the cycle by returning object for self-referencing types
                return typeof(object);
            }

            try
            {
                var result = MapCore(symbol);
                if (result == null)
                {
                    throw new InvalidOperationException(
                        $"TypeMapper: failed to map type '{symbol.Name}' (kind={symbol.TypeKind})");
                }
                if (!ContainsGenericParameter(result))
                {
                    _typeCache[symbol] = result;
                    if (result is not TypeBuilder)
                    {
                        _finalizedTypeIndex[symbol] = result;
                    }
                }
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
                // Go named function types (e.g. iter.Seq2[K,V] = func(...)) map to structural
                // delegates (Func<>/Action<>), which have no instantiable .NET generic definition.
                // Substitute the type arguments for the definition's parameters in the underlying
                // function type and map the concrete result. (The substitution produces a distinct
                // symbol per instantiation, so the type cache stays correct across instantiations.)
                if (inst.GenericType.UnderlyingType is FunctionTypeSymbol underlyingFunc
                    && inst.GenericType.TypeParameters.Count == inst.TypeArguments.Count
                    && inst.GenericType.TypeParameters.Count > 0)
                {
                    var bindings = new Dictionary<TypeParameterSymbol, TypeSymbol>();
                    for (int i = 0; i < inst.TypeArguments.Count; i++)
                    {
                        bindings[inst.GenericType.TypeParameters[i]] = inst.TypeArguments[i];
                    }
                    return Map(SubstituteTypeParameters(underlyingFunc, bindings));
                }

                var genericType = Map(inst.GenericType);
                if (!genericType.IsGenericTypeDefinition)
                {
                    if (_emitContext?.IsDependencyEmit == true)
                    {
                        _compilationContext.Log.Debug($"TypeMapper: '{inst.GenericType.Name}' is not generic, using as-is in dependency emit");
                        return genericType;
                    }
                    throw new InvalidOperationException(
                        $"Cannot instantiate '{inst.GenericType.Name}' (mapped to {genericType}) — it is not a generic type definition.");
                }

                var typeArgs = new Type[inst.TypeArguments.Count];
                for (int i = 0; i < inst.TypeArguments.Count; i++)
                {
                    typeArgs[i] = Map(inst.TypeArguments[i]);
                }

                return genericType.MakeGenericType(typeArgs);
            }

            // Named type with underlying type
            if (symbol.UnderlyingType != null && symbol.GetType() == typeof(TypeSymbol))
            {
                // For named types over composite types (slice/map/struct), resolve to CLR type
                // so method calls work (e.g., sort.StringSlice.Sort()).
                // For named types over primitives (time.Duration = int64), map through
                // the underlying type to preserve arithmetic and constant behavior.
                if (!string.IsNullOrEmpty(symbol.PackagePath))
                {
                    var underlyingKind = symbol.UnderlyingType.TypeKind;
                    if (underlyingKind == TypeKind.Slice || underlyingKind == TypeKind.Map
                        || underlyingKind == TypeKind.Struct || underlyingKind == TypeKind.Interface
                        || underlyingKind == TypeKind.Array || underlyingKind == TypeKind.Channel)
                    {
                        var resolved = _compilationContext.ResolveClrType(symbol.PackagePath, symbol.Name);
                        if (resolved != null)
                        {
                            return resolved;
                        }
                    }
                }
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
                    return typeof(GoString);

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
                    var elemType = ptrType.ElementType != null ? Map(ptrType.ElementType) : typeof(object);
                    if (elemType == null)
                    {
                        return typeof(object);
                    }
                    // Reference types (classes) don't need Ptr<T> wrapping
                    if (!elemType.IsValueType && elemType is not TypeBuilder && elemType is not GenericTypeParameterBuilder)
                    {
                        return elemType;
                    }
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

                    // Resolve via [GoType] annotations or source-compiled types
                    if (symbol.PackagePath != null)
                    {
                        var resolved = _compilationContext.ResolveClrType(symbol.PackagePath, symbol.Name);
                        if (resolved != null)
                        {
                            return resolved;
                        }
                    }

                    // Cross-boundary struct resolution is handled by the value-keyed
                    // _finalizedTypeIndex (consulted in Map before MapCore); the old
                    // name-only scan that could match a same-named type from the wrong
                    // package has been removed.

                    // Generate a CLR value type for anonymous/unresolved structs
                    if (symbol is StructTypeSymbol anonStruct && anonStruct.Fields.Count > 0 && _emitContext != null)
                    {
                        // The struct fingerprint is a Go type name (e.g. "struct{table [32]T;...}")
                        // whose '[', ']', '{', ';' etc. are reserved in the .NET type-name grammar.
                        // Encode it to a legal identifier segment before it becomes a CLR type name,
                        // deterministically so the definition and every reference resolve to the same name.
                        string qualifiedName;
                        string? namedStructPackage = null;
                        if (!string.IsNullOrEmpty(anonStruct.Name) && anonStruct.Name != "struct{}")
                        {
                            var pkgPath = anonStruct.PackagePath ?? _emitContext.CurrentPackagePath;
                            namedStructPackage = pkgPath;  // A4.3: this is a NAMED struct → stamp its defining package
                            qualifiedName = _emitContext.QualifyCrossPackageType(pkgPath, ClrTypeName.Escape(anonStruct.Name));
                        }
                        else
                        {
                            var nameBuilder = new System.Text.StringBuilder("__anon_");
                            foreach (var field in anonStruct.Fields)
                            {
                                nameBuilder.Append(field.Name);
                                nameBuilder.Append('_');
                                nameBuilder.Append(field.Type.Name);
                                nameBuilder.Append('_');
                            }
                            qualifiedName = _emitContext.QualifyName(ClrTypeName.Escape(nameBuilder.ToString()));
                        }

                        foreach (var cached in _typeCache.Values)
                        {
                            if (cached != null && GetSafeFullName(cached) == qualifiedName)
                            {
                                _typeCache[symbol] = cached;
                                foreach (var field in anonStruct.Fields)
                                {
                                    if (!_emitContext.StructFields.ContainsKey(field))
                                    {
                                        foreach (var existingEntry in _emitContext.StructFields)
                                        {
                                            var declaring = existingEntry.Value.DeclaringType;
                                            if (existingEntry.Key.Name == field.Name
                                                && declaring != null
                                                && GetSafeFullName(declaring) == qualifiedName)
                                            {
                                                _emitContext.StructFields[field] = existingEntry.Value;
                                                break;
                                            }
                                        }
                                    }
                                }
                                return cached;
                            }
                        }

                        Builder.ITypeBuilder structBuilder;
                        try
                        {
                            structBuilder = _emitContext.Module.DefineType(
                                qualifiedName,
                                System.Reflection.TypeAttributes.Public
                                | System.Reflection.TypeAttributes.Sealed
                                | System.Reflection.TypeAttributes.SequentialLayout,
                                typeof(System.ValueType));
                        }
                        catch (ArgumentException ex)
                        {
                            throw new InvalidOperationException(
                                $"TypeMapper: struct type name collision for '{qualifiedName}'", ex);
                        }

                        // A4.3: a NAMED struct carries its defining package so its token serializes as a
                        // canonical PackageTypeRef (anon structs — namedStructPackage null — keep TypeDef).
                        if (namedStructPackage != null)
                        {
                            structBuilder.StampPackagePath(namedStructPackage);
                        }

                        if (anonStruct.TypeParameters.Count > 0)
                        {
                            var gpNames = new string[anonStruct.TypeParameters.Count];
                            for (int gp = 0; gp < gpNames.Length; gp++)
                            {
                                gpNames[gp] = anonStruct.TypeParameters[gp].Name;
                            }
                            structBuilder.DefineGenericParameters(gpNames);
                        }

                        if (_emitContext.IsDependencyEmit
                            && _emitContext.Module is Builder.NgoModuleBuilder ngoMod)
                        {
                            bool isCurrentPackage = anonStruct.PackagePath == null
                                || anonStruct.PackagePath == _emitContext.CurrentPackagePath;
                            if (!isCurrentPackage)
                            {
                                ngoMod.ExternalTypeNames.Add(qualifiedName);
                            }
                        }

                        _emitContext.Definitions.RegisterType(qualifiedName, structBuilder);

                        foreach (var field in anonStruct.Fields)
                        {
                            var fieldType = Map(field.Type);
                            var fieldBuilder = structBuilder.DefineField(
                                field.Name,
                                fieldType,
                                System.Reflection.FieldAttributes.Public);
                            _emitContext.StructFields[field] = fieldBuilder;
                            _emitContext.Definitions.RegisterField(qualifiedName, field.Name, fieldBuilder);
                        }
                        structBuilder.CreateType();
                        return structBuilder.AsType();
                    }

                    // Struct without CLR type and no emit context — return object as fallback
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
                    // InterfaceTypeSymbol instance with the same method set (symbol identity issue)
                    foreach (var kv in _typeCache)
                    {
                        if (kv.Key is InterfaceTypeSymbol cachedIface
                            && cachedIface.Name == symbol.Name
                            && InterfaceMethodSetsMatch(cachedIface, ifaceType)
                            && kv.Value != typeof(object))
                        {
                            return kv.Value;
                        }
                    }

                    // The 'error' interface maps to object (errors are strings in ngo)
                    if (ifaceType.Name == "error")
                    {
                        return typeof(object);
                    }

                    // Anonymous or unresolved Go interface with methods:
                    // generate a .NET interface type so wrapper types can implement it.
                    if (ifaceType.Methods.Count > 0 && _emitContext != null)
                    {
                        var ifaceName = BuildAnonymousInterfaceName(ifaceType);
                        var qualifiedIfaceName = _emitContext.QualifyName(ifaceName);
                        var ifaceBuilder = _emitContext.Module.DefineType(
                            qualifiedIfaceName,
                            System.Reflection.TypeAttributes.Public
                            | System.Reflection.TypeAttributes.Interface
                            | System.Reflection.TypeAttributes.Abstract,
                            null!, // interfaces have no base type
                            System.Type.EmptyTypes);
                        _emitContext.Definitions.RegisterType(qualifiedIfaceName, ifaceBuilder);

                        foreach (var method in ifaceType.Methods)
                        {
                            var paramTypes = new Type[method.Parameters.Count];
                            for (int idx = 0; idx < method.Parameters.Count; idx++)
                            {
                                paramTypes[idx] = Map(method.Parameters[idx].Type);
                            }
                            var returnType = MapReturnType(method.ReturnTypes);
                            var ifaceMethod = ifaceBuilder.DefineMethod(
                                method.Name,
                                System.Reflection.MethodAttributes.Public
                                | System.Reflection.MethodAttributes.Virtual
                                | System.Reflection.MethodAttributes.Abstract,
                                returnType, paramTypes);
                            _emitContext.Definitions.RegisterMethod(qualifiedIfaceName, method.Name, paramTypes, ifaceMethod);
                        }
                        ifaceBuilder.CreateType();
                        return ifaceBuilder.AsType();
                    }

                    // Unresolved named interface → object (runtime dispatch)
                    return typeof(object);

                case TypeKind.Channel:
                    var chanType = (ChannelTypeSymbol)symbol;
                    return typeof(Channel<>).MakeGenericType(Map(chanType.ElementType));

                case TypeKind.Function:
                    if (symbol is FunctionTypeSymbol fts)
                    {
                        return MapFunctionType(fts);
                    }
                    return typeof(object);

                case TypeKind.UntypedNil:
                    return typeof(object);

                case TypeKind.TypeParameter:
                    // During dependency emit, unregistered type params come from OTHER
                    // generic contexts (e.g., a referenced generic type's definition).
                    // These represent "any type" in that position — map to object.
                    // This is NOT an error: the function's own type params were registered
                    // by DefineGenericParameters. The unregistered ones belong to types
                    // from other packages whose definitions weren't fully instantiated.
                    if (_emitContext?.IsDependencyEmit == true)
                    {
                        return typeof(object);
                    }
                    throw new InvalidOperationException(
                        $"Type parameter '{symbol.Name}' was not registered. " +
                        "Generic type parameters must be registered before use.");

                case TypeKind.Error:
                    // Go's 'error' interface — map to object (same as error interface)
                    return typeof(object);

                default:
                    throw new InvalidOperationException(
                        $"Unknown type kind '{symbol.TypeKind}' for type '{symbol.Name}'");
            }
        }

        private static bool InterfaceMethodSetsMatch(InterfaceTypeSymbol first, InterfaceTypeSymbol second)
        {
            if (first.Methods.Count != second.Methods.Count)
            {
                return false;
            }
            for (int index = 0; index < first.Methods.Count; index++)
            {
                if (first.Methods[index].Name != second.Methods[index].Name)
                {
                    return false;
                }
            }
            return true;
        }

        private static string BuildAnonymousInterfaceName(InterfaceTypeSymbol ifaceType)
        {
            if (string.IsNullOrEmpty(ifaceType.Name) || ifaceType.Name == "interface")
            {
                var sortedMethodNames = new List<string>(ifaceType.Methods.Count);
                foreach (var method in ifaceType.Methods)
                {
                    sortedMethodNames.Add(method.Name);
                }
                sortedMethodNames.Sort(StringComparer.Ordinal);
                return $"I__anon_{string.Join("_", sortedMethodNames)}";
            }

            // Encode the wrapped interface's declaring package so that two distinct named interfaces
            // sharing a short name (e.g. go/ast's Expr vs go/build/constraint's Expr) get distinct
            // wrapper types — otherwise both become "I__Expr" and a method token resolves to whichever
            // was registered, losing identity. Explicit identity from the symbol, not a name guess.
            if (!string.IsNullOrEmpty(ifaceType.PackagePath))
            {
                var sanitizedPackage = ifaceType.PackagePath!.Replace('/', '_').Replace('.', '_');
                return $"I__{sanitizedPackage}_{ifaceType.Name}";
            }
            return $"I__{ifaceType.Name}";
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

        // Produces a copy of a structural type with the given type parameters replaced by their
        // bound arguments. Used to instantiate a generic named function type (whose body references
        // its own type parameters) before mapping it to a concrete .NET delegate.
        private static TypeSymbol SubstituteTypeParameters(TypeSymbol type, Dictionary<TypeParameterSymbol, TypeSymbol> bindings)
        {
            switch (type)
            {
                case TypeParameterSymbol typeParameter:
                    return bindings.TryGetValue(typeParameter, out var bound) ? bound : type;
                case SliceTypeSymbol slice:
                    return new SliceTypeSymbol(SubstituteTypeParameters(slice.ElementType, bindings));
                case ArrayTypeSymbol array:
                    return new ArrayTypeSymbol(SubstituteTypeParameters(array.ElementType, bindings), array.Length);
                case PointerTypeSymbol pointer:
                    return new PointerTypeSymbol(SubstituteTypeParameters(pointer.ElementType, bindings));
                case ChannelTypeSymbol channel:
                    return new ChannelTypeSymbol(SubstituteTypeParameters(channel.ElementType, bindings));
                case MapTypeSymbol map:
                    return new MapTypeSymbol(
                        SubstituteTypeParameters(map.KeyType, bindings),
                        SubstituteTypeParameters(map.ValueType, bindings));
                case FunctionTypeSymbol function:
                    var substitutedParams = new List<TypeSymbol>(function.ParameterTypes.Count);
                    foreach (var parameterType in function.ParameterTypes)
                    {
                        substitutedParams.Add(SubstituteTypeParameters(parameterType, bindings));
                    }
                    var substitutedReturns = new List<TypeSymbol>(function.ReturnTypes.Count);
                    foreach (var returnType in function.ReturnTypes)
                    {
                        substitutedReturns.Add(SubstituteTypeParameters(returnType, bindings));
                    }
                    return new FunctionTypeSymbol(substitutedParams, substitutedReturns, function.IsVariadic);
                case InstantiatedTypeSymbol instantiated:
                    var substitutedArgs = new List<TypeSymbol>(instantiated.TypeArguments.Count);
                    foreach (var argument in instantiated.TypeArguments)
                    {
                        substitutedArgs.Add(SubstituteTypeParameters(argument, bindings));
                    }
                    return new InstantiatedTypeSymbol(instantiated.GenericType, substitutedArgs);
                default:
                    return type;
            }
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
        internal Type GetOrCreateInlineArrayType(Type elementType, int length)
        {
            if (length <= 0)
            {
                return elementType.MakeArrayType();
            }

            var key = (elementType, length);
            var cache = _emitContext?.InlineArrayTypes;
            if (cache != null && cache.TryGetValue(key, out var existing))
            {
                return existing;
            }

            System.Reflection.Emit.ModuleBuilder moduleBuilder;
            if (_emitContext?.Module is Builder.LiveModuleBuilder liveMod)
            {
                moduleBuilder = liveMod.Inner;
            }
            else
            {
                // NgoWriter path: create a throwaway module for InlineArray types
                if (_ngoInlineArrayModule == null)
                {
                    var asmName = new System.Reflection.AssemblyName("NgoInlineArrays_" + System.Guid.NewGuid().ToString("N")[..8]);
                    var asm = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
                        asmName, System.Reflection.Emit.AssemblyBuilderAccess.RunAndCollect);
                    _ngoInlineArrayModule = asm.DefineDynamicModule("NgoInlineArrays");
                }
                moduleBuilder = _ngoInlineArrayModule;
            }

            var elemName = elementType.Name
                .Replace('.', '_')
                .Replace('`', '_')
                .Replace('[', '_')
                .Replace(']', '_');
            var typeName = $"GoArray_{elemName}_{length}";

            // Check if already defined in this module.
            // PersistedAssemblyBuilder modules don't support GetType(), so use
            // a try/catch to fall through gracefully.
            try
            {
                var existingType = moduleBuilder.GetType(typeName);
                if (existingType != null)
                {
                    if (cache != null) cache[key] = existingType;
                    return existingType;
                }
            }
            catch (NotImplementedException)
            {
                // PersistedAssemblyBuilder modules don't support GetType.
                // The cache check above already handles deduplication.
            }

            var typeBuilder = moduleBuilder.DefineType(typeName,
                System.Reflection.TypeAttributes.Public
                | System.Reflection.TypeAttributes.SequentialLayout
                | System.Reflection.TypeAttributes.Sealed,
                typeof(System.ValueType));

            // Track it on the owned module registry so the finalization assert sees it (spec/A4 §A4.1).
            if (_emitContext?.Module is Builder.LiveModuleBuilder liveModuleForInlineArray)
            {
                liveModuleForInlineArray.RegisterDefinedType(typeBuilder);
            }

            var attrCtor = typeof(System.Runtime.CompilerServices.InlineArrayAttribute)
                .GetConstructor(new[] { typeof(int) })!;
            typeBuilder.SetCustomAttribute(
                new System.Reflection.Emit.CustomAttributeBuilder(attrCtor, new object[] { length }));

            // When the element type is an unfinished TypeBuilder (or wrapper like NgoBuilderType),
            // DefineField crashes with "Must be an array type" because the CLR's signature
            // encoder calls GetArrayRank() on non-RuntimeType types. Use typeof(byte) as a stand-in.
            bool isUnfinishedType = elementType is System.Reflection.Emit.TypeBuilder
                || elementType is System.Reflection.TypeDelegator;
            var fieldElementType = isUnfinishedType ? typeof(byte) : elementType;

            typeBuilder.DefineField("_element0", fieldElementType,
                System.Reflection.FieldAttributes.Private);

            if (cache != null)
            {
                cache[key] = typeBuilder;
            }
            return typeBuilder;
        }
    }
}
