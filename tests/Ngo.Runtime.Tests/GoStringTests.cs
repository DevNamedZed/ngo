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
        GoString gs = GoString.FromNetString("hello");
        Assert.AreEqual(5, gs.Len);
    }

    [TestMethod]
    public void Len_utf8_multibyte()
    {
        Assert.AreEqual(2, GoString.FromNetString("\u00e9").Len);
        Assert.AreEqual(3, GoString.FromNetString("世").Len);
        Assert.AreEqual(7, GoString.FromNetString("世界!").Len);
    }

    [TestMethod]
    public void Len_empty()
    {
        Assert.AreEqual(0, GoString.FromNetString("").Len);
    }

    [TestMethod]
    public void ByteAt_ascii()
    {
        GoString gs = GoString.FromNetString("hello");
        Assert.AreEqual((byte)'h', gs[0]);
        Assert.AreEqual((byte)'o', gs[4]);
    }

    [TestMethod]
    public void ByteAt_out_of_range()
    {
        GoString gs = GoString.FromNetString("hi");
        Assert.ThrowsException<GoPanicException>(() => { var _ = gs[2]; });
    }

    [TestMethod]
    public void ToBytes_and_back()
    {
        GoString gs = GoString.FromNetString("hello");
        var bytes = GoString.ToBytes(gs);
        Assert.AreEqual(5, bytes.Len);
        Assert.AreEqual((byte)'h', bytes[0]);
        var result = GoString.FromBytes(bytes);
        Assert.AreEqual("hello", result.ToNetString());
    }

    [TestMethod]
    public void ToBytes_utf8()
    {
        GoString gs = GoString.FromNetString("\u00e9");
        var bytes = GoString.ToBytes(gs);
        Assert.AreEqual(2, bytes.Len);
        Assert.AreEqual("\u00e9", GoString.FromBytes(bytes).ToNetString());
    }

    [TestMethod]
    public void ToRunes_ascii()
    {
        GoString gs = GoString.FromNetString("abc");
        var runes = GoString.ToRunes(gs);
        Assert.AreEqual(3, runes.Len);
        Assert.AreEqual('a', runes[0]);
        Assert.AreEqual('b', runes[1]);
        Assert.AreEqual('c', runes[2]);
    }

    [TestMethod]
    public void ToRunes_unicode()
    {
        GoString gs = GoString.FromNetString("世界");
        var runes = GoString.ToRunes(gs);
        Assert.AreEqual(2, runes.Len);
        Assert.AreEqual(0x4E16, runes[0]);
        Assert.AreEqual(0x754C, runes[1]);
    }

    [TestMethod]
    public void FromRunes()
    {
        var runes = new Slice<int>(new[] { 0x4E16, 0x754C });
        Assert.AreEqual("世界", GoString.FromRunes(runes).ToNetString());
    }

    [TestMethod]
    public void RangeRunes_ascii()
    {
        GoString gs = GoString.FromNetString("abc");
        var result = GoString.RangeRunes(gs).ToList();
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual((0, (int)'a'), result[0]);
        Assert.AreEqual((1, (int)'b'), result[1]);
        Assert.AreEqual((2, (int)'c'), result[2]);
    }

    [TestMethod]
    public void RangeRunes_multibyte()
    {
        GoString gs = GoString.FromNetString("a\u00e9");
        var result = GoString.RangeRunes(gs).ToList();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual((0, (int)'a'), result[0]);
        Assert.AreEqual((1, 0xE9), result[1]);
    }

    [TestMethod]
    public void FromRune()
    {
        Assert.AreEqual("A", GoString.FromRune(65).ToNetString());
        Assert.AreEqual("世", GoString.FromRune(0x4E16).ToNetString());
    }

    [TestMethod]
    public void Slice_ascii()
    {
        GoString gs = GoString.FromNetString("hello");
        Assert.AreEqual("ell", gs.Slice(1, 4).ToNetString());
    }

    [TestMethod]
    public void Slice_bounds_check()
    {
        GoString gs = GoString.FromNetString("hi");
        Assert.ThrowsException<GoPanicException>(() => gs.Slice(0, 5));
    }

    [TestMethod]
    public void Nil_bytes_round_trip()
    {
        Assert.AreEqual("", GoString.FromBytes(default).ToNetString());
    }

    [TestMethod]
    public void Nil_runes_round_trip()
    {
        Assert.AreEqual("", GoString.FromRunes(default).ToNetString());
    }

    [TestMethod]
    public void Len_four_byte_emoji()
    {
        Assert.AreEqual(4, GoString.FromNetString("\U0001F389").Len);
    }

    [TestMethod]
    public void Len_mixed_ascii_and_multibyte()
    {
        Assert.AreEqual(9, GoString.FromNetString("Go世界!").Len);
    }

    [TestMethod]
    public void ByteAt_multibyte()
    {
        GoString gs = GoString.FromNetString("\u00e9");
        Assert.AreEqual(0xC3, gs[0]);
        Assert.AreEqual(0xA9, gs[1]);
    }

    [TestMethod]
    public void ByteAt_four_byte_emoji()
    {
        GoString gs = GoString.FromNetString("\U0001F389");
        Assert.AreEqual(0xF0, gs[0]);
        Assert.AreEqual(0x9F, gs[1]);
        Assert.AreEqual(0x8E, gs[2]);
        Assert.AreEqual(0x89, gs[3]);
    }

    [TestMethod]
    public void ByteAt_mixed_ascii_then_multibyte()
    {
        GoString gs = GoString.FromNetString("a\u00e9");
        Assert.AreEqual(0x61, gs[0]);
        Assert.AreEqual(0xC3, gs[1]);
        Assert.AreEqual(0xA9, gs[2]);
    }

    [TestMethod]
    public void RangeRunes_four_byte_emoji()
    {
        GoString gs = GoString.FromNetString("A\U0001F389B");
        var result = GoString.RangeRunes(gs).ToList();
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual((0, (int)'A'), result[0]);
        Assert.AreEqual((1, 0x1F389), result[1]);
        Assert.AreEqual((5, (int)'B'), result[2]);
    }

    [TestMethod]
    public void Slice_multibyte()
    {
        GoString gs = GoString.FromNetString("a\u00e9b");
        Assert.AreEqual("\u00e9", gs.Slice(1, 3).ToNetString());
    }

    [TestMethod]
    public void Slice_four_byte_emoji()
    {
        GoString gs = GoString.FromNetString("A\U0001F389B");
        Assert.AreEqual("\U0001F389", gs.Slice(1, 5).ToNetString());
    }

    [TestMethod]
    public void ToBytes_four_byte_emoji()
    {
        GoString gs = GoString.FromNetString("\U0001F389");
        var bytes = GoString.ToBytes(gs);
        Assert.AreEqual(4, bytes.Len);
        Assert.AreEqual(0xF0, bytes[0]);
        Assert.AreEqual(0x9F, bytes[1]);
        Assert.AreEqual(0x8E, bytes[2]);
        Assert.AreEqual(0x89, bytes[3]);
    }

    [TestMethod]
    public void ToRunes_four_byte_emoji()
    {
        GoString gs = GoString.FromNetString("A\U0001F389B");
        var runes = GoString.ToRunes(gs);
        Assert.AreEqual(3, runes.Len);
        Assert.AreEqual('A', runes[0]);
        Assert.AreEqual(0x1F389, runes[1]);
        Assert.AreEqual('B', runes[2]);
    }

    [TestMethod]
    public void FromBytes_offset_slice()
    {
        GoString gs = GoString.FromNetString("hello world");
        var full = GoString.ToBytes(gs);
        var sub = full.Reslice(6, 11);
        Assert.AreEqual("world", GoString.FromBytes(sub).ToNetString());
    }

    [TestMethod]
    public void Equality_operators()
    {
        GoString a = GoString.FromNetString("hello");
        GoString b = GoString.FromNetString("hello");
        GoString c = GoString.FromNetString("world");
        Assert.IsTrue(a == b);
        Assert.IsFalse(a == c);
        Assert.IsTrue(a != c);
    }

    [TestMethod]
    public void Comparison_operators()
    {
        GoString a = GoString.FromNetString("abc");
        GoString b = GoString.FromNetString("abd");
        Assert.IsTrue(a < b);
        Assert.IsTrue(b > a);
        Assert.IsTrue(a <= b);
        GoString sameAsA = GoString.FromNetString("abc");
        Assert.IsTrue(a <= sameAsA);
    }

    [TestMethod]
    public void Concatenation()
    {
        GoString a = GoString.FromNetString("hello");
        GoString b = GoString.FromNetString(" world");
        GoString result = a + b;
        Assert.AreEqual("hello world", result.ToNetString());
    }

    [TestMethod]
    public void Default_is_empty_string()
    {
        GoString gs = default;
        Assert.AreEqual(0, gs.Len);
        Assert.AreEqual("", gs.ToNetString());
    }

    [TestMethod]
    public void Implicit_conversion_from_net_string()
    {
        GoString gs = "hello";
        Assert.AreEqual("hello", gs.ToNetString());
    }
}
