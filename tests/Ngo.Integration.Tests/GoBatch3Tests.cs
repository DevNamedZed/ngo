// -----------------------------------------------------------------------
// <copyright file="GoBatch3Tests.cs" company="Ziad">
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
public class GoBatch3Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoRendezvous()
    {
        // dgryski/go-rendezvous — rendezvous hashing
        var dir = EnsureModule("github.com/dgryski/go-rendezvous", "v0.0.0-20200823014737-9f7001d12a5f");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-rendezvous", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAcceptHeaders()
    {
        // timewasted/go-accept-headers — HTTP accept header parsing
        var dir = EnsureModule("github.com/timewasted/go-accept-headers", "v0.0.0-20130320203746-c78f304b1b09");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-accept-headers", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAlessioShellescape()
    {
        // alessio/shellescape — shell escape
        var dir = EnsureModule("github.com/alessio/shellescape", "v1.4.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("shellescape", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoBeorn7Perks()
    {
        // beorn7/perks — quantile estimation
        var dir = EnsureModule("github.com/beorn7/perks", "v1.0.1", subPkg: "quantile");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("perks-quantile", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoOsc52()
    {
        // aymanbagabas/go-osc52/v2 — terminal OSC52
        var dir = EnsureModule("github.com/aymanbagabas/go-osc52/v2", "v2.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-osc52", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoNilaway()
    {
        // santhosh-tekuri/jsonschema — too complex, try something simpler
        // asaskevich/govalidator — already tested
        // tklauser/go-sysconf — syscall-heavy
        // cespare/xxhash — already tested
        // go-playground/validator — too many deps
        // charlievieth/fastwalk — small fs walker
        var dir = EnsureModule("github.com/charlievieth/fastwalk", "v1.0.8");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("fastwalk", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoBoolToBytes()
    {
        // zeebo/xxh3 — already tested, try zeebo/errs
        var dir = EnsureModule("github.com/zeebo/errs", "v1.3.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("zeebo-errs", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoWild()
    {
        // gobwas/glob — already tested, try gobwas/pool
        var dir = EnsureModule("github.com/gobwas/pool", "v0.2.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gobwas-pool", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTview()
    {
        // rivo/uniseg — already tested, try deckarep/golang-set/v2
        var dir = EnsureModule("github.com/deckarep/golang-set/v2", "v2.6.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("golang-set-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSubprocess()
    {
        // kballard/go-shellquote — already tested, try minio/highwayhash
        var dir = EnsureModule("github.com/minio/highwayhash", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("highwayhash", errors);
    }
}
