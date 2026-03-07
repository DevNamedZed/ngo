// -----------------------------------------------------------------------
// <copyright file="GoSimpleTests.cs" company="Ziad">
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
public class GoSimpleTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_MaruelNatural()
    {
        var dir = EnsureModule("github.com/maruel/natural", "v1.1.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("maruel-natural", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_MgutzAnsi()
    {
        var dir = EnsureModule("github.com/mgutz/ansi", "v0.0.0-20200706080929-d51e80ef957d");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("mgutz-ansi", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoColorable()
    {
        var dir = EnsureModule("github.com/mattn/go-colorable", "v0.1.13");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-colorable", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHclog()
    {
        var dir = EnsureModule("github.com/hashicorp/go-hclog", "v1.5.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-hclog", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Sqids()
    {
        var dir = EnsureModule("github.com/sqids/sqids-go", "v0.4.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("sqids", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoDifflib()
    {
        var dir = EnsureModule("github.com/pmezard/go-difflib", "v1.0.0", subPkg: "difflib");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-difflib", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Objx()
    {
        var dir = EnsureModule("github.com/stretchr/objx", "v0.5.2");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("objx", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Jsonparser()
    {
        var dir = EnsureModule("github.com/buger/jsonparser", "v1.1.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("jsonparser", errors);
    }
}
