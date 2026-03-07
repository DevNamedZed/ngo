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
            if (source == target)
            {
                return true;
            }

            if (source == TypeSymbol.Error || target == TypeSymbol.Error)
            {
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

            if (source.TypeKind == TypeKind.UntypedString && target.TypeKind == TypeKind.String)
            {
                return true;
            }

            // nil is assignable to pointer, slice, map, interface
            if (source.TypeKind == TypeKind.UntypedNil && IsNilable(target))
            {
                return true;
            }

            // Pointer identity: same element type
            if (source is PointerTypeSymbol sourcePtr && target is PointerTypeSymbol targetPtr)
            {
                return IsAssignable(sourcePtr.ElementType, targetPtr.ElementType);
            }

            // Slice structural equality: same element type
            if (source is SliceTypeSymbol sourceSlice && target is SliceTypeSymbol targetSlice)
            {
                return IsAssignable(sourceSlice.ElementType, targetSlice.ElementType);
            }

            // Array structural equality: same element type and length
            if (source is ArrayTypeSymbol sourceArray && target is ArrayTypeSymbol targetArray)
            {
                return sourceArray.Length == targetArray.Length
                    && IsAssignable(sourceArray.ElementType, targetArray.ElementType);
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

            // Instantiated type structural equality: same generic type + same type args
            if (source is InstantiatedTypeSymbol sourceInst && target is InstantiatedTypeSymbol targetInst)
            {
                if (sourceInst.GenericType == targetInst.GenericType
                    && sourceInst.TypeArguments.Count == targetInst.TypeArguments.Count)
                {
                    for (int i = 0; i < sourceInst.TypeArguments.Count; i++)
                    {
                        if (!IsAssignable(sourceInst.TypeArguments[i], targetInst.TypeArguments[i]))
                        {
                            return false;
                        }
                    }

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

            // Type parameter: assignable if same name and ordinal
            if (source is TypeParameterSymbol srcTp && target is TypeParameterSymbol tgtTp)
            {
                return srcTp.Name == tgtTp.Name && srcTp.Ordinal == tgtTp.Ordinal;
            }
            if (source is TypeParameterSymbol || target is TypeParameterSymbol)
            {
                return false;
            }

            // byte ↔ uint8, rune ↔ int32
            if (source.UnderlyingType != null && source.UnderlyingType == target)
            {
                return true;
            }

            if (target.UnderlyingType != null && target.UnderlyingType == source)
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

            return null;
        }

        public static bool CanConvert(TypeSymbol source, TypeSymbol target)
        {
            if (IsAssignable(source, target))
            {
                return true;
            }

            // Numeric ↔ Numeric conversions are always allowed in Go
            if (IsNumeric(source) && IsNumeric(target))
            {
                return true;
            }

            // string ↔ integer conversions (rune, byte)
            if (source.TypeKind == TypeKind.String && IsInteger(target))
            {
                return true;
            }

            if (IsInteger(source) && target.TypeKind == TypeKind.String)
            {
                return true;
            }

            // string ↔ []byte and string ↔ []rune
            if ((source.TypeKind == TypeKind.String || source.TypeKind == TypeKind.UntypedString)
                && target is SliceTypeSymbol targetSlice
                && (targetSlice.ElementType.TypeKind == TypeKind.Uint8
                    || targetSlice.ElementType.TypeKind == TypeKind.Int32))
            {
                return true;
            }

            if (source is SliceTypeSymbol sourceSlice
                && (sourceSlice.ElementType.TypeKind == TypeKind.Uint8
                    || sourceSlice.ElementType.TypeKind == TypeKind.Int32)
                && target.TypeKind == TypeKind.String)
            {
                return true;
            }

            // Slice → Array conversion (Go 1.20+)
            if (source is SliceTypeSymbol sliceSrc && target is ArrayTypeSymbol arrTarget
                && CommonType(sliceSrc.ElementType, arrTarget.ElementType) != null)
            {
                return true;
            }

            // Conversion between named types with the same underlying type
            // e.g., type digest64 digest128 → allows (*digest64)(ptr) and (*digest128)(d)
            if (source is PointerTypeSymbol srcPtr && target is PointerTypeSymbol tgtPtr)
            {
                if (HaveSameUnderlyingType(srcPtr.ElementType, tgtPtr.ElementType))
                    return true;
            }

            if (HaveSameUnderlyingType(source, target))
                return true;

            // unsafe.Pointer ↔ any pointer type, and unsafe.Pointer ↔ uintptr
            if (IsUnsafePointer(source) || IsUnsafePointer(target))
            {
                if (source.TypeKind == TypeKind.Pointer || target.TypeKind == TypeKind.Pointer
                    || source.TypeKind == TypeKind.Uintptr || target.TypeKind == TypeKind.Uintptr
                    || IsUnsafePointer(source) || IsUnsafePointer(target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnsafePointer(TypeSymbol type)
        {
            return type is StructTypeSymbol sts && sts.Name == "Pointer"
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
                    return false;
            }
        }

        public static bool Satisfies(TypeSymbol type, InterfaceTypeSymbol iface)
        {
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
                    StructTypeSymbol? structType = type is PointerTypeSymbol p
                        ? p.ElementType as StructTypeSymbol
                        : type as StructTypeSymbol;

                    if (structType != null)
                    {
                        var promoted = structType.LookupPromotedMethod(required.Name);
                        if (promoted != null
                            && MethodSignaturesMatch(promoted.Value.method, required)
                            && (includePointerReceivers || !promoted.Value.method.IsPointerReceiver))
                        {
                            found = true;
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
                if (a.Parameters[i].Type != b.Parameters[i].Type
                    && !IsAssignable(a.Parameters[i].Type, b.Parameters[i].Type))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
