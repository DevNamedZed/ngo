// -----------------------------------------------------------------------
// <copyright file="GoBatch8Tests.cs" company="Ziad">
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
public class GoBatch8Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoBenbjohnsonClock()
    {
        var dir = EnsureModule("github.com/benbjohnson/clock", "v1.3.5");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("benbjohnson-clock", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoLusitaniae()
    {
        var dir = EnsureModule("github.com/armon/go-metrics", "v0.4.1", subPkg: "datadog");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-metrics-datadog", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTvix()
    {
        var dir = EnsureModule("github.com/tv42/httpunix", "v0.0.0-20191220191345-2ba4b9c3382c");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("httpunix", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoDatadogGostats()
    {
        var dir = EnsureModule("github.com/cespare/xxhash", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("cespare-xxhash-v1", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKoronHkxxx()
    {
        var dir = EnsureModule("github.com/andybalholm/brotli", "v1.0.5");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("andybalholm-brotli", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoJosharian()
    {
        var dir = EnsureModule("github.com/josharian/intern", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("josharian-intern", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoPmezardDifflib()
    {
        var dir = EnsureModule("github.com/pmezard/go-difflib", "v1.0.0", subPkg: "difflib");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("pmezard-go-difflib", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallGjsonV1()
    {
        // tidwall/btree is heavily generic, try gjson v1 (simpler than current v2)
        var dir = EnsureModule("github.com/tidwall/gjson", "v1.17.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-gjson-v1", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGofrsUuid()
    {
        var dir = EnsureModule("github.com/gofrs/uuid", "v4.4.0+incompatible");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gofrs-uuid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoChenzhuoyu()
    {
        var dir = EnsureModule("github.com/chenzhuoyu/base64x", "v0.0.0-20230717121745-296ad89f973d");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("chenzhuoyu-base64x", errors);
    }
}
