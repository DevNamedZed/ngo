// -----------------------------------------------------------------------
// <copyright file="BinaryReaderLittleEndianTests.cs" company="Ziad">
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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo.Binary;

namespace Ngo.Compiler.Tests.Cgo.Binary;

/// <summary>
/// Unit tests for <see cref="BinaryReaderLittleEndian"/>. These lock
/// in the little-endian byte ordering, cursor advancement, bounds
/// checking, and the Layer-1 exception contract
/// (<see cref="BinaryReadException"/> carrying the offset at which
/// a failing read began). Null terminator handling and LEB128
/// delegation are exercised here too because they are the only
/// surface that touches <see cref="Leb128"/> from the DWARF parser.
/// </summary>
[TestClass]
public class BinaryReaderLittleEndianTests
{
    [TestMethod]
    public void Constructor_NullData_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new BinaryReaderLittleEndian(null!));
    }

    [TestMethod]
    public void Constructor_NegativeStartOffset_Throws()
    {
        byte[] data = { 0x01, 0x02 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new BinaryReaderLittleEndian(data, -1));
    }

    [TestMethod]
    public void Constructor_StartOffsetPastLength_Throws()
    {
        byte[] data = { 0x01, 0x02 };
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => new BinaryReaderLittleEndian(data, 3));
    }

    [TestMethod]
    public void Constructor_StartOffsetEqualLength_AllowedWithZeroRemaining()
    {
        byte[] data = { 0x01, 0x02 };
        BinaryReaderLittleEndian reader = new(data, 2);
        Assert.AreEqual(2, reader.Position);
        Assert.AreEqual(0, reader.Remaining);
    }

    [TestMethod]
    public void PositionLengthRemaining_ReflectCursorState()
    {
        byte[] data = { 0x01, 0x02, 0x03, 0x04 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreEqual(0, reader.Position);
        Assert.AreEqual(4, reader.Length);
        Assert.AreEqual(4, reader.Remaining);

        reader.ReadU8();
        Assert.AreEqual(1, reader.Position);
        Assert.AreEqual(4, reader.Length);
        Assert.AreEqual(3, reader.Remaining);
    }

    [TestMethod]
    public void Seek_ValidPosition_UpdatesCursor()
    {
        byte[] data = { 0x01, 0x02, 0x03, 0x04 };
        BinaryReaderLittleEndian reader = new(data);
        reader.Seek(3);
        Assert.AreEqual(3, reader.Position);
        Assert.AreEqual(0x04, reader.ReadU8());
    }

    [TestMethod]
    public void Seek_OutOfRange_Throws()
    {
        byte[] data = { 0x01, 0x02 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.Seek(3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.Seek(-1));
    }

    [TestMethod]
    public void Skip_WithinBuffer_AdvancesCursor()
    {
        byte[] data = { 0x01, 0x02, 0x03, 0x04 };
        BinaryReaderLittleEndian reader = new(data);
        reader.Skip(2);
        Assert.AreEqual(2, reader.Position);
        Assert.AreEqual(0x03, reader.ReadU8());
    }

    [TestMethod]
    public void Skip_NegativeCount_Throws()
    {
        byte[] data = { 0x01, 0x02 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.Skip(-1));
    }

    [TestMethod]
    public void Skip_PastEndOfBuffer_ThrowsBinaryReadException()
    {
        byte[] data = { 0x01, 0x02 };
        BinaryReaderLittleEndian reader = new(data);
        BinaryReadException thrown = Assert.ThrowsException<BinaryReadException>(
            () => reader.Skip(3));
        Assert.AreEqual(0, thrown.Offset);
    }

    [TestMethod]
    public void ReadU8_SingleByte_ReturnsValueAndAdvances()
    {
        byte[] data = { 0xAB, 0xCD };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreEqual(0xAB, reader.ReadU8());
        Assert.AreEqual(0xCD, reader.ReadU8());
        Assert.AreEqual(2, reader.Position);
    }

    [TestMethod]
    public void ReadU8_AtEndOfBuffer_Throws()
    {
        byte[] data = Array.Empty<byte>();
        BinaryReaderLittleEndian reader = new(data);
        BinaryReadException thrown = Assert.ThrowsException<BinaryReadException>(
            () => reader.ReadU8());
        Assert.AreEqual(0, thrown.Offset);
    }

    [TestMethod]
    public void ReadU16_LittleEndianByteOrder()
    {
        byte[] data = { 0x34, 0x12 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreEqual((ushort)0x1234, reader.ReadU16());
        Assert.AreEqual(2, reader.Position);
    }

    [TestMethod]
    public void ReadU16_Truncated_ThrowsWithFailingOffset()
    {
        byte[] data = { 0x12 };
        BinaryReaderLittleEndian reader = new(data);
        BinaryReadException thrown = Assert.ThrowsException<BinaryReadException>(
            () => reader.ReadU16());
        Assert.AreEqual(0, thrown.Offset);
        Assert.AreEqual(0, reader.Position);
    }

    [TestMethod]
    public void ReadU32_LittleEndianByteOrder()
    {
        byte[] data = { 0x78, 0x56, 0x34, 0x12 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreEqual(0x12345678U, reader.ReadU32());
        Assert.AreEqual(4, reader.Position);
    }

    [TestMethod]
    public void ReadU32_MaxValue()
    {
        byte[] data = { 0xFF, 0xFF, 0xFF, 0xFF };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreEqual(uint.MaxValue, reader.ReadU32());
    }

    [TestMethod]
    public void ReadU32_Truncated_Throws()
    {
        byte[] data = { 0x01, 0x02, 0x03 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.ThrowsException<BinaryReadException>(() => reader.ReadU32());
    }

    [TestMethod]
    public void ReadU64_LittleEndianByteOrder()
    {
        byte[] data = { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreEqual(0x0123456789ABCDEFUL, reader.ReadU64());
        Assert.AreEqual(8, reader.Position);
    }

    [TestMethod]
    public void ReadU64_MaxValue()
    {
        byte[] data =
        {
            0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF,
        };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreEqual(ulong.MaxValue, reader.ReadU64());
    }

    [TestMethod]
    public void ReadU64_Truncated_Throws()
    {
        byte[] data = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.ThrowsException<BinaryReadException>(() => reader.ReadU64());
    }

    [TestMethod]
    public void ReadBytes_ZeroCount_ReturnsEmptyArrayWithoutAdvancing()
    {
        byte[] data = { 0x01, 0x02 };
        BinaryReaderLittleEndian reader = new(data);
        byte[] copy = reader.ReadBytes(0);
        Assert.AreEqual(0, copy.Length);
        Assert.AreEqual(0, reader.Position);
    }

    [TestMethod]
    public void ReadBytes_ValidCount_ReturnsIndependentCopy()
    {
        byte[] data = { 0x01, 0x02, 0x03, 0x04 };
        BinaryReaderLittleEndian reader = new(data);
        byte[] copy = reader.ReadBytes(3);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, copy);
        Assert.AreEqual(3, reader.Position);

        copy[0] = 0xFF;
        Assert.AreEqual(0x01, data[0]);
    }

    [TestMethod]
    public void ReadBytes_NegativeCount_Throws()
    {
        byte[] data = { 0x01 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.ReadBytes(-1));
    }

    [TestMethod]
    public void ReadBytes_PastEndOfBuffer_Throws()
    {
        byte[] data = { 0x01, 0x02 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.ThrowsException<BinaryReadException>(() => reader.ReadBytes(3));
    }

    [TestMethod]
    public void ReadNullTerminatedUtf8_AsciiString_ReturnsValueAndAdvancesPastTerminator()
    {
        byte[] data = Encoding.UTF8.GetBytes("hello\0world");
        BinaryReaderLittleEndian reader = new(data);
        string value = reader.ReadNullTerminatedUtf8();
        Assert.AreEqual("hello", value);
        Assert.AreEqual(6, reader.Position);
    }

    [TestMethod]
    public void ReadNullTerminatedUtf8_MultibyteCharacters_DecodedCorrectly()
    {
        byte[] payload = Encoding.UTF8.GetBytes("café\0");
        BinaryReaderLittleEndian reader = new(payload);
        string value = reader.ReadNullTerminatedUtf8();
        Assert.AreEqual("café", value);
    }

    [TestMethod]
    public void ReadNullTerminatedUtf8_LeadingTerminator_ReturnsEmptyString()
    {
        byte[] data = { 0x00, 0x41 };
        BinaryReaderLittleEndian reader = new(data);
        string value = reader.ReadNullTerminatedUtf8();
        Assert.AreEqual(string.Empty, value);
        Assert.AreEqual(1, reader.Position);
    }

    [TestMethod]
    public void ReadNullTerminatedUtf8_MissingTerminator_ThrowsWithStringStartOffset()
    {
        byte[] data = { 0x41, 0x42, 0x43 };
        BinaryReaderLittleEndian reader = new(data);
        reader.Skip(0);
        BinaryReadException thrown = Assert.ThrowsException<BinaryReadException>(
            () => reader.ReadNullTerminatedUtf8());
        Assert.AreEqual(0, thrown.Offset);
        StringAssert.Contains(thrown.Message, "no null terminator");
    }

    [TestMethod]
    public void ReadNullTerminatedUtf8_MissingTerminatorAfterNonZeroStart_OffsetMarksStringStart()
    {
        byte[] data = { 0x00, 0x41, 0x42, 0x43 };
        BinaryReaderLittleEndian reader = new(data, 1);
        BinaryReadException thrown = Assert.ThrowsException<BinaryReadException>(
            () => reader.ReadNullTerminatedUtf8());
        Assert.AreEqual(1, thrown.Offset);
    }

    [TestMethod]
    public void ReadUnsignedLeb128_DelegatesAndAdvancesCursor()
    {
        byte[] data = { 0xE5, 0x8E, 0x26, 0xAA };
        BinaryReaderLittleEndian reader = new(data);
        ulong value = reader.ReadUnsignedLeb128();
        Assert.AreEqual(624485UL, value);
        Assert.AreEqual(3, reader.Position);
    }

    [TestMethod]
    public void ReadSignedLeb128_DelegatesAndAdvancesCursor()
    {
        byte[] data = { 0x40, 0xAA };
        BinaryReaderLittleEndian reader = new(data);
        long value = reader.ReadSignedLeb128();
        Assert.AreEqual(-64L, value);
        Assert.AreEqual(1, reader.Position);
    }

    [TestMethod]
    public void ReadUnsignedLeb128AsInt32_DelegatesAndAdvancesCursor()
    {
        byte[] data = { 0x7F, 0xAA };
        BinaryReaderLittleEndian reader = new(data);
        int value = reader.ReadUnsignedLeb128AsInt32();
        Assert.AreEqual(127, value);
        Assert.AreEqual(1, reader.Position);
    }

    [TestMethod]
    public void ReadUnsignedLeb128_OnTruncatedInput_Throws()
    {
        byte[] data = { 0x80, 0x80 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.ThrowsException<Leb128ParseException>(() => reader.ReadUnsignedLeb128());
    }

    [TestMethod]
    public void Data_ExposesUnderlyingArrayReference()
    {
        byte[] data = { 0x01, 0x02 };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreSame(data, reader.Data);
    }

    [TestMethod]
    public void MixedReads_CursorAdvancesThroughEachOperation()
    {
        byte[] data =
        {
            0xAB,
            0x34, 0x12,
            0x78, 0x56, 0x34, 0x12,
            0x48, 0x69, 0x00,
        };
        BinaryReaderLittleEndian reader = new(data);
        Assert.AreEqual(0xAB, reader.ReadU8());
        Assert.AreEqual((ushort)0x1234, reader.ReadU16());
        Assert.AreEqual(0x12345678U, reader.ReadU32());
        Assert.AreEqual("Hi", reader.ReadNullTerminatedUtf8());
        Assert.AreEqual(data.Length, reader.Position);
        Assert.AreEqual(0, reader.Remaining);
    }
}
