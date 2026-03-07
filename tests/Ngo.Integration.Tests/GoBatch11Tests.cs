// -----------------------------------------------------------------------
// <copyright file="GoBatch11Tests.cs" company="Ziad">
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
public class GoBatch11Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoSimpleSlug()
    {
        var dir = EnsureModule("github.com/gosimple/slug", "v1.13.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gosimple-slug", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhIochan()
    {
        var dir = EnsureModule("github.com/mitchellh/iochan", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("iochan", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoRendezvous()
    {
        var dir = EnsureModule("github.com/dgryski/go-rendezvous", "v0.0.0-20200823014737-9f7001d12a5f");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-rendezvous", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpGoImmutable()
    {
        var dir = EnsureModule("github.com/hashicorp/go-immutable-radix", "v1.3.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-immutable-radix", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoFarm()
    {
        var dir = EnsureModule("github.com/dgryski/go-farm", "v0.0.0-20200201041132-a6ae2369ad13");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-farm", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoSipHash()
    {
        var dir = EnsureModule("github.com/dchest/siphash", "v1.2.3");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("siphash", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoNaturalSort()
    {
        var dir = EnsureModule("github.com/facette/natsort", "v0.0.0-20181210072756-2cd4dd1e2dcb");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("natsort", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoRobfigSched()
    {
        var dir = EnsureModule("github.com/robfig/cron/v3", "v3.0.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("robfig-cron-v3", errors);
    }
}
