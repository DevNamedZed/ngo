// -----------------------------------------------------------------------
// <copyright file="GoBatch27Tests.cs" company="Ziad">
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
public class GoBatch27Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoXdgBasedir()
    {
        var dir = EnsureModule("github.com/adrg/xdg", "v0.4.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("adrg-xdg", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpLogutils()
    {
        var dir = EnsureModule("github.com/hashicorp/logutils", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("hashicorp-logutils", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGotenvV16()
    {
        var dir = EnsureModule("github.com/subosito/gotenv", "v1.6.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("subosito-gotenv-v1.6", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAlecThomasRepr()
    {
        var dir = EnsureModule("github.com/alecthomas/repr", "v0.4.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("alecthomas-repr", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAlecThomasAssert()
    {
        var dir = EnsureModule("github.com/alecthomas/assert/v2", "v2.6.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("alecthomas-assert-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhReflectwalkV2()
    {
        var dir = EnsureModule("github.com/mitchellh/reflectwalk", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("reflectwalk-v1.0.2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoValyalaBytebufferpool()
    {
        var dir = EnsureModule("github.com/valyala/bytebufferpool", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("valyala-bytebufferpool", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpGoHclogV1()
    {
        var dir = EnsureModule("github.com/hashicorp/go-hclog", "v1.6.3");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-hclog-v1.6", errors);
    }
}
