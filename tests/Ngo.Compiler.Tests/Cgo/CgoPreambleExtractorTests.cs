// -----------------------------------------------------------------------
// <copyright file="CgoPreambleExtractorTests.cs" company="Ziad">
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Tests.Cgo;

/// <summary>
/// Unit tests for <see cref="CgoPreambleExtractor"/>. The extractor is the
/// only code that converts raw comment trivia into a C preamble string,
/// and a regression in its ordering logic silently produces garbled C
/// that the downstream probe compiler fails on with confusing errors.
/// These tests lock in line-comment ordering, block-comment inner-line
/// ordering, contiguity rules, and directive extraction.
/// </summary>
[TestClass]
public class CgoPreambleExtractorTests
{
    private const string TestSourceDirectory = "/project/pkg";

    [TestMethod]
    public void LineCommentPreamble_ExtractsCSourceInForwardOrder()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "// #include <stdio.h>\n" +
            "// int answer = 42;\n" +
            "import \"C\"\n";

        CgoPreamble preamble = ExtractOrFail(goSource);

        Assert.AreEqual(
            "#include <stdio.h>\nint answer = 42;",
            preamble.CSource);
    }

    [TestMethod]
    public void MultiLineBlockCommentPreamble_KeepsInnerLinesInSourceOrder()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "/*\n" +
            "#include <stdio.h>\n" +
            "int first(void);\n" +
            "int second(void);\n" +
            "*/\n" +
            "import \"C\"\n";

        CgoPreamble preamble = ExtractOrFail(goSource);

        string[] lines = preamble.CSource.Split('\n');
        int includeIndex = IndexOfLineContaining(lines, "#include");
        int firstIndex = IndexOfLineContaining(lines, "int first");
        int secondIndex = IndexOfLineContaining(lines, "int second");

        Assert.IsTrue(
            includeIndex >= 0 && firstIndex >= 0 && secondIndex >= 0,
            $"Expected all three lines in extracted CSource. Got:\n{preamble.CSource}");
        Assert.IsTrue(
            includeIndex < firstIndex,
            $"Expected #include before int first. Got:\n{preamble.CSource}");
        Assert.IsTrue(
            firstIndex < secondIndex,
            $"Expected int first before int second. Got:\n{preamble.CSource}");
    }

    [TestMethod]
    public void SingleLineBlockCommentPreamble_StripsDelimitersKeepsInnerText()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "/* #include <stdio.h> */\n" +
            "import \"C\"\n";

        CgoPreamble preamble = ExtractOrFail(goSource);

        StringAssert.Contains(preamble.CSource, "#include <stdio.h>");
        Assert.IsFalse(
            preamble.CSource.Contains("/*") || preamble.CSource.Contains("*/"),
            $"Block comment delimiters must be stripped. Got:\n{preamble.CSource}");
    }

    [TestMethod]
    public void MixedLineAndBlockCommentPreamble_PreservesFullSourceOrder()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "// #include <stdio.h>\n" +
            "/*\n" +
            "int middle(void);\n" +
            "*/\n" +
            "// int trailing = 1;\n" +
            "import \"C\"\n";

        CgoPreamble preamble = ExtractOrFail(goSource);

        string[] lines = preamble.CSource.Split('\n');
        int includeIndex = IndexOfLineContaining(lines, "#include");
        int middleIndex = IndexOfLineContaining(lines, "int middle");
        int trailingIndex = IndexOfLineContaining(lines, "int trailing");

        Assert.IsTrue(
            includeIndex >= 0 && middleIndex >= 0 && trailingIndex >= 0,
            $"Expected all three lines. Got:\n{preamble.CSource}");
        Assert.IsTrue(
            includeIndex < middleIndex && middleIndex < trailingIndex,
            $"Expected order include < middle < trailing. Got:\n{preamble.CSource}");
    }

    [TestMethod]
    public void BlankLineBreaksContiguity_TruncatesPreambleAtBlankLine()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "// unrelated doc comment\n" +
            "\n" +
            "// #include <stdio.h>\n" +
            "import \"C\"\n";

        CgoPreamble preamble = ExtractOrFail(goSource);

        StringAssert.Contains(preamble.CSource, "#include <stdio.h>");
        Assert.IsFalse(
            preamble.CSource.Contains("unrelated doc comment"),
            $"Comments separated by a blank line must not be part of the preamble. Got:\n{preamble.CSource}");
    }

    [TestMethod]
    public void NonCImport_ReturnsNull()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "// #include <stdio.h>\n" +
            "import \"fmt\"\n";

        CgoPreamble? preamble = ExtractFromFirstImport(goSource);

        Assert.IsNull(
            preamble,
            "Extractor must return null for non-\"C\" imports regardless of the leading comment block.");
    }

    [TestMethod]
    public void CgoDirectivesAreParsedIntoDirectivesAndStrippedFromCSource()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "// #cgo CFLAGS: -I/usr/include/foo\n" +
            "// #cgo LDFLAGS: -lfoo\n" +
            "// #include <foo.h>\n" +
            "import \"C\"\n";

        CgoPreamble preamble = ExtractOrFail(goSource);

        StringAssert.Contains(preamble.CSource, "#include <foo.h>");
        Assert.IsFalse(
            preamble.CSource.Contains("#cgo"),
            $"#cgo directives must not appear in CSource. Got:\n{preamble.CSource}");

        Assert.AreEqual(2, preamble.Directives.Count);
        CgoDirective cflags = preamble.Directives.Single(directive => directive.Kind == "CFLAGS");
        Assert.AreEqual("-I/usr/include/foo", cflags.Value);
        Assert.IsNull(cflags.OsConstraint);

        CgoDirective ldflags = preamble.Directives.Single(directive => directive.Kind == "LDFLAGS");
        Assert.AreEqual("-lfoo", ldflags.Value);
        Assert.IsNull(ldflags.OsConstraint);
    }

    [TestMethod]
    public void SourceDirectoryIsPropagatedOntoThePreamble()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "// #include <stdio.h>\n" +
            "import \"C\"\n";

        CgoPreamble preamble = ExtractOrFail(goSource, sourceDirectory: "/abs/path/to/pkg");

        Assert.AreEqual(
            "/abs/path/to/pkg",
            preamble.SourceDirectory,
            "The extractor must store the caller-supplied directory verbatim so CgoCompiler.BuildIncludeArgs can emit it as -I for the probe compile.");
    }

    [TestMethod]
    public void NoLeadingComments_ReturnsEmptyPreambleWithNoCSource()
    {
        string goSource =
            "package example\n" +
            "\n" +
            "import \"C\"\n";

        CgoPreamble? preamble = ExtractFromFirstImport(goSource);

        Assert.IsNotNull(preamble, "Extractor must not return null for a \"C\" import with no leading comments.");
        Assert.IsFalse(preamble!.HasCSource, $"Expected empty CSource. Got:\n{preamble.CSource}");
        Assert.AreEqual(0, preamble.Directives.Count);
    }

    private static CgoPreamble ExtractOrFail(string goSource, string sourceDirectory = TestSourceDirectory)
    {
        CgoPreamble? preamble = ExtractFromFirstImport(goSource, sourceDirectory);
        if (preamble == null)
        {
            Assert.Fail(
                "Extractor returned null for a \"C\" import. The test setup expected a preamble to be produced.");
        }
        return preamble!;
    }

    private static CgoPreamble? ExtractFromFirstImport(string goSource, string sourceDirectory = TestSourceDirectory)
    {
        SyntaxTree tree = SyntaxTree.Parse(goSource);
        Assert.IsFalse(
            tree.HasErrors,
            $"Test Go source failed to parse cleanly: {string.Join("; ", tree.Errors.Select(error => error.Message))}");
        Assert.IsTrue(
            tree.Root.Imports.Count > 0,
            "Test Go source must contain at least one import declaration.");

        ImportDeclarationSyntax importDeclaration = tree.Root.Imports[0];
        Assert.IsTrue(
            importDeclaration.Specs.Count > 0,
            "Test Go source's first import declaration must contain at least one spec.");
        ImportSpecSyntax importSpec = importDeclaration.Specs[0];

        var extractor = new CgoPreambleExtractor();
        CgoPreamble? fromKeyword = extractor.Extract(
            importSpec, importDeclaration.ImportKeyword, sourceDirectory);
        if (fromKeyword != null && (fromKeyword.HasCSource || fromKeyword.Directives.Count > 0))
        {
            return fromKeyword;
        }

        CgoPreamble? fromPath = extractor.Extract(importSpec, importSpec.Path, sourceDirectory);
        if (fromPath != null && (fromPath.HasCSource || fromPath.Directives.Count > 0))
        {
            return fromPath;
        }

        return fromKeyword ?? fromPath;
    }

    private static int IndexOfLineContaining(string[] lines, string needle)
    {
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (lines[lineIndex].Contains(needle))
            {
                return lineIndex;
            }
        }
        return -1;
    }
}
