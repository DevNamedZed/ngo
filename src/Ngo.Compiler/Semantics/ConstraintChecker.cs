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
                        if (typeArg == element.Type || typeArg.Name == element.Type.Name)
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
