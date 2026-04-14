// -----------------------------------------------------------------------
// <copyright file="Leb128Tests.cs" company="Ziad">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo.Binary;

namespace Ngo.Compiler.Tests.Cgo.Binary;

/// <summary>
/// Unit tests for <see cref="Leb128"/>. These cover the correctness
/// properties called out in the DWARF reader spec: full 64-bit range,
/// explicit overflow detection at the ten-byte boundary, explicit
/// truncation detection, correct signed sign-extension at every shift
/// boundary, and the <see cref="Leb128ParseException.StartOffset"/>
/// invariant. Values are chosen so that a regression in shift
/// arithmetic or termination logic fails at least one assertion —
/// blanket round-trip testing would miss bugs like "byte 9 continuation
/// silently accepted" or "value above <c>ulong.MaxValue</c> decoded as
/// garbage".
/// </summary>
[TestClass]
public class Leb128Tests
{
    [TestMethod]
    public void ReadUnsigned_SingleZeroByte_ReturnsZero()
    {
        byte[] data = { 0x00 };
        ulong value = Leb128.ReadUnsigned(data, 0, out int consumed);
        Assert.AreEqual(0UL, value);
        Assert.AreEqual(1, consumed);
    }

    [TestMethod]
    public void ReadUnsigned_SingleByteMaxPayload_Returns127()
    {
        byte[] data = { 0x7F };
        ulong value = Leb128.ReadUnsigned(data, 0, out int consumed);
        Assert.AreEqual(127UL, value);
        Assert.AreEqual(1, consumed);
    }

    [TestMethod]
    public void ReadUnsigned_TwoBytesAtBoundary_Returns128()
    {
        byte[] data = { 0x80, 0x01 };
        ulong value = Leb128.ReadUnsigned(data, 0, out int consumed);
        Assert.AreEqual(128UL, value);
        Assert.AreEqual(2, consumed);
    }

    [TestMethod]
    public void ReadUnsigned_CanonicalDwarfExample_Returns624485()
    {
        byte[] data = { 0xE5, 0x8E, 0x26 };
        ulong value = Leb128.ReadUnsigned(data, 0, out int consumed);
        Assert.AreEqual(624485UL, value);
        Assert.AreEqual(3, consumed);
    }

    [TestMethod]
    public void ReadUnsigned_TenBytesAtMaxRange_ReturnsUlongMaxValue()
    {
        byte[] data =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0x01,
        };
        ulong value = Leb128.ReadUnsigned(data, 0, out int consumed);
        Assert.AreEqual(ulong.MaxValue, value);
        Assert.AreEqual(10, consumed);
    }

    [TestMethod]
    public void ReadUnsigned_NonZeroStartOffset_DecodesFromOffset()
    {
        byte[] data = { 0xAA, 0xBB, 0xCC, 0xE5, 0x8E, 0x26, 0xDD };
        ulong value = Leb128.ReadUnsigned(data, 3, out int consumed);
        Assert.AreEqual(624485UL, value);
        Assert.AreEqual(3, consumed);
    }

    [TestMethod]
    public void ReadUnsigned_TerminatingByteIgnoresTrailingBytes()
    {
        byte[] data = { 0x7F, 0xFF, 0xFF };
        ulong value = Leb128.ReadUnsigned(data, 0, out int consumed);
        Assert.AreEqual(127UL, value);
        Assert.AreEqual(1, consumed);
    }

    [TestMethod]
    public void ReadUnsigned_EmptyBuffer_ThrowsWithStartOffset()
    {
        byte[] data = Array.Empty<byte>();
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadUnsigned(data, 0, out _));
        Assert.AreEqual(0, thrown.StartOffset);
        Assert.AreEqual(0, thrown.Offset);
    }

    [TestMethod]
    public void ReadUnsigned_ContinuationBitOnLastAvailableByte_ThrowsTruncation()
    {
        byte[] data = { 0x80, 0x80, 0x80 };
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadUnsigned(data, 0, out _));
        StringAssert.Contains(thrown.Message, "truncated");
        Assert.AreEqual(0, thrown.StartOffset);
    }

    [TestMethod]
    public void ReadUnsigned_TenthByteContinuationBitSet_ThrowsOverflow()
    {
        byte[] data =
        {
            0x80, 0x80, 0x80, 0x80, 0x80,
            0x80, 0x80, 0x80, 0x80, 0x80,
        };
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadUnsigned(data, 0, out _));
        StringAssert.Contains(thrown.Message, "64-bit range");
    }

    [TestMethod]
    public void ReadUnsigned_TenthBytePayloadAboveOne_ThrowsOverflow()
    {
        byte[] data =
        {
            0x80, 0x80, 0x80, 0x80, 0x80,
            0x80, 0x80, 0x80, 0x80, 0x02,
        };
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadUnsigned(data, 0, out _));
        StringAssert.Contains(thrown.Message, "64-bit range");
    }

    [TestMethod]
    public void ReadUnsigned_StartOffsetSurvivesIntoException()
    {
        byte[] data = { 0x00, 0x00, 0x80, 0x80 };
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadUnsigned(data, 2, out _));
        Assert.AreEqual(2, thrown.StartOffset);
    }

    [TestMethod]
    public void ReadSigned_SingleZeroByte_ReturnsZero()
    {
        byte[] data = { 0x00 };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(0L, value);
        Assert.AreEqual(1, consumed);
    }

    [TestMethod]
    public void ReadSigned_SinglePositiveByte_Returns63()
    {
        byte[] data = { 0x3F };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(63L, value);
        Assert.AreEqual(1, consumed);
    }

    [TestMethod]
    public void ReadSigned_SingleNegativeByte_ReturnsMinus64()
    {
        byte[] data = { 0x40 };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(-64L, value);
        Assert.AreEqual(1, consumed);
    }

    [TestMethod]
    public void ReadSigned_SingleByteAllOnesExceptContinuation_ReturnsMinusOne()
    {
        byte[] data = { 0x7F };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(-1L, value);
        Assert.AreEqual(1, consumed);
    }

    [TestMethod]
    public void ReadSigned_TwoBytesPositive_Returns64()
    {
        byte[] data = { 0xC0, 0x00 };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(64L, value);
        Assert.AreEqual(2, consumed);
    }

    [TestMethod]
    public void ReadSigned_TwoBytesNegative_ReturnsMinus128()
    {
        byte[] data = { 0x80, 0x7F };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(-128L, value);
        Assert.AreEqual(2, consumed);
    }

    [TestMethod]
    public void ReadSigned_TwoBytesNegativeOffBoundary_ReturnsMinus65()
    {
        byte[] data = { 0xBF, 0x7F };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(-65L, value);
        Assert.AreEqual(2, consumed);
    }

    [TestMethod]
    public void ReadSigned_SignExtendsAtEachShiftBoundary()
    {
        long[] negativeValuesAtEachShiftBoundary =
        {
            -1L,                     // terminates at shift 0 (1 byte)
            -128L,                   // terminates at shift 7 (2 bytes)
            -(1L << 13),             // terminates at shift 14 (3 bytes)
            -(1L << 20),             // terminates at shift 21 (4 bytes)
            -(1L << 27),             // terminates at shift 28 (5 bytes)
            -(1L << 34),             // terminates at shift 35 (6 bytes)
            -(1L << 41),             // terminates at shift 42 (7 bytes)
            -(1L << 48),             // terminates at shift 49 (8 bytes)
            -(1L << 55),             // terminates at shift 56 (9 bytes)
        };

        foreach (long expected in negativeValuesAtEachShiftBoundary)
        {
            byte[] encoded = EncodeSignedLeb128(expected);
            long decoded = Leb128.ReadSigned(encoded, 0, out int consumed);
            Assert.AreEqual(
                expected,
                decoded,
                "Round-trip failed for " + expected + " encoded as " + encoded.Length + " byte(s).");
            Assert.AreEqual(encoded.Length, consumed);
        }
    }

    [TestMethod]
    public void ReadSigned_TenBytes_ReturnsLongMaxValue()
    {
        byte[] data =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF, 0x00,
        };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(long.MaxValue, value);
        Assert.AreEqual(10, consumed);
    }

    [TestMethod]
    public void ReadSigned_TenBytes_ReturnsLongMinValue()
    {
        byte[] data =
        {
            0x80, 0x80, 0x80, 0x80, 0x80,
            0x80, 0x80, 0x80, 0x80, 0x7F,
        };
        long value = Leb128.ReadSigned(data, 0, out int consumed);
        Assert.AreEqual(long.MinValue, value);
        Assert.AreEqual(10, consumed);
    }

    [TestMethod]
    public void ReadSigned_TenthByteContinuationBitSet_ThrowsOverflow()
    {
        byte[] data =
        {
            0x80, 0x80, 0x80, 0x80, 0x80,
            0x80, 0x80, 0x80, 0x80, 0x80,
        };
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadSigned(data, 0, out _));
        StringAssert.Contains(thrown.Message, "64-bit range");
    }

    [TestMethod]
    public void ReadSigned_TenthBytePayloadNeitherZeroNorSignExtension_ThrowsOverflow()
    {
        byte[] data =
        {
            0x80, 0x80, 0x80, 0x80, 0x80,
            0x80, 0x80, 0x80, 0x80, 0x40,
        };
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadSigned(data, 0, out _));
        StringAssert.Contains(thrown.Message, "64-bit range");
    }

    [TestMethod]
    public void ReadSigned_ContinuationBitOnLastAvailableByte_ThrowsTruncation()
    {
        byte[] data = { 0x80, 0x80 };
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadSigned(data, 0, out _));
        StringAssert.Contains(thrown.Message, "truncated");
    }

    [TestMethod]
    public void ReadSigned_EmptyBuffer_ThrowsWithStartOffset()
    {
        byte[] data = Array.Empty<byte>();
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadSigned(data, 0, out _));
        Assert.AreEqual(0, thrown.StartOffset);
    }

    [TestMethod]
    public void ReadUnsignedAsInt32_ValueWithinIntRange_Returns()
    {
        byte[] data = EncodeUnsignedLeb128((ulong)int.MaxValue);
        int value = Leb128.ReadUnsignedAsInt32(data, 0, out int consumed);
        Assert.AreEqual(int.MaxValue, value);
        Assert.AreEqual(data.Length, consumed);
    }

    [TestMethod]
    public void ReadUnsignedAsInt32_ValueAboveIntRange_Throws()
    {
        byte[] data = EncodeUnsignedLeb128((ulong)int.MaxValue + 1UL);
        Leb128ParseException thrown = Assert.ThrowsException<Leb128ParseException>(
            () => Leb128.ReadUnsignedAsInt32(data, 0, out _));
        StringAssert.Contains(thrown.Message, "Int32 range");
    }

    [TestMethod]
    public void Leb128ParseException_InheritsFromBinaryReadException()
    {
        Leb128ParseException thrown = new Leb128ParseException("example", 17);
        Assert.IsInstanceOfType(thrown, typeof(BinaryReadException));
        Assert.AreEqual(17, thrown.Offset);
        Assert.AreEqual(17, thrown.StartOffset);
    }

    private static byte[] EncodeUnsignedLeb128(ulong value)
    {
        System.Collections.Generic.List<byte> output = new();
        while (true)
        {
            byte payload = (byte)(value & 0x7F);
            value >>= 7;
            if (value == 0)
            {
                output.Add(payload);
                return output.ToArray();
            }
            output.Add((byte)(payload | 0x80));
        }
    }

    private static byte[] EncodeSignedLeb128(long value)
    {
        System.Collections.Generic.List<byte> output = new();
        bool more = true;
        while (more)
        {
            byte payload = (byte)(value & 0x7F);
            long next = value >> 7;
            bool signBit = (payload & 0x40) != 0;
            bool terminalForPositive = next == 0 && !signBit;
            bool terminalForNegative = next == -1 && signBit;
            if (terminalForPositive || terminalForNegative)
            {
                output.Add(payload);
                more = false;
            }
            else
            {
                output.Add((byte)(payload | 0x80));
                value = next;
            }
        }
        return output.ToArray();
    }
}
