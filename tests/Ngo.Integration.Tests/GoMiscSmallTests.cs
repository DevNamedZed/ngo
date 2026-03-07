// -----------------------------------------------------------------------
// <copyright file="GoMiscSmallTests.cs" company="Ziad">
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
public class GoMiscSmallTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Reflectwalk()
    {
        var dir = EnsureModule("github.com/mitchellh/reflectwalk", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("reflectwalk-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoShortUUID()
    {
        var dir = EnsureModule("github.com/lithammer/shortuuid/v4", "v4.0.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("shortuuid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Iochan()
    {
        var dir = EnsureModule("github.com/mitchellh/iochan", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("iochan-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTestingInterface()
    {
        var dir = EnsureModule("github.com/mitchellh/go-testing-interface", "v1.14.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-testing-interface-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_FacetteNatsort()
    {
        var dir = EnsureModule("github.com/facette/natsort", "v0.0.0-20181210072756-2cd4dd1e2dcb");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("facette-natsort", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_HuanduXstrings()
    {
        var dir = EnsureModule("github.com/huandu/xstrings", "v1.4.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("huandu-xstrings", errors);
    }
}
