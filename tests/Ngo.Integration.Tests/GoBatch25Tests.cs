// -----------------------------------------------------------------------
// <copyright file="GoBatch25Tests.cs" company="Ziad">
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
public class GoBatch25Tests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoEmirpasicGodsLists()
    {
        var dir = EnsureModule("github.com/emirpasic/gods", "v1.18.1", "lists/arraylist");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gods-arraylist", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoEmirpasicGodsSets()
    {
        var dir = EnsureModule("github.com/emirpasic/gods", "v1.18.1", "sets/hashset");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gods-hashset", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoEmirpasicGodsMaps()
    {
        var dir = EnsureModule("github.com/emirpasic/gods", "v1.18.1", "maps/hashmap");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gods-hashmap", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoEmirpasicGodsStacks()
    {
        var dir = EnsureModule("github.com/emirpasic/gods", "v1.18.1", "stacks/arraystack");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gods-arraystack", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoEmirpasicGodsContainers()
    {
        var dir = EnsureModule("github.com/emirpasic/gods", "v1.18.1", "containers");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gods-containers", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoChenzhuyuIbase64()
    {
        var dir = EnsureModule("github.com/cristalhq/base64", "v0.1.2");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("cristalhq-base64-v0.1.2", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoMitchellhGoTesting()
    {
        var dir = EnsureModule("github.com/mitchellh/go-testing-interface", "v1.14.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("go-testing-interface-v1", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSeanpointGoUuid()
    {
        var dir = EnsureModule("github.com/pborman/uuid", "v1.2.1");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("pborman-uuid", errors);
    }
}
