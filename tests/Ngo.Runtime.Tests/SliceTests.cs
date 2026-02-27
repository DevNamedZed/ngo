// -----------------------------------------------------------------------
// <copyright file="SliceTests.cs" company="Ziad">
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
public class SliceTests
{
    [TestMethod]
    public void Default_slice_is_nil()
    {
        var s = default(Slice<int>);
        Assert.IsTrue(s.IsNil);
        Assert.AreEqual(0, s.Len);
        Assert.AreEqual(0, s.Cap);
    }

    [TestMethod]
    public void Make_creates_zeroed_slice()
    {
        var s = Slice<int>.Make(3);
        Assert.IsFalse(s.IsNil);
        Assert.AreEqual(3, s.Len);
        Assert.AreEqual(3, s.Cap);
        Assert.AreEqual(0, s[0]);
        Assert.AreEqual(0, s[1]);
        Assert.AreEqual(0, s[2]);
    }

    [TestMethod]
    public void Make_with_capacity()
    {
        var s = Slice<int>.Make(2, 5);
        Assert.AreEqual(2, s.Len);
        Assert.AreEqual(5, s.Cap);
    }

    [TestMethod]
    public void From_array()
    {
        var s = new Slice<int>(new[] { 10, 20, 30 });
        Assert.AreEqual(3, s.Len);
        Assert.AreEqual(10, s[0]);
        Assert.AreEqual(20, s[1]);
        Assert.AreEqual(30, s[2]);
    }

    [TestMethod]
    public void Index_out_of_range_panics()
    {
        var s = Slice<int>.Make(3);
        var ex = Assert.ThrowsException<GoPanicException>(() => { var _ = s[3]; });
        StringAssert.Contains(ex.Message, "index out of range");
    }

    [TestMethod]
    public void Negative_index_panics()
    {
        var s = Slice<int>.Make(3);
        Assert.ThrowsException<GoPanicException>(() => { var _ = s[-1]; });
    }

    [TestMethod]
    public void Set_element()
    {
        var s = Slice<int>.Make(3);
        s[1] = 42;
        Assert.AreEqual(42, s[1]);
    }

    [TestMethod]
    public void Reslice_two_index()
    {
        var s = new Slice<int>(new[] { 10, 20, 30, 40, 50 });
        var sub = s.Reslice(1, 3);
        Assert.AreEqual(2, sub.Len);
        Assert.AreEqual(4, sub.Cap); // cap = original cap - low
        Assert.AreEqual(20, sub[0]);
        Assert.AreEqual(30, sub[1]);
    }

    [TestMethod]
    public void Reslice_three_index()
    {
        var s = new Slice<int>(new[] { 10, 20, 30, 40, 50 });
        var sub = s.Reslice(1, 3, 4);
        Assert.AreEqual(2, sub.Len);
        Assert.AreEqual(3, sub.Cap); // cap = max - low
        Assert.AreEqual(20, sub[0]);
        Assert.AreEqual(30, sub[1]);
    }

    [TestMethod]
    public void Reslice_bounds_check()
    {
        var s = Slice<int>.Make(3);
        Assert.ThrowsException<GoPanicException>(() => s.Reslice(-1, 2));
        Assert.ThrowsException<GoPanicException>(() => s.Reslice(0, 4));
        Assert.ThrowsException<GoPanicException>(() => s.Reslice(2, 1));
    }

    [TestMethod]
    public void Shared_backing_array()
    {
        var s = new Slice<int>(new[] { 10, 20, 30 });
        var sub = s.Reslice(1, 3);
        sub[0] = 99;
        Assert.AreEqual(99, s[1]); // mutation visible through original
    }

    [TestMethod]
    public void Append_to_nil_slice()
    {
        var s = default(Slice<int>);
        s = Slice<int>.Append(s, 1, 2, 3);
        Assert.AreEqual(3, s.Len);
        Assert.AreEqual(1, s[0]);
        Assert.AreEqual(2, s[1]);
        Assert.AreEqual(3, s[2]);
    }

    [TestMethod]
    public void Append_within_capacity()
    {
        var s = Slice<int>.Make(2, 5);
        s[0] = 10;
        s[1] = 20;
        var s2 = Slice<int>.Append(s, 30);
        Assert.AreEqual(3, s2.Len);
        Assert.AreEqual(5, s2.Cap);
        Assert.AreEqual(30, s2[2]);
    }

    [TestMethod]
    public void Append_grows_when_capacity_exceeded()
    {
        var s = new Slice<int>(new[] { 1, 2, 3 });
        Assert.AreEqual(3, s.Cap);
        var s2 = Slice<int>.Append(s, 4);
        Assert.AreEqual(4, s2.Len);
        Assert.IsTrue(s2.Cap >= 4);
        Assert.AreEqual(4, s2[3]);
    }

    [TestMethod]
    public void Append_slice_to_slice()
    {
        var s1 = new Slice<int>(new[] { 1, 2 });
        var s2 = new Slice<int>(new[] { 3, 4 });
        var result = Slice<int>.Append(s1, s2);
        Assert.AreEqual(4, result.Len);
        Assert.AreEqual(1, result[0]);
        Assert.AreEqual(4, result[3]);
    }

    [TestMethod]
    public void Copy_slices()
    {
        var src = new Slice<int>(new[] { 10, 20, 30 });
        var dst = Slice<int>.Make(5);
        var n = Slice<int>.Copy(dst, src);
        Assert.AreEqual(3, n);
        Assert.AreEqual(10, dst[0]);
        Assert.AreEqual(20, dst[1]);
        Assert.AreEqual(30, dst[2]);
        Assert.AreEqual(0, dst[3]); // untouched
    }

    [TestMethod]
    public void Copy_truncates_when_dst_smaller()
    {
        var src = new Slice<int>(new[] { 10, 20, 30 });
        var dst = Slice<int>.Make(2);
        var n = Slice<int>.Copy(dst, src);
        Assert.AreEqual(2, n);
        Assert.AreEqual(10, dst[0]);
        Assert.AreEqual(20, dst[1]);
    }

    [TestMethod]
    public void Copy_nil_returns_zero()
    {
        var s = default(Slice<int>);
        Assert.AreEqual(0, Slice<int>.Copy(s, s));
    }

    [TestMethod]
    public void Enumeration()
    {
        var s = new Slice<int>(new[] { 10, 20, 30 });
        var items = s.ToList();
        Assert.AreEqual(3, items.Count);
        Assert.AreEqual(10, items[0]);
        Assert.AreEqual(20, items[1]);
        Assert.AreEqual(30, items[2]);
    }

    [TestMethod]
    public void String_slice()
    {
        var s = new Slice<string>(new[] { "hello", "world" });
        Assert.AreEqual(2, s.Len);
        Assert.AreEqual("hello", s[0]);
        Assert.AreEqual("world", s[1]);
    }

    [TestMethod]
    public void Growth_doubles_for_small_slices()
    {
        var s = default(Slice<int>);
        for (int i = 0; i < 10; i++)
        {
            s = Slice<int>.Append(s, i);
        }
        Assert.AreEqual(10, s.Len);
        Assert.IsTrue(s.Cap >= 10);
        // Verify all values
        for (int i = 0; i < 10; i++)
        {
            Assert.AreEqual(i, s[i]);
        }
    }

    [TestMethod]
    public void AsSpan_nil_slice_returns_empty()
    {
        var s = default(Slice<int>);
        Assert.IsTrue(s.AsSpan().IsEmpty);
        Assert.IsTrue(s.AsReadOnlySpan().IsEmpty);
    }

    [TestMethod]
    public void AsSpan_returns_correct_window()
    {
        var s = new Slice<int>(new[] { 10, 20, 30, 40, 50 });
        var sub = s.Reslice(1, 4);
        var span = sub.AsSpan();
        Assert.AreEqual(3, span.Length);
        Assert.AreEqual(20, span[0]);
        Assert.AreEqual(30, span[1]);
        Assert.AreEqual(40, span[2]);
    }

    [TestMethod]
    public void AsSpan_mutation_visible_through_slice()
    {
        var s = new Slice<int>(new[] { 1, 2, 3 });
        var span = s.AsSpan();
        span[1] = 99;
        Assert.AreEqual(99, s[1]);
    }

    [TestMethod]
    public void AsReadOnlySpan_matches_slice_contents()
    {
        var s = new Slice<byte>(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE });
        var sub = s.Reslice(1, 3);
        var ros = sub.AsReadOnlySpan();
        Assert.AreEqual(2, ros.Length);
        Assert.AreEqual(0xFE, ros[0]);
        Assert.AreEqual(0xBA, ros[1]);
    }

    [TestMethod]
    public void Append_slice_to_nil_slice()
    {
        var nil = default(Slice<int>);
        var other = new Slice<int>(new[] { 7, 8, 9 });
        var result = Slice<int>.Append(nil, other);
        Assert.AreEqual(3, result.Len);
        Assert.AreEqual(7, result[0]);
        Assert.AreEqual(9, result[2]);
    }
}
