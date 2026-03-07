// -----------------------------------------------------------------------
// <copyright file="GoBatch28Tests.cs" company="Ziad">
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
public class GoBatch28Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhGoWordwrap()
    {
        var dir = EnsureModule("github.com/mitchellh/go-wordwrap", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("mitchellh-go-wordwrap", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoKrFs()
    {
        var dir = EnsureModule("github.com/kr/fs", "v0.1.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("kr-fs", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoHashicorpGoSockaddr()
    {
        var dir = EnsureModule("github.com/hashicorp/go-sockaddr/template", "v1.0.6");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-sockaddr-template", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMattnGoZglob()
    {
        var dir = EnsureModule("github.com/mattn/go-zglob", "v0.0.4");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("mattn-go-zglob", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoGabsJson()
    {
        var dir = EnsureModule("github.com/Jeffail/gabs/v2", "v2.7.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("jeffail-gabs-v2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSanityIoLitter()
    {
        var dir = EnsureModule("github.com/sanity-io/litter", "v1.5.5");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("sanityio-litter", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallPjson()
    {
        var dir = EnsureModule("github.com/tidwall/pjson", "v0.2.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-pjson", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallRtree()
    {
        var dir = EnsureModule("github.com/tidwall/rtree", "v1.10.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-rtree", errors);
    }
}
