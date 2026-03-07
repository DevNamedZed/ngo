// -----------------------------------------------------------------------
// <copyright file="GoBatch24Tests.cs" company="Ziad">
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
public class GoBatch24Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGammazeroDeque()
    {
        var dir = EnsureModule("github.com/gammazero/deque", "v0.2.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gammazero-deque", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMozillazgGoPinyin()
    {
        var dir = EnsureModule("github.com/mozillazg/go-pinyin", "v0.20.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-pinyin", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAgnivadeLevenshtein()
    {
        var dir = EnsureModule("github.com/agnivade/levenshtein", "v1.1.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("agnivade-levenshtein", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoAraddonDateparse()
    {
        var dir = EnsureModule("github.com/araddon/dateparse", "v0.0.0-20210429162001-6b43995a97de");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("araddon-dateparse", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMaienMergeSort()
    {
        var dir = EnsureModule("github.com/emirpasic/gods", "v1.18.1", "utils");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gods-utils", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoJeremywhlGoTags()
    {
        var dir = EnsureModule("github.com/fatih/structtag", "v1.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("fatih-structtag", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGofrsUuidV5()
    {
        var dir = EnsureModule("github.com/gofrs/uuid/v5", "v5.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gofrs-uuid-v5", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoDecimalV2()
    {
        var dir = EnsureModule("github.com/shopspring/decimal", "v1.3.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("shopspring-decimal-v1.3", errors);
    }
}
