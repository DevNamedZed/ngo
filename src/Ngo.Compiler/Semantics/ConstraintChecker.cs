// -----------------------------------------------------------------------
// <copyright file="ConstraintChecker.cs" company="Ziad">
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

using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public static class ConstraintChecker
    {
        public static bool Satisfies(TypeSymbol typeArg, ConstraintInfo constraint)
        {
            if (constraint == ConstraintInfo.Any)
                return true;


            // Type parameter with same or stronger constraint satisfies
            if (typeArg is TypeParameterSymbol tp)
            {
                if (tp.Constraint == constraint)
                    return true;
                // comparable satisfies comparable
                // Also check lazily via the stored interface reference (handles declaration ordering)
                bool tpIsComparable = tp.Constraint.IsComparable
                    || (tp.Constraint.InterfaceType is InterfaceTypeSymbol tpIface && tpIface.IsComparable);
                if (constraint.IsComparable && tpIsComparable)
                    return true;
                // Named interface constraint: interface values are always comparable in Go,
                // and interfaces with union type elements (like ~int | ~string) contain only comparable types
                if (constraint.IsComparable && tp.Constraint.InterfaceType != null)
                    return true;
                // Union constraint with all comparable element types satisfies comparable
                if (constraint.IsComparable && tp.Constraint.TypeElements.Count > 0)
                {
                    bool allComparable = true;
                    foreach (var elem in tp.Constraint.TypeElements)
                    {
                        if (!IsComparable(elem.Type))
                        {
                            allComparable = false;
                            break;
                        }
                    }
                    if (allComparable)
                    {
                        return true;
                    }
                }
                // Type parameter always satisfies any (weakest constraint)
                if (constraint == ConstraintInfo.Any)
                    return true;
                // Type param with any constraint satisfies non-comparable empty constraints
                if (tp.Constraint == ConstraintInfo.Any && !constraint.IsComparable
                    && constraint.Methods.Count == 0 && constraint.TypeElements.Count == 0)
                    return true;
                // Type parameter with a constraint that includes the required constraint's
                // properties (comparable + methods + type elements) satisfies it.
                // In generic code, type params flow through and the real check is at instantiation.
                if (tpIsComparable || !constraint.IsComparable)
                {
                    if (constraint.Methods.Count == 0 || tp.Constraint.Methods.Count >= constraint.Methods.Count)
                    {
                        return true;
                    }
                }

                // A type parameter whose union constraint elements are all present
                // in the target constraint satisfies it (e.g., [bytes []byte | string]
                // passed to another function with the same constraint).
                if (tp.Constraint.TypeElements.Count > 0 && constraint.TypeElements.Count > 0)
                {
                    bool allMatch = true;
                    for (int i = 0; i < tp.Constraint.TypeElements.Count; i++)
                    {
                        var tpElem = tp.Constraint.TypeElements[i];
                        bool found = false;
                        for (int j = 0; j < constraint.TypeElements.Count; j++)
                        {
                            var cElem = constraint.TypeElements[j];
                            if (tpElem.Type == cElem.Type || tpElem.Type.Name == cElem.Type.Name)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found) { allMatch = false; break; }
                    }
                    if (allMatch)
                        return true;
                }
            }

            if (constraint.IsComparable)
            {
                if (!IsComparable(typeArg))
                    return false;
            }

            // Check method requirements
            for (int i = 0; i < constraint.Methods.Count; i++)
            {
                var required = constraint.Methods[i];
                var found = typeArg.LookupMethod(required.Name);
                if (found == null)
                    return false;
            }

            // Check union type elements
            if (constraint.TypeElements.Count > 0)
            {
                bool matchesAny = false;
                for (int i = 0; i < constraint.TypeElements.Count; i++)
                {
                    var element = constraint.TypeElements[i];
                    if (element.IsTilde)
                    {
                        // ~int matches any type whose underlying type is int
                        var underlying = typeArg.UnderlyingType ?? typeArg;
                        if (underlying == element.Type || underlying.Name == element.Type.Name)
                        {
                            matchesAny = true;
                            break;
                        }
                    }
                    else
                    {
                        if (typeArg == element.Type || typeArg.Name == element.Type.Name
                            || TypeChecker.IsAssignable(typeArg, element.Type))
                        {
                            matchesAny = true;
                            break;
                        }
                    }
                }
                if (!matchesAny)
                    return false;
            }

            return true;
        }

        private static bool IsComparable(TypeSymbol type)
        {
            // A type parameter with a comparable constraint is itself comparable
            if (type is TypeParameterSymbol tp && tp.Constraint.IsComparable)
                return true;

            if (type is InstantiatedTypeSymbol inst)
            {
                return IsComparable(inst.GenericType);
            }

            switch (type.TypeKind)
            {
                case TypeKind.Bool:
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
                case TypeKind.Float32:
                case TypeKind.Float64:
                case TypeKind.Complex64:
                case TypeKind.Complex128:
                case TypeKind.String:
                case TypeKind.Pointer:
                case TypeKind.Channel:
                case TypeKind.Interface:
                    return true;
                case TypeKind.Array:
                    if (type is ArrayTypeSymbol arr)
                        return IsComparable(arr.ElementType);
                    return false;
                case TypeKind.Struct:
                    if (type is StructTypeSymbol st)
                    {
                        for (int i = 0; i < st.Fields.Count; i++)
                        {
                            if (!IsComparable(st.Fields[i].Type))
                                return false;
                        }
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
    }
}
