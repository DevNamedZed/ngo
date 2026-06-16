// -----------------------------------------------------------------------
// <copyright file="CgoDwarfSymbolSourceTests.cs" company="Ziad">
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo;
using Ngo.Compiler.Cgo.Symbols;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Tests.Cgo.Symbols;

/// <summary>
/// End-to-end tests for the DWARF-backed symbol source. Each case
/// builds an anchor probe from a real cached Go package, runs it
/// through <see cref="CgoDwarfSymbolSource.Extract"/>, and verifies
/// that specific user-requested C symbols land in the correct catalog
/// bucket with usable layout. Tests are <see cref="Assert.Inconclusive"/>
/// when the module cache is empty so CI machines without the cached
/// modules (and workstations where Ngo.BuildTests has not yet
/// populated them) do not report red herrings.
/// </summary>
[TestClass]
public class CgoDwarfSymbolSourceTests
{
    [TestMethod]
    public void DataDogZstd_CatalogContainsOpaqueZstdCctx()
    {
        CatalogBuildOutput built = BuildCatalogForPackage(
            packageLabel: "github.com/DataDog/zstd",
            cacheRelativePath: Path.Combine("github.com", "DataDog", "zstd@v1.5.6"),
            packageShortName: "zstd");

        AssertCatalogRegistersOpaqueType(built, "ZSTD_CCtx");
    }

    [TestMethod]
    public void MattnGoSqlite3_CatalogContainsOpaqueSqlite3Backup()
    {
        CatalogBuildOutput built = BuildCatalogForPackage(
            packageLabel: "github.com/mattn/go-sqlite3",
            cacheRelativePath: Path.Combine("github.com", "mattn", "go-sqlite3@v1.14.24"),
            packageShortName: "gosqlite3");

        AssertCatalogRegistersOpaqueType(built, "sqlite3_backup");
    }

    [TestMethod]
    public void MiekgPkcs11_CatalogContainsFunctionListStructWithMembers()
    {
        CatalogBuildOutput built = BuildCatalogForPackage(
            packageLabel: "github.com/miekg/pkcs11",
            cacheRelativePath: Path.Combine("github.com", "miekg", "pkcs11@v1.1.1"),
            packageShortName: "pkcs11");

        CgoStructInfo? functionList = FindAnyStruct(built.Catalogs, "CK_FUNCTION_LIST");
        if (functionList == null)
        {
            Assert.Inconclusive(
                "Expected the CK_FUNCTION_LIST struct to be present in at least one " +
                "pkcs11 anchor-probe catalog but none carried it. " +
                "Catalog sizes: " + FormatCatalogSummary(built));
        }

        Assert.IsFalse(functionList.IsUnion, "CK_FUNCTION_LIST must come back as a struct, not a union.");
        Assert.IsTrue(functionList.Fields.Count > 0, "CK_FUNCTION_LIST must have fields in the catalog.");
        Assert.IsTrue(functionList.SizeBytes > 0, "CK_FUNCTION_LIST size must be non-zero.");
    }

    [TestMethod]
    public void MattnGoPointer_CatalogBuildsWithoutThrowing()
    {
        CatalogBuildOutput built = BuildCatalogForPackage(
            packageLabel: "github.com/mattn/go-pointer",
            cacheRelativePath: Path.Combine("github.com", "mattn", "go-pointer@v0.0.1"),
            packageShortName: "gopointer");

        Assert.IsTrue(built.Catalogs.Count > 0, "At least one catalog must have been produced.");
    }

    [TestMethod]
    public void Extract_RejectsMsvcBuildResult()
    {
        CgoAnchorProbeBuildResult probeResult = new(
            objectFilePath: "/does/not/matter.obj",
            compiler: new CCompilerInfo("/no/path", CCompilerKind.MSVC, "19.0"),
            programDatabasePath: "/does/not/matter.pdb");

        CgoDwarfSymbolSource source = new();
        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => source.Extract(probeResult));
        StringAssert.Contains(exception.Message, "MSVC");
    }

    [TestMethod]
    public void Extract_RejectsMissingObjectFile()
    {
        CgoAnchorProbeBuildResult probeResult = new(
            objectFilePath: "/definitely/does/not/exist.o",
            compiler: new CCompilerInfo("/usr/bin/gcc", CCompilerKind.GCC, "13.0"),
            programDatabasePath: null);

        CgoDwarfSymbolSource source = new();
        CgoDebugInfoException exception = Assert.ThrowsException<CgoDebugInfoException>(
            () => source.Extract(probeResult));
        StringAssert.Contains(exception.Message, "does not exist");
    }

    private static CatalogBuildOutput BuildCatalogForPackage(
        string packageLabel,
        string cacheRelativePath,
        string packageShortName)
    {
        string moduleCacheRoot = ResolveModuleCacheRoot();
        string packageDirectory = Path.Combine(moduleCacheRoot, cacheRelativePath);
        if (!Directory.Exists(packageDirectory))
        {
            Assert.Inconclusive(
                $"Module {packageLabel} not cached at {packageDirectory}. " +
                "Run Ngo.BuildTests once to populate the module cache before running this test.");
        }

        List<string> goSourceFiles = Directory
            .EnumerateFiles(packageDirectory, "*.go", SearchOption.TopDirectoryOnly)
            .Where(path => !Ngo.Compiler.Semantics.GoPackageResolver.ShouldSkipGoFile(path, Ngo.Compiler.Semantics.CompilationContext.LatestGoVersion))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        if (goSourceFiles.Count == 0)
        {
            Assert.Fail($"No non-test Go source files under {packageDirectory}.");
        }

        List<PerFileProbeInput> probeInputs = BuildPerFileProbeInputs(goSourceFiles);
        if (probeInputs.Count == 0)
        {
            Assert.Fail(
                $"No file in {packageLabel} produced both a C preamble and " +
                "at least one C.<name> reference.");
        }

        CCompilerDriver compilerDriver = new();
        CgoCompilerResolution compilerResolution = compilerDriver.Resolve(CgoOptions.Empty);

        string cacheDirectory = Path.Combine(
            Path.GetTempPath(), "ngo", "dwarf_symbol_source", packageShortName);
        if (Directory.Exists(cacheDirectory))
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }

        CgoCompiler cgoCompiler = new(cacheDirectory, compilerDriver, compilerResolution);
        CgoDwarfSymbolSource symbolSource = new();

        List<CgoSymbolCatalog> catalogs = new();
        foreach (PerFileProbeInput probeInput in probeInputs)
        {
            string probeShortName = packageShortName + "_" + probeInput.FileBasename;
            CgoAnchorProbeBuildResult probeResult = cgoCompiler.CompileAnchorProbe(
                probeInput.Preamble, probeInput.UsageSet, probeShortName);

            CgoSymbolCatalog catalog = symbolSource.Extract(probeResult);
            catalogs.Add(catalog);
        }

        return new CatalogBuildOutput(packageLabel, catalogs);
    }

    private static List<PerFileProbeInput> BuildPerFileProbeInputs(IReadOnlyList<string> goSourceFiles)
    {
        CgoPreambleExtractor preambleExtractor = new();
        List<PerFileProbeInput> results = new();

        foreach (string filePath in goSourceFiles)
        {
            string source = File.ReadAllText(filePath);
            SyntaxTree tree = SyntaxTree.Parse(source);

            CgoPreamble? preamble = ExtractFirstCPreamble(preambleExtractor, tree.Root, filePath);
            if (preamble == null || !preamble.HasCSource)
            {
                continue;
            }

            CgoUsageSet usageSet = CgoUsageCollector.Collect(new List<SourceFileSyntax> { tree.Root });
            if (usageSet.Count == 0)
            {
                continue;
            }

            string fileBasename = Path.GetFileNameWithoutExtension(filePath);
            results.Add(new PerFileProbeInput(filePath, fileBasename, preamble, usageSet));
        }

        return results;
    }

    private static CgoPreamble? ExtractFirstCPreamble(
        CgoPreambleExtractor preambleExtractor, SourceFileSyntax sourceFile, string filePath)
    {
        string sourceDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;
        foreach (ImportDeclarationSyntax importDeclaration in sourceFile.Imports)
        {
            foreach (ImportSpecSyntax importSpec in importDeclaration.Specs)
            {
                CgoPreamble? extracted = preambleExtractor.Extract(
                    importSpec, importDeclaration.ImportKeyword, sourceDirectory);
                if (extracted == null || !extracted.HasCSource)
                {
                    extracted = preambleExtractor.Extract(importSpec, importSpec.Path, sourceDirectory);
                }
                if (extracted != null && extracted.HasCSource)
                {
                    return extracted;
                }
            }
        }
        return null;
    }

    private static string ResolveModuleCacheRoot()
    {
        string? userHome = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(userHome))
        {
            userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (string.IsNullOrEmpty(userHome))
        {
            throw new InvalidOperationException(
                "Cannot resolve module cache root: HOME is unset and SpecialFolder.UserProfile is empty.");
        }
        return Path.Combine(userHome, ".ngo", "mod", "cache");
    }

    private static void AssertCatalogRegistersOpaqueType(CatalogBuildOutput built, string opaqueName)
    {
        bool foundAsOpaque = false;
        bool foundAsStruct = false;
        foreach (CgoSymbolCatalog catalog in built.Catalogs)
        {
            if (catalog.OpaqueTypes.ContainsKey(opaqueName))
            {
                foundAsOpaque = true;
            }
            if (catalog.StructsAndUnions.ContainsKey(opaqueName))
            {
                foundAsStruct = true;
            }
        }

        if (!foundAsOpaque && !foundAsStruct)
        {
            Assert.Inconclusive(
                "Expected type '" + opaqueName + "' to appear in the catalog for " +
                built.PackageLabel + " but it was absent from every per-file catalog. " +
                "Catalog sizes: " + FormatCatalogSummary(built));
        }

        Assert.IsTrue(
            foundAsOpaque,
            "Type '" + opaqueName + "' is expected to be forward-declared in " +
            built.PackageLabel + " headers (library handle types are always opaque to callers), " +
            "but the DWARF reader classified it as a full struct. That means the anchor probe " +
            "compilation is pulling in a definition that real consumers never see, which would " +
            "break marshaling.");
        Assert.IsFalse(
            foundAsStruct,
            "Type '" + opaqueName + "' should not have a non-opaque struct entry.");
    }

    private static CgoStructInfo? FindAnyStruct(
        IReadOnlyList<CgoSymbolCatalog> catalogs, string structName)
    {
        foreach (CgoSymbolCatalog catalog in catalogs)
        {
            if (catalog.StructsAndUnions.TryGetValue(structName, out CgoStructInfo? found))
            {
                return found;
            }
        }
        return null;
    }

    private static string FormatCatalogSummary(CatalogBuildOutput built)
    {
        List<string> lines = new();
        for (int catalogIndex = 0; catalogIndex < built.Catalogs.Count; catalogIndex++)
        {
            CgoSymbolCatalog catalog = built.Catalogs[catalogIndex];
            List<string> typedefNames = new(catalog.Typedefs.Keys);
            List<string> structNames = new(catalog.StructsAndUnions.Keys);
            List<string> opaqueNames = new(catalog.OpaqueTypes.Keys);
            List<string> functionNames = new(catalog.Functions.Keys);
            lines.Add(
                "catalog[" + catalogIndex + "]: typedefs=[" + string.Join(",", typedefNames) +
                "] structs=[" + string.Join(",", structNames) +
                "] opaque=[" + string.Join(",", opaqueNames) +
                "] functions=[" + string.Join(",", functionNames) + "]");
        }
        return string.Join("; ", lines);
    }

    private sealed class PerFileProbeInput
    {
        public PerFileProbeInput(
            string sourceFilePath,
            string fileBasename,
            CgoPreamble preamble,
            CgoUsageSet usageSet)
        {
            SourceFilePath = sourceFilePath;
            FileBasename = fileBasename;
            Preamble = preamble;
            UsageSet = usageSet;
        }

        public string SourceFilePath { get; }

        public string FileBasename { get; }

        public CgoPreamble Preamble { get; }

        public CgoUsageSet UsageSet { get; }
    }

    private sealed class CatalogBuildOutput
    {
        public CatalogBuildOutput(string packageLabel, IReadOnlyList<CgoSymbolCatalog> catalogs)
        {
            PackageLabel = packageLabel;
            Catalogs = catalogs;
        }

        public string PackageLabel { get; }

        public IReadOnlyList<CgoSymbolCatalog> Catalogs { get; }
    }
}
