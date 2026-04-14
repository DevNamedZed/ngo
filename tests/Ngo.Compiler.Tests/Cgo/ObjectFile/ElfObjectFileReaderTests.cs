// -----------------------------------------------------------------------
// <copyright file="ElfObjectFileReaderTests.cs" company="Ziad">
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo.ObjectFile;

namespace Ngo.Compiler.Tests.Cgo.ObjectFile;

/// <summary>
/// Unit tests for <see cref="ElfObjectFileReader"/> driven by a
/// synthetic ELF64 builder so every layout edge case is reachable
/// without invoking a C compiler. The "happy path" tests confirm
/// that section filtering keeps only <c>.debug_*</c> sections and
/// that <see cref="ObjectFileContents.PointerSize"/> is 8 for ELF64.
/// The "rejection" tests confirm that every container-level
/// malformation surfaces an <see cref="ObjectFileException"/>
/// rather than a silent parse or a lower-level crash.
/// </summary>
[TestClass]
public class ElfObjectFileReaderTests
{
    [TestMethod]
    public void Read_ElfWithSingleDebugSection_ReturnsThatSection()
    {
        byte[] debugInfoBytes = { 0x11, 0x22, 0x33, 0x44 };
        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", debugInfoBytes)
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            Assert.AreEqual(8, contents.PointerSize);
            Assert.AreEqual(1, contents.DebugSections.Count);
            Assert.AreEqual(".debug_info", contents.DebugSections[0].Name);
            CollectionAssert.AreEqual(debugInfoBytes, contents.DebugSections[0].Data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_ElfWithMultipleDebugSections_ReturnsAllInSectionOrder()
    {
        byte[] infoBytes = new byte[] { 0x01, 0x02 };
        byte[] abbrevBytes = new byte[] { 0x03, 0x04, 0x05 };
        byte[] stringTableBytes = new byte[] { 0x06 };
        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", infoBytes)
            .AddProgBitsSection(".debug_abbrev", abbrevBytes)
            .AddProgBitsSection(".debug_str", stringTableBytes)
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            string[] names = contents.DebugSections.Select(section => section.Name).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { ".debug_info", ".debug_abbrev", ".debug_str" },
                names);

            DebugSection infoSection = contents.DebugSections.Single(section => section.Name == ".debug_info");
            CollectionAssert.AreEqual(infoBytes, infoSection.Data);

            DebugSection abbrevSection = contents.DebugSections.Single(section => section.Name == ".debug_abbrev");
            CollectionAssert.AreEqual(abbrevBytes, abbrevSection.Data);

            DebugSection stringTableSection = contents.DebugSections.Single(section => section.Name == ".debug_str");
            CollectionAssert.AreEqual(stringTableBytes, stringTableSection.Data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_ElfWithNonDebugSections_FiltersDebugSectionsOnly()
    {
        byte[] textBytes = { 0x90, 0x90, 0x90 };
        byte[] dataBytes = { 0xAA, 0xBB };
        byte[] infoBytes = { 0x11 };
        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".text", textBytes)
            .AddProgBitsSection(".data", dataBytes)
            .AddProgBitsSection(".debug_info", infoBytes)
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            Assert.AreEqual(1, contents.DebugSections.Count);
            Assert.AreEqual(".debug_info", contents.DebugSections[0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_ElfWithNoDebugSections_ReturnsEmptyList()
    {
        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".text", new byte[] { 0x90 })
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            Assert.AreEqual(8, contents.PointerSize);
            Assert.AreEqual(0, contents.DebugSections.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_ElfWithDebugNoBitsSection_SkipsNoBitsEvenWhenNamedDebug()
    {
        string path = new SyntheticElf64Builder()
            .AddNoBitsSection(".debug_bss", sizeInBytes: 1024)
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            Assert.AreEqual(1, contents.DebugSections.Count);
            Assert.AreEqual(".debug_info", contents.DebugSections[0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_FileTooShortForHeader_Throws()
    {
        byte[] shortFile = new byte[32];
        shortFile[0] = 0x7F;
        shortFile[1] = (byte)'E';
        shortFile[2] = (byte)'L';
        shortFile[3] = (byte)'F';
        string path = WriteTempBytes(shortFile);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            Assert.AreEqual(path, thrown.FilePath);
            StringAssert.Contains(thrown.Message, "too short");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_MissingElfMagic_Throws()
    {
        byte[] bogus = new byte[64];
        string path = WriteTempBytes(bogus);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "ELF magic");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_ElfClass32_ThrowsWithUnsupportedMessage()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        fileBytes[4] = 1;                                             // ELFCLASS32
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "ELF32");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_UnknownElfClass_Throws()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        fileBytes[4] = 9;
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "ELF class");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_BigEndianElf_Throws()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        fileBytes[5] = 2;                                             // ELFDATA2MSB
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "Big-endian");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_UnknownElfVersion_Throws()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        fileBytes[6] = 9;
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "ELF version");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_UnexpectedHeaderSize_Throws()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        SyntheticElf64Builder.WriteU16(fileBytes, 52, 48);            // e_ehsize wrong
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "header size");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_UnexpectedSectionHeaderEntrySize_Throws()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        SyntheticElf64Builder.WriteU16(fileBytes, 58, 40);            // e_shentsize wrong
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "section header entry size");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_SectionHeaderTablePastEndOfFile_Throws()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        SyntheticElf64Builder.WriteU64(fileBytes, 40, (ulong)(fileBytes.Length + 1));
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "past end of file");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_StringTableIndexPastSectionCount_Throws()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        ushort shnum = BitConverter.ToUInt16(fileBytes, 60);
        SyntheticElf64Builder.WriteU16(fileBytes, 62, (ushort)(shnum + 5));
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "string table index");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_SectionBodyPastEndOfFile_Throws()
    {
        byte[] debugInfoBytes = new byte[] { 0x11, 0x22 };
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", debugInfoBytes)
            .Build();

        ulong sectionHeaderTableOffset = BitConverter.ToUInt64(fileBytes, 40);
        int firstUserHeaderOffset = (int)sectionHeaderTableOffset + 64;
        SyntheticElf64Builder.WriteU64(fileBytes, firstUserHeaderOffset + 32, (ulong)(fileBytes.Length + 1));

        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "past end of file");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_ZeroSectionHeaderCount_Throws()
    {
        byte[] fileBytes = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", new byte[] { 0x11 })
            .Build();
        SyntheticElf64Builder.WriteU16(fileBytes, 60, 0);
        string path = WriteTempBytes(fileBytes);

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "zero section headers");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_MissingFile_Throws()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "ngo-missing-" + Guid.NewGuid().ToString("N") + ".o");
        ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
            () => new ElfObjectFileReader().Read(missingPath));
        Assert.AreEqual(missingPath, thrown.FilePath);
    }

    [TestMethod]
    public void Read_NullPath_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new ElfObjectFileReader().Read(null!));
    }

    [TestMethod]
    public void DebugSections_NameFilterRejectsSimilarlyPrefixedSection()
    {
        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debuginfo_not_dwarf", new byte[] { 0x01 })
            .AddProgBitsSection(".debug_info", new byte[] { 0x02 })
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            Assert.AreEqual(1, contents.DebugSections.Count);
            Assert.AreEqual(".debug_info", contents.DebugSections[0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_RelaTargetingDebugInfo_ApplyPatchesSectionBytes()
    {
        byte[] debugInfoBytes = new byte[8];
        List<SynthesizedElfSymbol> symbols = new()
        {
            new SynthesizedElfSymbol(name: "", info: 0, value: 0, size: 0, definingSectionName: null),
            new SynthesizedElfSymbol(
                name: "", info: 3, value: 0, size: 0, definingSectionName: ".debug_str"),
        };
        List<SynthesizedElfRelocation> relocations = new()
        {
            new SynthesizedElfRelocation(
                offsetInTargetSection: 0,
                relocationType: 10,
                symbolIndex: 1,
                addend: 0x12345678),
        };

        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_str", new byte[] { 0x00 })
            .AddProgBitsSection(".debug_info", debugInfoBytes)
            .AddSymbolTable(".symtab", ".strtab", symbols)
            .AddRelaSection(".rela.debug_info", ".debug_info", ".symtab", relocations)
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            DebugSection debugInfo = contents.DebugSections.Single(section => section.Name == ".debug_info");
            Assert.AreEqual((byte)0x78, debugInfo.Data[0]);
            Assert.AreEqual((byte)0x56, debugInfo.Data[1]);
            Assert.AreEqual((byte)0x34, debugInfo.Data[2]);
            Assert.AreEqual((byte)0x12, debugInfo.Data[3]);
            Assert.AreEqual((byte)0x00, debugInfo.Data[4]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_RelaTargetingDebugInfo_PreservesOtherDebugSectionBytesUnrelocated()
    {
        byte[] debugInfoBytes = new byte[8];
        byte[] debugStrBytes = { 0x00, (byte)'h', (byte)'i', 0x00 };
        List<SynthesizedElfSymbol> symbols = new()
        {
            new SynthesizedElfSymbol("", 0, 0, 0, null),
            new SynthesizedElfSymbol("", 3, 0, 0, ".debug_str"),
        };
        List<SynthesizedElfRelocation> relocations = new()
        {
            new SynthesizedElfRelocation(0, 10, 1, 1),
        };

        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_str", debugStrBytes)
            .AddProgBitsSection(".debug_info", debugInfoBytes)
            .AddSymbolTable(".symtab", ".strtab", symbols)
            .AddRelaSection(".rela.debug_info", ".debug_info", ".symtab", relocations)
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            DebugSection debugStr = contents.DebugSections.Single(section => section.Name == ".debug_str");
            CollectionAssert.AreEqual(debugStrBytes, debugStr.Data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_RelaTargetingNonDebugSection_IsIgnored()
    {
        byte[] textBytes = new byte[8];
        byte[] debugInfoBytes = { 0x00, 0x00, 0x00, 0x00 };
        List<SynthesizedElfSymbol> symbols = new()
        {
            new SynthesizedElfSymbol("", 0, 0, 0, null),
            new SynthesizedElfSymbol("", 3, 0, 0, ".text"),
        };
        List<SynthesizedElfRelocation> relocations = new()
        {
            new SynthesizedElfRelocation(0, 10, 1, 0xAABBCCDD),
        };

        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".text", textBytes)
            .AddProgBitsSection(".debug_info", debugInfoBytes)
            .AddSymbolTable(".symtab", ".strtab", symbols)
            .AddRelaSection(".rela.text", ".text", ".symtab", relocations)
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            DebugSection debugInfo = contents.DebugSections.Single(section => section.Name == ".debug_info");
            CollectionAssert.AreEqual(debugInfoBytes, debugInfo.Data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_RelaWithUnsupportedType_ThrowsThroughReader()
    {
        byte[] debugInfoBytes = new byte[8];
        List<SynthesizedElfSymbol> symbols = new()
        {
            new SynthesizedElfSymbol("", 0, 0, 0, null),
            new SynthesizedElfSymbol("", 3, 0, 0, ".debug_info"),
        };
        List<SynthesizedElfRelocation> relocations = new()
        {
            new SynthesizedElfRelocation(0, relocationType: 7, symbolIndex: 1, addend: 0),
        };

        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", debugInfoBytes)
            .AddSymbolTable(".symtab", ".strtab", symbols)
            .AddRelaSection(".rela.debug_info", ".debug_info", ".symtab", relocations)
            .WriteToTempFile();

        try
        {
            ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
                () => new ElfObjectFileReader().Read(path));
            StringAssert.Contains(thrown.Message, "Unsupported");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_RelaOnSectionSymbolAndAddend_UsesAddendPlusSectionSymbolValue()
    {
        byte[] debugInfoBytes = new byte[4];
        List<SynthesizedElfSymbol> symbols = new()
        {
            new SynthesizedElfSymbol("", 0, 0, 0, null),
            new SynthesizedElfSymbol("", 3, value: 0, size: 0, definingSectionName: ".debug_str"),
        };
        List<SynthesizedElfRelocation> relocations = new()
        {
            new SynthesizedElfRelocation(
                offsetInTargetSection: 0,
                relocationType: 10,
                symbolIndex: 1,
                addend: 0x05),
        };

        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_str", new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 })
            .AddProgBitsSection(".debug_info", debugInfoBytes)
            .AddSymbolTable(".symtab", ".strtab", symbols)
            .AddRelaSection(".rela.debug_info", ".debug_info", ".symtab", relocations)
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            DebugSection debugInfo = contents.DebugSections.Single(section => section.Name == ".debug_info");
            Assert.AreEqual((byte)0x05, debugInfo.Data[0]);
            Assert.AreEqual((byte)0x00, debugInfo.Data[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Read_NoRelaSections_DebugSectionBytesUntouched()
    {
        byte[] debugInfoBytes = { 0x11, 0x22, 0x33, 0x44 };
        string path = new SyntheticElf64Builder()
            .AddProgBitsSection(".debug_info", debugInfoBytes)
            .WriteToTempFile();

        try
        {
            ObjectFileContents contents = new ElfObjectFileReader().Read(path);
            DebugSection debugInfo = contents.DebugSections.Single(section => section.Name == ".debug_info");
            CollectionAssert.AreEqual(debugInfoBytes, debugInfo.Data);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempBytes(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), "ngo-elf-" + Guid.NewGuid().ToString("N") + ".o");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
