// -----------------------------------------------------------------------
// <copyright file="BuildCgoPackageIntegrationTests.cs" company="Ziad">
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Tests.Cgo;

/// <summary>
/// End-to-end tests for the <c>import "C"</c> path through
/// <see cref="SemanticAnalyzer"/>. These tests pin the contract that
/// <see cref="DeclarationResolver"/> populates
/// <see cref="CompilationContext.CgoPackage"/> and
/// <see cref="CompilationContext.CgoCatalog"/> together, so a user of
/// one can rely on the other also being set. They also verify the
/// catalog-driven <see cref="CgoSymbolBuilder"/> rewiring — a C
/// function declared in the preamble must land in the resolved C
/// pseudo-package as a <see cref="FunctionSymbol"/> with parameter
/// shape matching the declaration.
/// </summary>
[TestClass]
public class BuildCgoPackageIntegrationTests
{
    private static readonly string TestProjectRoot = Path.Combine(
        Path.GetTempPath(), "ngo-test-project");

    static BuildCgoPackageIntegrationTests()
    {
        Directory.CreateDirectory(TestProjectRoot);
    }

    [TestMethod]
    public void ImportC_WithoutPreamble_PopulatesEmptyCatalogAndPackage()
    {
        CompilationContext compilation = new(TestProjectRoot);
        SyntaxTree tree = SyntaxTree.Parse(
            "package main\n" +
            "\n" +
            "import \"C\"\n" +
            "\n" +
            "func main() {}\n");

        AnalysisResult result = SemanticAnalyzer.Analyze(tree, compilation);

        Assert.IsFalse(result.HasErrors,
            "import \"C\" without a preamble must analyse cleanly — the emitter still needs the pseudo-package.");
        Assert.IsNotNull(compilation.CgoPackage,
            "CgoPackage must be set even when the preamble is empty so the emitter can still resolve helper symbols.");
        Assert.IsNotNull(compilation.CgoCatalog,
            "CgoCatalog must be set alongside CgoPackage so downstream code does not need a null check pair.");
        Assert.AreEqual(0, compilation.CgoCatalog!.Functions.Count,
            "Empty preamble means no user-declared C functions in the catalog.");

        Assert.IsInstanceOfType(
            compilation.CgoPackage!.LookupExport("CString"),
            typeof(FunctionSymbol),
            "Marshalling helpers must be exported even when the preamble is empty.");
        Assert.IsInstanceOfType(
            compilation.CgoPackage.LookupExport("int"),
            typeof(TypeSymbol),
            "Primitive aliases must be exported even when the preamble is empty.");
    }

    [TestMethod]
    public void ImportC_WithUserFunctionPreamble_ExposesFunctionInCatalogAndPackage()
    {
        if (!IsCCompilerAvailable(out string reason))
        {
            Assert.Inconclusive(
                "Skipping integration test: " + reason +
                ". A working C compiler is required to compile the anchor probe.");
        }

        CompilationContext compilation = new(TestProjectRoot);
        SyntaxTree tree = SyntaxTree.Parse(
            "package main\n" +
            "\n" +
            "/*\n" +
            "static int ngo_integration_add(int a, int b) { return a + b; }\n" +
            "*/\n" +
            "import \"C\"\n" +
            "\n" +
            "func main() {\n" +
            "    _ = C.ngo_integration_add(C.int(1), C.int(2))\n" +
            "}\n");

        AnalysisResult result = SemanticAnalyzer.Analyze(tree, compilation);

        Assert.IsFalse(result.HasErrors,
            "A preamble with a single static function must analyse without errors.");
        Assert.IsNotNull(compilation.CgoCatalog,
            "CgoCatalog must be populated after a successful BuildCgoPackage run with a non-empty preamble.");
        Assert.IsTrue(
            compilation.CgoCatalog!.Functions.ContainsKey("ngo_integration_add"),
            "The catalog must contain the user-declared C function so the P/Invoke emitter can generate a stub.");

        FunctionSymbol? function = compilation.CgoPackage!.LookupExport("ngo_integration_add")
            as FunctionSymbol;
        Assert.IsNotNull(function,
            "The catalog function must surface on the C pseudo-package as a FunctionSymbol for Go-side resolution.");
        Assert.AreEqual(2, function!.Parameters.Count,
            "Parameter count from the C declaration must survive the DWARF read and the BuildCPackage translation.");
    }

    private static bool IsCCompilerAvailable(out string reason)
    {
        try
        {
            new CCompilerDriver().Resolve(CgoOptions.Empty);
            reason = "";
            return true;
        }
        catch (CgoDisabledException ex)
        {
            reason = ex.Message;
            return false;
        }
        catch (CgoCompilerNotFoundException ex)
        {
            reason = ex.FormatDiagnostic();
            return false;
        }
    }
}
