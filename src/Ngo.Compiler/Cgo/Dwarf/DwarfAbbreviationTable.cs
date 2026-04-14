// -----------------------------------------------------------------------
// <copyright file="DwarfAbbreviationTable.cs" company="Ziad">
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
    /// A parsed abbreviation table from <c>.debug_abbrev</c>, indexed
    /// by the positive <see cref="DwarfAbbreviation.Code"/> that
    /// DIEs reference. Compilers emit one abbreviation table per
    /// compilation unit; the compilation-unit header carries the
    /// offset of the table it uses, so one <c>.debug_abbrev</c>
    /// section can contain many disjoint tables back-to-back.
    ///
    /// <see cref="Parse"/> reads a single table starting at the
    /// given offset and stops at the first abbreviation code of
    /// zero (the table terminator). Subsequent tables are parsed
    /// by a separate <see cref="Parse"/> call at the later offset.
    /// </summary>
    public sealed class DwarfAbbreviationTable
    {
        private const string DebugAbbrevSectionName = ".debug_abbrev";

        private readonly Dictionary<int, DwarfAbbreviation> _abbreviationsByCode;

        public DwarfAbbreviationTable(
            int offsetInSection, IReadOnlyDictionary<int, DwarfAbbreviation> abbreviationsByCode)
        {
            if (abbreviationsByCode == null)
            {
                throw new ArgumentNullException(nameof(abbreviationsByCode));
            }

            OffsetInSection = offsetInSection;
            _abbreviationsByCode = new Dictionary<int, DwarfAbbreviation>(abbreviationsByCode.Count);
            foreach (KeyValuePair<int, DwarfAbbreviation> entry in abbreviationsByCode)
            {
                _abbreviationsByCode[entry.Key] = entry.Value;
            }
        }

        /// <summary>
        /// Byte offset of the table's first abbreviation inside
        /// <c>.debug_abbrev</c>. Callers use this to cross-check the
        /// CU header's <c>debug_abbrev_offset</c> value.
        /// </summary>
        public int OffsetInSection { get; }

        public IReadOnlyDictionary<int, DwarfAbbreviation> AbbreviationsByCode
        {
            get { return _abbreviationsByCode; }
        }

        /// <summary>
        /// Look up the abbreviation a DIE references. Throws
        /// <see cref="DwarfParseException"/> when the code is not
        /// present — per the Layer 3 hardening list, an unknown
        /// abbreviation code must terminate the DIE walk rather
        /// than silently truncate the sibling chain.
        /// </summary>
        public DwarfAbbreviation Get(int code, string requestingSectionName, int requestingOffset)
        {
            if (_abbreviationsByCode.TryGetValue(code, out DwarfAbbreviation? abbreviation))
            {
                return abbreviation;
            }
            throw new DwarfParseException(
                "Abbreviation code " + code +
                " not defined in the abbreviation table at " +
                DebugAbbrevSectionName + "@" + OffsetInSection + ".",
                requestingSectionName,
                requestingOffset);
        }

        public static DwarfAbbreviationTable Parse(byte[] debugAbbrevSection, int offset)
        {
            if (debugAbbrevSection == null)
            {
                throw new ArgumentNullException(nameof(debugAbbrevSection));
            }
            if (offset < 0 || offset > debugAbbrevSection.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    "Abbreviation-table offset " + offset +
                    " is outside the .debug_abbrev section (" +
                    debugAbbrevSection.Length + " bytes).");
            }

            BinaryReaderLittleEndian reader = new(debugAbbrevSection, offset);
            Dictionary<int, DwarfAbbreviation> abbreviationsByCode = new();

            while (true)
            {
                int abbreviationStartOffset = reader.Position;
                int code = ReadAbbreviationCode(reader, abbreviationStartOffset);
                if (code == 0)
                {
                    break;
                }

                DwarfTag tag = ReadTag(reader, abbreviationStartOffset);
                bool hasChildren = ReadHasChildrenFlag(reader, abbreviationStartOffset);
                List<DwarfAbbreviationAttribute> attributes = ReadAttributeSpecs(
                    reader, abbreviationStartOffset);

                DwarfAbbreviation abbreviation = new(code, tag, hasChildren, attributes);

                if (abbreviationsByCode.ContainsKey(code))
                {
                    throw new DwarfParseException(
                        "Duplicate abbreviation code " + code +
                        " in abbreviation table starting at offset " + offset + ".",
                        DebugAbbrevSectionName,
                        abbreviationStartOffset);
                }
                abbreviationsByCode[code] = abbreviation;
            }

            return new DwarfAbbreviationTable(offset, abbreviationsByCode);
        }

        private static int ReadAbbreviationCode(BinaryReaderLittleEndian reader, int startOffset)
        {
            try
            {
                return reader.ReadUnsignedLeb128AsInt32();
            }
            catch (BinaryReadException truncatedOrTooLarge)
            {
                throw new DwarfParseException(
                    "Failed to read abbreviation code: " + truncatedOrTooLarge.Message,
                    DebugAbbrevSectionName,
                    startOffset,
                    truncatedOrTooLarge);
            }
        }

        private static DwarfTag ReadTag(BinaryReaderLittleEndian reader, int startOffset)
        {
            int tagCode;
            try
            {
                tagCode = reader.ReadUnsignedLeb128AsInt32();
            }
            catch (BinaryReadException truncatedOrTooLarge)
            {
                throw new DwarfParseException(
                    "Failed to read abbreviation tag: " + truncatedOrTooLarge.Message,
                    DebugAbbrevSectionName,
                    startOffset,
                    truncatedOrTooLarge);
            }
            return (DwarfTag)tagCode;
        }

        private static bool ReadHasChildrenFlag(BinaryReaderLittleEndian reader, int startOffset)
        {
            byte flagByte;
            try
            {
                flagByte = reader.ReadU8();
            }
            catch (BinaryReadException truncated)
            {
                throw new DwarfParseException(
                    "Failed to read abbreviation has-children flag: " + truncated.Message,
                    DebugAbbrevSectionName,
                    startOffset,
                    truncated);
            }

            if (flagByte != 0 && flagByte != 1)
            {
                throw new DwarfParseException(
                    "Abbreviation has-children flag must be 0 or 1; got 0x" +
                    flagByte.ToString("X2") + ".",
                    DebugAbbrevSectionName,
                    reader.Position - 1);
            }
            return flagByte == 1;
        }

        private static List<DwarfAbbreviationAttribute> ReadAttributeSpecs(
            BinaryReaderLittleEndian reader, int startOffset)
        {
            List<DwarfAbbreviationAttribute> attributes = new();

            while (true)
            {
                int attributeSpecStartOffset = reader.Position;
                int attributeCode;
                int formCode;
                try
                {
                    attributeCode = reader.ReadUnsignedLeb128AsInt32();
                    formCode = reader.ReadUnsignedLeb128AsInt32();
                }
                catch (BinaryReadException readFailure)
                {
                    throw new DwarfParseException(
                        "Failed to read abbreviation attribute spec: " + readFailure.Message,
                        DebugAbbrevSectionName,
                        attributeSpecStartOffset,
                        readFailure);
                }

                if (attributeCode == 0 && formCode == 0)
                {
                    return attributes;
                }

                if (attributeCode == 0 || formCode == 0)
                {
                    throw new DwarfParseException(
                        "Half-null abbreviation attribute spec (attribute=" + attributeCode +
                        ", form=" + formCode + "); both must be zero to terminate or both non-zero.",
                        DebugAbbrevSectionName,
                        attributeSpecStartOffset);
                }

                DwarfAttribute attribute = (DwarfAttribute)attributeCode;
                DwarfForm form = (DwarfForm)formCode;
                long implicitConstValue = 0;

                if (form == DwarfForm.ImplicitConst)
                {
                    try
                    {
                        implicitConstValue = reader.ReadSignedLeb128();
                    }
                    catch (BinaryReadException readFailure)
                    {
                        throw new DwarfParseException(
                            "Failed to read DW_FORM_implicit_const value: " + readFailure.Message,
                            DebugAbbrevSectionName,
                            attributeSpecStartOffset,
                            readFailure);
                    }
                }

                attributes.Add(new DwarfAbbreviationAttribute(attribute, form, implicitConstValue));
            }
        }
    }
}
