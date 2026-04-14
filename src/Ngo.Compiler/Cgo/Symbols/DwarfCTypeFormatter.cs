// -----------------------------------------------------------------------
// <copyright file="DwarfCTypeFormatter.cs" company="Ziad">
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
using System.Text;
using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Cgo.Symbols
{
    /// <summary>
    /// Renders a DWARF type DIE as the equivalent C source spelling,
    /// for example <c>const unsigned long *</c> or
    /// <c>struct sqlite3_backup</c>. The rendered string is what the
    /// catalog stores for typedef aliases, struct/union field types,
    /// and function signatures, so the P/Invoke emitter sees the same
    /// surface form regardless of whether the DIE came from a typedef,
    /// a const-qualified intermediate, or a bare base type.
    ///
    /// The formatter handles the type-forming tags produced by
    /// gcc/clang on the anchor probe: <c>DW_TAG_base_type</c>,
    /// <c>DW_TAG_typedef</c>, <c>DW_TAG_pointer_type</c>,
    /// <c>DW_TAG_const_type</c>, <c>DW_TAG_volatile_type</c>,
    /// <c>DW_TAG_restrict_type</c>, <c>DW_TAG_atomic_type</c>,
    /// <c>DW_TAG_array_type</c>, <c>DW_TAG_structure_type</c>,
    /// <c>DW_TAG_union_type</c>, <c>DW_TAG_enumeration_type</c>, and
    /// <c>DW_TAG_subroutine_type</c>. Any other tag triggers
    /// <see cref="CgoDebugInfoException"/> with the DIE offset so the
    /// bug is traceable back to the source.
    /// </summary>
    public sealed class DwarfCTypeFormatter
    {
        private const int MaxRecursionDepth = 64;

        private const string AnonymousTypeName = "_anonymous";

        private readonly DwarfCompilationUnit _compilationUnit;

        public DwarfCTypeFormatter(DwarfCompilationUnit compilationUnit)
        {
            if (compilationUnit == null)
            {
                throw new ArgumentNullException(nameof(compilationUnit));
            }

            _compilationUnit = compilationUnit;
        }

        /// <summary>
        /// Return the C source spelling of the type named by
        /// <paramref name="typeDie"/>. Resolves cross-references
        /// through <see cref="DwarfCompilationUnit.DiesByOffsetInDebugInfo"/>
        /// so qualifier and pointer chains are rendered even when the
        /// target DIEs live elsewhere in <c>.debug_info</c>.
        /// </summary>
        public string Format(DwarfDie typeDie)
        {
            if (typeDie == null)
            {
                throw new ArgumentNullException(nameof(typeDie));
            }

            return FormatInternal(typeDie, depth: 0);
        }

        private string FormatInternal(DwarfDie typeDie, int depth)
        {
            if (depth >= MaxRecursionDepth)
            {
                throw new CgoDebugInfoException(
                    "Type formatter exceeded " + MaxRecursionDepth +
                    " levels at DIE @" + typeDie.OffsetInDebugInfo +
                    "; likely a cyclic type chain.");
            }

            switch (typeDie.Tag)
            {
                case DwarfTag.BaseType:
                {
                    return ReadRequiredName(typeDie, "base type");
                }
                case DwarfTag.Typedef:
                {
                    return ReadRequiredName(typeDie, "typedef");
                }
                case DwarfTag.StructureType:
                {
                    return "struct " + ReadNameOrAnonymous(typeDie);
                }
                case DwarfTag.UnionType:
                {
                    return "union " + ReadNameOrAnonymous(typeDie);
                }
                case DwarfTag.EnumerationType:
                {
                    return "enum " + ReadNameOrAnonymous(typeDie);
                }
                case DwarfTag.PointerType:
                {
                    return FormatPointer(typeDie, depth);
                }
                case DwarfTag.ConstType:
                {
                    return "const " + FormatInnerTypeOrVoid(typeDie, depth);
                }
                case DwarfTag.VolatileType:
                {
                    return "volatile " + FormatInnerTypeOrVoid(typeDie, depth);
                }
                case DwarfTag.RestrictType:
                {
                    return FormatInnerTypeOrVoid(typeDie, depth) + " restrict";
                }
                case DwarfTag.AtomicType:
                {
                    return "_Atomic " + FormatInnerTypeOrVoid(typeDie, depth);
                }
                case DwarfTag.ArrayType:
                {
                    return FormatArray(typeDie, depth);
                }
                case DwarfTag.SubroutineType:
                {
                    return FormatSubroutine(typeDie, depth);
                }
                case DwarfTag.UnspecifiedType:
                {
                    return ReadRequiredName(typeDie, "unspecified type");
                }
                default:
                {
                    throw new CgoDebugInfoException(
                        "Cannot render type DIE @" + typeDie.OffsetInDebugInfo +
                        " with tag " + typeDie.Tag + " as a C type spelling.");
                }
            }
        }

        private string FormatPointer(DwarfDie pointerDie, int depth)
        {
            DwarfAttributeValue? typeAttribute = pointerDie.TryGetAttribute(DwarfAttribute.Type);
            if (typeAttribute == null)
            {
                return "void *";
            }

            DwarfDie pointee = ResolveReference(typeAttribute, pointerDie);
            string pointeeSpelling = FormatInternal(pointee, depth + 1);
            return pointeeSpelling + " *";
        }

        private string FormatArray(DwarfDie arrayDie, int depth)
        {
            DwarfAttributeValue? typeAttribute = arrayDie.TryGetAttribute(DwarfAttribute.Type);
            if (typeAttribute == null)
            {
                throw new CgoDebugInfoException(
                    "Array DIE @" + arrayDie.OffsetInDebugInfo +
                    " is missing DW_AT_type for its element type.");
            }

            DwarfDie element = ResolveReference(typeAttribute, arrayDie);
            string elementSpelling = FormatInternal(element, depth + 1);

            string dimensionSuffix = ReadArrayDimensions(arrayDie);
            return elementSpelling + dimensionSuffix;
        }

        private string FormatSubroutine(DwarfDie subroutineDie, int depth)
        {
            DwarfAttributeValue? returnTypeAttribute = subroutineDie.TryGetAttribute(DwarfAttribute.Type);
            string returnSpelling;
            if (returnTypeAttribute == null)
            {
                returnSpelling = "void";
            }
            else
            {
                DwarfDie returnType = ResolveReference(returnTypeAttribute, subroutineDie);
                returnSpelling = FormatInternal(returnType, depth + 1);
            }

            StringBuilder builder = new();
            builder.Append(returnSpelling);
            builder.Append(" (");
            bool first = true;
            bool isVariadic = false;
            foreach (DwarfDie child in subroutineDie.Children)
            {
                if (child.Tag == DwarfTag.UnspecifiedParameters)
                {
                    isVariadic = true;
                    continue;
                }
                if (child.Tag != DwarfTag.FormalParameter)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(", ");
                }
                first = false;

                DwarfAttributeValue? parameterTypeAttribute = child.TryGetAttribute(DwarfAttribute.Type);
                if (parameterTypeAttribute == null)
                {
                    throw new CgoDebugInfoException(
                        "Formal parameter DIE @" + child.OffsetInDebugInfo +
                        " inside subroutine type @" + subroutineDie.OffsetInDebugInfo +
                        " is missing DW_AT_type.");
                }

                DwarfDie parameterType = ResolveReference(parameterTypeAttribute, child);
                builder.Append(FormatInternal(parameterType, depth + 1));
            }

            if (first && !isVariadic)
            {
                builder.Append("void");
            }
            else if (isVariadic)
            {
                if (!first)
                {
                    builder.Append(", ");
                }
                builder.Append("...");
            }

            builder.Append(')');
            return builder.ToString();
        }

        private string FormatInnerTypeOrVoid(DwarfDie qualifierDie, int depth)
        {
            DwarfAttributeValue? typeAttribute = qualifierDie.TryGetAttribute(DwarfAttribute.Type);
            if (typeAttribute == null)
            {
                return "void";
            }

            DwarfDie inner = ResolveReference(typeAttribute, qualifierDie);
            return FormatInternal(inner, depth + 1);
        }

        private string ReadArrayDimensions(DwarfDie arrayDie)
        {
            StringBuilder builder = new();
            foreach (DwarfDie child in arrayDie.Children)
            {
                if (child.Tag != DwarfTag.SubrangeType)
                {
                    continue;
                }

                builder.Append('[');
                DwarfAttributeValue? upperBound = child.TryGetAttribute(DwarfAttribute.UpperBound);
                DwarfAttributeValue? count = child.TryGetAttribute(DwarfAttribute.Count);
                if (count != null)
                {
                    builder.Append(count.AsInteger());
                }
                else if (upperBound != null)
                {
                    long upper = upperBound.AsInteger();
                    long lower = ReadLowerBound(child);
                    long dimension = upper - lower + 1;
                    builder.Append(dimension);
                }
                builder.Append(']');
            }

            if (builder.Length == 0)
            {
                builder.Append("[]");
            }
            return builder.ToString();
        }

        private static long ReadLowerBound(DwarfDie subrangeDie)
        {
            DwarfAttributeValue? lowerBound = subrangeDie.TryGetAttribute(DwarfAttribute.LowerBound);
            if (lowerBound == null)
            {
                return 0;
            }
            return lowerBound.AsInteger();
        }

        private string ReadRequiredName(DwarfDie die, string contextDescription)
        {
            DwarfAttributeValue? nameAttribute = die.TryGetAttribute(DwarfAttribute.Name);
            if (nameAttribute == null)
            {
                throw new CgoDebugInfoException(
                    "DIE @" + die.OffsetInDebugInfo +
                    " (tag " + die.Tag + ", expected " + contextDescription +
                    ") is missing DW_AT_name.");
            }
            return nameAttribute.AsString();
        }

        private static string ReadNameOrAnonymous(DwarfDie die)
        {
            DwarfAttributeValue? nameAttribute = die.TryGetAttribute(DwarfAttribute.Name);
            if (nameAttribute == null)
            {
                return AnonymousTypeName;
            }
            return nameAttribute.AsString();
        }

        private DwarfDie ResolveReference(DwarfAttributeValue reference, DwarfDie origin)
        {
            int targetOffset = reference.AsReference();
            if (!_compilationUnit.DiesByOffsetInDebugInfo.TryGetValue(
                    targetOffset, out DwarfDie? target))
            {
                throw new CgoDebugInfoException(
                    "DW_AT_type reference from DIE @" + origin.OffsetInDebugInfo +
                    " points to offset " + targetOffset +
                    " which is not a known DIE in the compilation unit starting at @" +
                    _compilationUnit.HeaderOffsetInDebugInfo + ".");
            }
            return target;
        }
    }
}
