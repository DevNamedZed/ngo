// -----------------------------------------------------------------------
// <copyright file="GoBatch4Tests.cs" company="Ziad">
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
public class GoBatch4Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSerfHashicorp()
    {
        // hashicorp/go-cleanhttp — HTTP client utils
        var dir = EnsureModule("github.com/hashicorp/go-cleanhttp", "v0.5.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-cleanhttp-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhCopystructure()
    {
        // mitchellh/reflectwalk — already at 0, try copystructure deps
        var dir = EnsureModule("github.com/mitchellh/mapstructure", "v1.5.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("mapstructure-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpErrwrap()
    {
        // hashicorp/errwrap already tested (v1), try go-retryablehttp
        var dir = EnsureModule("github.com/hashicorp/go-retryablehttp", "v0.7.4");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-retryablehttp", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoDatadog()
    {
        // DataDog/datadog-go — datadog client, too big
        // try simpler: go-playground/form
        var dir = EnsureModule("github.com/go-playground/form/v4", "v4.2.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-playground-form", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellGlow()
    {
        // mitchellh/go-homedir — already tested, try go-wordwrap/v2
        var dir = EnsureModule("github.com/mitchellh/go-wordwrap", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-wordwrap-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTeaTable()
    {
        // charmbracelet/lipgloss — too many deps, try simple ones
        var dir = EnsureModule("github.com/mattn/go-runewidth", "v0.0.15");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-runewidth-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoPelletierToml()
    {
        // pelletier/go-toml/v2 — already tested v1, try v2
        var dir = EnsureModule("github.com/pelletier/go-toml/v2", "v2.1.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("pelletier-toml-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAlecAizanColor()
    {
        // alecthomas/chroma — too big, try repr
        var dir = EnsureModule("github.com/alecthomas/repr", "v0.3.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("alecthomas-repr", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoJinzhuCopier()
    {
        // already tested v1, try inflection
        var dir = EnsureModule("github.com/jinzhu/inflection", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("jinzhu-inflection", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoErrors()
    {
        // go-errors/errors — not pkg/errors, different package
        var dir = EnsureModule("github.com/segmentio/asm", "v1.2.0", subPkg: "ascii");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("segmentio-asm-ascii", errors);
    }
}
