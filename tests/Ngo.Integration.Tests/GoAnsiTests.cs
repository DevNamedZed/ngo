// -----------------------------------------------------------------------
// <copyright file="GoAnsiTests.cs" company="Ziad">
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
public class GoAnsiTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGlob()
    {
        // go-glob — simple glob matching (already tested as 0 errors)
        var dir = EnsureModule("github.com/ryanuber/go-glob", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-glob-2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoXid()
    {
        var dir = EnsureModule("github.com/rs/xid", "v1.5.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("xid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoShortId()
    {
        var dir = EnsureModule("github.com/teris-io/shortid", "v0.0.0-20220617161101-71ec9f2aa569");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("shortid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoCache()
    {
        var dir = EnsureModule("github.com/patrickmn/go-cache", "v2.1.0+incompatible");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-cache", errors);
    }
}
