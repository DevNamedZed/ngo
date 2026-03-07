// -----------------------------------------------------------------------
// <copyright file="GoBatch14Tests.cs" company="Ziad">
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
public class GoBatch14Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoJosharian()
    {
        // intern — string interning
        var dir = EnsureModule("github.com/josharian/intern", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("josharian-intern", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSegmentioAsm()
    {
        // segmentio/asm — keyset sub-package
        var dir = EnsureModule("github.com/segmentio/asm", "v1.2.0", "keyset");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("segmentio-asm-keyset", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoNightlyone()
    {
        // nightlyone/lockfile — file locking
        var dir = EnsureModule("github.com/nightlyone/lockfile", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("nightlyone-lockfile", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSubosito()
    {
        // subosito/gotenv — .env file parser
        var dir = EnsureModule("github.com/subosito/gotenv", "v1.4.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("subosito-gotenv", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoImdario()
    {
        // imdario/mergo — struct merging
        var dir = EnsureModule("github.com/imdario/mergo", "v0.3.16");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("imdario-mergo", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellReflectwalk()
    {
        var dir = EnsureModule("github.com/mitchellh/reflectwalk", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("reflectwalk", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMaas999()
    {
        // maas999/xid — globally unique ID generator
        var dir = EnsureModule("github.com/rs/xid", "v1.5.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("rs-xid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashLand()
    {
        // dolthub/maphash — backport of maphash.Bytes
        var dir = EnsureModule("github.com/dolthub/maphash", "v0.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("dolthub-maphash", errors);
    }
}
