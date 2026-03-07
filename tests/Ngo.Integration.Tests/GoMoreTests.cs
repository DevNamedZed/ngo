// -----------------------------------------------------------------------
// <copyright file="GoMoreTests.cs" company="Ziad">
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
public class GoMoreTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpErrwrap()
    {
        var dir = EnsureModule("github.com/hashicorp/errwrap", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("errwrap-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGodebugDiff()
    {
        var dir = EnsureModule("github.com/kylelemons/godebug", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("godebug-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAtomicfile()
    {
        var dir = EnsureModule("github.com/natefinch/atomic", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("natefinch-atomic", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_CopystructureV2()
    {
        var dir = EnsureModule("github.com/mitchellh/copystructure", "v1.2.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("copystructure-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpGoMultierror()
    {
        var dir = EnsureModule("github.com/hashicorp/go-multierror", "v1.1.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-multierror-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_CastV2()
    {
        var dir = EnsureModule("github.com/spf13/cast", "v1.6.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("cast-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_InternV2()
    {
        var dir = EnsureModule("github.com/josharian/intern", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("intern-v2", errors);
    }
}
