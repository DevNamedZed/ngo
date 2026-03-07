// -----------------------------------------------------------------------
// <copyright file="GodebugTests.cs" company="Ziad">
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
public class GodebugTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_diff()
    {
        var dir = EnsureModule("github.com/kylelemons/godebug", "v1.1.0", subPkg: "diff");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("godebug/diff", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_pretty()
    {
        var moduleDir = EnsureModule("github.com/kylelemons/godebug", "v1.1.0");
        var prettyDir = System.IO.Path.Combine(moduleDir, "pretty");
        var errors = AnalyzePackageDir(prettyDir, projectRoot: moduleDir);
        AssertZeroErrors("godebug/pretty", errors);
        // Depends on internal diff subpackage
    }
}
