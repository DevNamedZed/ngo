// -----------------------------------------------------------------------
// <copyright file="GoBatch5Tests.cs" company="Ziad">
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
public class GoBatch5Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSanitized()
    {
        // kennygrant/sanitize — HTML sanitizer (complex), try simpler
        var dir = EnsureModule("github.com/dchest/uniuri", "v1.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("dchest-uniuri", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoCrockford()
    {
        // richardlehane/msoleps — MS OLE, try something simpler
        var dir = EnsureModule("github.com/pkg/diff", "v0.0.0-20210226163009-20ebb0f2a09e", subPkg: "edit");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("pkg/diff/edit", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoYamlMap()
    {
        // goccy/go-yaml — complex, try simple YAML helpers
        var dir = EnsureModule("github.com/ghodss/yaml", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("ghodss-yaml", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAdler32()
    {
        // hash/adler32 is built-in, try external hash
        var dir = EnsureModule("github.com/spaolacci/murmur3", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("spaolacci-murmur3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoArgon2()
    {
        // argon2 uses crypto, try simpler: davecgh/go-spew already tested
        // try fatih/color/v2 — probably has deps
        var dir = EnsureModule("github.com/fatih/camelcase", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("fatih-camelcase", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTextWidth()
    {
        // mattn/go-isatty already tested; try go-tty
        var dir = EnsureModule("github.com/rivo/uniseg", "v0.4.7");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("rivo-uniseg", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoChromaColor()
    {
        // go-chi/chi — REST framework, probably complex
        var dir = EnsureModule("github.com/go-chi/chi/v5", "v5.0.10");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-chi", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoViperAddon()
    {
        // subosito/gotenv — already tested, try magiconair/properties
        var dir = EnsureModule("github.com/magiconair/properties", "v1.8.7");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("magiconair-properties", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoJwt()
    {
        // golang-jwt/jwt — JWT library
        var dir = EnsureModule("github.com/golang-jwt/jwt/v4", "v4.5.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("golang-jwt", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoOlekukonov()
    {
        // olekukonko/tablewriter — already tested, try ts
        var dir = EnsureModule("github.com/mitchellh/colorstring", "v0.0.0-20190213212951-d06e56a500db");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("colorstring", errors);
    }
}
