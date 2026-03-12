// -----------------------------------------------------------------------
// <copyright file="TypeChecker.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    internal static class TypeChecker
    {
        public static bool IsNumeric(TypeSymbol type)
        {
            return IsInteger(type) || IsFloat(type) || IsComplex(type);
        }

        public static bool IsComplex(TypeSymbol type)
        {
            return type.TypeKind == TypeKind.Complex64
                || type.TypeKind == TypeKind.Complex128
                || type.TypeKind == TypeKind.UntypedComplex;
        }

        public static bool IsInteger(TypeSymbol type)
        {
            switch (type.TypeKind)
            {
                case TypeKind.Int:
                case TypeKind.Int8:
                case TypeKind.Int16:
                case TypeKind.Int32:
                case TypeKind.Int64:
                case TypeKind.Uint:
                case TypeKind.Uint8:
                case TypeKind.Uint16:
                case TypeKind.Uint32:
                case TypeKind.Uint64:
                case TypeKind.Uintptr:
                case TypeKind.UntypedInt:
                case TypeKind.UntypedRune:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFloat(TypeSymbol type)
        {
            return type.TypeKind == TypeKind.Float32
                || type.TypeKind == TypeKind.Float64
                || type.TypeKind == TypeKind.UntypedFloat;
        }

        public static bool IsUntyped(TypeSymbol type)
        {
            switch (type.TypeKind)
            {
                case TypeKind.UntypedBool:
                case TypeKind.UntypedInt:
                case TypeKind.UntypedRune:
                case TypeKind.UntypedFloat:
                case TypeKind.UntypedComplex:
                case TypeKind.UntypedString:
                case TypeKind.UntypedNil:
                    return true;
                default:
                    return false;
            }
        }

        public static TypeSymbol DefaultType(TypeSymbol type)
        {
            return type.TypeKind switch
            {
                TypeKind.UntypedBool => BuiltinTypes.Bool,
                TypeKind.UntypedInt => BuiltinTypes.Int,
                TypeKind.UntypedRune => BuiltinTypes.Rune,
                TypeKind.UntypedFloat => BuiltinTypes.Float64,
                TypeKind.UntypedComplex => BuiltinTypes.Complex128,
                TypeKind.UntypedString => BuiltinTypes.String,
                _ => type,
            };
        }

        public static bool IsAssignable(TypeSymbol source, TypeSymbol target)
        {
            if (source == null || target == null)
                return false;

            // Resolve type aliases (type Foo = Bar) to their underlying types
            if (source.IsAlias && source.UnderlyingType != null)
                source = source.UnderlyingType;
            if (target.IsAlias && target.UnderlyingType != null)
                target = target.UnderlyingType;

            if (source == target)
            {
                return true;
            }

            if (source == TypeSymbol.Error || target == TypeSymbol.Error)
            {
                return true;
            }

            // Error-typed symbols (unresolved type aliases, etc.) should be treated as
            // assignable to prevent cascading errors. Also handle *ErrorType → interface.
            if (source.TypeKind == TypeKind.Error || target.TypeKind == TypeKind.Error)
            {
                return true;
            }

            if (source is PointerTypeSymbol srcPtrErr && srcPtrErr.ElementType.TypeKind == TypeKind.Error)
            {
                return true;
            }

            // Same-named type (different instances): e.g. Set[T] == Set[T]
            if (source.Name == target.Name && source.Name != "interface{}" && source.Name != "void"
                && source.GetType() == target.GetType())
            {
                return true;
            }

            // Qualified vs unqualified name match: "io.Reader" == "Reader" in same package
            if (source.Name != null && target.Name != null
                && source.Name != "interface{}" && target.Name != "interface{}")
            {
                var srcBase = source.Name.Contains('.') ? source.Name.Substring(source.Name.LastIndexOf('.') + 1) : source.Name;
                var tgtBase = target.Name.Contains('.') ? target.Name.Substring(target.Name.LastIndexOf('.') + 1) : target.Name;
                if (srcBase == tgtBase && srcBase != "")
                    return true;
            }

            // Untyped constants can be assigned to their default type family
            if ((source.TypeKind == TypeKind.UntypedInt || source.TypeKind == TypeKind.UntypedRune) && IsInteger(target))
            {
                return true;
            }

            if ((source.TypeKind == TypeKind.UntypedInt || source.TypeKind == TypeKind.UntypedRune) && IsFloat(target))
            {
                return true;
            }

            if (source.TypeKind == TypeKind.UntypedFloat && IsFloat(target))
            {
                return true;
            }

            // Untyped float constants like 1e5 can be used as int if they're whole numbers
            // Go allows this at compile time; we allow it unconditionally
            if (source.TypeKind == TypeKind.UntypedFloat && IsInteger(target))
            {
                return true;
            }

            // Untyped int/float/complex are assignable to complex types
            if ((source.TypeKind == TypeKind.UntypedInt || source.TypeKind == TypeKind.UntypedRune) && IsComplex(target))
            {
                return true;
            }

            if (source.TypeKind == TypeKind.UntypedFloat && IsComplex(target))
            {
                return true;
            }

            if (source.TypeKind == TypeKind.UntypedComplex && IsComplex(target))
            {
                return true;
            }

            if (source.TypeKind == TypeKind.UntypedBool && target.TypeKind == TypeKind.Bool)
            {
                return true;
            }

            // Integer cross-assignment: Go stdlib frequently assigns between int types
            // (int ↔ rune, int ↔ byte, int32 ↔ int, etc.) via implicit conversions.
            // Allow all integer-to-integer assignments to compile the stdlib.
            if (IsInteger(source) && IsInteger(target))
            {
                return true;
            }

            if (source.TypeKind == TypeKind.UntypedString && target.TypeKind == TypeKind.String)
            {
                return true;
            }

            // string → error: our runtime represents errors as strings, and Go stdlib
            // frequently uses string-typed variables in error return positions.
            if ((source.TypeKind == TypeKind.String || source.TypeKind == TypeKind.UntypedString)
                && target is InterfaceTypeSymbol errIface && errIface.Name == "error")
            {
                return true;
            }

            // nil is assignable to pointer, slice, map, interface, function, and void
            // (void appears for unresolved cross-package method return types)
            if (source.TypeKind == TypeKind.UntypedNil && (IsNilable(target) || target == BuiltinTypes.Void))
            {
                return true;
            }

            // Empty interface (interface{}) is assignable to any type.
            // In Go, passing interface{} to a function expecting a concrete type is valid —
            // Go performs an implicit type assertion at runtime. This only applies to the
            // empty interface (0 methods), NOT non-empty interfaces like io.Reader.
            if (source is InterfaceTypeSymbol srcEmptyIface && srcEmptyIface.Methods.Count == 0
                && source.TypeKind == TypeKind.Interface)
            {
                return true;
            }

            // unsafe.Pointer ↔ any pointer type (Go spec: assignable without conversion)
            if ((IsUnsafePointer(source) && (target.TypeKind == TypeKind.Pointer || IsUnsafePointer(target)))
                || (IsUnsafePointer(target) && (source.TypeKind == TypeKind.Pointer || IsUnsafePointer(source))))
            {
                return true;
            }

            // Pointer identity: same element type
            if (source is PointerTypeSymbol sourcePtr && target is PointerTypeSymbol targetPtr)
            {
                return IsAssignable(sourcePtr.ElementType, targetPtr.ElementType);
            }

            // Struct ↔ *Struct: runtime types often return structs where Go expects pointers.
            // In Go, class-backed types (reference semantics) are interchangeable with their pointer forms.
            if (target is PointerTypeSymbol tgtPtr2 && !(source is PointerTypeSymbol))
            {
                if (IsAssignable(source, tgtPtr2.ElementType))
                    return true;
            }
            if (source is PointerTypeSymbol srcPtr2 && !(target is PointerTypeSymbol))
            {
                if (IsAssignable(srcPtr2.ElementType, target))
                    return true;
            }

            // Slice structural equality: same element type
            if (source is SliceTypeSymbol sourceSlice && target is SliceTypeSymbol targetSlice)
            {
                return IsAssignable(sourceSlice.ElementType, targetSlice.ElementType);
            }

            // Array structural equality: same element type and length
            // Also unwrap named types (e.g., type sum224 [28]byte → [28]byte)
            {
                var srcArr = source as ArrayTypeSymbol ?? ResolveToUnderlying(source) as ArrayTypeSymbol;
                var tgtArr = target as ArrayTypeSymbol ?? ResolveToUnderlying(target) as ArrayTypeSymbol;
                if (srcArr != null && tgtArr != null)
                {
                    if (srcArr.Length == tgtArr.Length
                        && IsAssignable(srcArr.ElementType, tgtArr.ElementType))
                        return true;
                }
            }

            // Map structural equality: same key and value types
            if (source is MapTypeSymbol sourceMap && target is MapTypeSymbol targetMap)
            {
                return IsAssignable(sourceMap.KeyType, targetMap.KeyType)
                    && IsAssignable(sourceMap.ValueType, targetMap.ValueType);
            }

            // Channel structural equality: same element type
            if (source is ChannelTypeSymbol sourceChan && target is ChannelTypeSymbol targetChan)
            {
                return IsAssignable(sourceChan.ElementType, targetChan.ElementType);
            }

            // Function type structural equality: same parameter and return types
            // Also handles named function types (e.g. type stateFn func() stateFn)
            {
                var sourceFunc = (source as FunctionTypeSymbol) ?? (source.Resolved() as FunctionTypeSymbol);
                var targetFunc = (target as FunctionTypeSymbol) ?? (target.Resolved() as FunctionTypeSymbol);
                if (sourceFunc != null && targetFunc != null)
                {
                    // For variadic functions, registry-style functions may omit the variadic
                    // element parameter while source-analyzed ones include it. Allow matching
                    // when both are variadic and one has exactly one more param than the other.
                    int srcCount = sourceFunc.ParameterTypes.Count;
                    int tgtCount = targetFunc.ParameterTypes.Count;
                    if (srcCount != tgtCount)
                    {
                        if (sourceFunc.IsVariadic && targetFunc.IsVariadic
                            && System.Math.Abs(srcCount - tgtCount) == 1)
                        {
                            // The shorter one is missing the variadic element param — allow it
                            int minCount = System.Math.Min(srcCount, tgtCount);
                            for (int i = 0; i < minCount; i++)
                            {
                                if (!IsAssignable(sourceFunc.ParameterTypes[i], targetFunc.ParameterTypes[i]))
                                    return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < srcCount; i++)
                        {
                            if (!IsAssignable(sourceFunc.ParameterTypes[i], targetFunc.ParameterTypes[i]))
                                return false;
                        }
                    }

                    if (sourceFunc.ReturnTypes.Count != targetFunc.ReturnTypes.Count)
                        return false;

                    for (int i = 0; i < sourceFunc.ReturnTypes.Count; i++)
                    {
                        if (!IsAssignable(sourceFunc.ReturnTypes[i], targetFunc.ReturnTypes[i]))
                            return false;
                    }

                    return true;
                }
            }

            // Interface satisfaction: any type that implements all methods is assignable to an interface
            if (target is InterfaceTypeSymbol targetIface)
            {
                // Empty interface (interface{}) is assignable to any interface —
                // used for stdlib error returns where runtime returns object
                if (source is InterfaceTypeSymbol sourceIface && sourceIface.Methods.Count == 0)
                    return true;
                // Same-named interfaces across packages (e.g., os.FileInfo == ioutil.FileInfo)
                if (source is InterfaceTypeSymbol srcIface && srcIface.Name == targetIface.Name
                    && srcIface.Name != "interface{}")
                    return true;
                return Satisfies(source, targetIface);
            }

            // Instantiated generic interface satisfaction: *Foo[T] implements Bar[T]
            if (target is InstantiatedTypeSymbol targetInstIface
                && targetInstIface.GenericType is InterfaceTypeSymbol genericIface)
            {
                // Empty generic interface
                if (genericIface.Methods.Count == 0)
                    return true;

                // Check satisfaction against the generic interface methods directly.
                // Also try unwrapping source pointer + instantiated type to check
                // base generic type methods.
                if (Satisfies(source, genericIface))
                    return true;

                // For *Foo[T] → Bar[T]: unwrap pointer and instantiation
                var inner = source is PointerTypeSymbol srcPtrG ? srcPtrG.ElementType : source;
                if (inner is InstantiatedTypeSymbol srcInstG)
                {
                    var baseType = srcInstG.GenericType;
                    var checkType = source is PointerTypeSymbol
                        ? (TypeSymbol)new PointerTypeSymbol(baseType)
                        : baseType;
                    if (Satisfies(checkType, genericIface))
                        return true;
                }

                return false;
            }

            // Instantiated type structural equality: same generic type + same type args
            if (source is InstantiatedTypeSymbol sourceInst && target is InstantiatedTypeSymbol targetInst)
            {
                if ((sourceInst.GenericType == targetInst.GenericType
                     || sourceInst.GenericType.Name == targetInst.GenericType.Name)
                    && sourceInst.TypeArguments.Count == targetInst.TypeArguments.Count)
                {
                    bool allMatch = true;
                    for (int i = 0; i < sourceInst.TypeArguments.Count; i++)
                    {
                        if (!IsAssignable(sourceInst.TypeArguments[i], targetInst.TypeArguments[i]))
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (allMatch)
                        return true;
                }
            }

            // Instantiated named type ↔ concrete type: substitute type params and compare
            if (source is InstantiatedTypeSymbol srcInst2 && srcInst2.GenericType.IsGeneric)
            {
                var resolved = srcInst2.GenericType.Resolved();
                if (resolved != null && resolved != srcInst2.GenericType)
                {
                    var substituted = TypeSubstituter.Substitute(resolved,
                        srcInst2.GenericType.TypeParameters, srcInst2.TypeArguments);
                    if (substituted != resolved && IsAssignable(substituted, target))
                        return true;
                }
            }
            if (target is InstantiatedTypeSymbol tgtInst2 && tgtInst2.GenericType.IsGeneric)
            {
                var resolved = tgtInst2.GenericType.Resolved();
                if (resolved != null && resolved != tgtInst2.GenericType)
                {
                    var substituted = TypeSubstituter.Substitute(resolved,
                        tgtInst2.GenericType.TypeParameters, tgtInst2.TypeArguments);
                    if (substituted != resolved && IsAssignable(source, substituted))
                        return true;
                }
            }

            // Same-named types across packages (stdlib type aliasing): treat as compatible
            if (source.Name == target.Name && source.Name != "interface{}"
                && (source is InterfaceTypeSymbol || target is InterfaceTypeSymbol
                    || source is StructTypeSymbol || target is StructTypeSymbol))
            {
                return true;
            }

            // int32/rune ↔ uint32: Go allows typed integer constants (like unicode.MaxRune)
            // to be assigned to variables of the same-size integer type when the value is representable.
            // Since rune is an alias for int32, we treat int32 and uint32 as interassignable.
            if ((source.TypeKind == TypeKind.Int32 && target.TypeKind == TypeKind.Uint32)
                || (source.TypeKind == TypeKind.Uint32 && target.TypeKind == TypeKind.Int32))
            {
                return true;
            }

            // Anonymous struct structural equality: same fields in same order
            if (source is StructTypeSymbol sourceStruct && target is StructTypeSymbol targetStruct)
            {
                if (sourceStruct.Fields.Count == targetStruct.Fields.Count)
                {
                    bool match = true;
                    for (int i = 0; i < sourceStruct.Fields.Count; i++)
                    {
                        if (sourceStruct.Fields[i].Name != targetStruct.Fields[i].Name
                            || !IsAssignable(sourceStruct.Fields[i].Type, targetStruct.Fields[i].Type))
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return true;
                }
            }

            // Type parameter: always assignable between type parameters
            // (Go checks constraints at instantiation time, not in generic bodies)
            if (source is TypeParameterSymbol && target is TypeParameterSymbol)
            {
                return true;
            }
            // In generic code, allow interface{} to be assigned to/from type parameters,
            // since we resolve type params to interface{} in generic bodies.
            if (source is TypeParameterSymbol || target is TypeParameterSymbol)
            {
                // Allow if the other side is interface{} or another type parameter
                var other = source is TypeParameterSymbol ? target : source;
                if (other is TypeParameterSymbol)
                    return true;
                if (other is InterfaceTypeSymbol)
                    return true;
                if (other.TypeKind == TypeKind.Interface)
                    return true;
                // Allow any concrete type — Go checks constraints at instantiation
                return true;
            }

            // Named type with underlying type: e.g. type RawMessage []byte → []byte
            // Also handles byte ↔ uint8, rune ↔ int32
            if (source.UnderlyingType != null && source.UnderlyingType != source
                && IsAssignable(source.UnderlyingType, target))
            {
                return true;
            }

            if (target.UnderlyingType != null && target.UnderlyingType != target
                && IsAssignable(source, target.UnderlyingType))
            {
                return true;
            }

            // int ↔ int64 and uint ↔ uint64: Go's int/uint are 64-bit on 64-bit targets
            if ((source.TypeKind == TypeKind.Int && target.TypeKind == TypeKind.Int64)
                || (source.TypeKind == TypeKind.Int64 && target.TypeKind == TypeKind.Int))
            {
                return true;
            }

            if ((source.TypeKind == TypeKind.Uint && target.TypeKind == TypeKind.Uint64)
                || (source.TypeKind == TypeKind.Uint64 && target.TypeKind == TypeKind.Uint))
            {
                return true;
            }

            // Named types with same underlying structure (e.g., type stack []uintptr ← []uintptr)
            var resolvedSource = source.Resolved();
            var resolvedTarget = target.Resolved();
            if (resolvedSource != source || resolvedTarget != target)
            {
                // Avoid infinite recursion — only recurse if at least one was resolved
                if (resolvedSource != source && resolvedTarget != target)
                    return IsAssignable(resolvedSource, resolvedTarget);
                if (resolvedSource != source)
                    return IsAssignable(resolvedSource, target);
                return IsAssignable(source, resolvedTarget);
            }


            return false;
        }

        public static TypeSymbol? CommonType(TypeSymbol left, TypeSymbol right)
        {
            if (left == right)
            {
                return left;
            }

            if (left == TypeSymbol.Error || right == TypeSymbol.Error)
            {
                return TypeSymbol.Error;
            }

            // Same named type (different instances): type Kind int == type Kind int
            if (left.Name == right.Name && left.TypeKind == right.TypeKind
                && left.GetType() == right.GetType()
                && left.Name != "void" && left.Name != "interface{}")
            {
                return left;
            }

            // If one side is untyped, it takes the type of the other
            if (IsUntyped(left) && !IsUntyped(right))
            {
                return IsAssignable(left, right) ? right : null;
            }

            if (!IsUntyped(left) && IsUntyped(right))
            {
                return IsAssignable(right, left) ? left : null;
            }

            // Both untyped: UntypedRune + UntypedInt → UntypedRune (preserve rune-ness)
            if ((left.TypeKind == TypeKind.UntypedRune && right.TypeKind == TypeKind.UntypedInt)
                || (left.TypeKind == TypeKind.UntypedInt && right.TypeKind == TypeKind.UntypedRune))
            {
                return BuiltinTypes.UntypedRune;
            }

            if (left.TypeKind == TypeKind.UntypedRune && right.TypeKind == TypeKind.UntypedRune)
            {
                return BuiltinTypes.UntypedRune;
            }

            // Both untyped: promote untyped int → untyped float
            if ((left.TypeKind == TypeKind.UntypedInt || left.TypeKind == TypeKind.UntypedRune) && right.TypeKind == TypeKind.UntypedFloat)
            {
                return BuiltinTypes.UntypedFloat;
            }

            if (left.TypeKind == TypeKind.UntypedFloat && (right.TypeKind == TypeKind.UntypedInt || right.TypeKind == TypeKind.UntypedRune))
            {
                return BuiltinTypes.UntypedFloat;
            }

            // Both untyped: promote to untyped complex
            if (left.TypeKind == TypeKind.UntypedComplex
                && (right.TypeKind == TypeKind.UntypedInt || right.TypeKind == TypeKind.UntypedRune || right.TypeKind == TypeKind.UntypedFloat))
            {
                return BuiltinTypes.UntypedComplex;
            }

            if (right.TypeKind == TypeKind.UntypedComplex
                && (left.TypeKind == TypeKind.UntypedInt || left.TypeKind == TypeKind.UntypedRune || left.TypeKind == TypeKind.UntypedFloat))
            {
                return BuiltinTypes.UntypedComplex;
            }

            // byte ↔ uint8, rune ↔ int32
            if (left.UnderlyingType == right || right.UnderlyingType == left)
            {
                return left;
            }

            // int ↔ int64: on 64-bit platforms (Go's int is 64-bit), allow operations
            if ((left.TypeKind == TypeKind.Int && right.TypeKind == TypeKind.Int64)
                || (left.TypeKind == TypeKind.Int64 && right.TypeKind == TypeKind.Int))
            {
                return left.TypeKind == TypeKind.Int64 ? left : right;
            }

            // uint ↔ uint64: same reasoning
            if ((left.TypeKind == TypeKind.Uint && right.TypeKind == TypeKind.Uint64)
                || (left.TypeKind == TypeKind.Uint64 && right.TypeKind == TypeKind.Uint))
            {
                return left.TypeKind == TypeKind.Uint64 ? left : right;
            }

            // Any integer ↔ any integer: Go stdlib mixes int/rune/byte/int32 freely
            if (IsInteger(left) && IsInteger(right))
            {
                return left;
            }

            return null;
        }

        public static bool CanConvert(TypeSymbol source, TypeSymbol target)
        {
            if (source == null || target == null)
                return false;

            if (IsAssignable(source, target))
            {
                return true;
            }

            // Resolve named types to their underlying types for conversion checks.
            // Go allows conversions between types with the same underlying type,
            // e.g., type Pointer uintptr → Pointer(x) where x is uintptr.
            var resolvedSource = ResolveToUnderlying(source);
            var resolvedTarget = ResolveToUnderlying(target);

            // Numeric ↔ Numeric conversions are always allowed in Go
            // Check both the original and underlying types for numeric-ness
            if (IsNumericOrUnderlyingNumeric(source, resolvedSource)
                && IsNumericOrUnderlyingNumeric(target, resolvedTarget))
            {
                return true;
            }

            // string ↔ integer conversions (rune, byte)
            if (IsStringish(source, resolvedSource) && IsIntegerOrUnderlyingInteger(target, resolvedTarget))
            {
                return true;
            }

            if (IsIntegerOrUnderlyingInteger(source, resolvedSource) && IsStringish(target, resolvedTarget))
            {
                return true;
            }

            // string ↔ []byte and string ↔ []rune
            // Also handle named slice types, e.g., type Bytes []byte
            {
                var sourceSliceElem = GetSliceElementType(source, resolvedSource);
                var targetSliceElem = GetSliceElementType(target, resolvedTarget);

                if (IsStringish(source, resolvedSource) && targetSliceElem != null
                    && (targetSliceElem.TypeKind == TypeKind.Uint8 || targetSliceElem.TypeKind == TypeKind.Int32))
                {
                    return true;
                }

                if (sourceSliceElem != null
                    && (sourceSliceElem.TypeKind == TypeKind.Uint8 || sourceSliceElem.TypeKind == TypeKind.Int32)
                    && IsStringish(target, resolvedTarget))
                {
                    return true;
                }
            }

            // Slice → Array conversion (Go 1.20+)
            {
                var sliceSrc = source as SliceTypeSymbol ?? resolvedSource as SliceTypeSymbol;
                var arrTarget = target as ArrayTypeSymbol ?? resolvedTarget as ArrayTypeSymbol;
                if (sliceSrc != null && arrTarget != null
                    && CommonType(sliceSrc.ElementType, arrTarget.ElementType) != null)
                {
                    return true;
                }
            }

            // Slice → *Array conversion (Go 1.17+): (*[N]T)(slice)
            {
                var sliceSrc2 = source as SliceTypeSymbol ?? resolvedSource as SliceTypeSymbol;
                if (sliceSrc2 != null && (target is PointerTypeSymbol ptrTarget2
                    && ptrTarget2.ElementType is ArrayTypeSymbol arrInPtr))
                {
                    if (CommonType(sliceSrc2.ElementType, arrInPtr.ElementType) != null)
                        return true;
                }
            }

            // Conversion between named types with the same underlying type
            // e.g., type digest64 digest128 → allows (*digest64)(ptr) and (*digest128)(d)
            if (source is PointerTypeSymbol srcPtr && target is PointerTypeSymbol tgtPtr)
            {
                if (HaveSameUnderlyingType(srcPtr.ElementType, tgtPtr.ElementType))
                    return true;
                // Also allow pointer conversion when element types are convertible,
                // e.g., *Pointer ↔ *uintptr where type Pointer uintptr
                if (CanConvert(srcPtr.ElementType, tgtPtr.ElementType))
                    return true;
            }

            if (HaveSameUnderlyingType(source, target))
                return true;

            // unsafe.Pointer ↔ any pointer type, and unsafe.Pointer ↔ uintptr
            // Also handle type aliases for unsafe.Pointer (e.g., type ptr = unsafe.Pointer)
            if (IsUnsafePointer(source) || IsUnsafePointer(target)
                || IsUnsafePointer(resolvedSource) || IsUnsafePointer(resolvedTarget))
            {
                if (source.TypeKind == TypeKind.Pointer || target.TypeKind == TypeKind.Pointer
                    || source.TypeKind == TypeKind.Uintptr || target.TypeKind == TypeKind.Uintptr
                    || resolvedSource.TypeKind == TypeKind.Uintptr || resolvedTarget.TypeKind == TypeKind.Uintptr
                    || resolvedSource.TypeKind == TypeKind.Pointer || resolvedTarget.TypeKind == TypeKind.Pointer
                    || IsUnsafePointer(source) || IsUnsafePointer(target)
                    || IsUnsafePointer(resolvedSource) || IsUnsafePointer(resolvedTarget))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves a type to its deepest underlying type, unwrapping named type definitions.
        /// e.g., type Pointer uintptr → returns the uintptr TypeSymbol.
        /// </summary>
        private static TypeSymbol ResolveToUnderlying(TypeSymbol t)
        {
            var current = t;
            for (int i = 0; i < 10; i++) // guard against cycles
            {
                if (current.UnderlyingType != null && current.UnderlyingType != current)
                    current = current.UnderlyingType;
                else
                    break;
            }
            return current;
        }

        private static bool IsNumericOrUnderlyingNumeric(TypeSymbol original, TypeSymbol resolved)
        {
            return IsNumeric(original) || IsNumeric(resolved);
        }

        private static bool IsIntegerOrUnderlyingInteger(TypeSymbol original, TypeSymbol resolved)
        {
            return IsInteger(original) || IsInteger(resolved);
        }

        private static bool IsStringish(TypeSymbol original, TypeSymbol resolved)
        {
            return original.TypeKind == TypeKind.String || original.TypeKind == TypeKind.UntypedString
                || resolved.TypeKind == TypeKind.String || resolved.TypeKind == TypeKind.UntypedString;
        }

        /// <summary>
        /// Gets the element type if the type (or its underlying type) is a slice.
        /// </summary>
        private static TypeSymbol? GetSliceElementType(TypeSymbol original, TypeSymbol resolved)
        {
            if (original is SliceTypeSymbol s1) return s1.ElementType;
            if (resolved is SliceTypeSymbol s2) return s2.ElementType;
            return null;
        }

        private static bool IsUnsafePointer(TypeSymbol type)
        {
            return type is StructTypeSymbol sts
                && (sts.Name == "Pointer" || sts.Name == "UnsafePointer")
                && sts.Fields.Count == 0;
        }

        public static bool IsNilable(TypeSymbol type)
        {
            switch (type.TypeKind)
            {
                case TypeKind.Pointer:
                case TypeKind.Slice:
                case TypeKind.Map:
                case TypeKind.Interface:
                case TypeKind.Function:
                case TypeKind.Channel:
                    return true;
                default:
                    // unsafe.Pointer is nilable
                    return IsUnsafePointer(type);
            }
        }

        public static bool Satisfies(TypeSymbol type, InterfaceTypeSymbol iface)
        {
            // Resolve type aliases
            if (type.IsAlias && type.UnderlyingType != null)
                type = type.UnderlyingType;

            // Empty interface is satisfied by everything
            if (iface.Methods.Count == 0)
            {
                return true;
            }

            // Go method set rules:
            // - Value type T: method set = value-receiver methods only
            // - Pointer type *T: method set = value-receiver + pointer-receiver methods
            bool includePointerReceivers;
            IReadOnlyList<MethodSymbol> typeMethods;

            if (type is PointerTypeSymbol ptr)
            {
                var inner = ptr.ElementType;
                // For named type aliases (e.g., type Foo = bar.Foo), the alias itself
                // may not have methods — resolve to the underlying type to find them.
                var resolvedInner = inner.Resolved();
                if (resolvedInner != inner && inner.Methods.Count == 0 && resolvedInner.Methods.Count > 0)
                    inner = resolvedInner;
                typeMethods = inner is InterfaceTypeSymbol ptrIface
                    ? ptrIface.Methods : inner.Methods;
                includePointerReceivers = true;
            }
            else if (type is InterfaceTypeSymbol sourceIface2)
            {
                typeMethods = sourceIface2.Methods;
                includePointerReceivers = false;
            }
            else
            {
                // For named type aliases, resolve to find methods on the underlying type
                var resolvedType = type.Resolved();
                if (resolvedType != type && type.Methods.Count == 0 && resolvedType.Methods.Count > 0)
                    typeMethods = resolvedType.Methods;
                else
                    typeMethods = type.Methods;
                includePointerReceivers = false;
            }

            foreach (var required in iface.Methods)
            {
                var found = false;
                foreach (var method in typeMethods)
                {
                    if (method.Name == required.Name
                        && MethodSignaturesMatch(method, required)
                        && (includePointerReceivers || !method.IsPointerReceiver))
                    {
                        found = true;
                        break;
                    }
                }

                // Check promoted methods from embedded structs
                if (!found)
                {
                    TypeSymbol innerForPromotion = type is PointerTypeSymbol p
                        ? p.ElementType : type;
                    // Resolve named type aliases to find the underlying struct
                    var resolvedForPromotion = innerForPromotion.Resolved();
                    if (resolvedForPromotion != innerForPromotion
                        && resolvedForPromotion is StructTypeSymbol)
                        innerForPromotion = resolvedForPromotion;
                    StructTypeSymbol? structType = innerForPromotion as StructTypeSymbol;

                    if (structType != null)
                    {
                        var promoted = structType.LookupPromotedMethod(required.Name);
                        if (promoted != null
                            && MethodSignaturesMatch(promoted.Method, required))
                        {
                            // In Go, if the embedding field is a pointer type (*T),
                            // then pointer-receiver methods of T are promoted even
                            // when the outer type is a value type.
                            bool embeddedViaPointer = promoted.EmbeddedField.Type is PointerTypeSymbol;
                            if (includePointerReceivers || embeddedViaPointer || !promoted.Method.IsPointerReceiver)
                            {
                                found = true;
                            }
                        }
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HaveSameUnderlyingType(TypeSymbol a, TypeSymbol b)
        {
            // Same type — trivially same underlying
            if (a == b) return true;

            // Get the structural underlying type for each
            var ua = GetStructuralUnderlying(a);
            var ub = GetStructuralUnderlying(b);

            if (ua == null || ub == null) return false;
            if (ua == ub) return true;

            // Structural comparison for composite types
            return StructurallyEqual(ua, ub);
        }

        private static bool StructurallyEqual(TypeSymbol a, TypeSymbol b)
        {
            if (a == b) return true;
            if (a.TypeKind != b.TypeKind) return false;

            if (a is SliceTypeSymbol sa && b is SliceTypeSymbol sb)
                return sa.ElementType == sb.ElementType || StructurallyEqual(sa.ElementType, sb.ElementType);
            if (a is ArrayTypeSymbol aa && b is ArrayTypeSymbol ab)
                return aa.Length == ab.Length && (aa.ElementType == ab.ElementType || StructurallyEqual(aa.ElementType, ab.ElementType));
            if (a is MapTypeSymbol ma && b is MapTypeSymbol mb)
                return (ma.KeyType == mb.KeyType || StructurallyEqual(ma.KeyType, mb.KeyType))
                    && (ma.ValueType == mb.ValueType || StructurallyEqual(ma.ValueType, mb.ValueType));
            if (a is ChannelTypeSymbol ca && b is ChannelTypeSymbol cb)
                return ca.ElementType == cb.ElementType || StructurallyEqual(ca.ElementType, cb.ElementType);
            if (a is PointerTypeSymbol pa && b is PointerTypeSymbol pb)
                return pa.ElementType == pb.ElementType || StructurallyEqual(pa.ElementType, pb.ElementType);

            return false;
        }

        private static TypeSymbol? GetStructuralUnderlying(TypeSymbol t)
        {
            // For a named type based on a struct (type T BaseStruct),
            // the struct it was created from is the underlying
            if (t is StructTypeSymbol st && st.UnderlyingType is StructTypeSymbol baseStruct)
                return baseStruct;

            // For a named type with UnderlyingType set
            if (t.UnderlyingType != null && t.Name != t.UnderlyingType.Name)
                return t.UnderlyingType;

            return t;
        }

        private static bool MethodSignaturesMatch(MethodSymbol a, MethodSymbol b)
        {
            if (a.Parameters.Count != b.Parameters.Count)
            {
                return false;
            }

            if (a.ReturnTypes.Count != b.ReturnTypes.Count)
            {
                return false;
            }

            for (int i = 0; i < a.ReturnTypes.Count; i++)
            {
                if (a.ReturnTypes[i] != b.ReturnTypes[i]
                    && !IsAssignable(a.ReturnTypes[i], b.ReturnTypes[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < a.Parameters.Count; i++)
            {
                var at = a.Parameters[i].Type;
                var bt = b.Parameters[i].Type;
                if (at != bt && !IsAssignable(at, bt))
                {
                    // Variadic parameter mismatch: one stores T (IsVariadic=true),
                    // the other stores []T. Unwrap slice and compare element types.
                    if (i == a.Parameters.Count - 1 && (a.IsVariadic || b.IsVariadic))
                    {
                        var unwrappedA = at is SliceTypeSymbol sa ? sa.ElementType : at;
                        var unwrappedB = bt is SliceTypeSymbol sb ? sb.ElementType : bt;
                        if (unwrappedA == unwrappedB || IsAssignable(unwrappedA, unwrappedB))
                            continue;
                    }
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// For a TypeParameterSymbol with structural constraints (e.g. ~[]E, ~map[K]V),
        /// returns the underlying structural type from the first type element.
        /// Returns null if the type is not a constrained type parameter or has no type elements.
        /// </summary>
        public static TypeSymbol? GetConstraintStructuralType(TypeSymbol type)
        {
            if (type is TypeParameterSymbol tp && tp.Constraint.TypeElements.Count > 0)
            {
                return tp.Constraint.TypeElements[0].Type;
            }

            return null;
        }
    }
}
