// -----------------------------------------------------------------------
// <copyright file="PkgDiffTests.cs" company="Ziad">
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
public class PkgDiffTests : PackageTestBase
{
    private const string Module = "github.com/pkg/diff";
    private const string Version = "v0.0.0-20210226163009-20ebb0f2a09e";

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_edit()
    {
        var dir = EnsureModule(Module, Version, "edit");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("pkg/diff/edit", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_intern()
    {
        var dir = EnsureModule(Module, Version, "intern");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("pkg/diff/intern", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_write()
    {
        var dir = EnsureModule(Module, Version, "write");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("pkg/diff/write", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_myers()
    {
        var dir = EnsureModule(Module, Version, "myers");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("pkg/diff/myers", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_ctxt()
    {
        var dir = EnsureModule(Module, Version, "ctxt");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("pkg/diff/ctxt", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_root()
    {
        var dir = EnsureModule(Module, Version);
        var errors = AnalyzePackageDir(dir);
        DumpErrors("pkg/diff", errors);
    }
}
