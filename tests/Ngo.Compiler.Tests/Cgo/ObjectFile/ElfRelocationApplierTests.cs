// -----------------------------------------------------------------------
// <copyright file="ElfRelocationApplierTests.cs" company="Ziad">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo.ObjectFile;

namespace Ngo.Compiler.Tests.Cgo.ObjectFile;

/// <summary>
/// Unit tests for <see cref="ElfRelocationApplier"/> driven through
/// synthetic in-memory relocations and symbols so every x86_64
/// relocation type and every failure mode is exercised without
/// building a full ELF container. Reader-level integration tests
/// live alongside these in <see cref="ElfObjectFileReaderTests"/>.
/// </summary>
[TestClass]
public class ElfRelocationApplierTests
{
    private const uint RelocationType64 = 1;
    private const uint RelocationType32 = 10;
    private const uint RelocationType32Signed = 11;

    [TestMethod]
    public void Apply_R_X86_64_32_WritesAddendPlusSymbolValueAsLittleEndian()
    {
        byte[] target = new byte[12];
        List<ElfSymbol> symbols = new()
        {
            new ElfSymbol("", 0, 0),
            new ElfSymbol("sym1", 0x100, 1),
        };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(
                offsetInTargetSection: 4,
                relocationType: RelocationType32,
                symbolIndex: 1,
                addend: 0x42),
        };

        ElfRelocationApplier.Apply(
            target, ".debug_info", relocations, symbols, objectFilePath: "/synthetic.o");

        byte[] expected = new byte[12];
        expected[4] = 0x42;
        expected[5] = 0x01;
        expected[6] = 0x00;
        expected[7] = 0x00;
        CollectionAssert.AreEqual(expected, target);
    }

    [TestMethod]
    public void Apply_R_X86_64_64_WritesFullEightBytes()
    {
        byte[] target = new byte[16];
        List<ElfSymbol> symbols = new()
        {
            new ElfSymbol("", 0, 0),
            new ElfSymbol("sym", 0x1000000000000000UL, 1),
        };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(
                offsetInTargetSection: 0,
                relocationType: RelocationType64,
                symbolIndex: 1,
                addend: 0x20),
        };

        ElfRelocationApplier.Apply(
            target, ".debug_info", relocations, symbols, "/synthetic.o");

        byte[] expected = new byte[16];
        expected[0] = 0x20;
        expected[7] = 0x10;
        CollectionAssert.AreEqual(expected, target);
    }

    [TestMethod]
    public void Apply_R_X86_64_32S_BehavesLikeR_X86_64_32ForPositiveValues()
    {
        byte[] target = new byte[8];
        List<ElfSymbol> symbols = new() { new ElfSymbol("", 0, 0) };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(
                offsetInTargetSection: 0,
                relocationType: RelocationType32Signed,
                symbolIndex: 0,
                addend: 0x7F),
        };

        ElfRelocationApplier.Apply(
            target, ".debug_str", relocations, symbols, "/synthetic.o");

        Assert.AreEqual((byte)0x7F, target[0]);
        Assert.AreEqual((byte)0x00, target[1]);
    }

    [TestMethod]
    public void Apply_NoneType_DoesNothing()
    {
        byte[] target = { 0xAA, 0xBB, 0xCC, 0xDD };
        List<ElfSymbol> symbols = new() { new ElfSymbol("", 0, 0) };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(
                offsetInTargetSection: 0,
                relocationType: 0,
                symbolIndex: 0,
                addend: 999),
        };

        ElfRelocationApplier.Apply(
            target, ".debug_info", relocations, symbols, "/synthetic.o");

        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, target);
    }

    [TestMethod]
    public void Apply_UnsupportedRelocationType_Throws()
    {
        byte[] target = new byte[8];
        List<ElfSymbol> symbols = new() { new ElfSymbol("", 0, 0) };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(
                offsetInTargetSection: 0,
                relocationType: 42,
                symbolIndex: 0,
                addend: 0),
        };

        ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
            () => ElfRelocationApplier.Apply(
                target, ".debug_info", relocations, symbols, "/synthetic.o"));
        StringAssert.Contains(thrown.Message, "Unsupported");
        StringAssert.Contains(thrown.Message, "42");
    }

    [TestMethod]
    public void Apply_SymbolIndexOutOfRange_Throws()
    {
        byte[] target = new byte[8];
        List<ElfSymbol> symbols = new() { new ElfSymbol("", 0, 0) };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(
                offsetInTargetSection: 0,
                relocationType: RelocationType32,
                symbolIndex: 99,
                addend: 0),
        };

        ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
            () => ElfRelocationApplier.Apply(
                target, ".debug_info", relocations, symbols, "/synthetic.o"));
        StringAssert.Contains(thrown.Message, "symbol index");
        StringAssert.Contains(thrown.Message, "99");
    }

    [TestMethod]
    public void Apply_WriteBeyondSectionEnd_Throws()
    {
        byte[] target = new byte[3];
        List<ElfSymbol> symbols = new() { new ElfSymbol("", 0, 0) };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(
                offsetInTargetSection: 0,
                relocationType: RelocationType32,
                symbolIndex: 0,
                addend: 0),
        };

        ObjectFileException thrown = Assert.ThrowsException<ObjectFileException>(
            () => ElfRelocationApplier.Apply(
                target, ".debug_info", relocations, symbols, "/synthetic.o"));
        StringAssert.Contains(thrown.Message, "past end of section");
    }

    [TestMethod]
    public void Apply_MultipleRelocationsAllApplyInOrder()
    {
        byte[] target = new byte[16];
        List<ElfSymbol> symbols = new() { new ElfSymbol("", 0, 0) };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(0, RelocationType32, 0, 0x11223344),
            new ElfRelocation(4, RelocationType32, 0, 0x55667788),
            new ElfRelocation(8, RelocationType64, 0, 0x7FFFFFFFFFFFFFFF),
        };

        ElfRelocationApplier.Apply(
            target, ".debug_info", relocations, symbols, "/synthetic.o");

        Assert.AreEqual((byte)0x44, target[0]);
        Assert.AreEqual((byte)0x33, target[1]);
        Assert.AreEqual((byte)0x22, target[2]);
        Assert.AreEqual((byte)0x11, target[3]);
        Assert.AreEqual((byte)0x88, target[4]);
        Assert.AreEqual((byte)0x77, target[5]);
        Assert.AreEqual((byte)0x66, target[6]);
        Assert.AreEqual((byte)0x55, target[7]);
        Assert.AreEqual((byte)0xFF, target[8]);
        Assert.AreEqual((byte)0x7F, target[15]);
    }

    [TestMethod]
    public void Apply_SymbolIndexZero_TreatsSymbolValueAsZero()
    {
        byte[] target = new byte[4];
        List<ElfSymbol> symbols = new()
        {
            new ElfSymbol("", 0, 0),
            new ElfSymbol("unused", 0xDEADBEEF, 1),
        };
        List<ElfRelocation> relocations = new()
        {
            new ElfRelocation(
                offsetInTargetSection: 0,
                relocationType: RelocationType32,
                symbolIndex: 0,
                addend: 0x10),
        };

        ElfRelocationApplier.Apply(
            target, ".debug_info", relocations, symbols, "/synthetic.o");

        Assert.AreEqual((byte)0x10, target[0]);
        Assert.AreEqual((byte)0x00, target[1]);
    }
}
