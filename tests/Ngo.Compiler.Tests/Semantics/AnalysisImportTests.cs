// -----------------------------------------------------------------------
// <copyright file="AnalysisImportTests.cs" company="Ziad">
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

using System.Linq;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Semantics;

[TestClass]
public class AnalysisImportTests
{
    private static AnalysisResult Analyze(string source)
    {
        var tree = SyntaxTree.Parse(source);
        return SemanticAnalyzer.Analyze(tree);
    }

    [TestMethod]
    public void Import_fmt_no_errors()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    fmt.Println(""hello"")
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Import_fmt_println_with_args()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    fmt.Println(""hello"", 42, true)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Import_fmt_print()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    fmt.Print(""hello"")
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Import_fmt_printf_with_format()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    fmt.Printf(""%d\n"", 42)
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Import_fmt_sprintf_returns_string()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    s := fmt.Sprintf(""%d"", 42)
    var x string = s
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Import_fmt_sprint_returns_string()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    s := fmt.Sprint(42, ""hello"")
    var x string = s
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Import_unknown_package_error()
    {
        var result = Analyze(@"package main
import ""unknown""
func main() {}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName
            && e.Message.Contains("unknown")));
    }

    [TestMethod]
    public void Import_fmt_unknown_member_error()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    fmt.DoesNotExist()
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.UndeclaredName
            && e.Message.Contains("DoesNotExist")));
    }

    [TestMethod]
    public void Import_with_alias()
    {
        var result = Analyze(@"package main
import f ""fmt""
func main() {
    f.Println(""hello"")
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Import_grouped()
    {
        var result = Analyze(@"package main
import (
    ""fmt""
)
func main() {
    fmt.Println(""hello"")
}");
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void Import_stored_in_ast()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Imports.Count);
        Assert.AreEqual("fmt", result.Root.Imports[0].Path);
        Assert.AreEqual("fmt", result.Root.Imports[0].Package.Name);
        Assert.IsNull(result.Root.Imports[0].Alias);
    }

    [TestMethod]
    public void Import_with_alias_stored_in_ast()
    {
        var result = Analyze(@"package main
import f ""fmt""
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Root.Imports.Count);
        Assert.AreEqual("fmt", result.Root.Imports[0].Path);
        Assert.AreEqual("f", result.Root.Imports[0].Package.Name);
        Assert.AreEqual("f", result.Root.Imports[0].Alias);
    }

    [TestMethod]
    public void Import_fmt_printf_too_few_args_error()
    {
        var result = Analyze(@"package main
import ""fmt""
func main() {
    fmt.Printf()
}");
        Assert.IsTrue(result.HasErrors);
        Assert.IsTrue(result.Errors.Any(e => e.Code == ErrorCode.WrongArgumentCount));
    }

    [TestMethod]
    public void Import_blank_import_no_scope_pollution()
    {
        var result = Analyze(@"package main
import _ ""fmt""
func main() {}");
        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(0, result.Root.Imports.Count);
    }
}
