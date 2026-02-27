// -----------------------------------------------------------------------
// <copyright file="GoStringTests.cs" company="Ziad">
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Runtime.Tests;

[TestClass]
public class GoStringTests
{
    [TestMethod]
    public void Len_ascii()
    {
        Assert.AreEqual(5, GoString.Len("hello"));
    }

    [TestMethod]
    public void Len_utf8_multibyte()
    {
        // "\u00e9" is 2 bytes in UTF-8, "世" is 3 bytes
        Assert.AreEqual(2, GoString.Len("\u00e9"));
        Assert.AreEqual(3, GoString.Len("世"));
        Assert.AreEqual(7, GoString.Len("世界!"));  // 世=3 + 界=3 + !=1 → 7
    }

    [TestMethod]
    public void Len_empty()
    {
        Assert.AreEqual(0, GoString.Len(""));
    }

    [TestMethod]
    public void ByteAt_ascii()
    {
        Assert.AreEqual((byte)'h', GoString.ByteAt("hello", 0));
        Assert.AreEqual((byte)'o', GoString.ByteAt("hello", 4));
    }

    [TestMethod]
    public void ByteAt_out_of_range()
    {
        Assert.ThrowsException<GoPanicException>(() => GoString.ByteAt("hi", 2));
    }

    [TestMethod]
    public void ToBytes_and_back()
    {
        var bytes = GoString.ToBytes("hello");
        Assert.AreEqual(5, bytes.Len);
        Assert.AreEqual((byte)'h', bytes[0]);
        var s = GoString.FromBytes(bytes);
        Assert.AreEqual("hello", s);
    }

    [TestMethod]
    public void ToBytes_utf8()
    {
        var bytes = GoString.ToBytes("\u00e9");
        Assert.AreEqual(2, bytes.Len); // \u00e9 = 0xC3 0xA9
        Assert.AreEqual("\u00e9", GoString.FromBytes(bytes));
    }

    [TestMethod]
    public void ToRunes_ascii()
    {
        var runes = GoString.ToRunes("abc");
        Assert.AreEqual(3, runes.Len);
        Assert.AreEqual('a', runes[0]);
        Assert.AreEqual('b', runes[1]);
        Assert.AreEqual('c', runes[2]);
    }

    [TestMethod]
    public void ToRunes_unicode()
    {
        var runes = GoString.ToRunes("世界");
        Assert.AreEqual(2, runes.Len);
        Assert.AreEqual(0x4E16, runes[0]); // 世
        Assert.AreEqual(0x754C, runes[1]); // 界
    }

    [TestMethod]
    public void FromRunes()
    {
        var runes = new Slice<int>(new[] { 0x4E16, 0x754C });
        Assert.AreEqual("世界", GoString.FromRunes(runes));
    }

    [TestMethod]
    public void RangeRunes_ascii()
    {
        var result = GoString.RangeRunes("abc").ToList();
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual((0, (int)'a'), result[0]);
        Assert.AreEqual((1, (int)'b'), result[1]);
        Assert.AreEqual((2, (int)'c'), result[2]);
    }

    [TestMethod]
    public void RangeRunes_multibyte()
    {
        // "a\u00e9" = 'a'(1 byte) + '\u00e9'(2 bytes)
        var result = GoString.RangeRunes("a\u00e9").ToList();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual((0, (int)'a'), result[0]);
        Assert.AreEqual((1, 0xE9), result[1]); // \u00e9 at byte index 1
    }

    [TestMethod]
    public void FromRune()
    {
        Assert.AreEqual("A", GoString.FromRune(65));
        Assert.AreEqual("世", GoString.FromRune(0x4E16));
    }

    [TestMethod]
    public void SliceString_ascii()
    {
        Assert.AreEqual("ell", GoString.SliceString("hello", 1, 4));
    }

    [TestMethod]
    public void SliceString_bounds_check()
    {
        Assert.ThrowsException<GoPanicException>(() => GoString.SliceString("hi", 0, 5));
    }

    [TestMethod]
    public void Nil_bytes_round_trip()
    {
        Assert.AreEqual("", GoString.FromBytes(default));
    }

    [TestMethod]
    public void Nil_runes_round_trip()
    {
        Assert.AreEqual("", GoString.FromRunes(default));
    }

    [TestMethod]
    public void Len_four_byte_emoji()
    {
        // 🎉 U+1F389 = 4 bytes in UTF-8
        Assert.AreEqual(4, GoString.Len("\U0001F389"));
    }

    [TestMethod]
    public void Len_mixed_ascii_and_multibyte()
    {
        // "Go世界!" = G(1) + o(1) + 世(3) + 界(3) + !(1) = 9
        Assert.AreEqual(9, GoString.Len("Go世界!"));
    }

    [TestMethod]
    public void ByteAt_multibyte()
    {
        // "\u00e9" = 0xC3 0xA9
        Assert.AreEqual(0xC3, GoString.ByteAt("\u00e9", 0));
        Assert.AreEqual(0xA9, GoString.ByteAt("\u00e9", 1));
    }

    [TestMethod]
    public void ByteAt_four_byte_emoji()
    {
        // 🎉 U+1F389 = 0xF0 0x9F 0x8E 0x89
        Assert.AreEqual(0xF0, GoString.ByteAt("\U0001F389", 0));
        Assert.AreEqual(0x9F, GoString.ByteAt("\U0001F389", 1));
        Assert.AreEqual(0x8E, GoString.ByteAt("\U0001F389", 2));
        Assert.AreEqual(0x89, GoString.ByteAt("\U0001F389", 3));
    }

    [TestMethod]
    public void ByteAt_mixed_ascii_then_multibyte()
    {
        // "a\u00e9" = 'a'(0x61) + '\u00e9'(0xC3 0xA9)
        Assert.AreEqual(0x61, GoString.ByteAt("a\u00e9", 0));
        Assert.AreEqual(0xC3, GoString.ByteAt("a\u00e9", 1));
        Assert.AreEqual(0xA9, GoString.ByteAt("a\u00e9", 2));
    }

    [TestMethod]
    public void RangeRunes_four_byte_emoji()
    {
        // "A🎉B" = A(1 byte) + 🎉(4 bytes) + B(1 byte)
        var result = GoString.RangeRunes("A\U0001F389B").ToList();
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual((0, (int)'A'), result[0]);
        Assert.AreEqual((1, 0x1F389), result[1]);
        Assert.AreEqual((5, (int)'B'), result[2]);
    }

    [TestMethod]
    public void SliceString_multibyte()
    {
        // "a\u00e9b" = a(1) + \u00e9(2) + b(1) → 4 bytes total
        // s[1:3] should extract the 2 bytes of "\u00e9"
        Assert.AreEqual("\u00e9", GoString.SliceString("a\u00e9b", 1, 3));
    }

    [TestMethod]
    public void SliceString_four_byte_emoji()
    {
        // "A🎉B" = A(1) + 🎉(4) + B(1) → 6 bytes
        // s[1:5] should extract the emoji
        Assert.AreEqual("\U0001F389", GoString.SliceString("A\U0001F389B", 1, 5));
    }

    [TestMethod]
    public void ToBytes_four_byte_emoji()
    {
        var bytes = GoString.ToBytes("\U0001F389");
        Assert.AreEqual(4, bytes.Len);
        Assert.AreEqual(0xF0, bytes[0]);
        Assert.AreEqual(0x9F, bytes[1]);
        Assert.AreEqual(0x8E, bytes[2]);
        Assert.AreEqual(0x89, bytes[3]);
    }

    [TestMethod]
    public void ToRunes_four_byte_emoji()
    {
        var runes = GoString.ToRunes("A\U0001F389B");
        Assert.AreEqual(3, runes.Len);
        Assert.AreEqual('A', runes[0]);
        Assert.AreEqual(0x1F389, runes[1]);
        Assert.AreEqual('B', runes[2]);
    }

    [TestMethod]
    public void FromBytes_offset_slice()
    {
        // Create a byte slice that's a sub-slice of a larger array
        var full = GoString.ToBytes("hello world");
        var sub = full.Reslice(6, 11);
        Assert.AreEqual("world", GoString.FromBytes(sub));
    }
}
