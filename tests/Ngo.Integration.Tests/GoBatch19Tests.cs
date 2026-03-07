// -----------------------------------------------------------------------
// <copyright file="GoBatch19Tests.cs" company="Ziad">
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
public class GoBatch19Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMastermindsSprig()
    {
        var dir = EnsureModule("github.com/Masterminds/sprig/v3", "v3.2.3");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("sprig-v3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSchollzSegment()
    {
        var dir = EnsureModule("github.com/schollz/progressbar/v3", "v3.14.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("progressbar", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoCheggaaa()
    {
        var dir = EnsureModule("github.com/cheggaaa/pb/v3", "v3.1.5");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("cheggaaa-pb", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoOlekukonkoTablewriter()
    {
        var dir = EnsureModule("github.com/olekukonko/tablewriter", "v0.0.5");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tablewriter", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGookit_goutil()
    {
        var dir = EnsureModule("github.com/gookit/goutil", "v0.6.15", "strutil");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("goutil-strutil", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGocraft()
    {
        var dir = EnsureModule("github.com/gocraft/dbr/v2", "v2.7.6");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gocraft-dbr", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMaTouch()
    {
        var dir = EnsureModule("github.com/mattn/go-shellwords", "v1.0.12");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-shellwords", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoCoreos()
    {
        var dir = EnsureModule("github.com/coreos/go-semver", "v0.3.1", "semver");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("coreos-semver", errors);
    }
}
