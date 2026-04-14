// -----------------------------------------------------------------------
// <copyright file="CgoUsageCollectorTests.cs" company="Ziad">
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Tests.Cgo;

/// <summary>
/// Unit tests for <see cref="CgoUsageCollector"/>. The collector is the
/// input to the anchor probe, so any Go-side pseudo-names that leak
/// through will surface in C compiler output as <c>undeclared here</c>
/// errors with no obvious cause. These tests fix the pseudo-name
/// filter contract so regressions cannot ship silently.
/// </summary>
[TestClass]
public class CgoUsageCollectorTests
{
    [TestMethod]
    public void RealCSymbol_IsCollected()
    {
        CgoUsageSet usageSet = CollectFromSource(
            "package example\n" +
            "\n" +
            "import \"C\"\n" +
            "\n" +
            "func UseSymbol() {\n" +
            "    _ = C.sqlite3_open\n" +
            "}\n");

        Assert.IsTrue(usageSet.Contains("sqlite3_open"), "Real C symbols must be collected.");
    }

    [TestMethod]
    public void PseudoName_CString_IsFilteredOut()
    {
        CgoUsageSet usageSet = CollectFromSource(
            "package example\n" +
            "\n" +
            "import \"C\"\n" +
            "\n" +
            "func UseSymbol() {\n" +
            "    _ = C.CString(\"hello\")\n" +
            "}\n");

        Assert.IsFalse(
            usageSet.Contains("CString"),
            "C.CString is a Go-side helper and must not be forwarded to C tooling.");
    }

    [TestMethod]
    public void PseudoName_CBytes_IsFilteredOut()
    {
        CgoUsageSet usageSet = CollectFromSource(
            "package example\n" +
            "\n" +
            "import \"C\"\n" +
            "\n" +
            "func UseSymbol(data []byte) {\n" +
            "    _ = C.CBytes(data)\n" +
            "}\n");

        Assert.IsFalse(usageSet.Contains("CBytes"));
    }

    [TestMethod]
    public void PseudoName_GoString_IsFilteredOut()
    {
        CgoUsageSet usageSet = CollectFromSource(
            "package example\n" +
            "\n" +
            "import \"C\"\n" +
            "\n" +
            "func UseSymbol(ptr *C.char) {\n" +
            "    _ = C.GoString(ptr)\n" +
            "}\n");

        Assert.IsFalse(usageSet.Contains("GoString"));
    }

    [TestMethod]
    public void PseudoName_GoStringN_IsFilteredOut()
    {
        CgoUsageSet usageSet = CollectFromSource(
            "package example\n" +
            "\n" +
            "import \"C\"\n" +
            "\n" +
            "func UseSymbol(ptr *C.char, length C.int) {\n" +
            "    _ = C.GoStringN(ptr, length)\n" +
            "}\n");

        Assert.IsFalse(usageSet.Contains("GoStringN"));
    }

    [TestMethod]
    public void PseudoName_GoBytes_IsFilteredOut()
    {
        CgoUsageSet usageSet = CollectFromSource(
            "package example\n" +
            "\n" +
            "import \"C\"\n" +
            "\n" +
            "func UseSymbol(ptr unsafe.Pointer, length C.int) {\n" +
            "    _ = C.GoBytes(ptr, length)\n" +
            "}\n");

        Assert.IsFalse(usageSet.Contains("GoBytes"));
    }

    [TestMethod]
    public void PseudoNamesMixedWithRealSymbols_OnlyRealSymbolsRemain()
    {
        CgoUsageSet usageSet = CollectFromSource(
            "package example\n" +
            "\n" +
            "import \"C\"\n" +
            "\n" +
            "func UseSymbol(data []byte) {\n" +
            "    s := C.CString(\"hello\")\n" +
            "    _ = C.sqlite3_open\n" +
            "    _ = C.SQLITE_OK\n" +
            "    _ = C.GoBytes(nil, 0)\n" +
            "    _ = s\n" +
            "}\n");

        Assert.IsFalse(usageSet.Contains("CString"));
        Assert.IsFalse(usageSet.Contains("GoBytes"));
        Assert.IsTrue(usageSet.Contains("sqlite3_open"));
        Assert.IsTrue(usageSet.Contains("SQLITE_OK"));
    }

    [TestMethod]
    public void NonCgoSelector_IsIgnored()
    {
        CgoUsageSet usageSet = CollectFromSource(
            "package example\n" +
            "\n" +
            "import \"fmt\"\n" +
            "\n" +
            "func UseSymbol() {\n" +
            "    fmt.Println(\"hi\")\n" +
            "}\n");

        Assert.AreEqual(0, usageSet.Count, "Selectors on non-\"C\" receivers must not be collected.");
    }

    private static CgoUsageSet CollectFromSource(string goSource)
    {
        SyntaxTree tree = SyntaxTree.Parse(goSource);
        Assert.IsFalse(
            tree.HasErrors,
            "Test Go source failed to parse cleanly.");

        var sourceFiles = new List<SourceFileSyntax> { tree.Root };
        return CgoUsageCollector.Collect(sourceFiles);
    }
}
