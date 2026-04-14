// -----------------------------------------------------------------------
// <copyright file="DwarfReaderErrorTests.cs" company="Ziad">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Dwarf;

/// <summary>
/// Error-path coverage for <see cref="DwarfReader"/>. Each test
/// constructs a specifically-malformed byte sequence and asserts
/// <see cref="DwarfParseException"/> with a message pinpointing
/// what went wrong — hardening item #8. Silent recovery or
/// default values are not acceptable anywhere in the reader, so
/// each malformation gets its own named test rather than a single
/// "parses garbage" catch-all.
/// </summary>
[TestClass]
public class DwarfReaderErrorTests
{
    [TestMethod]
    public void Header_ReservedInitialLength_Throws()
    {
        byte[] debugInfo = new SyntheticDebugInfoBuilder()
            .AppendU32(0xFFFFFFF5u)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(debugInfo, new byte[] { 0x00 }, null, null)));
        StringAssert.Contains(exception.Message, "Reserved");
    }

    [TestMethod]
    public void Header_UnsupportedVersion_Throws()
    {
        byte[] debugInfo = new SyntheticDebugInfoBuilder()
            .AppendU32(7)
            .AppendU16(3)
            .AppendU32(0)
            .AppendU8(8)
            .AppendRawBytes(new byte[7])
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(debugInfo, new byte[] { 0x00 }, null, null)));
        StringAssert.Contains(exception.Message, "Unsupported DWARF version 3");
    }

    [TestMethod]
    public void Header_Dwarf5_NonCompileUnitType_Throws()
    {
        SyntheticDebugInfoBuilder builder = new();
        builder.AppendU32(8);
        builder.AppendU16(5);
        builder.AppendU8(2);
        builder.AppendU8(8);
        builder.AppendU32(0);

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                builder.ToArray(), new byte[] { 0x00 }, null, null)));
        StringAssert.Contains(exception.Message, "unit_type");
        StringAssert.Contains(exception.Message, "0x02");
    }

    [TestMethod]
    public void Header_InvalidAddressSize_Throws()
    {
        SyntheticDebugInfoBuilder builder = new();
        builder.AppendU32(8);
        builder.AppendU16(4);
        builder.AppendU32(0);
        builder.AppendU8(2);
        builder.AppendU8(0);

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                builder.ToArray(), new byte[] { 0x00 }, null, null)));
        StringAssert.Contains(exception.Message, "address_size");
    }

    [TestMethod]
    public void Header_CuLengthPastEnd_Throws()
    {
        SyntheticDebugInfoBuilder builder = new();
        builder.AppendU32(1024);
        builder.AppendU16(4);

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                builder.ToArray(), new byte[] { 0x00 }, null, null)));
        StringAssert.Contains(exception.Message, "extend past end");
    }

    [TestMethod]
    public void Body_TruncatedAttributeValue_Throws()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Data4),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendU8(0x42);
        debugInfoBuilder.EndCompilationUnit();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, null, null)));
        StringAssert.Contains(exception.Message, "attribute value");
    }

    [TestMethod]
    public void Body_Strp_WithMissingDebugStr_Throws()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendU32(0);
        debugInfoBuilder.EndCompilationUnit();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, null, null)));
        StringAssert.Contains(exception.Message, ".debug_str");
    }

    [TestMethod]
    public void Body_Strp_WithOffsetPastDebugStrEnd_Throws()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
                })
            .AppendTableTerminator()
            .ToArray();

        byte[] debugStr = new byte[] { (byte)'a', 0x00 };
        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendU32(50);
        debugInfoBuilder.EndCompilationUnit();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, debugStr, null)));
        StringAssert.Contains(exception.Message, "outside .debug_str");
    }

    [TestMethod]
    public void Body_Strp_WithUnterminatedString_Throws()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
                })
            .AppendTableTerminator()
            .ToArray();

        byte[] debugStr = new byte[] { (byte)'h', (byte)'i' };
        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendU32(0);
        debugInfoBuilder.EndCompilationUnit();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, debugStr, null)));
        StringAssert.Contains(exception.Message, "null terminator");
    }

    [TestMethod]
    public void Body_UnknownAbbreviationCode_Throws()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(99);
        debugInfoBuilder.EndCompilationUnit();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, null, null)));
        StringAssert.Contains(exception.Message, "Abbreviation code 99");
    }

    [TestMethod]
    public void Body_RefSig8_Throws()
    {
        DwarfParseException exception = AssertUnsupportedForm(DwarfForm.RefSig8);
        StringAssert.Contains(exception.Message, "ref_sig8");
    }

    [TestMethod]
    public void Body_Strx_Throws()
    {
        DwarfParseException exception = AssertUnsupportedForm(DwarfForm.Strx);
        StringAssert.Contains(exception.Message, "Strx");
    }

    [TestMethod]
    public void Body_Addrx_Throws()
    {
        DwarfParseException exception = AssertUnsupportedForm(DwarfForm.Addrx);
        StringAssert.Contains(exception.Message, "Addrx");
    }

    [TestMethod]
    public void Body_Loclistx_Throws()
    {
        DwarfParseException exception = AssertUnsupportedForm(DwarfForm.Loclistx);
        StringAssert.Contains(exception.Message, "Loclistx");
    }

    [TestMethod]
    public void Body_UnknownForm_Throws()
    {
        DwarfParseException exception = AssertUnsupportedForm((DwarfForm)0x99);
        StringAssert.Contains(exception.Message, "Unknown DWARF form");
    }

    [TestMethod]
    public void Body_IndirectFormReferencingIndirect_Throws()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(
                        DwarfAttribute.ByteSize, DwarfForm.Indirect),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendUnsignedLeb128((ulong)DwarfForm.Indirect);
        debugInfoBuilder.EndCompilationUnit();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, null, null)));
        StringAssert.Contains(exception.Message, "DW_FORM_indirect cannot reference another");
    }

    [TestMethod]
    public void Body_IndirectFormReferencingImplicitConst_Throws()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(
                        DwarfAttribute.ByteSize, DwarfForm.Indirect),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendUnsignedLeb128((ulong)DwarfForm.ImplicitConst);
        debugInfoBuilder.EndCompilationUnit();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, null, null)));
        StringAssert.Contains(exception.Message, "DW_FORM_implicit_const");
    }

    [TestMethod]
    public void Body_ChildChainRunsPastCuEnd_Throws()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                true,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.EndCompilationUnit();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, null, null)));
        StringAssert.Contains(exception.Message, "Child DIE chain");
    }

    [TestMethod]
    public void ErrorMessages_IncludeSectionNameAndOffset()
    {
        byte[] debugInfo = new SyntheticDebugInfoBuilder()
            .AppendU32(0xFFFFFFF5u)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(debugInfo, new byte[] { 0x00 }, null, null)));
        Assert.AreEqual(".debug_info", exception.SectionName);
        Assert.AreEqual(0, exception.OffsetInSection);
        StringAssert.Contains(exception.Message, ".debug_info@0");
    }

    private static DwarfParseException AssertUnsupportedForm(DwarfForm form)
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Type, form),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.EndCompilationUnit();

        return Assert.ThrowsException<DwarfParseException>(
            () => DwarfReader.Read(new DwarfSections(
                debugInfoBuilder.ToArray(), debugAbbrev, null, null)));
    }
}
