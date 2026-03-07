// -----------------------------------------------------------------------
// <copyright file="GoBatch18Tests.cs" company="Ziad">
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
public class GoBatch18Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGodotenvV1()
    {
        var dir = EnsureModule("github.com/joho/godotenv", "v1.5.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("joho-godotenv", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSpf13Pflag()
    {
        var dir = EnsureModule("github.com/spf13/pflag", "v1.0.5");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("spf13-pflag", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHuangjunwenTsmlProt()
    {
        var dir = EnsureModule("github.com/buger/jsonparser", "v1.1.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("buger-jsonparser", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoPelletierGoTomlV1()
    {
        var dir = EnsureModule("github.com/pelletier/go-toml", "v1.9.5");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("pelletier-toml-v1", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGabstv()
    {
        var dir = EnsureModule("github.com/gabstv/go-bsdiff", "v1.0.5", "pkg/bsdiff");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-bsdiff", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKenshaw()
    {
        var dir = EnsureModule("github.com/kenshaw/snaker", "v0.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("kenshaw-snaker", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoIni()
    {
        var dir = EnsureModule("github.com/go-ini/ini", "v1.67.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-ini", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGoYaml()
    {
        var dir = EnsureModule("gopkg.in/yaml.v2", "v2.4.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("yaml-v2", errors);
    }
}
