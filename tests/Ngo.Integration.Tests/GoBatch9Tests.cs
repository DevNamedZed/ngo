// -----------------------------------------------------------------------
// <copyright file="GoBatch9Tests.cs" company="Ziad">
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
public class GoBatch9Tests : PackageTestBase
{
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
    public void Analyze_GoSpf13Pflag()
    {
        var dir = EnsureModule("github.com/spf13/pflag", "v1.0.5");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("pflag", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoPelletierGoTomlV1()
    {
        var dir = EnsureModule("github.com/pelletier/go-toml", "v1.9.5");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("pelletier-toml", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGodotenvV1()
    {
        var dir = EnsureModule("github.com/joho/godotenv", "v1.5.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("godotenv", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKoanfMaps()
    {
        var dir = EnsureModule("github.com/knadh/koanf/maps", "v0.1.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("koanf-maps", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhReflectwalk()
    {
        var dir = EnsureModule("github.com/mitchellh/reflectwalk", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("reflectwalk", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHumanize()
    {
        var dir = EnsureModule("github.com/dustin/go-humanize", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-humanize", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSpf13Cast()
    {
        var dir = EnsureModule("github.com/spf13/cast", "v1.6.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("spf13-cast", errors);
    }
}
