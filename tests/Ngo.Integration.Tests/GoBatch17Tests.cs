// -----------------------------------------------------------------------
// <copyright file="GoBatch17Tests.cs" company="Ziad">
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
public class GoBatch17Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoogleUuid()
    {
        var dir = EnsureModule("github.com/google/uuid", "v1.4.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("google-uuid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGofrs()
    {
        var dir = EnsureModule("github.com/gofrs/uuid", "v4.4.0+incompatible");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gofrs-uuid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoOklog()
    {
        var dir = EnsureModule("github.com/oklog/ulid", "v1.3.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("oklog-ulid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhGoHomedir()
    {
        var dir = EnsureModule("github.com/mitchellh/go-homedir", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-homedir", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKellyclopsFlagV2()
    {
        var dir = EnsureModule("github.com/urfave/cli/v2", "v2.25.7", "internal/build");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("urfave-cli-build", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhIochan()
    {
        var dir = EnsureModule("github.com/mattn/go-runewidth", "v0.0.15");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-runewidth-v3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoFatihColor()
    {
        var dir = EnsureModule("github.com/fatih/color", "v1.16.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("fatih-color", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhColorstring()
    {
        var dir = EnsureModule("github.com/mitchellh/colorstring", "v0.0.0-20190213212951-d06e56a500db");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("mitchellh-colorstring", errors);
    }
}
