// -----------------------------------------------------------------------
// <copyright file="GoBatch22Tests.cs" company="Ziad">
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
public class GoBatch22Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHuanduXstrings()
    {
        var dir = EnsureModule("github.com/huandu/xstrings", "v1.5.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("huandu-xstrings", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoItchynyTimefmt()
    {
        var dir = EnsureModule("github.com/itchyny/timefmt-go", "v0.1.6");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("itchyny-timefmt", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHakoDurafmt()
    {
        var dir = EnsureModule("github.com/hako/durafmt", "v0.0.0-20210608085754-5c1018a4e16b");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("hako-durafmt", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoStoewerGoStrcase()
    {
        var dir = EnsureModule("github.com/stoewer/go-strcase", "v1.3.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("stoewer-go-strcase", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoJosharianNative()
    {
        var dir = EnsureModule("github.com/josharian/native", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("josharian-native", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAxiomhqVariance()
    {
        var dir = EnsureModule("github.com/axiomhq/variance", "v0.3.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("axiomhq-variance", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMontanaflynnStats()
    {
        var dir = EnsureModule("github.com/montanaflynn/stats", "v0.7.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("montanaflynn-stats", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoZeeboAssert()
    {
        var dir = EnsureModule("github.com/zeebo/assert", "v1.3.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("zeebo-assert", errors);
    }
}
