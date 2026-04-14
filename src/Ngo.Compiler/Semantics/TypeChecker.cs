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

using System;
using System.Collections.Generic;
using Ngo.Compiler.Cgo;
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
            return type.TypeKind is TypeKind.Complex64 or TypeKind.Complex128
                or TypeKind.UntypedComplex;
        }

        public static bool IsInteger(TypeSymbol type)
        {
            return type.TypeKind switch
            {
                TypeKind.Int or TypeKind.Int8 or TypeKind.Int16 or TypeKind.Int32 or TypeKind.Int64 => true,
                TypeKind.Uint or TypeKind.Uint8 or TypeKind.Uint16 or TypeKind.Uint32 or TypeKind.Uint64 => true,
                TypeKind.Uintptr => true,
                TypeKind.UntypedInt or TypeKind.UntypedRune => true,
                // Named types with integer underlying (type MyInt int, type Errno int32, etc.)
                _ when type.UnderlyingType != null && type.UnderlyingType != type => IsInteger(type.UnderlyingType),
                _ => false,
            };
        }

        public static bool IsFloat(TypeSymbol type)
        {
            return type.TypeKind is TypeKind.Float32 or TypeKind.Float64
                or TypeKind.UntypedFloat;
        }

        public static bool IsUntyped(TypeSymbol type)
        {
            return type.TypeKind switch
            {
                TypeKind.UntypedBool or TypeKind.UntypedInt or TypeKind.UntypedRune
                    or TypeKind.UntypedFloat or TypeKind.UntypedComplex
                    or TypeKind.UntypedString or TypeKind.UntypedNil => true,
                _ => false,
            };
        }

        public static TypeSymbol DefaultType(TypeSymbol type)
        {
            return type.TypeKind switch
            {
                TypeKind.UntypedBool => BuiltinTypes.Bool,
                TypeKind.UntypedInt => BuiltinTypes.Int,
                TypeKind.UntypedRune => BuiltinTypes.Int32,
                TypeKind.UntypedFloat => BuiltinTypes.Float64,
                TypeKind.UntypedComplex => BuiltinTypes.Complex128,
                TypeKind.UntypedString => BuiltinTypes.String,
                TypeKind.UntypedNil => BuiltinTypes.EmptyInterface,
                _ => type,
            };
        }

        // ---- IsAssignable: Go type assignability rules ----

        public static bool IsAssignable(TypeSymbol source, TypeSymbol target)
        {
            if (source == null || target == null) return false;

            // Resolve type aliases
            if (source.IsAlias && source.UnderlyingType != null) source = source.UnderlyingType;
            if (target.IsAlias && target.UnderlyingType != null) target = target.UnderlyingType;

            // Identity
            if (source == target) return true;

            // Error types — always assignable to prevent cascading errors
            if (IsErrorType(source) || IsErrorType(target)) return true;

            // Same-named types (different instances)
            if (SameNamedType(source, target)) return true;

            // Type parameters — always assignable (constraints checked at instantiation)
            if (IsTypeParameterAssignable(source, target)) return true;

            // Untyped constants → typed values
            if (IsUntypedAssignable(source, target)) return true;

            // Integer cross-assignment (int ↔ int32, byte ↔ uint8, etc.)
            if (IsInteger(source) && IsInteger(target)) return true;

            // Float cross-assignment (float32 ↔ float64)
            if (IsFloat(source) && IsFloat(target)) return true;

            // Complex cross-assignment (complex64 ↔ complex128)
            if (IsComplex(source) && IsComplex(target)) return true;

            // string → error (our runtime represents errors as strings)
            if (IsStringToError(source, target)) return true;

            // nil → nilable types
            if (source.TypeKind == TypeKind.UntypedNil && (IsNilable(target) || target == BuiltinTypes.Void))
                return true;

            // Empty interface{} → any type (implicit type assertion)
            if (source is InterfaceTypeSymbol srcEmpty && srcEmpty.Methods.Count == 0
                && source.TypeKind == TypeKind.Interface)
                return true;

            // unsafe.Pointer ↔ any pointer
            if (IsUnsafePointerAssignable(source, target)) return true;

            // C function pointer ↔ unsafe.Pointer ↔ *[0]byte ↔ C function pointer (cgo idiom)
            if (IsCFunctionPointerInterchangeable(source, target)) return true;

            // Direct function type assignability (before structural to avoid short-circuit issues)
            if (source is FunctionTypeSymbol && target is FunctionTypeSymbol)
            {
                if (IsFunctionTypeAssignable(source, target)) return true;
            }

            // Function type → named type with function underlying (e.g. func(...) → iter.Seq2[...])
            if (source is FunctionTypeSymbol && !(target is FunctionTypeSymbol)
                && HasFunctionUnderlying(target))
            {
                if (IsFunctionTypeAssignable(source, target)) return true;
            }

            // Structural type equality
            if (IsStructurallyAssignable(source, target)) return true;

            // Interface satisfaction
            if (IsInterfaceAssignable(source, target)) return true;

            // Generic type equality
            if (IsGenericAssignable(source, target)) return true;

            // Same-named struct/interface types across packages
            if (IsSameNamedCompositeType(source, target)) return true;

            // int32 ↔ uint32 (rune interop)
            if (IsSameSizeIntegerSwap(source, target)) return true;

            // Anonymous struct structural equality
            if (IsStructFieldsEqual(source, target)) return true;

            // Named type with underlying type (type RawMessage []byte → []byte)
            if (IsUnderlyingAssignable(source, target)) return true;

            // int ↔ int64, uint ↔ uint64 (64-bit platform)
            if (IsIntSizeEquivalent(source, target)) return true;

            // Resolved named types
            if (IsResolvedAssignable(source, target)) return true;

            return false;
        }

        // ---- IsAssignable helper methods ----

        private static bool IsErrorType(TypeSymbol type)
        {
            if (type == TypeSymbol.Error) return true;
            if (type.TypeKind == TypeKind.Error) return true;
            if (type is PointerTypeSymbol ptr && ptr.ElementType.TypeKind == TypeKind.Error) return true;
            return false;
        }

        private static bool SameNamedType(TypeSymbol source, TypeSymbol target)
        {
            if (source.Name == target.Name && source.Name != "interface{}" && source.Name != "void"
                && source.GetType() == target.GetType())
                return true;

            // Qualified vs unqualified: "io.Reader" == "Reader"
            if (source.Name != null && target.Name != null
                && source.Name != "interface{}" && target.Name != "interface{}")
            {
                var srcBase = source.Name.Contains('.') ? source.Name.Substring(source.Name.LastIndexOf('.') + 1) : source.Name;
                var tgtBase = target.Name.Contains('.') ? target.Name.Substring(target.Name.LastIndexOf('.') + 1) : target.Name;
                if (srcBase == tgtBase && srcBase != "") return true;
            }
            return false;
        }

        private static bool IsTypeParameterAssignable(TypeSymbol source, TypeSymbol target)
        {
            if (target is TypeParameterSymbol || source is TypeParameterSymbol) return true;

            // Pointer-to-type-parameter (*T)
            if ((target is PointerTypeSymbol tp && tp.ElementType is TypeParameterSymbol)
                || (source is PointerTypeSymbol sp && sp.ElementType is TypeParameterSymbol))
                return true;

            // Named *T (from generic resolution)
            if ((target.Name.StartsWith("*") && target.Name.Length <= 3)
                || (source.Name.StartsWith("*") && source.Name.Length <= 3))
                return true;

            return false;
        }

        private static bool IsUntypedAssignable(TypeSymbol source, TypeSymbol target)
        {
            var sk = source.TypeKind;

            if ((sk == TypeKind.UntypedInt || sk == TypeKind.UntypedRune)
                && (IsInteger(target) || IsFloat(target) || IsComplex(target)))
                return true;

            if (sk == TypeKind.UntypedFloat && (IsFloat(target) || IsInteger(target) || IsComplex(target)))
                return true;

            if (sk == TypeKind.UntypedComplex && IsComplex(target))
                return true;

            if (sk == TypeKind.UntypedBool && target.TypeKind == TypeKind.Bool)
                return true;

            if (sk == TypeKind.UntypedString && target.TypeKind == TypeKind.String)
                return true;

            return false;
        }

        private static bool IsStringToError(TypeSymbol source, TypeSymbol target)
        {
            return (source.TypeKind == TypeKind.String || source.TypeKind == TypeKind.UntypedString)
                && target is InterfaceTypeSymbol errIface && errIface.Name == "error";
        }

        private static bool IsUnsafePointerAssignable(TypeSymbol source, TypeSymbol target)
        {
            if (IsUnsafePointer(source)
                && (target.TypeKind == TypeKind.Pointer || IsUnsafePointer(target)
                    || target.TypeKind == TypeKind.Uintptr || IsInteger(target)))
            {
                return true;
            }
            if (IsUnsafePointer(target)
                && (source.TypeKind == TypeKind.Pointer || IsUnsafePointer(source)
                    || source.TypeKind == TypeKind.Uintptr || IsInteger(source)))
            {
                return true;
            }
            return false;
        }

        private static bool IsStructurallyAssignable(TypeSymbol source, TypeSymbol target)
        {
            // Pointer identity
            if (source is PointerTypeSymbol sp2 && target is PointerTypeSymbol tp2)
                return IsAssignable(sp2.ElementType, tp2.ElementType);

            // Struct ↔ *Struct (reference-backed types)
            if (target is PointerTypeSymbol tgtPtr && !(source is PointerTypeSymbol))
                if (IsAssignable(source, tgtPtr.ElementType)) return true;
            if (source is PointerTypeSymbol srcPtr && !(target is PointerTypeSymbol))
                if (IsAssignable(srcPtr.ElementType, target)) return true;

            // Slice
            if (source is SliceTypeSymbol ss && target is SliceTypeSymbol ts)
                return IsAssignable(ss.ElementType, ts.ElementType);

            // Array (including named types that resolve to arrays)
            var srcArr = source as ArrayTypeSymbol ?? ResolveToUnderlying(source) as ArrayTypeSymbol;
            var tgtArr = target as ArrayTypeSymbol ?? ResolveToUnderlying(target) as ArrayTypeSymbol;
            if (srcArr != null && tgtArr != null)
                return srcArr.Length == tgtArr.Length && IsAssignable(srcArr.ElementType, tgtArr.ElementType);

            // Map
            if (source is MapTypeSymbol sm && target is MapTypeSymbol tm)
                return IsAssignable(sm.KeyType, tm.KeyType) && IsAssignable(sm.ValueType, tm.ValueType);

            // Channel
            if (source is ChannelTypeSymbol sc && target is ChannelTypeSymbol tc)
                return IsAssignable(sc.ElementType, tc.ElementType);

            // Function type
            return IsFunctionTypeAssignable(source, target);
        }

        private static bool IsFunctionTypeAssignable(TypeSymbol source, TypeSymbol target)
        {
            var sf = (source as FunctionTypeSymbol)
                ?? (source.Resolved() as FunctionTypeSymbol)
                ?? (source.UnderlyingType as FunctionTypeSymbol)
                ?? (source.Resolved()?.UnderlyingType as FunctionTypeSymbol);
            var tf = (target as FunctionTypeSymbol)
                ?? (target.Resolved() as FunctionTypeSymbol)
                ?? (target.UnderlyingType as FunctionTypeSymbol)
                ?? (target.Resolved()?.UnderlyingType as FunctionTypeSymbol);

            // For instantiated generic named types (e.g. Seq2[int, *Selection]),
            // substitute type arguments into the underlying function type
            if (tf != null && target is InstantiatedTypeSymbol targetInst
                && targetInst.GenericType.TypeParameters.Count > 0
                && targetInst.TypeArguments.Count == targetInst.GenericType.TypeParameters.Count)
            {
                var substParams = new List<TypeSymbol>();
                foreach (var paramType in tf.ParameterTypes)
                {
                    substParams.Add(TypeSubstituter.Substitute(paramType,
                        targetInst.GenericType.TypeParameters, targetInst.TypeArguments));
                }
                var substReturns = new List<TypeSymbol>();
                foreach (var retType in tf.ReturnTypes)
                {
                    substReturns.Add(TypeSubstituter.Substitute(retType,
                        targetInst.GenericType.TypeParameters, targetInst.TypeArguments));
                }
                tf = new FunctionTypeSymbol(substParams, substReturns, tf.IsVariadic);
            }

            if (sf == null || tf == null) return false;

            int srcCount = sf.ParameterTypes.Count;
            int tgtCount = tf.ParameterTypes.Count;

            if (srcCount != tgtCount)
            {
                if (sf.IsVariadic && tf.IsVariadic && Math.Abs(srcCount - tgtCount) == 1)
                {
                    int minCount = Math.Min(srcCount, tgtCount);
                    for (int i = 0; i < minCount; i++)
                    {
                        if (!IsAssignable(sf.ParameterTypes[i], tf.ParameterTypes[i])
                            && !IsSameTypeByNameAndPackage(sf.ParameterTypes[i], tf.ParameterTypes[i]))
                        {
                            return false;
                        }
                    }
                }
                else return false;
            }
            else
            {
                for (int i = 0; i < srcCount; i++)
                {
                    if (!IsAssignable(sf.ParameterTypes[i], tf.ParameterTypes[i])
                        && !IsSameTypeByNameAndPackage(sf.ParameterTypes[i], tf.ParameterTypes[i]))
                    {
                        return false;
                    }
                }
            }

            if (sf.ReturnTypes.Count != tf.ReturnTypes.Count) return false;
            for (int i = 0; i < sf.ReturnTypes.Count; i++)
            {
                if (!IsAssignable(sf.ReturnTypes[i], tf.ReturnTypes[i])
                    && !IsSameTypeByNameAndPackage(sf.ReturnTypes[i], tf.ReturnTypes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsInterfaceAssignable(TypeSymbol source, TypeSymbol target)
        {
            if (target is InterfaceTypeSymbol targetIface)
            {
                if (source is InterfaceTypeSymbol si && si.Methods.Count == 0) return true;
                if (source is InterfaceTypeSymbol si2 && si2.Name == targetIface.Name
                    && si2.Name != "interface{}") return true;
                return Satisfies(source, targetIface);
            }

            if (target is InstantiatedTypeSymbol tgtInst && tgtInst.GenericType is InterfaceTypeSymbol genIface)
            {
                if (genIface.Methods.Count == 0) return true;

                // Create a substituted interface with type args applied to methods
                if (genIface.TypeParameters.Count > 0 && tgtInst.TypeArguments.Count > 0)
                {
                    var substMethods = new List<MethodSymbol>();
                    foreach (var method in genIface.Methods)
                    {
                        var substParams = TypeSubstituter.SubstituteParams(
                            method.Parameters, genIface.TypeParameters, tgtInst.TypeArguments);
                        var substReturns = TypeSubstituter.SubstituteTypes(
                            method.ReturnTypes, genIface.TypeParameters, tgtInst.TypeArguments);
                        substMethods.Add(new MethodSymbol(method.Name, method.ReceiverType, method.IsPointerReceiver,
                            method.TypeParameters, substParams, substReturns, method.IsVariadic));
                    }
                    var substIface = new InterfaceTypeSymbol(tgtInst.Name, substMethods);
                    if (Satisfies(source, substIface)) return true;
                }

                if (Satisfies(source, genIface)) return true;

                var inner = source is PointerTypeSymbol srcPtrG ? srcPtrG.ElementType : source;
                // Unwrap type aliases to find the instantiated type
                if (inner.IsAlias && inner.UnderlyingType != null)
                {
                    inner = inner.UnderlyingType;
                }
                if (inner is InstantiatedTypeSymbol srcInstG)
                {
                    var baseType = srcInstG.GenericType;
                    // Check with substituted methods from the instantiated source
                    if (baseType.TypeParameters.Count > 0 && srcInstG.TypeArguments.Count > 0)
                    {
                        var srcSubstMethods = new List<MethodSymbol>();
                        foreach (var m in baseType.Methods)
                        {
                            var sp = TypeSubstituter.SubstituteParams(m.Parameters, baseType.TypeParameters, srcInstG.TypeArguments);
                            var sr = TypeSubstituter.SubstituteTypes(m.ReturnTypes, baseType.TypeParameters, srcInstG.TypeArguments);
                            srcSubstMethods.Add(new MethodSymbol(m.Name, m.ReceiverType, m.IsPointerReceiver,
                                m.TypeParameters, sp, sr, m.IsVariadic));
                        }
                        var srcSubstType = new InterfaceTypeSymbol("$$substCheck", srcSubstMethods);
                        if (Satisfies(new PointerTypeSymbol(srcSubstType), genIface)) return true;
                    }

                    var checkType = source is PointerTypeSymbol
                        ? (TypeSymbol)new PointerTypeSymbol(baseType) : baseType;
                    if (Satisfies(checkType, genIface)) return true;
                }
                return false;
            }

            return false;
        }

        private static bool IsGenericAssignable(TypeSymbol source, TypeSymbol target)
        {
            if (source is InstantiatedTypeSymbol si && target is InstantiatedTypeSymbol ti)
            {
                if ((si.GenericType == ti.GenericType || si.GenericType.Name == ti.GenericType.Name)
                    && si.TypeArguments.Count == ti.TypeArguments.Count)
                {
                    for (int i = 0; i < si.TypeArguments.Count; i++)
                        if (!IsAssignable(si.TypeArguments[i], ti.TypeArguments[i])) return false;
                    return true;
                }
            }

            // Instantiated → concrete via substitution
            if (source is InstantiatedTypeSymbol srcInst && srcInst.GenericType.IsGeneric)
            {
                var resolved = srcInst.GenericType.Resolved();
                if (resolved != null && resolved != srcInst.GenericType)
                {
                    var sub = TypeSubstituter.Substitute(resolved, srcInst.GenericType.TypeParameters, srcInst.TypeArguments);
                    if (sub != resolved && IsAssignable(sub, target)) return true;
                }
            }
            if (target is InstantiatedTypeSymbol tgtInst && tgtInst.GenericType.IsGeneric)
            {
                var resolved = tgtInst.GenericType.Resolved();
                if (resolved != null && resolved != tgtInst.GenericType)
                {
                    var sub = TypeSubstituter.Substitute(resolved, tgtInst.GenericType.TypeParameters, tgtInst.TypeArguments);
                    if (sub != resolved && IsAssignable(source, sub)) return true;
                }
            }

            return false;
        }

        private static bool IsSameNamedCompositeType(TypeSymbol source, TypeSymbol target)
        {
            return source.Name == target.Name && source.Name != "interface{}"
                && (source is InterfaceTypeSymbol || target is InterfaceTypeSymbol
                    || source is StructTypeSymbol || target is StructTypeSymbol);
        }

        private static bool IsSameSizeIntegerSwap(TypeSymbol source, TypeSymbol target)
        {
            return (source.TypeKind == TypeKind.Int32 && target.TypeKind == TypeKind.Uint32)
                || (source.TypeKind == TypeKind.Uint32 && target.TypeKind == TypeKind.Int32);
        }

        private static bool IsStructFieldsEqual(TypeSymbol source, TypeSymbol target)
        {
            if (source is StructTypeSymbol ss && target is StructTypeSymbol ts
                && ss.Fields.Count == ts.Fields.Count)
            {
                for (int i = 0; i < ss.Fields.Count; i++)
                    if (ss.Fields[i].Name != ts.Fields[i].Name
                        || !IsAssignable(ss.Fields[i].Type, ts.Fields[i].Type))
                        return false;
                return true;
            }
            return false;
        }

        private static bool HasFunctionUnderlying(TypeSymbol type)
        {
            if (type.UnderlyingType is FunctionTypeSymbol) return true;
            if (type.Resolved()?.UnderlyingType is FunctionTypeSymbol) return true;
            if (type is InstantiatedTypeSymbol inst)
            {
                var genBase = inst.GenericType;
                if (genBase.UnderlyingType is FunctionTypeSymbol) return true;
                if (genBase.Resolved()?.UnderlyingType is FunctionTypeSymbol) return true;
            }
            return false;
        }

        private static bool IsUnderlyingAssignable(TypeSymbol source, TypeSymbol target)
        {
            if (source.UnderlyingType != null && source.UnderlyingType != source
                && IsAssignable(source.UnderlyingType, target))
                return true;
            if (target.UnderlyingType != null && target.UnderlyingType != target
                && IsAssignable(source, target.UnderlyingType))
                return true;
            return false;
        }

        private static bool IsIntSizeEquivalent(TypeSymbol source, TypeSymbol target)
        {
            return (source.TypeKind == TypeKind.Int && target.TypeKind == TypeKind.Int64)
                || (source.TypeKind == TypeKind.Int64 && target.TypeKind == TypeKind.Int)
                || (source.TypeKind == TypeKind.Uint && target.TypeKind == TypeKind.Uint64)
                || (source.TypeKind == TypeKind.Uint64 && target.TypeKind == TypeKind.Uint);
        }

        private static bool IsResolvedAssignable(TypeSymbol source, TypeSymbol target)
        {
            var rs = source.Resolved();
            var rt = target.Resolved();
            if (rs != source || rt != target)
            {
                if (rs != source && rt != target) return IsAssignable(rs, rt);
                if (rs != source) return IsAssignable(rs, target);
                return IsAssignable(source, rt);
            }
            return false;
        }

        // ---- Other type checking utilities ----
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

            // float32 ↔ float64: Go allows mixed float operations, widening float32 to float64
            if ((left.TypeKind == TypeKind.Float32 && right.TypeKind == TypeKind.Float64)
                || (left.TypeKind == TypeKind.Float64 && right.TypeKind == TypeKind.Float32))
            {
                return left.TypeKind == TypeKind.Float64 ? left : right;
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

            // Conversion to interface is allowed if the source satisfies the interface
            if (target is InterfaceTypeSymbol tgtIface)
            {
                if (tgtIface.Methods.Count == 0 || Satisfies(source, tgtIface))
                {
                    return true;
                }
            }
            if (resolvedTarget is InterfaceTypeSymbol resolvedTgtIface)
            {
                if (resolvedTgtIface.Methods.Count == 0 || Satisfies(source, resolvedTgtIface))
                {
                    return true;
                }
            }

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

        private static bool IsZeroLengthBytePointer(TypeSymbol type)
        {
            if (type is not PointerTypeSymbol ptr)
            {
                return false;
            }
            var element = ptr.ElementType;
            if (element is not ArrayTypeSymbol arr)
            {
                return false;
            }
            if (arr.Length != 0)
            {
                return false;
            }
            return arr.ElementType.TypeKind == TypeKind.Uint8;
        }

        private static bool IsCFunctionPointerInterchangeable(TypeSymbol source, TypeSymbol target)
        {
            bool sourceIsCFunc = source is CFunctionPointerTypeSymbol;
            bool targetIsCFunc = target is CFunctionPointerTypeSymbol;
            if (!sourceIsCFunc && !targetIsCFunc)
            {
                return false;
            }
            if (sourceIsCFunc && targetIsCFunc)
            {
                return true;
            }
            var other = sourceIsCFunc ? target : source;
            if (IsUnsafePointer(other))
            {
                return true;
            }
            if (IsZeroLengthBytePointer(other))
            {
                return true;
            }
            return false;
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

        [System.ThreadStatic] private static HashSet<(string, string)>? _satisfiesGuard;

        public static bool Satisfies(TypeSymbol type, InterfaceTypeSymbol iface)
        {
            // Recursion guard: prevent infinite loops from circular interface references
            // (e.g., type asciiString with method Concat(String) String where String requires Concat)
            _satisfiesGuard ??= new HashSet<(string, string)>();
            var key = (type.Name, iface.Name);
            if (!_satisfiesGuard.Add(key))
            {
                return true;
            }
            try
            {
                return SatisfiesCore(type, iface);
            }
            finally
            {
                _satisfiesGuard.Remove(key);
            }
        }

        private static bool SatisfiesCore(TypeSymbol type, InterfaceTypeSymbol iface)
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
                {
                    inner = resolvedInner;
                }
                // For instantiated generic types, substitute type args in the generic definition's methods
                if (inner is InstantiatedTypeSymbol instInner && inner.Methods.Count == 0)
                {
                    var genBase = instInner.GenericType;
                    if (genBase.TypeParameters.Count > 0 && instInner.TypeArguments.Count > 0)
                    {
                        var substMethods = new List<MethodSymbol>();
                        foreach (var m in genBase.Methods)
                        {
                            var sp = TypeSubstituter.SubstituteParams(m.Parameters, genBase.TypeParameters, instInner.TypeArguments);
                            var sr = TypeSubstituter.SubstituteTypes(m.ReturnTypes, genBase.TypeParameters, instInner.TypeArguments);
                            substMethods.Add(new MethodSymbol(m.Name, m.ReceiverType, m.IsPointerReceiver,
                                m.TypeParameters, sp, sr, m.IsVariadic));
                        }
                        typeMethods = substMethods;
                    }
                    else
                    {
                        typeMethods = genBase.Methods;
                    }
                }
                else
                {
                    typeMethods = inner is InterfaceTypeSymbol ptrIface
                        ? ptrIface.Methods : inner.Methods;
                }
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
                {
                    typeMethods = resolvedType.Methods;
                }
                else if (type is InstantiatedTypeSymbol instType && type.Methods.Count == 0)
                {
                    typeMethods = instType.GenericType.Methods;
                }
                else
                {
                    typeMethods = type.Methods;
                }
                includePointerReceivers = false;
            }

            foreach (var required in iface.Methods)
            {
                // Skip .NET runtime methods that leak into Go interface definitions
                if (IsNetFrameworkMethod(required.Name))
                {
                    continue;
                }

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

        private static bool IsSameTypeByNameAndPackage(TypeSymbol a, TypeSymbol b)
        {
            if (a == null || b == null) return false;
            if (a.Name == b.Name && a.Name != "interface{}" && a.Name != "void")
            {
                if (!string.IsNullOrEmpty(a.PackagePath) && a.PackagePath == b.PackagePath)
                {
                    return true;
                }
                if (string.IsNullOrEmpty(a.PackagePath) || string.IsNullOrEmpty(b.PackagePath))
                {
                    return true;
                }
            }
            return false;
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
                    && !IsAssignable(a.ReturnTypes[i], b.ReturnTypes[i])
                    && !TypeNamesMatch(a.ReturnTypes[i], b.ReturnTypes[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < a.Parameters.Count; i++)
            {
                var at = a.Parameters[i].Type;
                var bt = b.Parameters[i].Type;
                if (at != bt && !IsAssignable(at, bt) && !TypeNamesMatch(at, bt))
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

        private static bool IsNetFrameworkMethod(string name)
        {
            return name == "GetType" || name == "ToString" || name == "Equals"
                || name == "GetHashCode" || name == "MemberwiseClone"
                || name == "Finalize" || name == "ReferenceEquals";
        }

        private static bool TypeNamesMatch(TypeSymbol a, TypeSymbol b)
        {
            // Slice types: compare element names
            if (a is SliceTypeSymbol sa && b is SliceTypeSymbol sb)
            {
                return TypeNamesMatch(sa.ElementType, sb.ElementType);
            }
            // Pointer types: compare element names
            if (a is PointerTypeSymbol pa && b is PointerTypeSymbol pb)
            {
                return TypeNamesMatch(pa.ElementType, pb.ElementType);
            }
            // Named types: same name means same Go type, even if resolved
            // from different sources (Go source vs C# runtime)
            if (a.Name == b.Name && !string.IsNullOrEmpty(a.Name)
                && a.Name != "interface{}" && a.Name != "struct{}")
            {
                return true;
            }
            return false;
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
