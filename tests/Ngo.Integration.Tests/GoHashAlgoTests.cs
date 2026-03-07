// -----------------------------------------------------------------------
// <copyright file="GoHashAlgoTests.cs" company="Ziad">
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
public class GoHashAlgoTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoFarmHash()
    {
        var dir = EnsureModule("github.com/dgryski/go-farm", "v0.0.0-20200201041132-a6ae2369ad13");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-farm", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_XxhashV2()
    {
        var dir = EnsureModule("github.com/cespare/xxhash/v2", "v2.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("xxhash-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_SipHash()
    {
        var dir = EnsureModule("github.com/dchest/siphash", "v1.2.3");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("siphash", errors);
    }
}
