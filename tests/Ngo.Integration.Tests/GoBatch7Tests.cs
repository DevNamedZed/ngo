// -----------------------------------------------------------------------
// <copyright file="GoBatch7Tests.cs" company="Ziad">
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
public class GoBatch7Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_OklogUlid()
    {
        var dir = EnsureModule("github.com/oklog/ulid", "v1.3.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("oklog-ulid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_BmatcukDoublestar()
    {
        var dir = EnsureModule("github.com/bmatcuk/doublestar", "v1.3.4");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("bmatcuk-doublestar", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoogleGoQuerystring()
    {
        var dir = EnsureModule("github.com/google/go-querystring", "v1.1.0", subPkg: "query");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-querystring", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_DenverquaneGoSlugify()
    {
        var dir = EnsureModule("github.com/avelino/slugify", "v0.0.0-20180501145920-855f152bd774");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("avelino-slugify", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_KeboolaDiff()
    {
        var dir = EnsureModule("github.com/kylelemons/godebug", "v1.1.0", subPkg: "diff");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("kylelemons-godebug-diff", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_MastermindsGoutils()
    {
        // mitchellh/go-wordwrap already tested, try another simple utility
        var dir = EnsureModule("github.com/alessio/shellescape", "v1.4.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("alessio-shellescape", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_NbioMail()
    {
        var dir = EnsureModule("github.com/nbio/st", "v0.0.0-20140626010706-e9e8d9816f32");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("nbio-st", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTestDeep()
    {
        var dir = EnsureModule("github.com/maxatome/go-testdeep", "v1.13.0", subPkg: "internal/util");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-testdeep-util", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoogleUuid()
    {
        var dir = EnsureModule("github.com/google/uuid", "v1.6.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("google-uuid", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_CespareXxhash()
    {
        // xxhash v2 already tested, try a different hash lib
        var dir = EnsureModule("github.com/minio/highwayhash", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("minio-highwayhash", errors);
    }
}
