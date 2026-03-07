// -----------------------------------------------------------------------
// <copyright file="GoBatch15Tests.cs" company="Ziad">
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
public class GoBatch15Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMailruEasyjsonJlexer()
    {
        var dir = EnsureModule("github.com/mailru/easyjson", "v0.7.7", "jlexer");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("easyjson-jlexer", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMailruEasyjsonJwriter()
    {
        var dir = EnsureModule("github.com/mailru/easyjson", "v0.7.7", "jwriter");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("easyjson-jwriter", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMailruEasyjsonBuffer()
    {
        var dir = EnsureModule("github.com/mailru/easyjson", "v0.7.7", "buffer");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("easyjson-buffer", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKeeganCbor()
    {
        var dir = EnsureModule("github.com/fxamacker/cbor/v2", "v2.5.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("fxamacker-cbor-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpErrwrap()
    {
        var dir = EnsureModule("github.com/hashicorp/errwrap", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("hashicorp-errwrap", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhGo_wordwrap()
    {
        var dir = EnsureModule("github.com/mitchellh/go-wordwrap", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-wordwrap", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhGo_testing_interface()
    {
        var dir = EnsureModule("github.com/mitchellh/go-testing-interface", "v1.14.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-testing-interface", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoRyanuberGo_glob()
    {
        var dir = EnsureModule("github.com/ryanuber/go-glob", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("ryanuber-go-glob", errors);
    }
}
