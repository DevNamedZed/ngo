// -----------------------------------------------------------------------
// <copyright file="SyntheticElf64Builder.cs" company="Ziad">
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
using System.Text;

namespace Ngo.Compiler.Tests.Cgo.ObjectFile;

/// <summary>
/// Builds a minimal valid ELF64 little-endian byte stream for unit
/// tests against <c>ElfObjectFileReader</c>. Users append sections
/// one at a time, then call <see cref="Build"/>. The builder owns
/// the file layout: header at offset 0, section data packed
/// sequentially, the <c>.shstrtab</c> after user sections, and the
/// section header table at the end. Layout constants match the
/// reader's expectations exactly so a builder bug cannot mask a
/// reader bug.
///
/// Beyond the simple <see cref="AddProgBitsSection"/> and
/// <see cref="AddNoBitsSection"/> helpers, the builder also supports
/// wiring up symbol tables (<see cref="AddSymbolTable"/>) and RELA
/// relocation sections (<see cref="AddRelaSection"/>) with correct
/// <c>sh_link</c> and <c>sh_info</c> cross-references resolved at
/// <see cref="Build"/> time.
/// </summary>
public sealed class SyntheticElf64Builder
{
    private const byte ElfClass64 = 2;
    private const byte ElfDataLittleEndian = 1;
    private const byte ElfVersionCurrent = 1;
    private const ushort Elf64HeaderSize = 64;
    private const ushort Elf64SectionHeaderSize = 64;
    private const int Elf64SymbolEntrySize = 24;
    private const int Elf64RelaEntrySize = 24;

    private const uint SectionTypeProgBits = 1;
    private const uint SectionTypeSymbolTable = 2;
    private const uint SectionTypeStringTable = 3;
    private const uint SectionTypeRela = 4;
    private const uint SectionTypeNoBits = 8;

    private readonly List<PendingSection> _userSections = new();

    public SyntheticElf64Builder AddProgBitsSection(string name, byte[] data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        _userSections.Add(new PendingSection(
            kind: PendingSectionKind.ProgBits,
            name: name,
            type: SectionTypeProgBits,
            inlineData: data,
            noBitsSize: 0,
            linkedSectionName: null,
            targetSectionName: null,
            entrySize: 0,
            symbols: null,
            symbolNameOffsets: null,
            relocations: null));
        return this;
    }

    public SyntheticElf64Builder AddNoBitsSection(string name, ulong sizeInBytes)
    {
        _userSections.Add(new PendingSection(
            kind: PendingSectionKind.NoBits,
            name: name,
            type: SectionTypeNoBits,
            inlineData: Array.Empty<byte>(),
            noBitsSize: sizeInBytes,
            linkedSectionName: null,
            targetSectionName: null,
            entrySize: 0,
            symbols: null,
            symbolNameOffsets: null,
            relocations: null));
        return this;
    }

    /// <summary>
    /// Add a string-table section whose contents are determined by
    /// the builder from the paired symbol list, and a matching
    /// symbol-table section. The symbol table's <c>sh_link</c> is set
    /// to the string table's final section index.
    /// </summary>
    public SyntheticElf64Builder AddSymbolTable(
        string symbolTableName,
        string stringTableName,
        IReadOnlyList<SynthesizedElfSymbol> symbols)
    {
        if (symbolTableName == null)
        {
            throw new ArgumentNullException(nameof(symbolTableName));
        }
        if (stringTableName == null)
        {
            throw new ArgumentNullException(nameof(stringTableName));
        }
        if (symbols == null)
        {
            throw new ArgumentNullException(nameof(symbols));
        }

        byte[] stringTableBytes = BuildSymbolStringTableBytes(
            symbols, out Dictionary<string, uint> nameOffsets);

        _userSections.Add(new PendingSection(
            kind: PendingSectionKind.SymbolStringTable,
            name: stringTableName,
            type: SectionTypeStringTable,
            inlineData: stringTableBytes,
            noBitsSize: 0,
            linkedSectionName: null,
            targetSectionName: null,
            entrySize: 0,
            symbols: null,
            symbolNameOffsets: null,
            relocations: null));

        _userSections.Add(new PendingSection(
            kind: PendingSectionKind.SymbolTable,
            name: symbolTableName,
            type: SectionTypeSymbolTable,
            inlineData: null,
            noBitsSize: 0,
            linkedSectionName: stringTableName,
            targetSectionName: null,
            entrySize: Elf64SymbolEntrySize,
            symbols: symbols,
            symbolNameOffsets: nameOffsets,
            relocations: null));
        return this;
    }

    /// <summary>
    /// Add a <c>.rela.*</c> section whose <c>sh_info</c> points to
    /// <paramref name="targetSectionName"/> and whose <c>sh_link</c>
    /// points to <paramref name="symbolTableName"/>. Both sections
    /// must already have been added to the builder (usually earlier
    /// in the chain) so <see cref="Build"/> can resolve them.
    /// </summary>
    public SyntheticElf64Builder AddRelaSection(
        string relaSectionName,
        string targetSectionName,
        string symbolTableName,
        IReadOnlyList<SynthesizedElfRelocation> relocations)
    {
        if (relaSectionName == null)
        {
            throw new ArgumentNullException(nameof(relaSectionName));
        }
        if (targetSectionName == null)
        {
            throw new ArgumentNullException(nameof(targetSectionName));
        }
        if (symbolTableName == null)
        {
            throw new ArgumentNullException(nameof(symbolTableName));
        }
        if (relocations == null)
        {
            throw new ArgumentNullException(nameof(relocations));
        }

        _userSections.Add(new PendingSection(
            kind: PendingSectionKind.RelaTable,
            name: relaSectionName,
            type: SectionTypeRela,
            inlineData: null,
            noBitsSize: 0,
            linkedSectionName: symbolTableName,
            targetSectionName: targetSectionName,
            entrySize: Elf64RelaEntrySize,
            symbols: null,
            symbolNameOffsets: null,
            relocations: relocations));
        return this;
    }

    public byte[] Build()
    {
        Dictionary<string, int> sectionIndexByName = new(StringComparer.Ordinal);
        sectionIndexByName[string.Empty] = 0;
        for (int userIndex = 0; userIndex < _userSections.Count; userIndex++)
        {
            PendingSection pending = _userSections[userIndex];
            if (sectionIndexByName.ContainsKey(pending.Name))
            {
                throw new InvalidOperationException(
                    "Duplicate section name '" + pending.Name + "' in synthetic builder.");
            }
            sectionIndexByName[pending.Name] = userIndex + 1;
        }
        int stringTableSectionIndex = _userSections.Count + 1;
        sectionIndexByName[".shstrtab"] = stringTableSectionIndex;

        byte[][] serializedSectionBytes = new byte[_userSections.Count][];
        for (int userIndex = 0; userIndex < _userSections.Count; userIndex++)
        {
            serializedSectionBytes[userIndex] = SerializePendingSection(
                _userSections[userIndex], sectionIndexByName);
        }

        byte[] sectionNameStringTableBytes = BuildSectionNameStringTable(
            out Dictionary<string, uint> sectionNameOffsets);

        List<SectionPlacement> placements = new();
        int runningFileOffset = Elf64HeaderSize;

        placements.Add(new SectionPlacement(
            name: string.Empty,
            type: 0,
            fileOffset: 0,
            sizeInBytes: 0,
            link: 0,
            info: 0,
            entrySize: 0,
            data: Array.Empty<byte>()));

        for (int userIndex = 0; userIndex < _userSections.Count; userIndex++)
        {
            PendingSection pending = _userSections[userIndex];
            byte[] data = serializedSectionBytes[userIndex];

            uint link = pending.LinkedSectionName == null
                ? 0u
                : (uint)sectionIndexByName[pending.LinkedSectionName];
            uint info = pending.TargetSectionName == null
                ? 0u
                : (uint)sectionIndexByName[pending.TargetSectionName];

            if (pending.Kind == PendingSectionKind.NoBits)
            {
                placements.Add(new SectionPlacement(
                    name: pending.Name,
                    type: pending.Type,
                    fileOffset: 0,
                    sizeInBytes: pending.NoBitsSize,
                    link: link,
                    info: info,
                    entrySize: pending.EntrySize,
                    data: Array.Empty<byte>()));
                continue;
            }

            placements.Add(new SectionPlacement(
                name: pending.Name,
                type: pending.Type,
                fileOffset: (ulong)runningFileOffset,
                sizeInBytes: (ulong)data.Length,
                link: link,
                info: info,
                entrySize: pending.EntrySize,
                data: data));
            runningFileOffset += data.Length;
        }

        int sectionNameStringTableFileOffset = runningFileOffset;
        runningFileOffset += sectionNameStringTableBytes.Length;

        placements.Add(new SectionPlacement(
            name: ".shstrtab",
            type: SectionTypeStringTable,
            fileOffset: (ulong)sectionNameStringTableFileOffset,
            sizeInBytes: (ulong)sectionNameStringTableBytes.Length,
            link: 0,
            info: 0,
            entrySize: 0,
            data: sectionNameStringTableBytes));

        int sectionHeaderTableOffset = runningFileOffset;
        int sectionHeaderCount = placements.Count;
        int totalFileSize = sectionHeaderTableOffset + sectionHeaderCount * Elf64SectionHeaderSize;

        byte[] output = new byte[totalFileSize];

        WriteElfHeader(
            output,
            sectionHeaderTableOffset: (ulong)sectionHeaderTableOffset,
            sectionHeaderCount: (ushort)sectionHeaderCount,
            stringTableSectionIndex: (ushort)stringTableSectionIndex);

        foreach (SectionPlacement placement in placements)
        {
            if (placement.Type == SectionTypeNoBits || placement.Data.Length == 0)
            {
                continue;
            }
            Array.Copy(placement.Data, 0, output, (int)placement.FileOffset, placement.Data.Length);
        }

        for (int sectionIndex = 0; sectionIndex < sectionHeaderCount; sectionIndex++)
        {
            SectionPlacement placement = placements[sectionIndex];
            uint nameOffset = sectionIndex == 0
                ? 0
                : sectionNameOffsets[placement.Name];
            int headerOffset = sectionHeaderTableOffset + sectionIndex * Elf64SectionHeaderSize;
            WriteSectionHeader(
                output,
                offset: headerOffset,
                nameOffset: nameOffset,
                type: placement.Type,
                fileOffset: placement.FileOffset,
                sizeInBytes: placement.SizeInBytes,
                link: placement.Link,
                info: placement.Info,
                entrySize: placement.EntrySize);
        }

        return output;
    }

    public string WriteToTempFile()
    {
        byte[] bytes = Build();
        string path = Path.Combine(Path.GetTempPath(), "ngo-elf-" + Guid.NewGuid().ToString("N") + ".o");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private byte[] SerializePendingSection(
        PendingSection pending, IReadOnlyDictionary<string, int> sectionIndexByName)
    {
        switch (pending.Kind)
        {
            case PendingSectionKind.ProgBits:
            case PendingSectionKind.SymbolStringTable:
            {
                return pending.InlineData ?? Array.Empty<byte>();
            }
            case PendingSectionKind.NoBits:
            {
                return Array.Empty<byte>();
            }
            case PendingSectionKind.SymbolTable:
            {
                return SerializeSymbolTable(pending, sectionIndexByName);
            }
            case PendingSectionKind.RelaTable:
            {
                return SerializeRelocationSection(pending);
            }
            default:
            {
                throw new InvalidOperationException(
                    "Unknown pending section kind " + pending.Kind + ".");
            }
        }
    }

    private static byte[] SerializeSymbolTable(
        PendingSection pending, IReadOnlyDictionary<string, int> sectionIndexByName)
    {
        IReadOnlyList<SynthesizedElfSymbol> symbols = pending.Symbols!;
        IReadOnlyDictionary<string, uint> nameOffsets = pending.SymbolNameOffsets!;

        byte[] output = new byte[symbols.Count * Elf64SymbolEntrySize];
        for (int symbolIndex = 0; symbolIndex < symbols.Count; symbolIndex++)
        {
            SynthesizedElfSymbol symbol = symbols[symbolIndex];
            int entryOffset = symbolIndex * Elf64SymbolEntrySize;

            uint nameOffsetValue = symbol.Name.Length == 0
                ? 0u
                : nameOffsets[symbol.Name];
            ushort definingSectionIndex = symbol.DefiningSectionName == null
                ? (ushort)0
                : (ushort)sectionIndexByName[symbol.DefiningSectionName];

            WriteU32(output, entryOffset + 0, value: nameOffsetValue);       // st_name
            output[entryOffset + 4] = symbol.Info;                           // st_info
            output[entryOffset + 5] = 0;                                     // st_other
            WriteU16(output, entryOffset + 6, value: definingSectionIndex);  // st_shndx
            WriteU64(output, entryOffset + 8, value: symbol.Value);          // st_value
            WriteU64(output, entryOffset + 16, value: symbol.Size);          // st_size
        }
        return output;
    }

    private static byte[] SerializeRelocationSection(PendingSection pending)
    {
        IReadOnlyList<SynthesizedElfRelocation> relocations = pending.Relocations!;
        byte[] output = new byte[relocations.Count * Elf64RelaEntrySize];
        for (int relocationIndex = 0; relocationIndex < relocations.Count; relocationIndex++)
        {
            SynthesizedElfRelocation relocation = relocations[relocationIndex];
            int entryOffset = relocationIndex * Elf64RelaEntrySize;

            ulong info = ((ulong)relocation.SymbolIndex << 32) | relocation.RelocationType;

            WriteU64(output, entryOffset + 0, value: relocation.OffsetInTargetSection);
            WriteU64(output, entryOffset + 8, value: info);
            WriteU64(output, entryOffset + 16, value: unchecked((ulong)relocation.Addend));
        }
        return output;
    }

    private static byte[] BuildSymbolStringTableBytes(
        IReadOnlyList<SynthesizedElfSymbol> symbols,
        out Dictionary<string, uint> nameOffsets)
    {
        nameOffsets = new Dictionary<string, uint>(StringComparer.Ordinal);
        using MemoryStream buffer = new();
        buffer.WriteByte(0);

        foreach (SynthesizedElfSymbol symbol in symbols)
        {
            if (symbol.Name.Length == 0 || nameOffsets.ContainsKey(symbol.Name))
            {
                continue;
            }
            nameOffsets[symbol.Name] = (uint)buffer.Position;
            byte[] utf8 = Encoding.UTF8.GetBytes(symbol.Name);
            buffer.Write(utf8, 0, utf8.Length);
            buffer.WriteByte(0);
        }
        return buffer.ToArray();
    }

    private byte[] BuildSectionNameStringTable(out Dictionary<string, uint> nameOffsets)
    {
        nameOffsets = new Dictionary<string, uint>(StringComparer.Ordinal);
        using MemoryStream buffer = new();

        buffer.WriteByte(0);

        foreach (PendingSection pending in _userSections)
        {
            if (!nameOffsets.ContainsKey(pending.Name))
            {
                nameOffsets[pending.Name] = (uint)buffer.Position;
                byte[] utf8 = Encoding.UTF8.GetBytes(pending.Name);
                buffer.Write(utf8, 0, utf8.Length);
                buffer.WriteByte(0);
            }
        }

        nameOffsets[".shstrtab"] = (uint)buffer.Position;
        byte[] stringTableNameBytes = Encoding.UTF8.GetBytes(".shstrtab");
        buffer.Write(stringTableNameBytes, 0, stringTableNameBytes.Length);
        buffer.WriteByte(0);

        return buffer.ToArray();
    }

    private static void WriteElfHeader(
        byte[] output,
        ulong sectionHeaderTableOffset,
        ushort sectionHeaderCount,
        ushort stringTableSectionIndex)
    {
        output[0] = 0x7F;
        output[1] = (byte)'E';
        output[2] = (byte)'L';
        output[3] = (byte)'F';
        output[4] = ElfClass64;
        output[5] = ElfDataLittleEndian;
        output[6] = ElfVersionCurrent;
        WriteU16(output, 16, value: 1);                                           // e_type = ET_REL
        WriteU16(output, 18, value: 0x3E);                                        // e_machine = EM_X86_64
        WriteU32(output, 20, value: 1);                                           // e_version
        WriteU64(output, 24, value: 0);                                           // e_entry
        WriteU64(output, 32, value: 0);                                           // e_phoff
        WriteU64(output, 40, value: sectionHeaderTableOffset);                    // e_shoff
        WriteU32(output, 48, value: 0);                                           // e_flags
        WriteU16(output, 52, value: Elf64HeaderSize);                             // e_ehsize
        WriteU16(output, 54, value: 0);                                           // e_phentsize
        WriteU16(output, 56, value: 0);                                           // e_phnum
        WriteU16(output, 58, value: Elf64SectionHeaderSize);                      // e_shentsize
        WriteU16(output, 60, value: sectionHeaderCount);                          // e_shnum
        WriteU16(output, 62, value: stringTableSectionIndex);                     // e_shstrndx
    }

    private static void WriteSectionHeader(
        byte[] output,
        int offset,
        uint nameOffset,
        uint type,
        ulong fileOffset,
        ulong sizeInBytes,
        uint link,
        uint info,
        ulong entrySize)
    {
        WriteU32(output, offset + 0, value: nameOffset);
        WriteU32(output, offset + 4, value: type);
        WriteU64(output, offset + 8, value: 0);                                   // sh_flags
        WriteU64(output, offset + 16, value: 0);                                  // sh_addr
        WriteU64(output, offset + 24, value: fileOffset);                         // sh_offset
        WriteU64(output, offset + 32, value: sizeInBytes);                        // sh_size
        WriteU32(output, offset + 40, value: link);                               // sh_link
        WriteU32(output, offset + 44, value: info);                               // sh_info
        WriteU64(output, offset + 48, value: 0);                                  // sh_addralign
        WriteU64(output, offset + 56, value: entrySize);                          // sh_entsize
    }

    public static void WriteU16(byte[] output, int offset, ushort value)
    {
        output[offset + 0] = (byte)(value & 0xFF);
        output[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    public static void WriteU32(byte[] output, int offset, uint value)
    {
        output[offset + 0] = (byte)(value & 0xFF);
        output[offset + 1] = (byte)((value >> 8) & 0xFF);
        output[offset + 2] = (byte)((value >> 16) & 0xFF);
        output[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    public static void WriteU64(byte[] output, int offset, ulong value)
    {
        output[offset + 0] = (byte)(value & 0xFF);
        output[offset + 1] = (byte)((value >> 8) & 0xFF);
        output[offset + 2] = (byte)((value >> 16) & 0xFF);
        output[offset + 3] = (byte)((value >> 24) & 0xFF);
        output[offset + 4] = (byte)((value >> 32) & 0xFF);
        output[offset + 5] = (byte)((value >> 40) & 0xFF);
        output[offset + 6] = (byte)((value >> 48) & 0xFF);
        output[offset + 7] = (byte)((value >> 56) & 0xFF);
    }

    private enum PendingSectionKind
    {
        ProgBits,
        NoBits,
        SymbolStringTable,
        SymbolTable,
        RelaTable,
    }

    private sealed class PendingSection
    {
        public PendingSection(
            PendingSectionKind kind,
            string name,
            uint type,
            byte[]? inlineData,
            ulong noBitsSize,
            string? linkedSectionName,
            string? targetSectionName,
            ulong entrySize,
            IReadOnlyList<SynthesizedElfSymbol>? symbols,
            IReadOnlyDictionary<string, uint>? symbolNameOffsets,
            IReadOnlyList<SynthesizedElfRelocation>? relocations)
        {
            Kind = kind;
            Name = name;
            Type = type;
            InlineData = inlineData;
            NoBitsSize = noBitsSize;
            LinkedSectionName = linkedSectionName;
            TargetSectionName = targetSectionName;
            EntrySize = entrySize;
            Symbols = symbols;
            SymbolNameOffsets = symbolNameOffsets;
            Relocations = relocations;
        }

        public PendingSectionKind Kind { get; }

        public string Name { get; }

        public uint Type { get; }

        public byte[]? InlineData { get; }

        public ulong NoBitsSize { get; }

        public string? LinkedSectionName { get; }

        public string? TargetSectionName { get; }

        public ulong EntrySize { get; }

        public IReadOnlyList<SynthesizedElfSymbol>? Symbols { get; }

        public IReadOnlyDictionary<string, uint>? SymbolNameOffsets { get; }

        public IReadOnlyList<SynthesizedElfRelocation>? Relocations { get; }
    }

    private sealed class SectionPlacement
    {
        public SectionPlacement(
            string name,
            uint type,
            ulong fileOffset,
            ulong sizeInBytes,
            uint link,
            uint info,
            ulong entrySize,
            byte[] data)
        {
            Name = name;
            Type = type;
            FileOffset = fileOffset;
            SizeInBytes = sizeInBytes;
            Link = link;
            Info = info;
            EntrySize = entrySize;
            Data = data;
        }

        public string Name { get; }

        public uint Type { get; }

        public ulong FileOffset { get; }

        public ulong SizeInBytes { get; }

        public uint Link { get; }

        public uint Info { get; }

        public ulong EntrySize { get; }

        public byte[] Data { get; }
    }
}
