// -----------------------------------------------------------------------
// <copyright file="DwarfTypeResolver.cs" company="Ziad">
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
using System.Text;
using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Cgo.Symbols
{
    /// <summary>
    /// Walks the type graph inside a single
    /// <see cref="DwarfCompilationUnit"/> and produces layout
    /// information for structs, unions, and enums. Responsibilities
    /// are limited to the three operations Step 5 of the CGo DWARF
    /// reader spec calls out: unwrap type-alias chains
    /// (typedef/const/volatile/restrict/atomic), resolve struct and
    /// union member offsets with DWARF 4+ bitfield semantics, and
    /// extract enumerator lists. The resolver does not map C types
    /// to .NET types and does not materialise a catalog — that is
    /// the <c>CgoDwarfSymbolSource</c> glue layer's job (Step 7).
    ///
    /// All failures raise <see cref="CgoDebugInfoException"/>.
    /// Nothing silently falls back and nothing synthesises a default
    /// type per the hardening list in the spec.
    /// </summary>
    public sealed class DwarfTypeResolver
    {
        private const int MaxUnwrapDepth = 64;

        private const byte DwOpPlusUconst = 0x23;

        private readonly DwarfCompilationUnit _compilationUnit;

        public DwarfTypeResolver(DwarfCompilationUnit compilationUnit)
        {
            if (compilationUnit == null)
            {
                throw new ArgumentNullException(nameof(compilationUnit));
            }

            _compilationUnit = compilationUnit;
        }

        /// <summary>
        /// Strip any leading typedef, const, volatile, restrict, or
        /// atomic wrappers from <paramref name="die"/> and return the
        /// underlying layout DIE. Throws
        /// <see cref="CgoDebugInfoException"/> if the chain is cyclic,
        /// the target of a <c>DW_AT_type</c> reference cannot be
        /// found, or a wrapper DIE omits <c>DW_AT_type</c> (an
        /// untyped <c>const</c> carries no meaning in C).
        /// </summary>
        public DwarfDie UnwrapTypeAliases(DwarfDie die)
        {
            if (die == null)
            {
                throw new ArgumentNullException(nameof(die));
            }

            DwarfDie current = die;
            int depth = 0;
            while (IsTypeAlias(current.Tag))
            {
                if (depth >= MaxUnwrapDepth)
                {
                    throw new CgoDebugInfoException(
                        "Type-alias chain exceeds " + MaxUnwrapDepth +
                        " levels starting at DIE @" + die.OffsetInDebugInfo +
                        "; likely a cyclic chain in malformed DWARF.");
                }
                depth++;

                DwarfAttributeValue? typeAttribute = current.TryGetAttribute(DwarfAttribute.Type);
                if (typeAttribute == null)
                {
                    throw new CgoDebugInfoException(
                        "Type-alias DIE @" + current.OffsetInDebugInfo +
                        " (tag " + current.Tag + ") is missing DW_AT_type.");
                }
                current = FollowReference(typeAttribute, current);
            }

            return current;
        }

        /// <summary>
        /// Resolve <paramref name="parent"/>'s <c>DW_AT_type</c>
        /// attribute to the referenced DIE. Does not unwrap — use
        /// <see cref="UnwrapTypeAliases"/> on the result when the
        /// caller needs the underlying layout type. Throws if the
        /// attribute is absent or points to an unknown offset.
        /// </summary>
        public DwarfDie ResolveTypeReference(DwarfDie parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            DwarfAttributeValue? typeAttribute = parent.TryGetAttribute(DwarfAttribute.Type);
            if (typeAttribute == null)
            {
                throw new CgoDebugInfoException(
                    "DIE @" + parent.OffsetInDebugInfo +
                    " (tag " + parent.Tag + ") is missing DW_AT_type.");
            }
            return FollowReference(typeAttribute, parent);
        }

        /// <summary>
        /// Walk a <see cref="DwarfTag.StructureType"/> or
        /// <see cref="DwarfTag.UnionType"/> DIE and produce its field
        /// layout. Declaration-only DIEs (<c>DW_AT_declaration</c>
        /// with no children) come back as opaque. Throws when the
        /// DIE is neither a struct nor a union, when a required
        /// attribute is missing, when the legacy DWARF 3
        /// <c>DW_AT_bit_offset</c> is encountered, or when a
        /// <c>DW_AT_data_member_location</c> expression uses
        /// anything beyond <c>DW_OP_plus_uconst</c>.
        /// </summary>
        public DwarfResolvedStructLayout ResolveStructLayout(DwarfDie die)
        {
            if (die == null)
            {
                throw new ArgumentNullException(nameof(die));
            }
            if (die.Tag != DwarfTag.StructureType && die.Tag != DwarfTag.UnionType)
            {
                throw new CgoDebugInfoException(
                    "Cannot resolve struct layout for DIE @" + die.OffsetInDebugInfo +
                    ": tag is " + die.Tag + ", expected StructureType or UnionType.");
            }

            bool isUnion = die.Tag == DwarfTag.UnionType;
            string? name = TryReadString(die, DwarfAttribute.Name);
            bool hasMembers = DieHasMemberChildren(die);
            bool isDeclaration = ReadFlag(die, DwarfAttribute.Declaration);

            if (isDeclaration && !hasMembers)
            {
                return new DwarfResolvedStructLayout(
                    name,
                    isUnion,
                    sizeBytes: 0,
                    alignmentBytes: 0,
                    isOpaque: true,
                    fields: Array.Empty<DwarfResolvedField>());
            }

            long sizeBytes = ReadByteSize(
                die, isUnion ? "Union layout" : "Struct layout");
            long alignmentBytes = ReadOptionalAlignment(die);

            List<DwarfResolvedField> fields = new();
            foreach (DwarfDie child in die.Children)
            {
                if (child.Tag != DwarfTag.Member)
                {
                    continue;
                }
                fields.Add(ResolveMemberField(child, isUnion));
            }

            return new DwarfResolvedStructLayout(
                name, isUnion, sizeBytes, alignmentBytes, isOpaque: false, fields);
        }

        /// <summary>
        /// Walk a <see cref="DwarfTag.EnumerationType"/> DIE and
        /// produce its enumerator list. Throws when the DIE is not
        /// an enumeration, when an enumerator is missing
        /// <c>DW_AT_name</c> or <c>DW_AT_const_value</c>, or when
        /// <c>DW_AT_byte_size</c> is missing on the enum itself.
        /// </summary>
        public DwarfResolvedEnum ResolveEnum(DwarfDie die)
        {
            if (die == null)
            {
                throw new ArgumentNullException(nameof(die));
            }
            if (die.Tag != DwarfTag.EnumerationType)
            {
                throw new CgoDebugInfoException(
                    "Cannot resolve enum for DIE @" + die.OffsetInDebugInfo +
                    ": tag is " + die.Tag + ", expected EnumerationType.");
            }

            string? name = TryReadString(die, DwarfAttribute.Name);
            long sizeBytes = ReadByteSize(die, "Enum layout");
            long alignmentBytes = ReadOptionalAlignment(die);
            bool isSigned = DetectEnumSignedness(die);

            List<DwarfResolvedEnumerator> enumerators = new();
            foreach (DwarfDie child in die.Children)
            {
                if (child.Tag != DwarfTag.Enumerator)
                {
                    continue;
                }
                enumerators.Add(ReadEnumerator(child));
            }

            return new DwarfResolvedEnum(
                name, sizeBytes, alignmentBytes, isSigned, enumerators);
        }

        private static bool IsTypeAlias(DwarfTag tag)
        {
            return tag == DwarfTag.Typedef
                || tag == DwarfTag.ConstType
                || tag == DwarfTag.VolatileType
                || tag == DwarfTag.RestrictType
                || tag == DwarfTag.AtomicType;
        }

        private DwarfDie FollowReference(
            DwarfAttributeValue reference, DwarfDie origin)
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

        private static bool DieHasMemberChildren(DwarfDie die)
        {
            foreach (DwarfDie child in die.Children)
            {
                if (child.Tag == DwarfTag.Member)
                {
                    return true;
                }
            }
            return false;
        }

        private DwarfResolvedField ResolveMemberField(
            DwarfDie memberDie, bool containerIsUnion)
        {
            string? fieldName = TryReadString(memberDie, DwarfAttribute.Name);
            if (fieldName == null)
            {
                throw new CgoDebugInfoException(
                    "Member DIE @" + memberDie.OffsetInDebugInfo +
                    " is missing DW_AT_name.");
            }

            DwarfDie rawType = ResolveTypeReference(memberDie);
            DwarfDie unwrappedType = UnwrapTypeAliases(rawType);

            RejectLegacyBitOffset(memberDie);

            BitfieldDescriptor bitfield = ReadBitfieldDescriptor(memberDie);
            long byteOffset;
            int bitOffset;

            if (containerIsUnion)
            {
                byteOffset = 0;
                bitOffset = bitfield.BitSize > 0 ? bitfield.BitOffsetWithinByte : 0;
            }
            else if (bitfield.HasDataBitOffset)
            {
                byteOffset = bitfield.AbsoluteBitOffset / 8;
                bitOffset = (int)(bitfield.AbsoluteBitOffset - (byteOffset * 8));
            }
            else
            {
                byteOffset = ReadDataMemberLocation(memberDie);
                bitOffset = 0;
            }

            long fieldSizeBytes = DetermineFieldSize(memberDie, unwrappedType);

            return new DwarfResolvedField(
                fieldName,
                unwrappedType,
                byteOffset,
                fieldSizeBytes,
                bitOffset,
                bitfield.BitSize);
        }

        private static void RejectLegacyBitOffset(DwarfDie memberDie)
        {
            DwarfAttributeValue? legacy = memberDie.TryGetAttribute(DwarfAttribute.BitOffset);
            if (legacy != null)
            {
                throw new CgoDebugInfoException(
                    "Member @" + memberDie.OffsetInDebugInfo +
                    " uses legacy DWARF 3 DW_AT_bit_offset; only DWARF 4+ " +
                    "DW_AT_data_bit_offset is supported.");
            }
        }

        private static BitfieldDescriptor ReadBitfieldDescriptor(DwarfDie memberDie)
        {
            DwarfAttributeValue? bitSizeAttr = memberDie.TryGetAttribute(DwarfAttribute.BitSize);
            if (bitSizeAttr == null)
            {
                return BitfieldDescriptor.None;
            }

            long bitSize = bitSizeAttr.AsInteger();
            if (bitSize < 0 || bitSize > int.MaxValue)
            {
                throw new CgoDebugInfoException(
                    "Member @" + memberDie.OffsetInDebugInfo +
                    " DW_AT_bit_size " + bitSize + " is outside the supported range.");
            }

            DwarfAttributeValue? dataBitOffsetAttr =
                memberDie.TryGetAttribute(DwarfAttribute.DataBitOffset);
            if (dataBitOffsetAttr == null)
            {
                return new BitfieldDescriptor(
                    bitSize: (int)bitSize,
                    hasDataBitOffset: false,
                    absoluteBitOffset: 0,
                    bitOffsetWithinByte: 0);
            }

            long absolute = dataBitOffsetAttr.AsInteger();
            if (absolute < 0)
            {
                throw new CgoDebugInfoException(
                    "Member @" + memberDie.OffsetInDebugInfo +
                    " DW_AT_data_bit_offset " + absolute + " is negative.");
            }

            long byteOffset = absolute / 8;
            int withinByte = (int)(absolute - (byteOffset * 8));
            return new BitfieldDescriptor(
                bitSize: (int)bitSize,
                hasDataBitOffset: true,
                absoluteBitOffset: absolute,
                bitOffsetWithinByte: withinByte);
        }

        private static long ReadDataMemberLocation(DwarfDie memberDie)
        {
            DwarfAttributeValue? location =
                memberDie.TryGetAttribute(DwarfAttribute.DataMemberLocation);
            if (location == null)
            {
                return 0;
            }
            if (location is DwarfIntegerAttributeValue integer)
            {
                if (integer.Value < 0)
                {
                    throw new CgoDebugInfoException(
                        "Member @" + memberDie.OffsetInDebugInfo +
                        " DW_AT_data_member_location is negative (" + integer.Value + ").");
                }
                return integer.Value;
            }
            if (location is DwarfBlockAttributeValue block)
            {
                return DecodePlusUConstExpression(block.Value, memberDie.OffsetInDebugInfo);
            }
            throw new CgoDebugInfoException(
                "Member @" + memberDie.OffsetInDebugInfo +
                " has DW_AT_data_member_location with unsupported form " + location.Form + ".");
        }

        private static long DecodePlusUConstExpression(byte[] expressionBytes, int dieOffset)
        {
            if (expressionBytes == null || expressionBytes.Length < 1)
            {
                throw new CgoDebugInfoException(
                    "Member @" + dieOffset +
                    " DW_AT_data_member_location expression is empty.");
            }
            if (expressionBytes[0] != DwOpPlusUconst)
            {
                throw new CgoDebugInfoException(
                    "Member @" + dieOffset +
                    " DW_AT_data_member_location expression does not begin with " +
                    "DW_OP_plus_uconst (0x23); only DW_OP_plus_uconst is supported. " +
                    "Expression bytes: " + FormatHex(expressionBytes));
            }

            int position = 1;
            ulong value = 0;
            int shift = 0;
            while (position < expressionBytes.Length)
            {
                byte current = expressionBytes[position++];
                ulong payload = (ulong)(current & 0x7F);
                if (shift >= 64 && payload != 0)
                {
                    throw new CgoDebugInfoException(
                        "Member @" + dieOffset +
                        " DW_OP_plus_uconst ULEB128 operand overflows 64 bits.");
                }
                value |= payload << shift;
                if ((current & 0x80) == 0)
                {
                    if (position != expressionBytes.Length)
                    {
                        throw new CgoDebugInfoException(
                            "Member @" + dieOffset +
                            " DW_OP_plus_uconst expression has trailing bytes after the ULEB128 " +
                            "operand. Expression bytes: " + FormatHex(expressionBytes));
                    }
                    if (value > long.MaxValue)
                    {
                        throw new CgoDebugInfoException(
                            "Member @" + dieOffset +
                            " DW_OP_plus_uconst operand " + value +
                            " exceeds signed 64-bit range.");
                    }
                    return (long)value;
                }
                shift += 7;
            }
            throw new CgoDebugInfoException(
                "Member @" + dieOffset +
                " DW_OP_plus_uconst ULEB128 operand is truncated. " +
                "Expression bytes: " + FormatHex(expressionBytes));
        }

        private static long DetermineFieldSize(DwarfDie memberDie, DwarfDie unwrappedType)
        {
            DwarfAttributeValue? memberByteSize =
                memberDie.TryGetAttribute(DwarfAttribute.ByteSize);
            if (memberByteSize != null)
            {
                return memberByteSize.AsInteger();
            }

            DwarfAttributeValue? typeByteSize =
                unwrappedType.TryGetAttribute(DwarfAttribute.ByteSize);
            if (typeByteSize != null)
            {
                return typeByteSize.AsInteger();
            }

            return 0;
        }

        private bool DetectEnumSignedness(DwarfDie enumDie)
        {
            DwarfAttributeValue? typeAttribute = enumDie.TryGetAttribute(DwarfAttribute.Type);
            if (typeAttribute == null)
            {
                return true;
            }

            DwarfDie baseType = UnwrapTypeAliases(FollowReference(typeAttribute, enumDie));
            if (baseType.Tag != DwarfTag.BaseType)
            {
                return true;
            }

            DwarfAttributeValue? encoding = baseType.TryGetAttribute(DwarfAttribute.Encoding);
            if (encoding == null)
            {
                return true;
            }

            DwarfTypeEncoding encodingValue = (DwarfTypeEncoding)encoding.AsInteger();
            return encodingValue == DwarfTypeEncoding.Signed
                || encodingValue == DwarfTypeEncoding.SignedChar
                || encodingValue == DwarfTypeEncoding.SignedFixed;
        }

        private static DwarfResolvedEnumerator ReadEnumerator(DwarfDie enumeratorDie)
        {
            string? name = TryReadString(enumeratorDie, DwarfAttribute.Name);
            if (name == null)
            {
                throw new CgoDebugInfoException(
                    "Enumerator DIE @" + enumeratorDie.OffsetInDebugInfo +
                    " is missing DW_AT_name.");
            }

            DwarfAttributeValue? constValue =
                enumeratorDie.TryGetAttribute(DwarfAttribute.ConstValue);
            if (constValue == null)
            {
                throw new CgoDebugInfoException(
                    "Enumerator '" + name + "' at DIE @" + enumeratorDie.OffsetInDebugInfo +
                    " is missing DW_AT_const_value.");
            }

            return new DwarfResolvedEnumerator(name, constValue.AsInteger());
        }

        private static string? TryReadString(DwarfDie die, DwarfAttribute attribute)
        {
            DwarfAttributeValue? value = die.TryGetAttribute(attribute);
            if (value == null)
            {
                return null;
            }
            return value.AsString();
        }

        private static bool ReadFlag(DwarfDie die, DwarfAttribute attribute)
        {
            DwarfAttributeValue? value = die.TryGetAttribute(attribute);
            if (value == null)
            {
                return false;
            }
            return value.AsFlag();
        }

        private static long ReadByteSize(DwarfDie die, string contextDescription)
        {
            DwarfAttributeValue? value = die.TryGetAttribute(DwarfAttribute.ByteSize);
            if (value == null)
            {
                throw new CgoDebugInfoException(
                    contextDescription + " DIE @" + die.OffsetInDebugInfo +
                    " is missing DW_AT_byte_size.");
            }
            long size = value.AsInteger();
            if (size < 0)
            {
                throw new CgoDebugInfoException(
                    contextDescription + " DIE @" + die.OffsetInDebugInfo +
                    " has negative DW_AT_byte_size " + size + ".");
            }
            return size;
        }

        private static long ReadOptionalAlignment(DwarfDie die)
        {
            DwarfAttributeValue? value = die.TryGetAttribute(DwarfAttribute.Alignment);
            if (value == null)
            {
                return 0;
            }
            long alignment = value.AsInteger();
            if (alignment < 0)
            {
                throw new CgoDebugInfoException(
                    "DIE @" + die.OffsetInDebugInfo +
                    " has negative DW_AT_alignment " + alignment + ".");
            }
            return alignment;
        }

        private static string FormatHex(byte[] bytes)
        {
            StringBuilder builder = new();
            for (int index = 0; index < bytes.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(bytes[index].ToString("X2"));
            }
            return builder.ToString();
        }

        private readonly struct BitfieldDescriptor
        {
            public BitfieldDescriptor(
                int bitSize,
                bool hasDataBitOffset,
                long absoluteBitOffset,
                int bitOffsetWithinByte)
            {
                BitSize = bitSize;
                HasDataBitOffset = hasDataBitOffset;
                AbsoluteBitOffset = absoluteBitOffset;
                BitOffsetWithinByte = bitOffsetWithinByte;
            }

            public int BitSize { get; }

            public bool HasDataBitOffset { get; }

            public long AbsoluteBitOffset { get; }

            public int BitOffsetWithinByte { get; }

            public static BitfieldDescriptor None
            {
                get
                {
                    return new BitfieldDescriptor(
                        bitSize: 0,
                        hasDataBitOffset: false,
                        absoluteBitOffset: 0,
                        bitOffsetWithinByte: 0);
                }
            }
        }
    }
}
