// -----------------------------------------------------------------------
// <copyright file="GoBatch10Tests.cs" company="Ziad">
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
public class GoBatch10Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpGoVersion()
    {
        var dir = EnsureModule("github.com/hashicorp/go-version", "v1.6.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-version", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSpf13Afero()
    {
        var dir = EnsureModule("github.com/spf13/afero", "v1.11.0", subPkg: "mem");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("afero-mem", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoDifflib()
    {
        var dir = EnsureModule("github.com/sergi/go-diff", "v1.3.1", subPkg: "diffmatchpatch");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-diff", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSemverV3()
    {
        var dir = EnsureModule("github.com/Masterminds/semver/v3", "v3.2.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("semver-v3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAlexflintGoArg()
    {
        var dir = EnsureModule("github.com/alexflint/go-arg", "v1.4.3");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-arg", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhGoWordwrap()
    {
        var dir = EnsureModule("github.com/mitchellh/go-wordwrap", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-wordwrap-v1", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoXdgScribble()
    {
        var dir = EnsureModule("github.com/nanobox-io/golang-scribble", "v0.0.0-20190309225732-aa3e7c118975");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("scribble", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhGoHomedir()
    {
        var dir = EnsureModule("github.com/mitchellh/go-homedir", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-homedir", errors);
    }
}
