// -----------------------------------------------------------------------
// <copyright file="GoSortTests.cs" company="Ziad">
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

using Ngo.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Runtime.Tests;

[TestClass]
public class GoSortTests
{
    [TestMethod]
    public void Ints_sorts_ascending()
    {
        var s = new Slice<long>(new long[] { 5, 3, 1, 4, 2 });
        GoSort.Ints(s);
        Assert.AreEqual(1, s[0]);
        Assert.AreEqual(2, s[1]);
        Assert.AreEqual(3, s[2]);
        Assert.AreEqual(4, s[3]);
        Assert.AreEqual(5, s[4]);
    }

    [TestMethod]
    public void Strings_sorts_lexicographic()
    {
        var s = new Slice<string>(new[] { "banana", "apple", "cherry" });
        GoSort.Strings(s);
        Assert.AreEqual("apple", s[0]);
        Assert.AreEqual("banana", s[1]);
        Assert.AreEqual("cherry", s[2]);
    }

    [TestMethod]
    public void Float64s_sorts_ascending()
    {
        var s = new Slice<double>(new[] { 3.14, 1.0, 2.71 });
        GoSort.Float64s(s);
        Assert.AreEqual(1.0, s[0]);
        Assert.AreEqual(2.71, s[1]);
        Assert.AreEqual(3.14, s[2]);
    }

    [TestMethod]
    public void IntsAreSorted_returns_true_for_sorted()
    {
        var s = new Slice<long>(new long[] { 1, 2, 3, 4, 5 });
        Assert.IsTrue(GoSort.IntsAreSorted(s));
    }

    [TestMethod]
    public void IntsAreSorted_returns_false_for_unsorted()
    {
        var s = new Slice<long>(new long[] { 3, 1, 2 });
        Assert.IsFalse(GoSort.IntsAreSorted(s));
    }

    [TestMethod]
    public void StringsAreSorted_works()
    {
        var sorted = new Slice<string>(new[] { "a", "b", "c" });
        Assert.IsTrue(GoSort.StringsAreSorted(sorted));

        var unsorted = new Slice<string>(new[] { "b", "a" });
        Assert.IsFalse(GoSort.StringsAreSorted(unsorted));
    }

    [TestMethod]
    public void Float64sAreSorted_works()
    {
        var sorted = new Slice<double>(new[] { 1.0, 2.0, 3.0 });
        Assert.IsTrue(GoSort.Float64sAreSorted(sorted));

        var unsorted = new Slice<double>(new[] { 3.0, 1.0 });
        Assert.IsFalse(GoSort.Float64sAreSorted(unsorted));
    }

    [TestMethod]
    public void SearchInts_finds_insertion_point()
    {
        var s = new Slice<long>(new long[] { 1, 3, 5, 7, 9 });
        Assert.AreEqual(2, GoSort.SearchInts(s, 5));  // exact match
        Assert.AreEqual(2, GoSort.SearchInts(s, 4));  // between 3 and 5
        Assert.AreEqual(0, GoSort.SearchInts(s, 0));  // before all
        Assert.AreEqual(5, GoSort.SearchInts(s, 10)); // after all
    }

    [TestMethod]
    public void SearchStrings_finds_insertion_point()
    {
        var s = new Slice<string>(new[] { "apple", "banana", "cherry" });
        Assert.AreEqual(1, GoSort.SearchStrings(s, "banana"));  // exact
        Assert.AreEqual(1, GoSort.SearchStrings(s, "avocado")); // between apple and banana
    }

    [TestMethod]
    public void Ints_sorts_empty_slice()
    {
        var s = Slice<long>.Make(0);
        GoSort.Ints(s);
        Assert.AreEqual(0, s.Len);
    }

    [TestMethod]
    public void Ints_sorts_single_element()
    {
        var s = new Slice<long>(new long[] { 42 });
        GoSort.Ints(s);
        Assert.AreEqual(42, s[0]);
    }
}
