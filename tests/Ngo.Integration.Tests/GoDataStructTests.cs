// -----------------------------------------------------------------------
// <copyright file="GoDataStructTests.cs" company="Ziad">
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
public class GoDataStructTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoCircbuf()
    {
        var dir = EnsureModule("github.com/armon/circbuf", "v0.0.0-20190214190532-5111143e8da2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("circbuf", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Levenshtein()
    {
        var dir = EnsureModule("github.com/agnivade/levenshtein", "v1.1.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("agnivade-levenshtein", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoConvey()
    {
        // armon/go-radix — already tested, try go-splay
        var dir = EnsureModule("github.com/ryanuber/columnize", "v2.1.2+incompatible");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("columnize-v2", errors);
    }
}
