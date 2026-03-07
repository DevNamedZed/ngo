// -----------------------------------------------------------------------
// <copyright file="GoTextTests.cs" company="Ziad">
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
public class GoTextTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTabwriter()
    {
        // juju/ansiterm — simple ANSI term helpers
        var dir = EnsureModule("github.com/olekukonko/ts", "v0.0.0-20171002115256-78ecb04241c0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("ts", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoUnidecode()
    {
        // rainycape/unidecode — unicode transliteration
        var dir = EnsureModule("github.com/rainycape/unidecode", "v0.0.0-20150907023854-cb7f23ec59be");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("unidecode", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHomedir()
    {
        // mitchellh/go-homedir — already tested via other, try adrg/xdg
        var dir = EnsureModule("github.com/adrg/xdg", "v0.4.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("xdg", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoEnvParse()
    {
        // caarlos0/env/v6 — env variable parsing
        var dir = EnsureModule("github.com/caarlos0/env/v6", "v6.10.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("env-v6", errors);
    }
}
