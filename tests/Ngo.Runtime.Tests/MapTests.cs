// -----------------------------------------------------------------------
// <copyright file="MapTests.cs" company="Ziad">
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
public class MapTests
{
    [TestMethod]
    public void Empty_map()
    {
        var m = new Map<string, int>();
        Assert.IsFalse(m.IsNil);
        Assert.AreEqual(0, m.Len);
    }

    [TestMethod]
    public void Nil_map()
    {
        var m = Map<string, int>.Nil();
        Assert.IsTrue(m.IsNil);
        Assert.AreEqual(0, m.Len);
    }

    [TestMethod]
    public void Set_and_get()
    {
        var m = new Map<string, int>();
        m["a"] = 1;
        m["b"] = 2;
        Assert.AreEqual(1, m["a"]);
        Assert.AreEqual(2, m["b"]);
        Assert.AreEqual(2, m.Len);
    }

    [TestMethod]
    public void Missing_key_returns_zero_value()
    {
        var m = new Map<string, int>();
        Assert.AreEqual(0, m["missing"]);
    }

    [TestMethod]
    public void Missing_key_string_returns_empty()
    {
        var m = new Map<int, string>();
        Assert.IsNull(m[42]); // .NET default for string is null
    }

    [TestMethod]
    public void Nil_map_read_returns_zero()
    {
        var m = Map<string, int>.Nil();
        Assert.AreEqual(0, m["anything"]);
    }

    [TestMethod]
    public void Nil_map_write_panics()
    {
        var m = Map<string, int>.Nil();
        var ex = Assert.ThrowsException<GoPanicException>(() => m["key"] = 42);
        StringAssert.Contains(ex.Message, "nil map");
    }

    [TestMethod]
    public void Two_value_lookup_found()
    {
        var m = new Map<string, int>();
        m["key"] = 42;
        var (value, ok) = m.Get("key");
        Assert.AreEqual(42, value);
        Assert.IsTrue(ok);
    }

    [TestMethod]
    public void Two_value_lookup_not_found()
    {
        var m = new Map<string, int>();
        var (value, ok) = m.Get("missing");
        Assert.AreEqual(0, value);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Two_value_lookup_nil_map()
    {
        var m = Map<string, int>.Nil();
        var (value, ok) = m.Get("anything");
        Assert.AreEqual(0, value);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Delete_key()
    {
        var m = new Map<string, int>();
        m["a"] = 1;
        m["b"] = 2;
        m.Delete("a");
        Assert.AreEqual(1, m.Len);
        Assert.IsFalse(m.ContainsKey("a"));
    }

    [TestMethod]
    public void Delete_missing_key_is_noop()
    {
        var m = new Map<string, int>();
        m.Delete("missing"); // should not throw
    }

    [TestMethod]
    public void Delete_nil_map_is_noop()
    {
        var m = Map<string, int>.Nil();
        m.Delete("anything"); // should not throw
    }

    [TestMethod]
    public void Range_returns_all_entries()
    {
        var m = new Map<string, int>();
        m["a"] = 1;
        m["b"] = 2;
        m["c"] = 3;

        var entries = m.Range().ToList();
        Assert.AreEqual(3, entries.Count);

        // All keys should be present (order may vary)
        var keys = entries.Select(e => e.key).OrderBy(k => k).ToList();
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, keys);
    }

    [TestMethod]
    public void Range_nil_map_returns_empty()
    {
        var m = Map<string, int>.Nil();
        var entries = m.Range().ToList();
        Assert.AreEqual(0, entries.Count);
    }

    [TestMethod]
    public void Overwrite_existing_key()
    {
        var m = new Map<string, int>();
        m["key"] = 1;
        m["key"] = 2;
        Assert.AreEqual(2, m["key"]);
        Assert.AreEqual(1, m.Len);
    }

    [TestMethod]
    public void Map_with_capacity_hint()
    {
        var m = new Map<string, int>(100);
        Assert.IsFalse(m.IsNil);
        Assert.AreEqual(0, m.Len);
        m["a"] = 1;
        Assert.AreEqual(1, m.Len);
    }
}
