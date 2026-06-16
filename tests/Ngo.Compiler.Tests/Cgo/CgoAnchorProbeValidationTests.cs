// -----------------------------------------------------------------------
// <copyright file="CgoAnchorProbeValidationTests.cs" company="Ziad">
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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Cgo;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;
using Ngo.Compiler.Semantics;

namespace Ngo.Compiler.Tests.Cgo;

/// <summary>
/// End-to-end validation that the anchor-probe pipeline accepts real
/// preambles from the four packages currently listed as failing in
/// Ngo.BuildTests (datadog-zstd, mattn-go-pointer, mattn-go-sqlite3,
/// miekg-pkcs11). Run before committing to a DWARF or PDB reader:
/// a failure here means the probe generator design needs revisiting,
/// not that the reader is wrong. The test is <see cref="Assert.Inconclusive"/>
/// when a package is absent from the local module cache so that the
/// broader test run on machines that have not populated the cache
/// does not produce red herrings — Ngo.BuildTests is responsible for
/// populating the cache and should be run first.
/// </summary>
[TestClass]
public class CgoAnchorProbeValidationTests
{
    [TestMethod]
    public void DataDogZstd_AnchorProbeCompiles()
    {
        RunAnchorProbeValidation(new AnchorProbeValidationCase(
            packageLabel: "github.com/DataDog/zstd",
            cacheRelativePath: Path.Combine("github.com", "DataDog", "zstd@v1.5.6"),
            packageShortName: "zstd"));
    }

    [TestMethod]
    public void MattnGoPointer_AnchorProbeCompiles()
    {
        RunAnchorProbeValidation(new AnchorProbeValidationCase(
            packageLabel: "github.com/mattn/go-pointer",
            cacheRelativePath: Path.Combine("github.com", "mattn", "go-pointer@v0.0.1"),
            packageShortName: "gopointer"));
    }

    [TestMethod]
    public void MattnGoSqlite3_AnchorProbeCompiles()
    {
        RunAnchorProbeValidation(new AnchorProbeValidationCase(
            packageLabel: "github.com/mattn/go-sqlite3",
            cacheRelativePath: Path.Combine("github.com", "mattn", "go-sqlite3@v1.14.24"),
            packageShortName: "gosqlite3"));
    }

    [TestMethod]
    public void MiekgPkcs11_AnchorProbeCompiles()
    {
        RunAnchorProbeValidation(new AnchorProbeValidationCase(
            packageLabel: "github.com/miekg/pkcs11",
            cacheRelativePath: Path.Combine("github.com", "miekg", "pkcs11@v1.1.1"),
            packageShortName: "pkcs11"));
    }

    private static void RunAnchorProbeValidation(AnchorProbeValidationCase validationCase)
    {
        string moduleCacheRoot = ResolveModuleCacheRoot();
        string packageDirectory = Path.Combine(moduleCacheRoot, validationCase.CacheRelativePath);
        if (!Directory.Exists(packageDirectory))
        {
            Assert.Inconclusive(
                $"Module {validationCase.PackageLabel} not cached at {packageDirectory}. " +
                "Run Ngo.BuildTests once to populate the module cache before running this validation.");
        }

        List<string> goSourceFiles = Directory
            .EnumerateFiles(packageDirectory, "*.go", SearchOption.TopDirectoryOnly)
            .Where(path => !GoPackageResolver.ShouldSkipGoFile(path, CompilationContext.LatestGoVersion))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (goSourceFiles.Count == 0)
        {
            Assert.Fail(
                $"No non-test Go source files under {packageDirectory} for {validationCase.PackageLabel}.");
        }

        List<PerFileProbeInput> perFileProbes = BuildPerFileProbeInputs(goSourceFiles);
        if (perFileProbes.Count == 0)
        {
            Assert.Fail(
                $"No file in {validationCase.PackageLabel} produced both a C preamble and " +
                $"at least one C.<name> reference. Extractor or usage collector stage broke.");
        }

        var compilerDriver = new CCompilerDriver();
        CgoCompilerResolution compilerResolution = compilerDriver.Resolve(CgoOptions.Empty);

        string validationCacheDirectory = Path.Combine(
            Path.GetTempPath(), "ngo", "anchor_validation", validationCase.PackageShortName);
        if (Directory.Exists(validationCacheDirectory))
        {
            Directory.Delete(validationCacheDirectory, recursive: true);
        }

        var cgoCompiler = new CgoCompiler(validationCacheDirectory, compilerDriver, compilerResolution);

        int totalObjectBytes = 0;
        foreach (PerFileProbeInput probeInput in perFileProbes)
        {
            string probeShortName = validationCase.PackageShortName + "_" + probeInput.FileBasename;

            CgoAnchorProbeBuildResult probeResult;
            try
            {
                probeResult = cgoCompiler.CompileAnchorProbe(
                    probeInput.Preamble, probeInput.UsageSet, probeShortName);
            }
            catch (CgoCCompileException compileException)
            {
                Assert.Fail(BuildCompileFailureMessage(
                    validationCase, probeInput, compileException));
                throw;
            }

            Assert.IsTrue(
                File.Exists(probeResult.ObjectFilePath),
                $"CompileAnchorProbe returned without throwing for {validationCase.PackageLabel} " +
                $"(file {probeInput.FileBasename}) but the object file {probeResult.ObjectFilePath} is missing.");

            long objectFileSize = new FileInfo(probeResult.ObjectFilePath).Length;
            Assert.IsTrue(
                objectFileSize > 0,
                $"Object file for {validationCase.PackageLabel} file {probeInput.FileBasename} " +
                $"exists but is empty ({probeResult.ObjectFilePath}).");

            totalObjectBytes += (int)objectFileSize;
        }

        Console.WriteLine(
            $"{validationCase.PackageLabel}: " +
            $"sources={goSourceFiles.Count}, " +
            $"probes={perFileProbes.Count}, " +
            $"totalObjects={totalObjectBytes}B at {validationCacheDirectory}");
    }

    private static List<PerFileProbeInput> BuildPerFileProbeInputs(IReadOnlyList<string> goSourceFiles)
    {
        var preambleExtractor = new CgoPreambleExtractor();
        var perFileProbes = new List<PerFileProbeInput>();

        foreach (string filePath in goSourceFiles)
        {
            string source = File.ReadAllText(filePath);
            SyntaxTree tree = SyntaxTree.Parse(source);

            CgoPreamble? filePreamble = ExtractFirstCPreamble(preambleExtractor, tree.Root, filePath);
            if (filePreamble == null || !filePreamble.HasCSource)
            {
                continue;
            }

            CgoUsageSet fileUsageSet = CgoUsageCollector.Collect(new List<SourceFileSyntax> { tree.Root });
            if (fileUsageSet.Count == 0)
            {
                continue;
            }

            string fileBasename = Path.GetFileNameWithoutExtension(filePath);
            perFileProbes.Add(new PerFileProbeInput(filePath, fileBasename, filePreamble, fileUsageSet));
        }

        return perFileProbes;
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

    private static string BuildCompileFailureMessage(
        AnchorProbeValidationCase validationCase,
        PerFileProbeInput probeInput,
        CgoCCompileException compileException)
    {
        var message = new StringBuilder();
        message.AppendLine(
            $"Anchor probe failed for {validationCase.PackageLabel} file {probeInput.FileBasename}.");
        message.AppendLine(
            $"  sourceFile: {probeInput.SourceFilePath}");
        message.AppendLine(
            $"  preamble: {probeInput.Preamble.CSource.Length} bytes");
        message.AppendLine(
            $"  usage: {probeInput.UsageSet.Count} names");
        message.AppendLine(
            $"  first 20 C.<name> references: " +
            string.Join(", ", probeInput.UsageSet.Names.Take(20)));
        message.AppendLine("--- C compiler stderr ---");
        message.AppendLine(compileException.CompilerOutput ?? "(empty)");
        return message.ToString();
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

    private sealed class AnchorProbeValidationCase
    {
        public AnchorProbeValidationCase(
            string packageLabel,
            string cacheRelativePath,
            string packageShortName)
        {
            PackageLabel = packageLabel;
            CacheRelativePath = cacheRelativePath;
            PackageShortName = packageShortName;
        }

        public string PackageLabel { get; }

        public string CacheRelativePath { get; }

        public string PackageShortName { get; }
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
}
