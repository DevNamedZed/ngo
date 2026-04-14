// -----------------------------------------------------------------------
// <copyright file="DwarfReaderFormTests.cs" company="Ziad">
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
using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Dwarf;

/// <summary>
/// Exercises <see cref="DwarfReader"/>'s form-decoding switch.
/// Each test encodes one DIE carrying exactly one attribute with
/// a specific form, so a decoder regression for any one form
/// fails a single named test rather than cascading through the
/// whole integration suite.
/// </summary>
[TestClass]
public class DwarfReaderFormTests
{
    [TestMethod]
    public void Form_Addr_EightByte_DecodesAsUnsignedInteger()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Addr,
            addressSize: 8,
            builder => builder.AppendU64(0x11223344_55667788UL));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Addr);
        Assert.AreEqual(unchecked((long)0x11223344_55667788UL), integer.Value);
        Assert.IsTrue(integer.IsUnsigned);
    }

    [TestMethod]
    public void Form_Addr_FourByte_DecodesAsUnsignedInteger()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Addr,
            addressSize: 4,
            builder => builder.AppendU32(0xDEADBEEFu));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Addr);
        Assert.AreEqual(0xDEADBEEFL, integer.Value);
        Assert.IsTrue(integer.IsUnsigned);
    }

    [TestMethod]
    public void Form_Data1_DecodesEightBitUnsigned()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Data1,
            addressSize: 8,
            builder => builder.AppendU8(0xFE));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Data1);
        Assert.AreEqual(0xFEL, integer.Value);
        Assert.IsTrue(integer.IsUnsigned);
    }

    [TestMethod]
    public void Form_Data2_DecodesSixteenBitLittleEndian()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Data2,
            addressSize: 8,
            builder => builder.AppendU16(0xCAFE));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Data2);
        Assert.AreEqual(0xCAFEL, integer.Value);
    }

    [TestMethod]
    public void Form_Data4_DecodesThirtyTwoBitLittleEndian()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Data4,
            addressSize: 8,
            builder => builder.AppendU32(0x12345678u));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Data4);
        Assert.AreEqual(0x12345678L, integer.Value);
    }

    [TestMethod]
    public void Form_Data8_DecodesSixtyFourBitLittleEndian()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Data8,
            addressSize: 8,
            builder => builder.AppendU64(0x0123456789ABCDEFUL));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Data8);
        Assert.AreEqual(unchecked((long)0x0123456789ABCDEFUL), integer.Value);
    }

    [TestMethod]
    public void Form_Data16_DecodesAsBlock()
    {
        byte[] sixteenBytes = new byte[]
        {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
        };
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Data16,
            addressSize: 8,
            builder => builder.AppendRawBytes(sixteenBytes));

        DwarfBlockAttributeValue block = AssertBlock(value, DwarfForm.Data16);
        CollectionAssert.AreEqual(sixteenBytes, block.Value);
    }

    [TestMethod]
    public void Form_Sdata_DecodesSignedLeb128AsNegative()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Sdata,
            addressSize: 8,
            builder => builder.AppendSignedLeb128(-42));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Sdata);
        Assert.AreEqual(-42L, integer.Value);
        Assert.IsFalse(integer.IsUnsigned);
    }

    [TestMethod]
    public void Form_Udata_DecodesUnsignedLeb128()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Udata,
            addressSize: 8,
            builder => builder.AppendUnsignedLeb128(624485));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Udata);
        Assert.AreEqual(624485L, integer.Value);
        Assert.IsTrue(integer.IsUnsigned);
    }

    [TestMethod]
    public void Form_String_DecodesInlineNullTerminated()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.String,
            addressSize: 8,
            builder => builder.AppendNullTerminatedUtf8("int"));

        DwarfStringAttributeValue str = AssertString(value, DwarfForm.String);
        Assert.AreEqual("int", str.Value);
    }

    [TestMethod]
    public void Form_Strp_ResolvesFromDebugStr()
    {
        byte[] debugStr = BuildDebugStr("", "size_t");
        int sizeTOffset = 1;

        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Strp,
            addressSize: 8,
            builder => builder.AppendU32((uint)sizeTOffset),
            debugStr: debugStr);

        DwarfStringAttributeValue str = AssertString(value, DwarfForm.Strp);
        Assert.AreEqual("size_t", str.Value);
    }

    [TestMethod]
    public void Form_LineStrp_ResolvesFromDebugLineStr()
    {
        byte[] debugLineStr = BuildDebugStr("", "/tmp/a.c");
        int offset = 1;

        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.LineStrp,
            addressSize: 8,
            builder => builder.AppendU32((uint)offset),
            debugLineStr: debugLineStr);

        DwarfStringAttributeValue str = AssertString(value, DwarfForm.LineStrp);
        Assert.AreEqual("/tmp/a.c", str.Value);
    }

    [TestMethod]
    public void Form_Flag_ZeroByte_DecodesFalse()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Flag,
            addressSize: 8,
            builder => builder.AppendU8(0x00));

        DwarfFlagAttributeValue flag = AssertFlag(value, DwarfForm.Flag);
        Assert.IsFalse(flag.Value);
    }

    [TestMethod]
    public void Form_Flag_NonZeroByte_DecodesTrue()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Flag,
            addressSize: 8,
            builder => builder.AppendU8(0x02));

        DwarfFlagAttributeValue flag = AssertFlag(value, DwarfForm.Flag);
        Assert.IsTrue(flag.Value);
    }

    [TestMethod]
    public void Form_FlagPresent_ConsumesNoBytesAndDecodesTrue()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.FlagPresent,
            addressSize: 8,
            builder => { });

        DwarfFlagAttributeValue flag = AssertFlag(value, DwarfForm.FlagPresent);
        Assert.IsTrue(flag.Value);
    }

    [TestMethod]
    public void Form_Block_UlebLengthPrefix_CapturesExactBytes()
    {
        byte[] payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x11 };
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Block,
            addressSize: 8,
            builder =>
            {
                builder.AppendUnsignedLeb128((ulong)payload.Length);
                builder.AppendRawBytes(payload);
            });

        DwarfBlockAttributeValue block = AssertBlock(value, DwarfForm.Block);
        CollectionAssert.AreEqual(payload, block.Value);
    }

    [TestMethod]
    public void Form_Block1_ByteLengthPrefix_CapturesExactBytes()
    {
        byte[] payload = new byte[] { 0x10, 0x20, 0x30 };
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Block1,
            addressSize: 8,
            builder =>
            {
                builder.AppendU8((byte)payload.Length);
                builder.AppendRawBytes(payload);
            });

        DwarfBlockAttributeValue block = AssertBlock(value, DwarfForm.Block1);
        CollectionAssert.AreEqual(payload, block.Value);
    }

    [TestMethod]
    public void Form_Block2_U16LengthPrefix_CapturesExactBytes()
    {
        byte[] payload = new byte[300];
        for (int index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index & 0xFF);
        }
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Block2,
            addressSize: 8,
            builder =>
            {
                builder.AppendU16((ushort)payload.Length);
                builder.AppendRawBytes(payload);
            });

        DwarfBlockAttributeValue block = AssertBlock(value, DwarfForm.Block2);
        CollectionAssert.AreEqual(payload, block.Value);
    }

    [TestMethod]
    public void Form_Block4_U32LengthPrefix_CapturesExactBytes()
    {
        byte[] payload = new byte[] { 0xAA, 0xBB };
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Block4,
            addressSize: 8,
            builder =>
            {
                builder.AppendU32((uint)payload.Length);
                builder.AppendRawBytes(payload);
            });

        DwarfBlockAttributeValue block = AssertBlock(value, DwarfForm.Block4);
        CollectionAssert.AreEqual(payload, block.Value);
    }

    [TestMethod]
    public void Form_Exprloc_UlebLengthPrefix_CapturesExactBytes()
    {
        byte[] payload = new byte[] { 0x03, 0x08, 0x00, 0x00, 0x00 };
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.Exprloc,
            addressSize: 8,
            builder =>
            {
                builder.AppendUnsignedLeb128((ulong)payload.Length);
                builder.AppendRawBytes(payload);
            });

        DwarfBlockAttributeValue block = AssertBlock(value, DwarfForm.Exprloc);
        CollectionAssert.AreEqual(payload, block.Value);
    }

    [TestMethod]
    public void Form_SecOffset_DecodesAsIntegerInDwarf32()
    {
        DwarfAttributeValue value = DecodeSingleAttributeValue(
            DwarfForm.SecOffset,
            addressSize: 8,
            builder => builder.AppendU32(0x1337));

        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.SecOffset);
        Assert.AreEqual(0x1337L, integer.Value);
        Assert.IsTrue(integer.IsUnsigned);
    }

    [TestMethod]
    public void Form_SecOffset_EightByteInDwarf64()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.CompileUnit,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.Ranges, DwarfForm.SecOffset),
                    new SyntheticAbbreviationAttribute(DwarfAttribute.ByteSize, DwarfForm.Data1),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf64, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendU64(0x11223344UL);
        debugInfoBuilder.AppendU8(0x55);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfDie cuDie = debugInfo.CompilationUnits[0].TopLevelDies[0];
        DwarfIntegerAttributeValue rangesInteger =
            AssertInteger(cuDie.Attributes[DwarfAttribute.Ranges], DwarfForm.SecOffset);
        Assert.AreEqual(0x11223344L, rangesInteger.Value);
        DwarfIntegerAttributeValue byteSizeInteger =
            AssertInteger(cuDie.Attributes[DwarfAttribute.ByteSize], DwarfForm.Data1);
        Assert.AreEqual(0x55L, byteSizeInteger.Value);
    }

    [TestMethod]
    public void Form_ImplicitConst_UsesInlineAbbreviationValue()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.Enumerator,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(
                        DwarfAttribute.ConstValue, DwarfForm.ImplicitConst, -77),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf5, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfAttributeValue value =
            debugInfo.CompilationUnits[0].TopLevelDies[0].Attributes[DwarfAttribute.ConstValue];
        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.ImplicitConst);
        Assert.AreEqual(-77L, integer.Value);
        Assert.IsFalse(integer.IsUnsigned);
    }

    [TestMethod]
    public void Form_Indirect_RedirectsToRuntimeForm()
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.BaseType,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.ByteSize, DwarfForm.Indirect),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize: 8, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        debugInfoBuilder.AppendUnsignedLeb128((ulong)DwarfForm.Data1);
        debugInfoBuilder.AppendU8(8);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, null, null));

        DwarfAttributeValue value =
            debugInfo.CompilationUnits[0].TopLevelDies[0].Attributes[DwarfAttribute.ByteSize];
        DwarfIntegerAttributeValue integer = AssertInteger(value, DwarfForm.Data1);
        Assert.AreEqual(8L, integer.Value);
    }

    private static DwarfAttributeValue DecodeSingleAttributeValue(
        DwarfForm form,
        int addressSize,
        System.Action<SyntheticDebugInfoBuilder> appendValueBytes,
        byte[]? debugStr = null,
        byte[]? debugLineStr = null)
    {
        byte[] debugAbbrev = new SyntheticAbbreviationTableBuilder()
            .AppendAbbreviation(
                1,
                DwarfTag.BaseType,
                false,
                new List<SyntheticAbbreviationAttribute>
                {
                    new SyntheticAbbreviationAttribute(DwarfAttribute.ByteSize, form),
                })
            .AppendTableTerminator()
            .ToArray();

        SyntheticDebugInfoBuilder debugInfoBuilder = new();
        debugInfoBuilder.StartCompilationUnit(
            DwarfFormat.Dwarf4, DwarfUnitFormat.Dwarf32, addressSize, debugAbbrevOffset: 0);
        debugInfoBuilder.AppendUnsignedLeb128(1);
        appendValueBytes(debugInfoBuilder);
        debugInfoBuilder.EndCompilationUnit();

        DwarfDebugInfo debugInfo = DwarfReader.Read(new DwarfSections(
            debugInfoBuilder.ToArray(), debugAbbrev, debugStr, debugLineStr));

        return debugInfo.CompilationUnits[0].TopLevelDies[0].Attributes[DwarfAttribute.ByteSize];
    }

    private static byte[] BuildDebugStr(params string[] strings)
    {
        List<byte> bytes = new();
        foreach (string value in strings)
        {
            bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(value));
            bytes.Add(0);
        }
        return bytes.ToArray();
    }

    private static DwarfIntegerAttributeValue AssertInteger(
        DwarfAttributeValue value, DwarfForm expectedForm)
    {
        Assert.IsInstanceOfType(value, typeof(DwarfIntegerAttributeValue));
        Assert.AreEqual(expectedForm, value.Form);
        return (DwarfIntegerAttributeValue)value;
    }

    private static DwarfStringAttributeValue AssertString(
        DwarfAttributeValue value, DwarfForm expectedForm)
    {
        Assert.IsInstanceOfType(value, typeof(DwarfStringAttributeValue));
        Assert.AreEqual(expectedForm, value.Form);
        return (DwarfStringAttributeValue)value;
    }

    private static DwarfBlockAttributeValue AssertBlock(
        DwarfAttributeValue value, DwarfForm expectedForm)
    {
        Assert.IsInstanceOfType(value, typeof(DwarfBlockAttributeValue));
        Assert.AreEqual(expectedForm, value.Form);
        return (DwarfBlockAttributeValue)value;
    }

    private static DwarfFlagAttributeValue AssertFlag(
        DwarfAttributeValue value, DwarfForm expectedForm)
    {
        Assert.IsInstanceOfType(value, typeof(DwarfFlagAttributeValue));
        Assert.AreEqual(expectedForm, value.Form);
        return (DwarfFlagAttributeValue)value;
    }
}
