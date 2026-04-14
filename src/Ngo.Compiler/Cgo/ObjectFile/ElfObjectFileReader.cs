// -----------------------------------------------------------------------
// <copyright file="ElfObjectFileReader.cs" company="Ziad">
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
using System.IO;
using Ngo.Compiler.Cgo.Binary;

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// Reads debug sections out of an ELF64 little-endian object file.
    /// Anchor probes produced by <c>gcc -c -g</c> on Linux and FreeBSD
    /// land here. ELF32, big-endian ELF, and non-ELF containers are
    /// rejected with <see cref="ObjectFileException"/> rather than
    /// being silently half-read.
    ///
    /// The reader returns every section whose name begins with
    /// <c>.debug_</c>. Before handing each section's bytes to the
    /// DWARF reader, it applies any matching <c>.rela.debug_*</c>
    /// relocation section in place — gcc and clang leave DWARF
    /// cross-reference offsets zeroed in unlinked <c>.o</c> files and
    /// supply their real values through RELA companion sections.
    /// Without the applier the DWARF layer reads zeros everywhere
    /// (every string name resolves to the first entry of
    /// <c>.debug_str</c>) and returns nonsense.
    /// </summary>
    public sealed class ElfObjectFileReader : IObjectFileReader
    {
        private const byte ElfClass32 = 1;
        private const byte ElfClass64 = 2;
        private const byte ElfDataLittleEndian = 1;
        private const byte ElfDataBigEndian = 2;
        private const byte ElfVersionCurrent = 1;

        private const int Elf64HeaderSize = 64;
        private const int Elf64SectionHeaderSize = 64;
        private const int Elf64SymbolEntrySize = 24;
        private const int Elf64RelaEntrySize = 24;

        private const uint SectionTypeNull = 0;
        private const uint SectionTypeSymbolTable = 2;
        private const uint SectionTypeStringTable = 3;
        private const uint SectionTypeRela = 4;
        private const uint SectionTypeNoBits = 8;

        private const string DebugSectionNamePrefix = ".debug_";

        public ObjectFileContents Read(string objectFilePath)
        {
            if (objectFilePath == null)
            {
                throw new ArgumentNullException(nameof(objectFilePath));
            }

            byte[] fileBytes;
            try
            {
                fileBytes = File.ReadAllBytes(objectFilePath);
            }
            catch (IOException ioException)
            {
                throw new ObjectFileException(
                    "Failed to read object file bytes: " + ioException.Message,
                    objectFilePath,
                    ioException);
            }

            ValidateElfIdentification(fileBytes, objectFilePath);

            BinaryReaderLittleEndian headerReader = new(fileBytes);
            headerReader.Skip(16);
            headerReader.ReadU16();                                           // e_type
            headerReader.ReadU16();                                           // e_machine
            headerReader.ReadU32();                                           // e_version
            headerReader.ReadU64();                                           // e_entry
            headerReader.ReadU64();                                           // e_phoff
            ulong sectionHeaderOffset = headerReader.ReadU64();               // e_shoff
            headerReader.ReadU32();                                           // e_flags
            ushort elfHeaderSize = headerReader.ReadU16();                    // e_ehsize
            headerReader.ReadU16();                                           // e_phentsize
            headerReader.ReadU16();                                           // e_phnum
            ushort sectionHeaderEntrySize = headerReader.ReadU16();           // e_shentsize
            ushort sectionHeaderCount = headerReader.ReadU16();               // e_shnum
            ushort sectionNameStringTableIndex = headerReader.ReadU16();      // e_shstrndx

            if (elfHeaderSize != Elf64HeaderSize)
            {
                throw new ObjectFileException(
                    "ELF64 header size is " + elfHeaderSize + "; expected " + Elf64HeaderSize + ".",
                    objectFilePath);
            }
            if (sectionHeaderEntrySize != Elf64SectionHeaderSize)
            {
                throw new ObjectFileException(
                    "ELF64 section header entry size is " + sectionHeaderEntrySize +
                    "; expected " + Elf64SectionHeaderSize + ".",
                    objectFilePath);
            }
            if (sectionHeaderCount == 0)
            {
                throw new ObjectFileException(
                    "ELF64 file has zero section headers; nothing to read.",
                    objectFilePath);
            }

            ValidateTableRangeInsideFile(
                fileBytes.Length,
                sectionHeaderOffset,
                (ulong)sectionHeaderCount * Elf64SectionHeaderSize,
                "section header table",
                objectFilePath);

            if (sectionNameStringTableIndex >= sectionHeaderCount)
            {
                throw new ObjectFileException(
                    "Section name string table index " + sectionNameStringTableIndex +
                    " is outside the section header count " + sectionHeaderCount + ".",
                    objectFilePath);
            }

            SectionHeader[] sectionHeaders = ReadSectionHeaders(
                fileBytes, sectionHeaderOffset, sectionHeaderCount, objectFilePath);

            byte[] sectionNameStringTable = LoadSectionNameStringTable(
                fileBytes, sectionHeaders[sectionNameStringTableIndex], objectFilePath);

            Dictionary<int, PreparedRelocations> relocationBundlesByTargetSectionIndex =
                CollectRelocationBundlesForDebugSections(
                    fileBytes, sectionHeaders, sectionNameStringTable, objectFilePath);

            List<DebugSection> debugSections = new();
            for (int sectionIndex = 0; sectionIndex < sectionHeaders.Length; sectionIndex++)
            {
                SectionHeader sectionHeader = sectionHeaders[sectionIndex];
                if (sectionHeader.Type == SectionTypeNull || sectionHeader.Type == SectionTypeNoBits)
                {
                    continue;
                }

                string sectionName = ReadSectionName(
                    sectionNameStringTable, sectionHeader.NameOffset, objectFilePath);

                if (!sectionName.StartsWith(DebugSectionNamePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                byte[] sectionBytes = ExtractSectionBytes(
                    fileBytes, sectionHeader, sectionName, objectFilePath);

                if (relocationBundlesByTargetSectionIndex.TryGetValue(
                        sectionIndex, out PreparedRelocations? bundle))
                {
                    ElfRelocationApplier.Apply(
                        sectionBytes,
                        sectionName,
                        bundle.Relocations,
                        bundle.SymbolTable,
                        objectFilePath);
                }

                debugSections.Add(new DebugSection(sectionName, sectionBytes));
            }

            return new ObjectFileContents(pointerSize: 8, debugSections);
        }

        private static void ValidateElfIdentification(byte[] fileBytes, string objectFilePath)
        {
            if (fileBytes.Length < Elf64HeaderSize)
            {
                throw new ObjectFileException(
                    "File is " + fileBytes.Length + " bytes; too short for an ELF64 header (" +
                    Elf64HeaderSize + " bytes).",
                    objectFilePath);
            }

            bool hasElfMagic =
                fileBytes[0] == 0x7F &&
                fileBytes[1] == (byte)'E' &&
                fileBytes[2] == (byte)'L' &&
                fileBytes[3] == (byte)'F';
            if (!hasElfMagic)
            {
                throw new ObjectFileException(
                    "File does not start with the ELF magic 0x7F 'E' 'L' 'F'; got " +
                    FormatMagicBytes(fileBytes) + ".",
                    objectFilePath);
            }

            byte elfClass = fileBytes[4];
            if (elfClass == ElfClass32)
            {
                throw new ObjectFileException(
                    "ELF32 object files are not supported in this stage; only ELF64.",
                    objectFilePath);
            }
            if (elfClass != ElfClass64)
            {
                throw new ObjectFileException(
                    "Unknown ELF class byte " + elfClass + "; expected 1 (ELF32) or 2 (ELF64).",
                    objectFilePath);
            }

            byte elfData = fileBytes[5];
            if (elfData == ElfDataBigEndian)
            {
                throw new ObjectFileException(
                    "Big-endian ELF object files are not supported in this stage; " +
                    "only little-endian.",
                    objectFilePath);
            }
            if (elfData != ElfDataLittleEndian)
            {
                throw new ObjectFileException(
                    "Unknown ELF data encoding byte " + elfData + "; expected 1 (little-endian) or 2 (big-endian).",
                    objectFilePath);
            }

            byte elfVersion = fileBytes[6];
            if (elfVersion != ElfVersionCurrent)
            {
                throw new ObjectFileException(
                    "Unknown ELF version byte " + elfVersion + "; expected " + ElfVersionCurrent + ".",
                    objectFilePath);
            }
        }

        private static SectionHeader[] ReadSectionHeaders(
            byte[] fileBytes,
            ulong sectionHeaderOffset,
            ushort sectionHeaderCount,
            string objectFilePath)
        {
            SectionHeader[] headers = new SectionHeader[sectionHeaderCount];
            BinaryReaderLittleEndian reader = new(fileBytes, checked((int)sectionHeaderOffset));

            for (int index = 0; index < sectionHeaderCount; index++)
            {
                uint nameOffset = reader.ReadU32();
                uint sectionType = reader.ReadU32();
                reader.ReadU64();                                   // sh_flags
                reader.ReadU64();                                   // sh_addr
                ulong fileOffset = reader.ReadU64();
                ulong sizeInBytes = reader.ReadU64();
                uint link = reader.ReadU32();                       // sh_link
                uint info = reader.ReadU32();                       // sh_info
                reader.ReadU64();                                   // sh_addralign
                ulong entrySize = reader.ReadU64();                 // sh_entsize

                headers[index] = new SectionHeader(
                    nameOffset, sectionType, fileOffset, sizeInBytes,
                    link, info, entrySize);

                if (sectionType != SectionTypeNoBits && sizeInBytes > 0)
                {
                    ValidateTableRangeInsideFile(
                        fileBytes.Length,
                        fileOffset,
                        sizeInBytes,
                        "section " + index + " body",
                        objectFilePath);
                }
            }

            return headers;
        }

        private static byte[] LoadSectionNameStringTable(
            byte[] fileBytes, SectionHeader stringTableHeader, string objectFilePath)
        {
            if (stringTableHeader.Type == SectionTypeNoBits)
            {
                throw new ObjectFileException(
                    "Section name string table is marked SHT_NOBITS; it must carry data.",
                    objectFilePath);
            }

            byte[] stringTable = new byte[stringTableHeader.SizeInBytes];
            if (stringTable.Length > 0)
            {
                Array.Copy(
                    fileBytes,
                    checked((int)stringTableHeader.FileOffset),
                    stringTable,
                    0,
                    stringTable.Length);
            }
            return stringTable;
        }

        private static string ReadSectionName(
            byte[] stringTable, uint nameOffset, string objectFilePath)
        {
            if (nameOffset >= stringTable.Length)
            {
                throw new ObjectFileException(
                    "Section name offset " + nameOffset +
                    " is outside the section-name string table (" + stringTable.Length + " bytes).",
                    objectFilePath);
            }

            BinaryReaderLittleEndian nameReader = new(stringTable, checked((int)nameOffset));
            try
            {
                return nameReader.ReadNullTerminatedUtf8();
            }
            catch (BinaryReadException missingTerminator)
            {
                throw new ObjectFileException(
                    "Section name at offset " + nameOffset +
                    " is not null-terminated inside the section-name string table.",
                    objectFilePath,
                    missingTerminator);
            }
        }

        private static byte[] ExtractSectionBytes(
            byte[] fileBytes, SectionHeader header, string sectionName, string objectFilePath)
        {
            int sizeInBytes = checked((int)header.SizeInBytes);
            byte[] sectionBytes = new byte[sizeInBytes];
            if (sizeInBytes > 0)
            {
                Array.Copy(
                    fileBytes,
                    checked((int)header.FileOffset),
                    sectionBytes,
                    0,
                    sizeInBytes);
            }
            return sectionBytes;
        }

        private static Dictionary<int, PreparedRelocations>
            CollectRelocationBundlesForDebugSections(
                byte[] fileBytes,
                SectionHeader[] sectionHeaders,
                byte[] sectionNameStringTable,
                string objectFilePath)
        {
            Dictionary<int, PreparedRelocations> bundles = new();
            Dictionary<int, IReadOnlyList<ElfSymbol>> symbolTablesBySectionIndex = new();

            for (int relaSectionIndex = 0;
                 relaSectionIndex < sectionHeaders.Length;
                 relaSectionIndex++)
            {
                SectionHeader relaHeader = sectionHeaders[relaSectionIndex];
                if (relaHeader.Type != SectionTypeRela)
                {
                    continue;
                }

                int targetSectionIndex = checked((int)relaHeader.Info);
                if (targetSectionIndex < 0 || targetSectionIndex >= sectionHeaders.Length)
                {
                    throw new ObjectFileException(
                        "RELA section at index " + relaSectionIndex +
                        " has sh_info = " + relaHeader.Info +
                        ", which is outside the section header range [0, " +
                        sectionHeaders.Length + ").",
                        objectFilePath);
                }

                SectionHeader targetHeader = sectionHeaders[targetSectionIndex];
                string targetSectionName = ReadSectionName(
                    sectionNameStringTable, targetHeader.NameOffset, objectFilePath);
                if (!targetSectionName.StartsWith(DebugSectionNamePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                int symbolTableSectionIndex = checked((int)relaHeader.Link);
                if (symbolTableSectionIndex < 0 || symbolTableSectionIndex >= sectionHeaders.Length)
                {
                    throw new ObjectFileException(
                        "RELA section targeting " + targetSectionName +
                        " has sh_link = " + relaHeader.Link +
                        ", which is outside the section header range [0, " +
                        sectionHeaders.Length + ").",
                        objectFilePath);
                }

                if (!symbolTablesBySectionIndex.TryGetValue(
                        symbolTableSectionIndex, out IReadOnlyList<ElfSymbol>? symbolTable))
                {
                    symbolTable = ParseSymbolTable(
                        fileBytes,
                        sectionHeaders,
                        symbolTableSectionIndex,
                        objectFilePath);
                    symbolTablesBySectionIndex[symbolTableSectionIndex] = symbolTable;
                }

                IReadOnlyList<ElfRelocation> relocations = ParseRelocationSection(
                    fileBytes, relaHeader, targetSectionName, objectFilePath);

                if (bundles.ContainsKey(targetSectionIndex))
                {
                    throw new ObjectFileException(
                        "Multiple RELA sections target section " + targetSectionName +
                        " (index " + targetSectionIndex + "); only one is supported per target.",
                        objectFilePath);
                }
                bundles[targetSectionIndex] = new PreparedRelocations(relocations, symbolTable);
            }

            return bundles;
        }

        private static IReadOnlyList<ElfRelocation> ParseRelocationSection(
            byte[] fileBytes,
            SectionHeader relaHeader,
            string targetSectionName,
            string objectFilePath)
        {
            if (relaHeader.EntrySize != (ulong)Elf64RelaEntrySize)
            {
                throw new ObjectFileException(
                    "RELA section targeting " + targetSectionName +
                    " has sh_entsize = " + relaHeader.EntrySize +
                    "; expected " + Elf64RelaEntrySize + " for ELF64 Elf64_Rela.",
                    objectFilePath);
            }
            if (relaHeader.SizeInBytes % (ulong)Elf64RelaEntrySize != 0)
            {
                throw new ObjectFileException(
                    "RELA section targeting " + targetSectionName +
                    " has size " + relaHeader.SizeInBytes +
                    " which is not a multiple of the Elf64_Rela entry size " +
                    Elf64RelaEntrySize + ".",
                    objectFilePath);
            }

            int entryCount = checked((int)(relaHeader.SizeInBytes / (ulong)Elf64RelaEntrySize));
            BinaryReaderLittleEndian reader = new(
                fileBytes, checked((int)relaHeader.FileOffset));
            List<ElfRelocation> relocations = new(entryCount);
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                ulong offsetInTargetSection = reader.ReadU64();
                ulong info = reader.ReadU64();
                long addend = unchecked((long)reader.ReadU64());

                uint relocationType = (uint)(info & 0xFFFFFFFFUL);
                uint symbolIndex = (uint)(info >> 32);

                relocations.Add(new ElfRelocation(
                    offsetInTargetSection, relocationType, symbolIndex, addend));
            }
            return relocations;
        }

        private static IReadOnlyList<ElfSymbol> ParseSymbolTable(
            byte[] fileBytes,
            SectionHeader[] sectionHeaders,
            int symbolTableSectionIndex,
            string objectFilePath)
        {
            SectionHeader symbolTableHeader = sectionHeaders[symbolTableSectionIndex];
            if (symbolTableHeader.Type != SectionTypeSymbolTable)
            {
                throw new ObjectFileException(
                    "Section at index " + symbolTableSectionIndex +
                    " referenced from a RELA sh_link has type " + symbolTableHeader.Type +
                    "; expected SHT_SYMTAB (" + SectionTypeSymbolTable + ").",
                    objectFilePath);
            }
            if (symbolTableHeader.EntrySize != (ulong)Elf64SymbolEntrySize)
            {
                throw new ObjectFileException(
                    "Symbol table at section index " + symbolTableSectionIndex +
                    " has sh_entsize = " + symbolTableHeader.EntrySize +
                    "; expected " + Elf64SymbolEntrySize + " for ELF64 Elf64_Sym.",
                    objectFilePath);
            }
            if (symbolTableHeader.SizeInBytes % (ulong)Elf64SymbolEntrySize != 0)
            {
                throw new ObjectFileException(
                    "Symbol table at section index " + symbolTableSectionIndex +
                    " has size " + symbolTableHeader.SizeInBytes +
                    " which is not a multiple of the Elf64_Sym entry size " +
                    Elf64SymbolEntrySize + ".",
                    objectFilePath);
            }

            int stringTableSectionIndex = checked((int)symbolTableHeader.Link);
            if (stringTableSectionIndex < 0 || stringTableSectionIndex >= sectionHeaders.Length)
            {
                throw new ObjectFileException(
                    "Symbol table at section index " + symbolTableSectionIndex +
                    " has sh_link = " + symbolTableHeader.Link +
                    ", which is outside the section header range [0, " +
                    sectionHeaders.Length + ").",
                    objectFilePath);
            }

            SectionHeader stringTableHeader = sectionHeaders[stringTableSectionIndex];
            if (stringTableHeader.Type != SectionTypeStringTable)
            {
                throw new ObjectFileException(
                    "Symbol string table at section index " + stringTableSectionIndex +
                    " (referenced by symbol table " + symbolTableSectionIndex +
                    ") has type " + stringTableHeader.Type +
                    "; expected SHT_STRTAB (" + SectionTypeStringTable + ").",
                    objectFilePath);
            }

            byte[] stringTableBytes = new byte[checked((int)stringTableHeader.SizeInBytes)];
            if (stringTableBytes.Length > 0)
            {
                Array.Copy(
                    fileBytes,
                    checked((int)stringTableHeader.FileOffset),
                    stringTableBytes,
                    0,
                    stringTableBytes.Length);
            }

            int entryCount = checked((int)(
                symbolTableHeader.SizeInBytes / (ulong)Elf64SymbolEntrySize));
            BinaryReaderLittleEndian reader = new(
                fileBytes, checked((int)symbolTableHeader.FileOffset));
            List<ElfSymbol> symbols = new(entryCount);
            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                uint nameOffset = reader.ReadU32();
                reader.ReadU8();                                   // st_info
                reader.ReadU8();                                   // st_other
                ushort sectionIndex = reader.ReadU16();            // st_shndx
                ulong value = reader.ReadU64();                    // st_value
                reader.ReadU64();                                  // st_size

                string name = ReadSymbolName(stringTableBytes, nameOffset, objectFilePath);
                symbols.Add(new ElfSymbol(name, value, sectionIndex));
            }
            return symbols;
        }

        private static string ReadSymbolName(
            byte[] stringTableBytes, uint nameOffset, string objectFilePath)
        {
            if (nameOffset == 0)
            {
                return string.Empty;
            }
            if (nameOffset >= stringTableBytes.Length)
            {
                throw new ObjectFileException(
                    "Symbol name offset " + nameOffset +
                    " is outside the symbol string table (" + stringTableBytes.Length + " bytes).",
                    objectFilePath);
            }

            BinaryReaderLittleEndian nameReader = new(stringTableBytes, checked((int)nameOffset));
            try
            {
                return nameReader.ReadNullTerminatedUtf8();
            }
            catch (BinaryReadException missingTerminator)
            {
                throw new ObjectFileException(
                    "Symbol name at offset " + nameOffset +
                    " is not null-terminated inside the symbol string table.",
                    objectFilePath,
                    missingTerminator);
            }
        }

        private static void ValidateTableRangeInsideFile(
            int fileLength,
            ulong rangeStart,
            ulong rangeLength,
            string rangeLabel,
            string objectFilePath)
        {
            ulong fileLengthAsUlong = (ulong)fileLength;
            ulong rangeEnd;
            try
            {
                rangeEnd = checked(rangeStart + rangeLength);
            }
            catch (OverflowException overflow)
            {
                throw new ObjectFileException(
                    rangeLabel + " range at offset " + rangeStart + " with length " +
                    rangeLength + " overflows 64-bit arithmetic.",
                    objectFilePath,
                    overflow);
            }

            if (rangeEnd > fileLengthAsUlong)
            {
                throw new ObjectFileException(
                    rangeLabel + " range [" + rangeStart + ", " + rangeEnd +
                    ") extends past end of file (" + fileLength + " bytes).",
                    objectFilePath);
            }
        }

        private static string FormatMagicBytes(byte[] fileBytes)
        {
            int byteCount = Math.Min(4, fileBytes.Length);
            string[] parts = new string[byteCount];
            for (int index = 0; index < byteCount; index++)
            {
                parts[index] = "0x" + fileBytes[index].ToString("X2");
            }
            return string.Join(" ", parts);
        }

        private readonly struct SectionHeader
        {
            public SectionHeader(
                uint nameOffset,
                uint type,
                ulong fileOffset,
                ulong sizeInBytes,
                uint link,
                uint info,
                ulong entrySize)
            {
                NameOffset = nameOffset;
                Type = type;
                FileOffset = fileOffset;
                SizeInBytes = sizeInBytes;
                Link = link;
                Info = info;
                EntrySize = entrySize;
            }

            public uint NameOffset { get; }

            public uint Type { get; }

            public ulong FileOffset { get; }

            public ulong SizeInBytes { get; }

            public uint Link { get; }

            public uint Info { get; }

            public ulong EntrySize { get; }
        }

        private sealed class PreparedRelocations
        {
            public PreparedRelocations(
                IReadOnlyList<ElfRelocation> relocations,
                IReadOnlyList<ElfSymbol> symbolTable)
            {
                Relocations = relocations;
                SymbolTable = symbolTable;
            }

            public IReadOnlyList<ElfRelocation> Relocations { get; }

            public IReadOnlyList<ElfSymbol> SymbolTable { get; }
        }
    }
}
