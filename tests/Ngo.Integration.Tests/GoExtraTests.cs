// -----------------------------------------------------------------------
// <copyright file="GoExtraTests.cs" company="Ziad">
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
public class GoExtraTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Envconfig()
    {
        var dir = EnsureModule("github.com/kelseyhightower/envconfig", "v1.4.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("envconfig", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoShellquote()
    {
        var dir = EnsureModule("github.com/kballard/go-shellquote", "v0.0.0-20180428030007-95032a82bc51");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-shellquote", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoScalar()
    {
        var dir = EnsureModule("github.com/alexflint/go-scalar", "v1.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-scalar", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_TwmbMurmur3()
    {
        var dir = EnsureModule("github.com/twmb/murmur3", "v1.1.8");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("twmb-murmur3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Bitset()
    {
        var dir = EnsureModule("github.com/bits-and-blooms/bitset", "v1.10.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("bitset", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Xxh3()
    {
        var dir = EnsureModule("github.com/zeebo/xxh3", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("xxh3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Gotenv()
    {
        var dir = EnsureModule("github.com/subosito/gotenv", "v1.6.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gotenv", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GolangLruV2()
    {
        var dir = EnsureModule("github.com/hashicorp/golang-lru/v2", "v2.0.7");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("golang-lru-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoIsattyV2()
    {
        var dir = EnsureModule("github.com/mattn/go-isatty", "v0.0.20");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-isatty-v2", errors);
    }

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
    public void Analyze_GoMitchellhHashstructure()
    {
        var dir = EnsureModule("github.com/mitchellh/hashstructure", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("hashstructure-v1", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoImdarioMergo()
    {
        var dir = EnsureModule("github.com/imdario/mergo", "v0.3.16");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("mergo", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoEnv()
    {
        var dir = EnsureModule("github.com/caarlos0/env/v6", "v6.10.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("caarlos0-env", errors);
    }
}
