// -----------------------------------------------------------------------
// <copyright file="BuiltInTests.cs" company="Ziad">
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Runtime.Tests;

[TestClass]
public class BuiltInTests
{
    [TestMethod]
    public void Len_slice()
    {
        var s = new Slice<int>(new[] { 1, 2, 3 });
        Assert.AreEqual(3, BuiltIn.Len(s));
    }

    [TestMethod]
    public void Len_string()
    {
        Assert.AreEqual(5, BuiltIn.Len("hello"));
    }

    [TestMethod]
    public void Len_map()
    {
        var m = new Map<string, int>();
        m["a"] = 1;
        Assert.AreEqual(1, BuiltIn.Len(m));
    }

    [TestMethod]
    public void Len_channel()
    {
        var ch = new Channel<int>(3);
        ch.Send(1);
        Assert.AreEqual(1, BuiltIn.Len(ch));
    }

    [TestMethod]
    public void Cap_slice()
    {
        var s = Slice<int>.Make(2, 10);
        Assert.AreEqual(10, BuiltIn.Cap(s));
    }

    [TestMethod]
    public void Cap_channel()
    {
        var ch = new Channel<int>(5);
        Assert.AreEqual(5, BuiltIn.Cap(ch));
    }

    [TestMethod]
    public void MakeSlice()
    {
        var s = BuiltIn.MakeSlice<int>(3, 5);
        Assert.AreEqual(3, s.Len);
        Assert.AreEqual(5, s.Cap);
    }

    [TestMethod]
    public void MakeMap()
    {
        var m = BuiltIn.MakeMap<string, int>();
        Assert.IsFalse(m.IsNil);
        Assert.AreEqual(0, m.Len);
    }

    [TestMethod]
    public void MakeChan()
    {
        var ch = BuiltIn.MakeChan<int>(3);
        Assert.AreEqual(3, ch.Capacity);
    }

    [TestMethod]
    public void Append_elements()
    {
        var s = new Slice<int>(new[] { 1, 2 });
        var s2 = BuiltIn.Append(s, 3, 4);
        Assert.AreEqual(4, s2.Len);
        Assert.AreEqual(3, s2[2]);
        Assert.AreEqual(4, s2[3]);
    }

    [TestMethod]
    public void Copy_slices()
    {
        var src = new Slice<int>(new[] { 10, 20, 30 });
        var dst = Slice<int>.Make(3);
        var n = BuiltIn.Copy(dst, src);
        Assert.AreEqual(3, n);
        Assert.AreEqual(10, dst[0]);
    }

    [TestMethod]
    public void Delete_map()
    {
        var m = new Map<string, int>();
        m["a"] = 1;
        BuiltIn.Delete(m, "a");
        Assert.AreEqual(0, m.Len);
    }

    [TestMethod]
    public void Close_channel()
    {
        var ch = new Channel<int>(1);
        BuiltIn.Close(ch);
        Assert.IsTrue(ch.IsClosed);
    }

    [TestMethod]
    public void New_returns_zeroed_ptr()
    {
        var p = BuiltIn.New<int>();
        Assert.AreEqual(0, p.Value);
    }

    [TestMethod]
    public void Panic_throws()
    {
        var ex = Assert.ThrowsException<GoPanicException>(() => BuiltIn.Panic("boom"));
        Assert.AreEqual("boom", ex.Value);
    }

    [TestMethod]
    public void Recover_outside_panic_returns_null()
    {
        Assert.IsNull(BuiltIn.Recover());
    }
}
