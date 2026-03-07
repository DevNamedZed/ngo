// -----------------------------------------------------------------------
// <copyright file="GoBatch6Tests.cs" company="Ziad">
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
public class GoBatch6Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoRossGerber()
    {
        // ross/go-stringmatch — string matching
        var dir = EnsureModule("github.com/jmespath/go-jmespath", "v0.4.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-jmespath", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhIochan()
    {
        // mitchellh/iochan — already tested, try go-linereader
        var dir = EnsureModule("github.com/mitchellh/go-linereader", "v0.0.0-20190213213312-1b945b3263eb");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-linereader", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhPrefixedWriter()
    {
        // mitchellh/prefixedio — prefixed io
        var dir = EnsureModule("github.com/mitchellh/prefixedio", "v0.0.0-20190213213902-5733675afd51");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("prefixedio", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoArmonCircbuf()
    {
        // armon/circbuf — already tested, try go-metrics subpkg
        var dir = EnsureModule("github.com/armon/go-radix", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("armon-go-radix", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoRyanuberGo()
    {
        // ryanuber/columnize — already tested, try go-glob v2
        var dir = EnsureModule("github.com/ryanuber/go-glob", "v1.0.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("ryanuber-go-glob-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMattnIsatty()
    {
        // mattn/go-isatty — already tested, try go-pointer
        var dir = EnsureModule("github.com/mattn/go-pointer", "v0.0.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-pointer", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSeanGruber()
    {
        // sgreben/pq — priority queue
        var dir = EnsureModule("github.com/gammazero/deque", "v0.2.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("gammazero-deque-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoVarnam()
    {
        // valyala/fasthttp — too complex, try valyala/bytebufferpool (already tested)
        // try valyala/tcplisten
        var dir = EnsureModule("github.com/valyala/histogram", "v1.2.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("valyala-histogram", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoJinzhuNow()
    {
        // jinzhu/now — already tested, try copier v2
        var dir = EnsureModule("github.com/jinzhu/copier", "v0.4.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("jinzhu-copier-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSpf13()
    {
        // spf13/cast — already tested, try jwalterweatherman
        var dir = EnsureModule("github.com/spf13/jwalterweatherman", "v1.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("jwalterweatherman", errors);
    }
}
