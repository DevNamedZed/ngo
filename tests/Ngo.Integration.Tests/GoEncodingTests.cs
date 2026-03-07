// -----------------------------------------------------------------------
// <copyright file="GoEncodingTests.cs" company="Ziad">
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
public class GoEncodingTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoBase32()
    {
        // whyrusleeping/base32 — base32 encoding
        var dir = EnsureModule("github.com/whyrusleeping/base32", "v0.0.0-20170828182744-c30ac30633cc");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("base32", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoBase58()
    {
        // mr-tron/base58 — base58 encoding
        var dir = EnsureModule("github.com/mr-tron/base58", "v1.2.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("base58", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoWordwrap()
    {
        var dir = EnsureModule("github.com/mitchellh/go-wordwrap", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-wordwrap-v1", errors);
    }
}
