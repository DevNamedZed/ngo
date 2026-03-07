// -----------------------------------------------------------------------
// <copyright file="GoBatch12Tests.cs" company="Ziad">
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
public class GoBatch12Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallMatch()
    {
        var dir = EnsureModule("github.com/tidwall/match", "v1.1.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("tidwall-match", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallPretty()
    {
        var dir = EnsureModule("github.com/tidwall/pretty", "v1.2.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("tidwall-pretty", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallBtree()
    {
        var dir = EnsureModule("github.com/tidwall/btree", "v1.7.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-btree", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallHashmap()
    {
        var dir = EnsureModule("github.com/tidwall/hashmap", "v1.8.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-hashmap", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoCespareXxhashV2()
    {
        var dir = EnsureModule("github.com/cespare/xxhash/v2", "v2.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("cespare-xxhash-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpGolangLru()
    {
        var dir = EnsureModule("github.com/hashicorp/golang-lru", "v0.5.4");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("golang-lru-v1", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGobwasBuf()
    {
        var dir = EnsureModule("github.com/gobwas/httphead", "v0.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gobwas-httphead", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallSjson()
    {
        var dir = EnsureModule("github.com/tidwall/sjson", "v1.2.5");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-sjson", errors);
    }
}
