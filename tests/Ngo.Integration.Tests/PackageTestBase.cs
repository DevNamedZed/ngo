// -----------------------------------------------------------------------
// <copyright file="PackageTestBase.cs" company="Ziad">
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
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using Ngo.Compiler;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Integration.Tests;

public abstract class PackageTestBase
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ngo", "mod", "cache");

    private static readonly HttpClient Http = new();

    protected static string EnsureModule(string module, string version, string? subPkg = null)
    {
        var escaped = EscapeModulePath(module);
        var cacheKey = $"{module}@{version}";
        var moduleDir = Path.Combine(CacheDir, cacheKey.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(moduleDir) || Directory.GetFiles(moduleDir, "*.go", SearchOption.AllDirectories).Length == 0)
        {
            var url = $"https://proxy.golang.org/{escaped}/@v/{version}.zip";
            Console.WriteLine($"Downloading {url}...");

            var zipBytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
            var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var zipPrefix = module + "@" + version + "/";

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("/")) continue;

                var relativePath = entry.FullName;
                if (relativePath.StartsWith(zipPrefix))
                    relativePath = relativePath.Substring(zipPrefix.Length);
                else
                {
                    var atIdx = relativePath.IndexOf('@');
                    if (atIdx >= 0)
                    {
                        var slashAfterVersion = relativePath.IndexOf('/', atIdx);
                        if (slashAfterVersion >= 0)
                            relativePath = relativePath.Substring(slashAfterVersion + 1);
                    }
                }

                if (string.IsNullOrEmpty(relativePath)) continue;

                var targetPath = Path.Combine(moduleDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var targetDir = Path.GetDirectoryName(targetPath)!;
                Directory.CreateDirectory(targetDir);

                using var entryStream = entry.Open();
                using var fileStream = File.Create(targetPath);
                entryStream.CopyTo(fileStream);
            }
        }

        if (subPkg != null)
        {
            return Path.Combine(moduleDir, subPkg.Replace('/', Path.DirectorySeparatorChar));
        }

        return moduleDir;
    }

    protected static IReadOnlyList<CompileError> AnalyzePackageDir(string dir, string? projectRoot = null)
    {
        var goFiles = Directory.GetFiles(dir, "*.go");
        var trees = new List<SyntaxTree>();

        foreach (var file in goFiles)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                continue;
            if (IsPlatformFile(fileName))
                continue;

            var source = File.ReadAllText(file);
            if (HasPlatformBuildTag(source))
                continue;

            trees.Add(SyntaxTree.Parse(source));
        }

        if (trees.Count == 0)
            return Array.Empty<CompileError>();

        if (projectRoot != null)
            PackageRegistry.SetProjectRoot(projectRoot);
        try
        {
            var result = SemanticAnalyzer.Analyze(trees);
            return result.Errors.Where(e => e.Severity == ErrorSeverity.Error).ToList();
        }
        finally
        {
            if (projectRoot != null)
                PackageRegistry.SetProjectRoot(null);
        }
    }

    protected static void DumpErrors(string label, IReadOnlyList<CompileError> errors)
    {
        Console.WriteLine($"{label}: {errors.Count} errors");
        if (errors.Count > 0)
        {
            var grouped = errors.GroupBy(e => e.Code).OrderByDescending(g => g.Count());
            foreach (var g in grouped)
                Console.WriteLine($"  {g.Key}: {g.Count()}");
            foreach (var e in errors)
                Console.WriteLine($"  [{e.Code}] {e.Message} at {e.Location}");
        }
    }

    protected static void AssertZeroErrors(string label, IReadOnlyList<CompileError> errors)
    {
        DumpErrors(label, errors);
        Assert.AreEqual(0, errors.Count, $"{label}: Expected 0 errors, got {errors.Count}");
    }

    private static string EscapeModulePath(string path)
    {
        var chars = new List<char>();
        foreach (var c in path)
        {
            if (char.IsUpper(c))
            {
                chars.Add('!');
                chars.Add(char.ToLower(c));
            }
            else
            {
                chars.Add(c);
            }
        }
        return new string(chars.ToArray());
    }

    private static bool IsPlatformFile(string fileName)
    {
        var suffixes = new[]
        {
            "_windows.go", "_darwin.go", "_freebsd.go", "_openbsd.go", "_netbsd.go",
            "_solaris.go", "_plan9.go", "_aix.go", "_ios.go", "_js.go", "_wasip1.go",
            "_android.go", "_illumos.go", "_dragonfly.go", "_hurd.go",
            "_386.go", "_arm.go", "_arm64.go", "_mips.go", "_mips64.go",
            "_mipsle.go", "_mips64le.go", "_ppc64.go", "_ppc64le.go",
            "_riscv64.go", "_s390x.go", "_wasm.go", "_loong64.go", "_nacl.go",
        };
        foreach (var s in suffixes)
        {
            if (fileName.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasPlatformBuildTag(string source)
    {
        var platforms = new[]
        {
            "windows", "darwin", "freebsd", "openbsd", "netbsd",
            "solaris", "plan9", "aix", "ios", "js", "wasip1",
            "android", "illumos", "dragonfly", "hurd", "cgo",
            "ignore", "generate",
        };

        var lines = source.Split('\n');
        for (int i = 0; i < Math.Min(lines.Length, 30); i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("package ")) break;

            if (line.StartsWith("// +build ") || line.StartsWith("//go:build "))
            {
                var tag = line.StartsWith("// +build ") ? line.Substring(10).Trim() : line.Substring(11).Trim();

                // Old-style: spaces = OR groups, commas = AND within group
                // Exclude if no OR group can be satisfied in our environment
                // Our environment: linux/amd64, safe mode, go1.22+
                var orGroups = tag.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bool anyGroupSatisfied = false;
                foreach (var group in orGroups)
                {
                    var andTerms = group.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    bool groupSatisfied = true;
                    foreach (var term in andTerms)
                    {
                        var t = term.Trim();
                        if (!EvalBuildTerm(t, platforms))
                        {
                            groupSatisfied = false;
                            break;
                        }
                    }
                    if (groupSatisfied)
                    {
                        anyGroupSatisfied = true;
                        break;
                    }
                }
                if (!anyGroupSatisfied)
                    return true;
            }
        }
        return false;
    }

    private static bool EvalBuildTerm(string term, string[] platforms)
    {
        bool negated = term.StartsWith("!");
        var name = negated ? term.Substring(1) : term;

        bool active;
        if (name == "linux" || name == "amd64")
            active = true;
        else if (name == "safe" || name == "disableunsafe")
            active = true; // ngo runs in safe mode (no unsafe support)
        else if (Array.IndexOf(platforms, name) >= 0 || name == "appengine")
            active = false;
        else if (name.StartsWith("go1."))
        {
            // go1.N — we satisfy go1.4 through go1.22
            if (int.TryParse(name.Substring(4), out int ver))
                active = ver <= 22;
            else
                active = true;
        }
        else if (name == "none" || name == "generate" || name == "tools" || name == "example" || name == "protolegacy")
            active = false; // known custom tags that are never active during normal builds
        else
            active = true; // unknown tags default to satisfied (allows stubs etc.)

        return negated ? !active : active;
    }
}
