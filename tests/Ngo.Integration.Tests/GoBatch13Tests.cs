// -----------------------------------------------------------------------
// <copyright file="GoBatch13Tests.cs" company="Ziad">
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
public class GoBatch13Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSpaolacciMurmur3()
    {
        var dir = EnsureModule("github.com/spaolacci/murmur3", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("spaolacci-murmur3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhHashstructure()
    {
        var dir = EnsureModule("github.com/mitchellh/hashstructure", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("hashstructure", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhHashstructureV2()
    {
        var dir = EnsureModule("github.com/mitchellh/hashstructure/v2", "v2.0.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("hashstructure-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKylelemons()
    {
        var dir = EnsureModule("github.com/kylelemons/godebug", "v1.1.0", "diff");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("godebug-diff", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoPkgErrors()
    {
        var dir = EnsureModule("github.com/pkg/errors", "v0.9.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("pkg-errors", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoDavecghSpew()
    {
        var dir = EnsureModule("github.com/davecgh/go-spew", "v1.1.1", "spew");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-spew", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoPmezardDiffmatchpatch()
    {
        var dir = EnsureModule("github.com/pmezard/go-difflib", "v1.0.0", "difflib");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-difflib", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTwitchtvGosu()
    {
        var dir = EnsureModule("github.com/twitchtv/twirp", "v8.1.3+incompatible", "internal/contextkeys");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("twirp-contextkeys", errors);
    }
}
