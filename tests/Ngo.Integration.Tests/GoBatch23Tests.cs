// -----------------------------------------------------------------------
// <copyright file="GoBatch23Tests.cs" company="Ziad">
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
public class GoBatch23Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGosimpleUnidecode()
    {
        var dir = EnsureModule("github.com/gosimple/unidecode", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gosimple-unidecode", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoogleGoIntervals()
    {
        var dir = EnsureModule("github.com/google/go-intervals", "v0.0.2", "timespanset");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-intervals-timespanset", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoDustinGoHumanize()
    {
        var dir = EnsureModule("github.com/dustin/go-humanize", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("dustin-go-humanize", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoLucasbeyerGoColorful()
    {
        var dir = EnsureModule("github.com/lucasb-eyer/go-colorful", "v1.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-colorful", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAymanbagabasGoOsc52()
    {
        var dir = EnsureModule("github.com/aymanbagabas/go-osc52/v2", "v2.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-osc52-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTsenartGoTsz()
    {
        var dir = EnsureModule("github.com/tsenart/go-tsz", "v0.0.0-20180814235614-0bd30b3df1c3");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tsenart-go-tsz", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoBitsAndBloomsBitset()
    {
        var dir = EnsureModule("github.com/bits-and-blooms/bitset", "v1.13.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("bitset", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoRivoUniseg()
    {
        var dir = EnsureModule("github.com/rivo/uniseg", "v0.4.7");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("rivo-uniseg", errors);
    }
}
