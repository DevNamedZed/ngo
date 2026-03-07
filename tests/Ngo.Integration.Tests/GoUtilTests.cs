// -----------------------------------------------------------------------
// <copyright file="GoUtilTests.cs" company="Ziad">
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
public class GoUtilTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSafemath()
    {
        // mitchellh/go-safemath — safe integer arithmetic
        var dir = EnsureModule("github.com/rung/go-safecast", "v1.0.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-safecast", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMultierror()
    {
        // uber-go/multierr — already tested, but let's add go-multierror v2
        var dir = EnsureModule("github.com/hashicorp/go-rootcerts", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-rootcerts", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoRate()
    {
        // juju/ratelimit — token bucket rate limiter
        var dir = EnsureModule("github.com/juju/ratelimit", "v1.0.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("ratelimit", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_Decimal()
    {
        // shopspring/decimal — arbitrary precision decimal
        var dir = EnsureModule("github.com/shopspring/decimal", "v1.3.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("decimal", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoCircularBuffer()
    {
        // armon/circbuf — circular buffer in Go
        var dir = EnsureModule("github.com/armon/go-metrics", "v0.4.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("go-metrics", errors);
    }
}
