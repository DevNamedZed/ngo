// -----------------------------------------------------------------------
// <copyright file="DwarfAbbreviationTableTests.cs" company="Ziad">
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
/// Unit tests for <see cref="DwarfAbbreviationTable"/>. The parser is
/// the narrowest layer of the DWARF reader: the rest of the parser
/// depends on its output being faithful to the encoded byte stream,
/// so these tests pin down every shape a compiler is allowed to
/// emit, every shape a compiler must not emit (half-null pairs,
/// duplicate codes, invalid has-children flags), and the
/// <see cref="DwarfParseException"/> contract on truncation. The
/// synthetic byte streams are built by
/// <see cref="SyntheticAbbreviationTableBuilder"/> so the "what the
/// encoder lays down" and "what the parser pulls back up" ends stay
/// legible side by side.
/// </summary>
[TestClass]
public class DwarfAbbreviationTableTests
{
    private const string DebugAbbrevSectionName = ".debug_abbrev";

    [TestMethod]
    public void Parse_EmptyTable_ReturnsNoAbbreviations()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        Assert.AreEqual(0, table.AbbreviationsByCode.Count);
        Assert.AreEqual(0, table.OffsetInSection);
    }

    [TestMethod]
    public void Parse_SingleAbbreviationNoChildren_RoundTripsFields()
    {
        List<SyntheticAbbreviationAttribute> attributes = new()
        {
            new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
            new SyntheticAbbreviationAttribute(DwarfAttribute.ByteSize, DwarfForm.Data1),
        };
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(1, DwarfTag.BaseType, false, attributes)
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        Assert.AreEqual(1, table.AbbreviationsByCode.Count);
        DwarfAbbreviation abbreviation = table.Get(1, DebugAbbrevSectionName, 0);
        Assert.AreEqual(1, abbreviation.Code);
        Assert.AreEqual(DwarfTag.BaseType, abbreviation.Tag);
        Assert.IsFalse(abbreviation.HasChildren);
        Assert.AreEqual(2, abbreviation.Attributes.Count);
        Assert.AreEqual(DwarfAttribute.Name, abbreviation.Attributes[0].Attribute);
        Assert.AreEqual(DwarfForm.Strp, abbreviation.Attributes[0].Form);
        Assert.AreEqual(DwarfAttribute.ByteSize, abbreviation.Attributes[1].Attribute);
        Assert.AreEqual(DwarfForm.Data1, abbreviation.Attributes[1].Form);
    }

    [TestMethod]
    public void Parse_SingleAbbreviationWithChildren_SetsFlag()
    {
        List<SyntheticAbbreviationAttribute> attributes = new()
        {
            new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
        };
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(1, DwarfTag.StructureType, true, attributes)
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        DwarfAbbreviation abbreviation = table.Get(1, DebugAbbrevSectionName, 0);
        Assert.IsTrue(abbreviation.HasChildren);
    }

    [TestMethod]
    public void Parse_AbbreviationWithNoAttributes_ReturnsEmptyAttributeList()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.Null,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        DwarfAbbreviation abbreviation = table.Get(1, DebugAbbrevSectionName, 0);
        Assert.AreEqual(0, abbreviation.Attributes.Count);
    }

    [TestMethod]
    public void Parse_TwoAbbreviations_BothPresentInTable()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                true,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Producer, DwarfForm.Strp),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Language, DwarfForm.Data2),
                })
            .AppendAbbreviation(
                2,
                DwarfTag.BaseType,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Encoding, DwarfForm.Data1),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.ByteSize, DwarfForm.Data1),
                })
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        Assert.AreEqual(2, table.AbbreviationsByCode.Count);
        Assert.AreEqual(DwarfTag.CompileUnit, table.Get(1, DebugAbbrevSectionName, 0).Tag);
        Assert.AreEqual(DwarfTag.BaseType, table.Get(2, DebugAbbrevSectionName, 0).Tag);
    }

    [TestMethod]
    public void Parse_NonSequentialCodes_IndexedByEncodedCode()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                7,
                DwarfTag.CompileUnit,
                true,
                new List<SyntheticAbbreviationAttribute>())
            .AppendAbbreviation(
                19,
                DwarfTag.StructureType,
                true,
                new List<SyntheticAbbreviationAttribute>())
            .AppendAbbreviation(
                42,
                DwarfTag.Member,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        Assert.AreEqual(3, table.AbbreviationsByCode.Count);
        Assert.AreEqual(DwarfTag.CompileUnit, table.Get(7, DebugAbbrevSectionName, 0).Tag);
        Assert.AreEqual(DwarfTag.StructureType, table.Get(19, DebugAbbrevSectionName, 0).Tag);
        Assert.AreEqual(DwarfTag.Member, table.Get(42, DebugAbbrevSectionName, 0).Tag);
    }

    [TestMethod]
    public void Parse_MultibyteLeb128Code_DecodesCorrectly()
    {
        int multiByteCode = 0x1234;
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                multiByteCode,
                DwarfTag.Variable,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        Assert.AreEqual(1, table.AbbreviationsByCode.Count);
        Assert.AreEqual(
            DwarfTag.Variable,
            table.Get(multiByteCode, DebugAbbrevSectionName, 0).Tag);
    }

    [TestMethod]
    public void Parse_ImplicitConstForm_CapturesInlineValue()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.Enumerator,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
                    new SyntheticAbbreviationAttribute(
                        DwarfAttribute.ConstValue, DwarfForm.ImplicitConst, 42),
                })
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        DwarfAbbreviation abbreviation = table.Get(1, DebugAbbrevSectionName, 0);
        Assert.AreEqual(2, abbreviation.Attributes.Count);
        Assert.AreEqual(DwarfForm.ImplicitConst, abbreviation.Attributes[1].Form);
        Assert.AreEqual(42L, abbreviation.Attributes[1].ImplicitConstValue);
        Assert.AreEqual(0L, abbreviation.Attributes[0].ImplicitConstValue);
    }

    [TestMethod]
    public void Parse_ImplicitConstForm_NegativeValue_SignExtended()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.Enumerator,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(
                        DwarfAttribute.ConstValue, DwarfForm.ImplicitConst, -1234),
                })
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        DwarfAbbreviation abbreviation = table.Get(1, DebugAbbrevSectionName, 0);
        Assert.AreEqual(-1234L, abbreviation.Attributes[0].ImplicitConstValue);
    }

    [TestMethod]
    public void Parse_AbbreviationWithManyCommonForms_DecodesEachForm()
    {
        DwarfForm[] commonForms =
        {
            DwarfForm.Addr, DwarfForm.Block2, DwarfForm.Block4,
            DwarfForm.Data2, DwarfForm.Data4, DwarfForm.Data8,
            DwarfForm.String, DwarfForm.Block, DwarfForm.Block1,
            DwarfForm.Data1, DwarfForm.Flag, DwarfForm.Sdata,
            DwarfForm.Strp, DwarfForm.Udata, DwarfForm.RefAddr,
            DwarfForm.Ref1, DwarfForm.Ref2, DwarfForm.Ref4,
            DwarfForm.Ref8, DwarfForm.RefUdata,
        };
        List<SyntheticAbbreviationAttribute> attributes = new();
        foreach (DwarfForm form in commonForms)
        {
            attributes.Add(new SyntheticAbbreviationAttribute(DwarfAttribute.Name, form));
        }
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(1, DwarfTag.Subprogram, false, attributes)
            .AppendTableTerminator()
            .ToArray();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        DwarfAbbreviation abbreviation = table.Get(1, DebugAbbrevSectionName, 0);
        Assert.AreEqual(commonForms.Length, abbreviation.Attributes.Count);
        for (int index = 0; index < commonForms.Length; index++)
        {
            Assert.AreEqual(
                commonForms[index],
                abbreviation.Attributes[index].Form,
                "attribute[" + index + "] form mismatch");
        }
    }

    [TestMethod]
    public void Parse_AtNonZeroOffset_ParsesFromOffset()
    {
        SyntheticAbbreviationTableBuilder builder = new();
        builder.AppendRawBytes(0xAA, 0xBB, 0xCC);
        int tableStart = builder.Position;
        builder
            .AppendAbbreviation(
                1,
                DwarfTag.Variable,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
                })
            .AppendTableTerminator();

        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(builder.ToArray(), tableStart);

        Assert.AreEqual(tableStart, table.OffsetInSection);
        Assert.AreEqual(DwarfTag.Variable, table.Get(1, DebugAbbrevSectionName, 0).Tag);
    }

    [TestMethod]
    public void Parse_TwoDisjointTables_EachParsedIndependently()
    {
        SyntheticAbbreviationTableBuilder builder = new();
        int firstTableOffset = builder.Position;
        builder
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                true,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
                })
            .AppendTableTerminator();
        int secondTableOffset = builder.Position;
        builder
            .AppendAbbreviation(
                1,
                DwarfTag.BaseType,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Encoding, DwarfForm.Data1),
                })
            .AppendAbbreviation(
                2,
                DwarfTag.PointerType,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Type, DwarfForm.Ref4),
                })
            .AppendTableTerminator();
        byte[] sectionBytes = builder.ToArray();

        DwarfAbbreviationTable firstTable =
            DwarfAbbreviationTable.Parse(sectionBytes, firstTableOffset);
        DwarfAbbreviationTable secondTable =
            DwarfAbbreviationTable.Parse(sectionBytes, secondTableOffset);

        Assert.AreEqual(firstTableOffset, firstTable.OffsetInSection);
        Assert.AreEqual(1, firstTable.AbbreviationsByCode.Count);
        Assert.AreEqual(
            DwarfTag.CompileUnit,
            firstTable.Get(1, DebugAbbrevSectionName, 0).Tag);

        Assert.AreEqual(secondTableOffset, secondTable.OffsetInSection);
        Assert.AreEqual(2, secondTable.AbbreviationsByCode.Count);
        Assert.AreEqual(
            DwarfTag.BaseType,
            secondTable.Get(1, DebugAbbrevSectionName, 0).Tag);
        Assert.AreEqual(
            DwarfTag.PointerType,
            secondTable.Get(2, DebugAbbrevSectionName, 0).Tag);
    }

    [TestMethod]
    public void Parse_NullData_ThrowsArgumentNull()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => DwarfAbbreviationTable.Parse(null!, 0));
    }

    [TestMethod]
    public void Parse_NegativeOffset_ThrowsArgumentOutOfRange()
    {
        byte[] bytes = { 0x00 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => DwarfAbbreviationTable.Parse(bytes, -1));
    }

    [TestMethod]
    public void Parse_OffsetBeyondBuffer_ThrowsArgumentOutOfRange()
    {
        byte[] bytes = { 0x00 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => DwarfAbbreviationTable.Parse(bytes, 2));
    }

    [TestMethod]
    public void Parse_EmptyBuffer_ThrowsDwarfParseOnTruncatedCode()
    {
        byte[] bytes = Array.Empty<byte>();
        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        Assert.AreEqual(DebugAbbrevSectionName, exception.SectionName);
    }

    [TestMethod]
    public void Parse_MissingTerminator_ThrowsDwarfParseOnTruncatedCode()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.Variable,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Name, DwarfForm.Strp),
                })
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        Assert.AreEqual(DebugAbbrevSectionName, exception.SectionName);
    }

    [TestMethod]
    public void Parse_TruncatedTag_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendUnsignedLeb128Raw(1)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "tag");
    }

    [TestMethod]
    public void Parse_TruncatedHasChildrenFlag_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendUnsignedLeb128Raw(1)
            .AppendUnsignedLeb128Raw((ulong)DwarfTag.BaseType)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "has-children");
    }

    [TestMethod]
    public void Parse_InvalidHasChildrenFlagTwo_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendUnsignedLeb128Raw(1)
            .AppendUnsignedLeb128Raw((ulong)DwarfTag.BaseType)
            .AppendRawByte(0x02)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "has-children");
        StringAssert.Contains(exception.Message, "0x02");
    }

    [TestMethod]
    public void Parse_InvalidHasChildrenFlagMaxByte_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendUnsignedLeb128Raw(1)
            .AppendUnsignedLeb128Raw((ulong)DwarfTag.BaseType)
            .AppendRawByte(0xFF)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "0xFF");
    }

    [TestMethod]
    public void Parse_TruncatedAttributeSpec_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendUnsignedLeb128Raw(1)
            .AppendUnsignedLeb128Raw((ulong)DwarfTag.BaseType)
            .AppendRawByte(0x00)
            .AppendUnsignedLeb128Raw((ulong)DwarfAttribute.Name)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "attribute spec");
    }

    [TestMethod]
    public void Parse_HalfNullAttributePair_AttributeZeroFormNonZero_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendUnsignedLeb128Raw(1)
            .AppendUnsignedLeb128Raw((ulong)DwarfTag.BaseType)
            .AppendRawByte(0x00)
            .AppendUnsignedLeb128Raw(0)
            .AppendUnsignedLeb128Raw((ulong)DwarfForm.Data1)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "Half-null");
    }

    [TestMethod]
    public void Parse_HalfNullAttributePair_AttributeNonZeroFormZero_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendUnsignedLeb128Raw(1)
            .AppendUnsignedLeb128Raw((ulong)DwarfTag.BaseType)
            .AppendRawByte(0x00)
            .AppendUnsignedLeb128Raw((ulong)DwarfAttribute.Name)
            .AppendUnsignedLeb128Raw(0)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "Half-null");
    }

    [TestMethod]
    public void Parse_DuplicateCode_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.BaseType,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendAbbreviation(
                1,
                DwarfTag.PointerType,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "Duplicate abbreviation code 1");
    }

    [TestMethod]
    public void Parse_TruncatedImplicitConstValue_ThrowsDwarfParse()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendUnsignedLeb128Raw(1)
            .AppendUnsignedLeb128Raw((ulong)DwarfTag.Enumerator)
            .AppendRawByte(0x00)
            .AppendUnsignedLeb128Raw((ulong)DwarfAttribute.ConstValue)
            .AppendUnsignedLeb128Raw((ulong)DwarfForm.ImplicitConst)
            .AppendRawByte(0x80)
            .ToArray();

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, 0));
        StringAssert.Contains(exception.Message, "implicit_const");
    }

    [TestMethod]
    public void Parse_OffsetRecordedOnError_PointsInsideFailingAbbreviation()
    {
        SyntheticAbbreviationTableBuilder builder = new();
        builder.AppendAbbreviation(
            1,
            DwarfTag.BaseType,
            false,
            new List<SyntheticAbbreviationAttribute>());
        int failingAbbreviationStart = builder.Position;
        builder.AppendUnsignedLeb128Raw(2);
        builder.AppendUnsignedLeb128Raw((ulong)DwarfTag.PointerType);
        builder.AppendRawByte(0x02);

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(builder.ToArray(), 0));
        Assert.IsTrue(
            exception.OffsetInSection >= failingAbbreviationStart,
            "offset " + exception.OffsetInSection +
            " should be at or after failing abbreviation start " + failingAbbreviationStart);
    }

    [TestMethod]
    public void Parse_OffsetAtSectionEnd_ThrowsDwarfParse()
    {
        byte[] bytes = { 0x00 };

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => DwarfAbbreviationTable.Parse(bytes, bytes.Length));
        Assert.AreEqual(DebugAbbrevSectionName, exception.SectionName);
    }

    [TestMethod]
    public void Get_MissingCode_ThrowsDwarfParseWithRequestingContext()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.BaseType,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();
        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        DwarfParseException exception = Assert.ThrowsException<DwarfParseException>(
            () => table.Get(99, ".debug_info", 0x1234));

        Assert.AreEqual(".debug_info", exception.SectionName);
        Assert.AreEqual(0x1234, exception.OffsetInSection);
        StringAssert.Contains(exception.Message, "99");
        StringAssert.Contains(exception.Message, DebugAbbrevSectionName);
    }

    [TestMethod]
    public void Get_ExistingCode_ReturnsAbbreviation()
    {
        byte[] bytes = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                42,
                DwarfTag.Variable,
                false,
                new List<SyntheticAbbreviationAttribute>())
            .AppendTableTerminator()
            .ToArray();
        DwarfAbbreviationTable table = DwarfAbbreviationTable.Parse(bytes, 0);

        DwarfAbbreviation abbreviation = table.Get(42, ".debug_info", 0);

        Assert.AreEqual(42, abbreviation.Code);
        Assert.AreEqual(DwarfTag.Variable, abbreviation.Tag);
    }

    [TestMethod]
    public void Constructor_NullAbbreviations_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new DwarfAbbreviationTable(0, null!));
    }

    [TestMethod]
    public void Constructor_CopiesInputDictionary()
    {
        List<DwarfAbbreviationAttribute> attributes = new();
        DwarfAbbreviation abbreviation =
            new DwarfAbbreviation(1, DwarfTag.BaseType, false, attributes);
        Dictionary<int, DwarfAbbreviation> input = new() { { 1, abbreviation } };

        DwarfAbbreviationTable table = new(0, input);
        input[2] = new DwarfAbbreviation(2, DwarfTag.Variable, false, attributes);

        Assert.AreEqual(1, table.AbbreviationsByCode.Count);
        Assert.IsFalse(table.AbbreviationsByCode.ContainsKey(2));
    }
}
