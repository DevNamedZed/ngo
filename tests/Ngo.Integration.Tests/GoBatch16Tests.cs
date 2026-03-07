// -----------------------------------------------------------------------
// <copyright file="GoBatch16Tests.cs" company="Ziad">
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
public class GoBatch16Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKcgClock()
    {
        var dir = EnsureModule("github.com/benbjohnson/clock", "v1.3.5");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("benbjohnson-clock-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhMapstructure()
    {
        var dir = EnsureModule("github.com/mitchellh/mapstructure", "v1.5.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("mapstructure", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoStretchrObjx()
    {
        var dir = EnsureModule("github.com/stretchr/objx", "v0.5.2");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("objx-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoBrianvoeGenetics()
    {
        var dir = EnsureModule("github.com/brianvoe/gofakeit/v6", "v6.25.0", "data");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gofakeit-data", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpGo_version()
    {
        var dir = EnsureModule("github.com/hashicorp/go-version", "v1.6.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-version", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMastermindsGoutils()
    {
        var dir = EnsureModule("github.com/Masterminds/semver/v3", "v3.2.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("semver-v3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallGjson()
    {
        var dir = EnsureModule("github.com/tidwall/gjson", "v1.17.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-gjson", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKyleBanks()
    {
        var dir = EnsureModule("github.com/kyokomi/emoji/v2", "v2.2.12");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("kyokomi-emoji", errors);
    }
}
