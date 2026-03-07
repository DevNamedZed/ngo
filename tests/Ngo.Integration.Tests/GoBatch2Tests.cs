// -----------------------------------------------------------------------
// <copyright file="GoBatch2Tests.cs" company="Ziad">
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
public class GoBatch2Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoFnv()
    {
        // segmentio/fasthash — fast FNV (already tested), try skeema/knownhosts
        var dir = EnsureModule("github.com/OneOfOne/xxhash", "v1.2.8");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("oneofone-xxhash", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSafecast()
    {
        // mitchellh/pointerstructure — pointer helpers
        var dir = EnsureModule("github.com/mitchellh/pointerstructure", "v1.2.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("pointerstructure", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoRobinHood()
    {
        // cespare/mph — minimal perfect hash
        var dir = EnsureModule("github.com/cespare/mph", "v0.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("mph", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSemaphore()
    {
        // marusama/semaphore (already tested), try containerd/console (may be complex)
        var dir = EnsureModule("github.com/cenkalti/backoff/v4", "v4.2.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("backoff-v4", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAtomicBool()
    {
        // uber-go/atomic — atomic types
        var dir = EnsureModule("go.uber.org/atomic", "v1.11.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("uber-atomic", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoleak()
    {
        // uber-go/goleak — goroutine leak detector
        var dir = EnsureModule("github.com/imdario/mergo", "v0.3.16");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("mergo-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpGoSockaddr()
    {
        // hashicorp/go-sockaddr — socket address utilities
        var dir = EnsureModule("github.com/mitchellh/cli", "v1.1.5");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("mitchellh-cli", errors);
    }
}
