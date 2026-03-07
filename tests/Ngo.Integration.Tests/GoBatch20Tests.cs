// -----------------------------------------------------------------------
// <copyright file="GoBatch20Tests.cs" company="Ziad">
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

namespace Ngo.Integration.Tests;

[TestClass]
public class GoBatch20Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoX448()
    {
        var dir = EnsureModule("github.com/x448/float16", "v0.8.4");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("x448-float16", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAlexflint()
    {
        var dir = EnsureModule("github.com/alexflint/go-scalar", "v1.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-scalar", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMrjermpGo_spin()
    {
        var dir = EnsureModule("github.com/tj/go-spin", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("tj-go-spin", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAndrewstuart()
    {
        var dir = EnsureModule("github.com/andrew-d/go-termutil", "v0.0.0-20150726205930-009166a695a2");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-termutil", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoRussmillRankDB()
    {
        var dir = EnsureModule("github.com/russross/blackfriday/v2", "v2.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("blackfriday-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoLithammer()
    {
        var dir = EnsureModule("github.com/lithammer/shortuuid/v3", "v3.0.7");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("shortuuid-v3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoVapierlipgloss()
    {
        var dir = EnsureModule("github.com/muesli/reflow", "v0.3.0", "wordwrap");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("muesli-wordwrap", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMuesliAnsi()
    {
        var dir = EnsureModule("github.com/muesli/reflow", "v0.3.0", "ansi");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("muesli-ansi", errors);
    }
}
