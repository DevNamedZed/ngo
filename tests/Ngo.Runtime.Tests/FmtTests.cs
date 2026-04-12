// -----------------------------------------------------------------------
// <copyright file="FmtTests.cs" company="Ziad">
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
using static global::Ngo.Runtime.Fmt.Package;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Runtime.Tests;

[TestClass]
public class FmtTests
{
    [TestMethod]
    public void Sprintf_string_verb()
    {
        var result = Sprintf("hello %s", "world");
        Assert.AreEqual("hello world", result);
    }

    [TestMethod]
    public void Sprintf_int_verb()
    {
        var result = Sprintf("x=%d", 42);
        Assert.AreEqual("x=42", result);
    }

    [TestMethod]
    public void Sprintf_multiple_verbs()
    {
        var result = Sprintf("%s is %d", "age", 30);
        Assert.AreEqual("age is 30", result);
    }

    [TestMethod]
    public void Sprintf_value_verb()
    {
        var result = Sprintf("%v %v %v", 1, "hi", true);
        Assert.AreEqual("1 hi true", result);
    }

    [TestMethod]
    public void Sprintf_percent_literal()
    {
        var result = Sprintf("100%%");
        Assert.AreEqual("100%", result);
    }

    [TestMethod]
    public void Sprintf_hex_verb()
    {
        var result = Sprintf("%x", 255);
        Assert.AreEqual("ff", result);
    }

    [TestMethod]
    public void Sprintf_bool_verb()
    {
        var result = Sprintf("%t", true);
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void Sprint_non_strings_get_spaces()
    {
        var result = Sprint(1, 2, 3);
        Assert.AreEqual("1 2 3", result);
    }

    [TestMethod]
    public void Sprint_strings_no_spaces()
    {
        var result = Sprint("a", "b");
        Assert.AreEqual("ab", result);
    }

    [TestMethod]
    public void Sprintln_adds_spaces_and_newline()
    {
        var result = Sprintln("hello", "world");
        Assert.AreEqual("hello world\n", result);
    }

    [TestMethod]
    public void Sprintf_quoted_string()
    {
        var result = Sprintf("%q", "hello");
        Assert.AreEqual("\"hello\"", result);
    }

    [TestMethod]
    public void Sprintf_type_verb()
    {
        var result = Sprintf("%T", 42);
        Assert.AreEqual("int", result);
    }

    // ---- Width/Precision/Flags ----

    [TestMethod]
    public void Sprintf_width_right_pad_int()
    {
        var result = Sprintf("%5d", 42);
        Assert.AreEqual("   42", result);
    }

    [TestMethod]
    public void Sprintf_width_left_align_int()
    {
        var result = Sprintf("%-5d", 42);
        Assert.AreEqual("42   ", result);
    }

    [TestMethod]
    public void Sprintf_width_zero_pad_int()
    {
        var result = Sprintf("%05d", 42);
        Assert.AreEqual("00042", result);
    }

    [TestMethod]
    public void Sprintf_precision_float()
    {
        var result = Sprintf("%.2f", 3.14159);
        Assert.AreEqual("3.14", result);
    }

    [TestMethod]
    public void Sprintf_width_and_precision_float()
    {
        var result = Sprintf("%10.2f", 3.14);
        Assert.AreEqual("      3.14", result);
    }

    [TestMethod]
    public void Sprintf_plus_flag_int()
    {
        var result = Sprintf("%+d %+d", 42, -42);
        Assert.AreEqual("+42 -42", result);
    }

    [TestMethod]
    public void Sprintf_precision_string_truncation()
    {
        var result = Sprintf("%.3s", "hello");
        Assert.AreEqual("hel", result);
    }

    [TestMethod]
    public void Sprintf_width_left_align_string()
    {
        var result = Sprintf("%-10s", "hello");
        Assert.AreEqual("hello     ", result);
    }

    [TestMethod]
    public void Sprintf_hash_flag_hex()
    {
        var result = Sprintf("%#x", 42);
        Assert.AreEqual("0x2a", result);
    }

    [TestMethod]
    public void Sprintf_hash_flag_octal()
    {
        var result = Sprintf("%#o", 42);
        Assert.AreEqual("052", result);
    }

    [TestMethod]
    public void Sprintf_precision_zero_float()
    {
        var result = Sprintf("%.0f", 3.7);
        Assert.AreEqual("4", result);
    }
}
