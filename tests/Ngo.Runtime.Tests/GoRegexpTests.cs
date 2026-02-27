// -----------------------------------------------------------------------
// <copyright file="GoRegexpTests.cs" company="Ziad">
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
public class GoRegexpTests
{
    [TestMethod]
    public void Compile_valid_pattern()
    {
        var (re, err) = GoRegexp.Compile("[a-z]+");
        Assert.AreEqual("", err);
        Assert.IsTrue(re.MatchString("hello"));
        Assert.IsFalse(re.MatchString("123"));
    }

    [TestMethod]
    public void MustCompile_valid_pattern()
    {
        var re = GoRegexp.MustCompile("[0-9]+");
        Assert.IsTrue(re.MatchString("abc123"));
        Assert.IsFalse(re.MatchString("abc"));
    }

    [TestMethod]
    public void FindString()
    {
        var re = GoRegexp.MustCompile("[0-9]+");
        Assert.AreEqual("123", re.FindString("abc123def"));
        Assert.AreEqual("", re.FindString("abcdef"));
    }

    [TestMethod]
    public void FindAllString()
    {
        var re = GoRegexp.MustCompile("[0-9]+");
        var result = re.FindAllString("a1b22c333", -1);
        Assert.AreEqual(3, result.Len);
        Assert.AreEqual("1", result[0]);
        Assert.AreEqual("22", result[1]);
        Assert.AreEqual("333", result[2]);
    }

    [TestMethod]
    public void FindAllString_limited()
    {
        var re = GoRegexp.MustCompile("[0-9]+");
        var result = re.FindAllString("a1b22c333", 2);
        Assert.AreEqual(2, result.Len);
    }

    [TestMethod]
    public void ReplaceAllString()
    {
        var re = GoRegexp.MustCompile("[0-9]+");
        Assert.AreEqual("a_b_c_", re.ReplaceAllString("a1b22c333", "_"));
    }

    [TestMethod]
    public void Split()
    {
        var re = GoRegexp.MustCompile("[,;]");
        var result = re.Split("a,b;c,d", -1);
        Assert.AreEqual(4, result.Len);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("d", result[3]);
    }

    [TestMethod]
    public void MatchString_static()
    {
        var (matched, err) = GoRegexp.MatchString("^[a-z]+$", "hello");
        Assert.AreEqual("", err);
        Assert.IsTrue(matched);
    }

    [TestMethod]
    public void FindStringSubmatch()
    {
        var re = GoRegexp.MustCompile("([a-z]+)([0-9]+)");
        var result = re.FindStringSubmatch("abc123");
        Assert.AreEqual(3, result.Len);
        Assert.AreEqual("abc123", result[0]);
        Assert.AreEqual("abc", result[1]);
        Assert.AreEqual("123", result[2]);
    }
}
