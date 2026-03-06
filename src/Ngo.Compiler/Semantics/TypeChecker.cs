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
            if (source.TypeKind == TypeKind.UntypedInt && IsInteger(target))
            {
                return true;
            }

            if (source.TypeKind == TypeKind.UntypedInt && IsFloat(target))
            {
                return true;
            }

            if (source.TypeKind == TypeKind.UntypedFloat && IsFloat(target))
            {
                return true;
            }

            // Untyped int/float/complex are assignable to complex types
            if (source.TypeKind == TypeKind.UntypedInt && IsComplex(target))
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
            if (source is FunctionTypeSymbol sourceFunc && target is FunctionTypeSymbol targetFunc)
            {
                if (sourceFunc.ParameterTypes.Count != targetFunc.ParameterTypes.Count)
                {
                    return false;
                }

                if (sourceFunc.ReturnTypes.Count != targetFunc.ReturnTypes.Count)
                {
                    return false;
                }

                for (int i = 0; i < sourceFunc.ParameterTypes.Count; i++)
                {
                    if (!IsAssignable(sourceFunc.ParameterTypes[i], targetFunc.ParameterTypes[i]))
                    {
                        return false;
                    }
                }

                for (int i = 0; i < sourceFunc.ReturnTypes.Count; i++)
                {
                    if (!IsAssignable(sourceFunc.ReturnTypes[i], targetFunc.ReturnTypes[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            // Interface satisfaction: any type that implements all methods is assignable to an interface
            if (target is InterfaceTypeSymbol targetIface)
            {
                // Empty interface (interface{}) is assignable to any interface —
                // used for stdlib error returns where runtime returns object
                if (source is InterfaceTypeSymbol sourceIface && sourceIface.Methods.Count == 0)
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

            // Type parameter: assignable to itself only (identity checked above)
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

            // Both untyped: promote untyped int → untyped float
            if (left.TypeKind == TypeKind.UntypedInt && right.TypeKind == TypeKind.UntypedFloat)
            {
                return BuiltinTypes.UntypedFloat;
            }

            if (left.TypeKind == TypeKind.UntypedFloat && right.TypeKind == TypeKind.UntypedInt)
            {
                return BuiltinTypes.UntypedFloat;
            }

            // Both untyped: promote to untyped complex
            if (left.TypeKind == TypeKind.UntypedComplex
                && (right.TypeKind == TypeKind.UntypedInt || right.TypeKind == TypeKind.UntypedFloat))
            {
                return BuiltinTypes.UntypedComplex;
            }

            if (right.TypeKind == TypeKind.UntypedComplex
                && (left.TypeKind == TypeKind.UntypedInt || left.TypeKind == TypeKind.UntypedFloat))
            {
                return BuiltinTypes.UntypedComplex;
            }

            // byte ↔ uint8, rune ↔ int32
            if (left.UnderlyingType == right || right.UnderlyingType == left)
            {
                return left;
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
                typeMethods = ptr.ElementType.Methods;
                includePointerReceivers = true;
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
