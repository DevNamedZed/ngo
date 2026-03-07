// -----------------------------------------------------------------------
// <copyright file="GoBatch21Tests.cs" company="Ziad">
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
public class GoBatch21Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallTinyqueue()
    {
        var dir = EnsureModule("github.com/tidwall/tinyqueue", "v0.1.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("tidwall-tinyqueue", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallLotsa()
    {
        var dir = EnsureModule("github.com/tidwall/lotsa", "v1.0.3");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("tidwall-lotsa", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallRtred()
    {
        var dir = EnsureModule("github.com/tidwall/rtred", "v0.1.2");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-rtred", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallRhh()
    {
        var dir = EnsureModule("github.com/tidwall/assert", "v0.1.0");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("tidwall-assert", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallGrect()
    {
        var dir = EnsureModule("github.com/tidwall/grect", "v0.1.4");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-grect", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallRedbench()
    {
        var dir = EnsureModule("github.com/tidwall/redbench", "v0.1.0");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-redbench", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoTidwallResp()
    {
        var dir = EnsureModule("github.com/tidwall/resp", "v0.1.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("tidwall-resp", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSegmentioEncoding()
    {
        var dir = EnsureModule("github.com/segmentio/encoding", "v0.3.6", "ascii");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("segmentio-ascii", errors);
    }
}
