// -----------------------------------------------------------------------
// <copyright file="GoBatch26Tests.cs" company="Ziad">
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
public class GoBatch26Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashlandMurmur3()
    {
        var dir = EnsureModule("github.com/twmb/murmur3", "v1.1.8");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("twmb-murmur3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoEdwinTechGenericollections()
    {
        var dir = EnsureModule("github.com/deckarep/golang-set", "v1.8.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("golang-set-v1", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoYuanHjj1Naturalsort()
    {
        var dir = EnsureModule("github.com/maruel/natural", "v1.1.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("maruel-natural", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSpf13JWalterWeatherman()
    {
        var dir = EnsureModule("github.com/spf13/jwalterweatherman", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("jwalterweatherman", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSubosinAtomic()
    {
        var dir = EnsureModule("go.uber.org/atomic", "v1.11.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("uber-atomic-v1.11", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMrjonesMsteams()
    {
        var dir = EnsureModule("github.com/matryer/is", "v1.4.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("matryer-is", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSamsarahq()
    {
        var dir = EnsureModule("github.com/segmentio/ksuid", "v1.0.4");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("segmentio-ksuid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoNbioUlid()
    {
        var dir = EnsureModule("github.com/oklog/ulid/v2", "v2.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("oklog-ulid-v2", errors);
    }
}
