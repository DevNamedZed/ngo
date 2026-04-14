// -----------------------------------------------------------------------
// <copyright file="DwarfReader.cs" company="Ziad">
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
using Ngo.Compiler.Cgo.Binary;

namespace Ngo.Compiler.Cgo.Dwarf
{
    /// <summary>
    /// Parses <c>.debug_info</c> into a tree of
    /// <see cref="DwarfCompilationUnit"/> objects. The reader is
    /// instanced per call (<see cref="Read"/> creates one internally)
    /// so the per-CU state — CU header offset, unit format, address
    /// size, abbreviation table — lives on the instance and does not
    /// need to be threaded through every recursive DIE call.
    /// </summary>
    public sealed class DwarfReader
    {
        private const string DebugInfoSectionName = ".debug_info";
        private const string DebugStrSectionName = ".debug_str";
        private const string DebugLineStrSectionName = ".debug_line_str";

        private const int DwarfUnitTypeCompile = 0x01;

        private readonly DwarfSections _sections;
        private readonly BinaryReaderLittleEndian _debugInfoReader;
        private readonly Dictionary<int, DwarfAbbreviationTable> _abbreviationTablesByOffset;

        private int _cuHeaderStart;
        private DwarfUnitFormat _cuUnitFormat;
        private int _cuAddressSize;
        private DwarfAbbreviationTable? _cuAbbreviationTable;

        private DwarfReader(DwarfSections sections)
        {
            _sections = sections;
            _debugInfoReader = new BinaryReaderLittleEndian(sections.DebugInfo);
            _abbreviationTablesByOffset = new Dictionary<int, DwarfAbbreviationTable>();
        }

        /// <summary>
        /// Parse every compilation unit in
        /// <paramref name="sections"/>.DebugInfo. Each CU is parsed
        /// independently; a malformed CU raises
        /// <see cref="DwarfParseException"/> with an offset pointing
        /// into <c>.debug_info</c> so diagnosis against the failing
        /// object file is unambiguous.
        /// </summary>
        public static DwarfDebugInfo Read(DwarfSections sections)
        {
            if (sections == null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            DwarfReader reader = new(sections);
            return reader.ReadAllCompilationUnits();
        }

        private DwarfDebugInfo ReadAllCompilationUnits()
        {
            List<DwarfCompilationUnit> compilationUnits = new();
            DwarfFormat highestFormatSeen = DwarfFormat.Dwarf4;

            while (_debugInfoReader.Position < _debugInfoReader.Length)
            {
                DwarfCompilationUnit compilationUnit = ReadCompilationUnit();
                compilationUnits.Add(compilationUnit);
                if (compilationUnit.Version == (int)DwarfFormat.Dwarf5)
                {
                    highestFormatSeen = DwarfFormat.Dwarf5;
                }
            }

            return new DwarfDebugInfo(compilationUnits, highestFormatSeen);
        }

        private DwarfCompilationUnit ReadCompilationUnit()
        {
            int cuHeaderStart = _debugInfoReader.Position;
            CompilationUnitLength length = ReadUnitLength(cuHeaderStart);
            int cuEndOffset = cuHeaderStart + length.HeaderPrefixBytes + length.ContentsLength;
            if (cuEndOffset > _debugInfoReader.Length)
            {
                throw new DwarfParseException(
                    "Compilation-unit length " + length.ContentsLength +
                    " would extend past end of " + DebugInfoSectionName +
                    " (" + _debugInfoReader.Length + " bytes).",
                    DebugInfoSectionName,
                    cuHeaderStart);
            }

            int version = ReadVersion(cuHeaderStart);
            int debugAbbrevOffset;
            int addressSize;

            if (version == (int)DwarfFormat.Dwarf5)
            {
                int unitType = _debugInfoReader.ReadU8();
                if (unitType != DwarfUnitTypeCompile)
                {
                    throw new DwarfParseException(
                        "DWARF 5 unit_type 0x" + unitType.ToString("X2") +
                        " not supported; only DW_UT_compile (0x01) is recognised in this stage.",
                        DebugInfoSectionName,
                        cuHeaderStart);
                }
                addressSize = _debugInfoReader.ReadU8();
                debugAbbrevOffset = ReadSectionOffset(length.UnitFormat, cuHeaderStart);
            }
            else
            {
                debugAbbrevOffset = ReadSectionOffset(length.UnitFormat, cuHeaderStart);
                addressSize = _debugInfoReader.ReadU8();
            }

            ValidateAddressSize(addressSize, cuHeaderStart);

            DwarfAbbreviationTable abbreviationTable = LoadAbbreviationTable(debugAbbrevOffset);
            _cuHeaderStart = cuHeaderStart;
            _cuUnitFormat = length.UnitFormat;
            _cuAddressSize = addressSize;
            _cuAbbreviationTable = abbreviationTable;

            Dictionary<int, DwarfDie> diesByOffset = new();
            List<DwarfDie> topLevelDies = new();
            while (_debugInfoReader.Position < cuEndOffset)
            {
                DwarfDie? die = ReadDie(cuEndOffset, diesByOffset);
                if (die == null)
                {
                    break;
                }
                topLevelDies.Add(die);
            }

            if (_debugInfoReader.Position != cuEndOffset)
            {
                _debugInfoReader.Seek(cuEndOffset);
            }

            string? name = TryGetStringOnFirstDie(topLevelDies, DwarfAttribute.Name);
            string? compDir = TryGetStringOnFirstDie(topLevelDies, DwarfAttribute.CompDir);
            string? producer = TryGetStringOnFirstDie(topLevelDies, DwarfAttribute.Producer);
            DwarfLanguage language = TryGetLanguageOnFirstDie(topLevelDies);

            return new DwarfCompilationUnit(
                version,
                addressSize,
                length.UnitFormat,
                cuHeaderStart,
                debugAbbrevOffset,
                name,
                compDir,
                producer,
                language,
                diesByOffset,
                topLevelDies);
        }

        private CompilationUnitLength ReadUnitLength(int cuHeaderStart)
        {
            uint firstWord;
            try
            {
                firstWord = _debugInfoReader.ReadU32();
            }
            catch (BinaryReadException truncated)
            {
                throw new DwarfParseException(
                    "Failed to read compilation-unit length: " + truncated.Message,
                    DebugInfoSectionName,
                    cuHeaderStart,
                    truncated);
            }

            if (firstWord == 0xFFFFFFFFu)
            {
                ulong length64;
                try
                {
                    length64 = _debugInfoReader.ReadU64();
                }
                catch (BinaryReadException truncated)
                {
                    throw new DwarfParseException(
                        "Failed to read 64-bit compilation-unit length: " + truncated.Message,
                        DebugInfoSectionName,
                        cuHeaderStart,
                        truncated);
                }
                if (length64 > int.MaxValue)
                {
                    throw new DwarfParseException(
                        "DWARF64 compilation-unit length " + length64 +
                        " exceeds addressable " + DebugInfoSectionName +
                        " size (int.MaxValue).",
                        DebugInfoSectionName,
                        cuHeaderStart);
                }
                return new CompilationUnitLength((int)length64, DwarfUnitFormat.Dwarf64, 12);
            }

            if (firstWord >= 0xFFFFFFF0u)
            {
                throw new DwarfParseException(
                    "Reserved compilation-unit initial length value 0x" +
                    firstWord.ToString("X8") + ".",
                    DebugInfoSectionName,
                    cuHeaderStart);
            }

            return new CompilationUnitLength((int)firstWord, DwarfUnitFormat.Dwarf32, 4);
        }

        private int ReadVersion(int cuHeaderStart)
        {
            ushort version;
            try
            {
                version = _debugInfoReader.ReadU16();
            }
            catch (BinaryReadException truncated)
            {
                throw new DwarfParseException(
                    "Failed to read compilation-unit version: " + truncated.Message,
                    DebugInfoSectionName,
                    cuHeaderStart,
                    truncated);
            }

            if (version != (int)DwarfFormat.Dwarf4 && version != (int)DwarfFormat.Dwarf5)
            {
                throw new DwarfParseException(
                    "Unsupported DWARF version " + version +
                    "; this stage supports DWARF 4 and DWARF 5 only.",
                    DebugInfoSectionName,
                    cuHeaderStart);
            }
            return version;
        }

        private static void ValidateAddressSize(int addressSize, int cuHeaderStart)
        {
            if (addressSize != 4 && addressSize != 8)
            {
                throw new DwarfParseException(
                    "Unsupported CU address_size " + addressSize +
                    "; expected 4 or 8.",
                    DebugInfoSectionName,
                    cuHeaderStart);
            }
        }

        private DwarfAbbreviationTable LoadAbbreviationTable(int debugAbbrevOffset)
        {
            if (_abbreviationTablesByOffset.TryGetValue(
                    debugAbbrevOffset, out DwarfAbbreviationTable? cached))
            {
                return cached;
            }

            DwarfAbbreviationTable parsed =
                DwarfAbbreviationTable.Parse(_sections.DebugAbbrev, debugAbbrevOffset);
            _abbreviationTablesByOffset[debugAbbrevOffset] = parsed;
            return parsed;
        }

        private DwarfDie? ReadDie(int cuEndOffset, Dictionary<int, DwarfDie> diesByOffset)
        {
            int dieStartOffset = _debugInfoReader.Position;
            int abbreviationCode;
            try
            {
                abbreviationCode = _debugInfoReader.ReadUnsignedLeb128AsInt32();
            }
            catch (BinaryReadException truncated)
            {
                throw new DwarfParseException(
                    "Failed to read DIE abbreviation code: " + truncated.Message,
                    DebugInfoSectionName,
                    dieStartOffset,
                    truncated);
            }

            if (abbreviationCode == 0)
            {
                return null;
            }

            DwarfAbbreviation abbreviation = _cuAbbreviationTable!.Get(
                abbreviationCode, DebugInfoSectionName, dieStartOffset);

            Dictionary<DwarfAttribute, DwarfAttributeValue> attributes = new();
            foreach (DwarfAbbreviationAttribute attributeSpec in abbreviation.Attributes)
            {
                DwarfAttributeValue value = ReadAttributeValue(attributeSpec, dieStartOffset);
                attributes[attributeSpec.Attribute] = value;
            }

            List<DwarfDie> children = new();
            if (abbreviation.HasChildren)
            {
                while (true)
                {
                    if (_debugInfoReader.Position >= cuEndOffset)
                    {
                        throw new DwarfParseException(
                            "Child DIE chain for DIE at offset " + dieStartOffset +
                            " ran past the compilation-unit end without a null terminator.",
                            DebugInfoSectionName,
                            _debugInfoReader.Position);
                    }
                    DwarfDie? child = ReadDie(cuEndOffset, diesByOffset);
                    if (child == null)
                    {
                        break;
                    }
                    children.Add(child);
                }
            }

            DwarfDie die = new(abbreviation.Tag, dieStartOffset, attributes, children);
            diesByOffset[dieStartOffset] = die;
            return die;
        }

        private DwarfAttributeValue ReadAttributeValue(
            DwarfAbbreviationAttribute attributeSpec, int dieStartOffset)
        {
            DwarfForm form = attributeSpec.Form;
            try
            {
                return DecodeFormValue(form, attributeSpec, dieStartOffset);
            }
            catch (BinaryReadException truncated)
            {
                throw new DwarfParseException(
                    "Failed to read attribute value (attribute=" + attributeSpec.Attribute +
                    ", form=" + form + "): " + truncated.Message,
                    DebugInfoSectionName,
                    dieStartOffset,
                    truncated);
            }
        }

        private DwarfAttributeValue DecodeFormValue(
            DwarfForm form, DwarfAbbreviationAttribute attributeSpec, int dieStartOffset)
        {
            switch (form)
            {
                case DwarfForm.Addr:
                {
                    long address = _cuAddressSize == 4
                        ? _debugInfoReader.ReadU32()
                        : (long)_debugInfoReader.ReadU64();
                    return new DwarfIntegerAttributeValue(form, address, true);
                }
                case DwarfForm.Block1:
                {
                    int length = _debugInfoReader.ReadU8();
                    return new DwarfBlockAttributeValue(form, _debugInfoReader.ReadBytes(length));
                }
                case DwarfForm.Block2:
                {
                    int length = _debugInfoReader.ReadU16();
                    return new DwarfBlockAttributeValue(form, _debugInfoReader.ReadBytes(length));
                }
                case DwarfForm.Block4:
                {
                    uint length = _debugInfoReader.ReadU32();
                    if (length > int.MaxValue)
                    {
                        throw new DwarfParseException(
                            "DW_FORM_block4 length " + length + " exceeds int.MaxValue.",
                            DebugInfoSectionName,
                            dieStartOffset);
                    }
                    return new DwarfBlockAttributeValue(
                        form, _debugInfoReader.ReadBytes((int)length));
                }
                case DwarfForm.Block:
                case DwarfForm.Exprloc:
                {
                    int length = _debugInfoReader.ReadUnsignedLeb128AsInt32();
                    return new DwarfBlockAttributeValue(form, _debugInfoReader.ReadBytes(length));
                }
                case DwarfForm.Data1:
                    return new DwarfIntegerAttributeValue(form, _debugInfoReader.ReadU8(), true);
                case DwarfForm.Data2:
                    return new DwarfIntegerAttributeValue(form, _debugInfoReader.ReadU16(), true);
                case DwarfForm.Data4:
                    return new DwarfIntegerAttributeValue(form, _debugInfoReader.ReadU32(), true);
                case DwarfForm.Data8:
                    return new DwarfIntegerAttributeValue(
                        form, (long)_debugInfoReader.ReadU64(), true);
                case DwarfForm.Data16:
                    return new DwarfBlockAttributeValue(form, _debugInfoReader.ReadBytes(16));
                case DwarfForm.Sdata:
                    return new DwarfIntegerAttributeValue(
                        form, _debugInfoReader.ReadSignedLeb128(), false);
                case DwarfForm.Udata:
                {
                    ulong value = _debugInfoReader.ReadUnsignedLeb128();
                    return new DwarfIntegerAttributeValue(form, (long)value, true);
                }
                case DwarfForm.String:
                    return new DwarfStringAttributeValue(
                        form, _debugInfoReader.ReadNullTerminatedUtf8());
                case DwarfForm.Strp:
                {
                    int offset = ReadSectionOffset(_cuUnitFormat, dieStartOffset);
                    string value = ReadDebugString(
                        _sections.DebugStr, offset, DebugStrSectionName, dieStartOffset);
                    return new DwarfStringAttributeValue(form, value);
                }
                case DwarfForm.LineStrp:
                {
                    int offset = ReadSectionOffset(_cuUnitFormat, dieStartOffset);
                    string value = ReadDebugString(
                        _sections.DebugLineStr, offset, DebugLineStrSectionName, dieStartOffset);
                    return new DwarfStringAttributeValue(form, value);
                }
                case DwarfForm.Flag:
                    return new DwarfFlagAttributeValue(form, _debugInfoReader.ReadU8() != 0);
                case DwarfForm.FlagPresent:
                    return new DwarfFlagAttributeValue(form, true);
                case DwarfForm.RefAddr:
                {
                    int offset = ReadSectionOffset(_cuUnitFormat, dieStartOffset);
                    return new DwarfReferenceAttributeValue(form, offset);
                }
                case DwarfForm.Ref1:
                    return new DwarfReferenceAttributeValue(
                        form, AddCuRelativeOffset(_debugInfoReader.ReadU8(), dieStartOffset));
                case DwarfForm.Ref2:
                    return new DwarfReferenceAttributeValue(
                        form, AddCuRelativeOffset(_debugInfoReader.ReadU16(), dieStartOffset));
                case DwarfForm.Ref4:
                    return new DwarfReferenceAttributeValue(
                        form, AddCuRelativeOffset(_debugInfoReader.ReadU32(), dieStartOffset));
                case DwarfForm.Ref8:
                {
                    ulong rawOffset = _debugInfoReader.ReadU64();
                    return new DwarfReferenceAttributeValue(
                        form, AddCuRelativeOffset(rawOffset, dieStartOffset));
                }
                case DwarfForm.RefUdata:
                {
                    ulong rawOffset = _debugInfoReader.ReadUnsignedLeb128();
                    return new DwarfReferenceAttributeValue(
                        form, AddCuRelativeOffset(rawOffset, dieStartOffset));
                }
                case DwarfForm.SecOffset:
                {
                    int offset = ReadSectionOffset(_cuUnitFormat, dieStartOffset);
                    return new DwarfIntegerAttributeValue(form, offset, true);
                }
                case DwarfForm.ImplicitConst:
                    return new DwarfIntegerAttributeValue(
                        form, attributeSpec.ImplicitConstValue, false);
                case DwarfForm.Indirect:
                    return ReadIndirectFormValue(attributeSpec, dieStartOffset);
                case DwarfForm.RefSig8:
                    throw new DwarfParseException(
                        "DW_FORM_ref_sig8 (type-unit signatures) is not supported in this stage.",
                        DebugInfoSectionName,
                        dieStartOffset);
                case DwarfForm.Strx:
                case DwarfForm.Strx1:
                case DwarfForm.Strx2:
                case DwarfForm.Strx3:
                case DwarfForm.Strx4:
                case DwarfForm.Addrx:
                case DwarfForm.Addrx1:
                case DwarfForm.Addrx2:
                case DwarfForm.Addrx3:
                case DwarfForm.Addrx4:
                case DwarfForm.RefSup4:
                case DwarfForm.RefSup8:
                case DwarfForm.StrpSup:
                case DwarfForm.Loclistx:
                case DwarfForm.Rnglistx:
                    throw new DwarfParseException(
                        "DWARF 5 indexed form " + form +
                        " is not supported in this stage (anchor probes compile with -gdwarf-4).",
                        DebugInfoSectionName,
                        dieStartOffset);
                default:
                    throw new DwarfParseException(
                        "Unknown DWARF form 0x" + ((int)form).ToString("X2") +
                        "; cannot determine byte layout to advance the cursor.",
                        DebugInfoSectionName,
                        dieStartOffset);
            }
        }

        private DwarfAttributeValue ReadIndirectFormValue(
            DwarfAbbreviationAttribute attributeSpec, int dieStartOffset)
        {
            int indirectFormCode;
            try
            {
                indirectFormCode = _debugInfoReader.ReadUnsignedLeb128AsInt32();
            }
            catch (BinaryReadException truncated)
            {
                throw new DwarfParseException(
                    "Failed to read DW_FORM_indirect form code: " + truncated.Message,
                    DebugInfoSectionName,
                    dieStartOffset,
                    truncated);
            }

            DwarfForm indirectForm = (DwarfForm)indirectFormCode;
            if (indirectForm == DwarfForm.Indirect)
            {
                throw new DwarfParseException(
                    "DW_FORM_indirect cannot reference another DW_FORM_indirect.",
                    DebugInfoSectionName,
                    dieStartOffset);
            }
            if (indirectForm == DwarfForm.ImplicitConst)
            {
                throw new DwarfParseException(
                    "DW_FORM_indirect cannot reference DW_FORM_implicit_const; " +
                    "implicit_const carries its value in the abbreviation, not the DIE.",
                    DebugInfoSectionName,
                    dieStartOffset);
            }

            DwarfAbbreviationAttribute redirectedSpec = new(
                attributeSpec.Attribute, indirectForm, 0);
            return DecodeFormValue(indirectForm, redirectedSpec, dieStartOffset);
        }

        private int AddCuRelativeOffset(ulong rawOffset, int dieStartOffset)
        {
            long combined = _cuHeaderStart + (long)rawOffset;
            if (combined < 0 || combined > int.MaxValue)
            {
                throw new DwarfParseException(
                    "CU-relative reference " + rawOffset +
                    " plus CU header offset " + _cuHeaderStart +
                    " overflows int.",
                    DebugInfoSectionName,
                    dieStartOffset);
            }
            return (int)combined;
        }

        private int ReadSectionOffset(DwarfUnitFormat unitFormat, int dieStartOffset)
        {
            if (unitFormat == DwarfUnitFormat.Dwarf32)
            {
                uint value = _debugInfoReader.ReadU32();
                if (value > int.MaxValue)
                {
                    throw new DwarfParseException(
                        "DWARF32 section offset 0x" + value.ToString("X8") +
                        " exceeds int.MaxValue.",
                        DebugInfoSectionName,
                        dieStartOffset);
                }
                return (int)value;
            }

            ulong value64 = _debugInfoReader.ReadU64();
            if (value64 > int.MaxValue)
            {
                throw new DwarfParseException(
                    "DWARF64 section offset 0x" + value64.ToString("X16") +
                    " exceeds int.MaxValue.",
                    DebugInfoSectionName,
                    dieStartOffset);
            }
            return (int)value64;
        }

        private static string ReadDebugString(
            byte[]? sectionBytes, int offset, string sectionName, int requestingOffset)
        {
            if (sectionBytes == null)
            {
                throw new DwarfParseException(
                    "Attribute references " + sectionName + "@" + offset +
                    " but the object file has no " + sectionName + " section.",
                    DebugInfoSectionName,
                    requestingOffset);
            }
            if (offset < 0 || offset >= sectionBytes.Length)
            {
                throw new DwarfParseException(
                    "String offset " + offset + " is outside " + sectionName +
                    " (" + sectionBytes.Length + " bytes).",
                    sectionName,
                    offset);
            }

            BinaryReaderLittleEndian stringReader = new(sectionBytes, offset);
            try
            {
                return stringReader.ReadNullTerminatedUtf8();
            }
            catch (BinaryReadException truncated)
            {
                throw new DwarfParseException(
                    "Failed to read null-terminated string: " + truncated.Message,
                    sectionName,
                    offset,
                    truncated);
            }
        }

        private static string? TryGetStringOnFirstDie(
            IReadOnlyList<DwarfDie> topLevelDies, DwarfAttribute attribute)
        {
            if (topLevelDies.Count == 0)
            {
                return null;
            }
            DwarfAttributeValue? value = topLevelDies[0].TryGetAttribute(attribute);
            return value is DwarfStringAttributeValue stringValue ? stringValue.Value : null;
        }

        private static DwarfLanguage TryGetLanguageOnFirstDie(IReadOnlyList<DwarfDie> topLevelDies)
        {
            if (topLevelDies.Count == 0)
            {
                return DwarfLanguage.Unknown;
            }
            DwarfAttributeValue? value =
                topLevelDies[0].TryGetAttribute(DwarfAttribute.Language);
            if (value is DwarfIntegerAttributeValue integerValue)
            {
                return (DwarfLanguage)(int)integerValue.Value;
            }
            return DwarfLanguage.Unknown;
        }

        private readonly struct CompilationUnitLength
        {
            public CompilationUnitLength(
                int contentsLength, DwarfUnitFormat unitFormat, int headerPrefixBytes)
            {
                ContentsLength = contentsLength;
                UnitFormat = unitFormat;
                HeaderPrefixBytes = headerPrefixBytes;
            }

            public int ContentsLength { get; }

            public DwarfUnitFormat UnitFormat { get; }

            public int HeaderPrefixBytes { get; }
        }
    }
}
