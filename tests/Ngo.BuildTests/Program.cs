// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Ziad">
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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Ngo.Compiler;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;

var options = ParseArgs(args);

if (options.ShowHelp)
{
    Console.WriteLine("Usage: ngo-buildtest [command] [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  packages    Test third-party packages from packages.json (default)");
    Console.WriteLine("  stdlib      Test Go standard library compilation from source");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -f, --filter <text>   Filter by name/module");
    Console.WriteLine("  -v, --verbose         Show passing tests");
    Console.WriteLine("  --go-version <ver>    Go version for stdlib (default: from stdlib.json)");
    Console.WriteLine("  -h, --help            Show this help");
    return 0;
}

if (options.Command == "stdlib")
    return RunStdlib(options);
else
    return RunPackages(options);

// -----------------------------------------------------------------------
// Package runner (third-party open source packages)
// -----------------------------------------------------------------------

static int RunPackages(Options options)
{
    var packagesPath = Path.Combine(AppContext.BaseDirectory, "packages.json");
    if (!File.Exists(packagesPath))
        packagesPath = Path.Combine(Directory.GetCurrentDirectory(), "packages.json");
    if (!File.Exists(packagesPath))
    {
        Console.Error.WriteLine("packages.json not found");
        return 1;
    }

    var allPackages = JsonSerializer.Deserialize<List<PackageEntry>>(
        File.ReadAllText(packagesPath))!;

    var packages = allPackages.AsEnumerable();

    if (options.Filter != null)
    {
        var filter = options.Filter;
        packages = packages.Where(p =>
            p.label.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            p.module.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    var list = packages.ToList();
    Console.WriteLine($"Running {list.Count} packages...");
    Console.WriteLine();

    var sw = Stopwatch.StartNew();
    int passed = 0, failed = 0, skipped = 0;
    var failures = new List<(string label, int errors, string detail)>();

    foreach (var pkg in list)
    {
        string dir;
        try
        {
            if (pkg.deps != null)
            {
                foreach (var dep in pkg.deps)
                    ModuleCache.EnsureModule(dep.module, dep.version);
            }
            dir = ModuleCache.EnsureModule(pkg.module, pkg.version, pkg.subPackage);
        }
        catch (Exception ex)
        {
            if (options.Verbose)
                Console.WriteLine($"  SKIP  {pkg.label} -- download failed: {ex.Message}");
            skipped++;
            continue;
        }

        var errors = Analyzer.AnalyzePackageDir(dir);

        bool ok = errors.Count == 0;

        if (ok)
        {
            passed++;
            if (options.Verbose)
            {
                Console.WriteLine($"  PASS  {pkg.label}");
            }
        }
        else
        {
            failed++;
            var grouped = errors.GroupBy(e => e.Code)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}:{g.Count()}");
            var detail = string.Join(" ", grouped);
            failures.Add((pkg.label, errors.Count, detail));
            Console.WriteLine($"  FAIL  {pkg.label} -- {errors.Count} errors [{detail}]");
            if (options.Verbose)
            {
                foreach (var err in errors.Take(15))
                    Console.WriteLine($"         {err.Code}: {err.Message} ({err.Location})");
                if (errors.Count > 15)
                    Console.WriteLine($"         ... and {errors.Count - 15} more");
            }
        }

    }

    sw.Stop();
    Console.WriteLine();
    Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"  Passed:  {passed}");
    Console.WriteLine($"  Failed:  {failed}");
    Console.WriteLine($"  Skipped: {skipped}");
    Console.WriteLine($"  Total:   {list.Count}");

    if (failures.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Failures:");
        foreach (var (label, errors, detail) in failures)
            Console.WriteLine($"  {label}: {errors} errors [{detail}]");
    }

    return failed > 0 ? 1 : 0;
}

// -----------------------------------------------------------------------
// Stdlib runner (compile Go stdlib from source)
// -----------------------------------------------------------------------

static int RunStdlib(Options options)
{
    var stdlibPath = Path.Combine(AppContext.BaseDirectory, "stdlib.json");
    if (!File.Exists(stdlibPath))
        stdlibPath = Path.Combine(Directory.GetCurrentDirectory(), "stdlib.json");
    if (!File.Exists(stdlibPath))
    {
        Console.Error.WriteLine("stdlib.json not found");
        return 1;
    }

    var manifest = JsonSerializer.Deserialize<StdlibManifest>(
        File.ReadAllText(stdlibPath))!;

    var goVersion = options.GoVersion ?? manifest.goVersion;
    Console.WriteLine($"Go stdlib version: {goVersion}");

    // Download and cache Go source
    var goSrcDir = GoSourceCache.EnsureGoSource(goVersion);
    Console.WriteLine($"Go source: {goSrcDir}");
    Console.WriteLine();

    var packages = manifest.packages.AsEnumerable();

    if (options.Filter != null)
    {
        var filter = options.Filter;
        packages = packages.Where(p =>
            p.path.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    var list = packages.ToList();
    Console.WriteLine($"Testing {list.Count} stdlib packages...");
    Console.WriteLine();

    var sw = Stopwatch.StartNew();
    int passed = 0, failed = 0, skipped = 0;
    var failures = new List<(string path, int errors, string detail)>();

    foreach (var pkg in list)
    {
        var pkgDir = Path.Combine(goSrcDir, pkg.path.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(pkgDir))
        {
            if (options.Verbose)
                Console.WriteLine($"  SKIP  {pkg.path} -- directory not found");
            skipped++;
            continue;
        }

        IReadOnlyList<CompileError> errors;
        try
        {
            errors = Analyzer.AnalyzePackageDir(pkgDir);
        }
        catch (Exception ex)
        {
            failed++;
            var detail = $"CRASH:{ex.GetType().Name}";
            failures.Add((pkg.path, -1, detail));
            if (options.Verbose)
                Console.WriteLine($"  CRASH {pkg.path} -- {ex.GetType().Name}: {ex.Message}");
            continue;
        }

        if (errors.Count == 0)
        {
            passed++;
            if (options.Verbose)
                Console.WriteLine($"  PASS  {pkg.path}");
        }
        else
        {
            failed++;
            var grouped = errors.GroupBy(e => e.Code)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}:{g.Count()}");
            var detail = string.Join(" ", grouped);
            failures.Add((pkg.path, errors.Count, detail));
            if (options.Verbose)
            {
                Console.WriteLine($"  FAIL  {pkg.path} -- {errors.Count} errors [{detail}]");
                if (errors.Count <= 400)
                {
                    foreach (var e in errors.Take(50))
                        Console.WriteLine($"         {e.Code}: {e.Message} ({e.Location})");
                }
            }
        }

    }

    sw.Stop();

    // Summary
    Console.WriteLine();
    Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"  Passed:  {passed}");
    Console.WriteLine($"  Failed:  {failed}");
    Console.WriteLine($"  Skipped: {skipped}");
    Console.WriteLine($"  Total:   {list.Count}");

    // Group by impl type
    var goPackages = manifest.packages.Where(p => p.impl == "go").ToList();
    var csharpPackages = manifest.packages.Where(p => p.impl == "csharp").ToList();
    Console.WriteLine();
    Console.WriteLine($"  Target self-hosted (go):  {goPackages.Count}");
    Console.WriteLine($"  Staying in C# (csharp):   {csharpPackages.Count}");

    if (failures.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Failures (top 30):");
        foreach (var (path, errors, detail) in failures.Take(30))
            Console.WriteLine($"  {path}: {errors} errors [{detail}]");
        if (failures.Count > 30)
            Console.WriteLine($"  ... and {failures.Count - 30} more");
    }

    return 0; // Don't fail — this is exploratory
}

// -----------------------------------------------------------------------
// Arg parsing
// -----------------------------------------------------------------------

static Options ParseArgs(string[] args)
{
    var opts = new Options();
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "packages":
                opts.Command = "packages";
                break;
            case "stdlib":
                opts.Command = "stdlib";
                break;
            case "--filter" or "-f":
                opts.Filter = args[++i];
                break;
            case "--verbose" or "-v":
                opts.Verbose = true;
                break;
            case "--go-version":
                opts.GoVersion = args[++i];
                break;
            case "--help" or "-h":
                opts.ShowHelp = true;
                break;
            default:
                if (!args[i].StartsWith("-"))
                {
                    if (opts.Command == null && args[i] is "stdlib" or "packages")
                        opts.Command = args[i];
                    else
                        opts.Filter = args[i];
                }
                break;
        }
    }
    opts.Command ??= "packages";
    return opts;
}

// -----------------------------------------------------------------------
// Types
// -----------------------------------------------------------------------

class Options
{
    public string? Command;
    public string? Filter;
    public bool Verbose;
    public string? GoVersion;
    public bool ShowHelp;
}

class PackageEntry
{
    public string module { get; set; } = "";
    public string version { get; set; } = "";
    public string? subPackage { get; set; }
    public string label { get; set; } = "";
    public List<DepEntry>? deps { get; set; }
}

class DepEntry
{
    public string module { get; set; } = "";
    public string version { get; set; } = "";
}

class StdlibManifest
{
    public string goVersion { get; set; } = "";
    public List<StdlibPackageEntry> packages { get; set; } = new();
}

class StdlibPackageEntry
{
    public string path { get; set; } = "";
    public string impl { get; set; } = "go"; // "go" = compile from source, "csharp" = keep C# runtime
    public string? reason { get; set; }       // why it stays in C# (if impl=csharp)
}

// -----------------------------------------------------------------------
// Go source download cache
// -----------------------------------------------------------------------

static class GoSourceCache
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ngo", "gosrc");

    private static readonly HttpClient Http = new();

    public static string EnsureGoSource(string version)
    {
        var srcDir = Path.Combine(CacheDir, version, "src");

        if (Directory.Exists(srcDir) &&
            Directory.GetDirectories(srcDir).Length > 0)
        {
            return srcDir;
        }

        var versionDir = Path.Combine(CacheDir, version);
        Directory.CreateDirectory(versionDir);

        var url = $"https://go.dev/dl/{version}.src.tar.gz";
        Console.WriteLine($"Downloading {url}...");

        var tgzBytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();

        Console.WriteLine($"Extracting ({tgzBytes.Length / 1024 / 1024}MB)...");

        // .tar.gz: decompress gzip, then extract tar
        using var gzStream = new GZipStream(new MemoryStream(tgzBytes), CompressionMode.Decompress);
        using var tarStream = new MemoryStream();
        gzStream.CopyTo(tarStream);
        tarStream.Position = 0;

        ExtractTar(tarStream, versionDir);

        // The tarball extracts as go/src/... — we want just src/ at versionDir/src/
        // Check if it extracted as go/src or directly as src
        var goSubdir = Path.Combine(versionDir, "go", "src");
        if (Directory.Exists(goSubdir) && !Directory.Exists(srcDir))
        {
            Directory.Move(Path.Combine(versionDir, "go"), Path.Combine(versionDir, "_go"));
            foreach (var item in Directory.GetFileSystemEntries(Path.Combine(versionDir, "_go")))
            {
                var name = Path.GetFileName(item);
                var dest = Path.Combine(versionDir, name);
                if (!Directory.Exists(dest) && !File.Exists(dest))
                    Directory.Move(item, dest);
            }
            try { Directory.Delete(Path.Combine(versionDir, "_go"), true); } catch { }
        }

        if (!Directory.Exists(srcDir))
            throw new Exception($"Go source extraction failed — {srcDir} not found");

        Console.WriteLine($"Cached at {versionDir}");
        return srcDir;
    }

    private static void ExtractTar(Stream tarStream, string outputDir)
    {
        // Minimal tar extractor (POSIX tar format)
        var buffer = new byte[512];
        while (true)
        {
            int bytesRead = ReadFull(tarStream, buffer, 0, 512);
            if (bytesRead < 512) break;

            // Check for end-of-archive (two zero blocks)
            if (buffer.All(b => b == 0)) break;

            // Parse header
            var name = ReadTarString(buffer, 0, 100);
            var sizeStr = ReadTarString(buffer, 124, 12);
            var typeFlag = (char)buffer[156];
            var prefix = ReadTarString(buffer, 345, 155);

            if (!string.IsNullOrEmpty(prefix))
                name = prefix + "/" + name;

            long size = 0;
            if (!string.IsNullOrEmpty(sizeStr))
            {
                try { size = Convert.ToInt64(sizeStr.Trim(), 8); } catch { }
            }

            // Only extract .go files and go.mod to save space
            bool shouldExtract = typeFlag is '0' or '\0' &&
                (name.EndsWith(".go") || name.EndsWith("go.mod") || name.EndsWith("go.sum"));

            if (shouldExtract && size > 0)
            {
                // Strip leading "go/" prefix from tarball
                var relativePath = name;
                if (relativePath.StartsWith("go/"))
                    relativePath = relativePath.Substring(3);

                var targetPath = Path.Combine(outputDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                var fileData = new byte[size];
                ReadFull(tarStream, fileData, 0, (int)size);

                File.WriteAllBytes(targetPath, fileData);

                // Skip padding to 512-byte boundary
                var remainder = (int)(size % 512);
                if (remainder > 0)
                    ReadFull(tarStream, new byte[512 - remainder], 0, 512 - remainder);
            }
            else if (size > 0)
            {
                // Skip file data + padding
                long total = size;
                var remainder = (int)(size % 512);
                if (remainder > 0) total += 512 - remainder;

                var skipBuf = new byte[4096];
                while (total > 0)
                {
                    int toRead = (int)Math.Min(total, skipBuf.Length);
                    int read = tarStream.Read(skipBuf, 0, toRead);
                    if (read == 0) break;
                    total -= read;
                }
            }
        }
    }

    private static int ReadFull(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0) break;
            totalRead += read;
        }
        return totalRead;
    }

    private static string ReadTarString(byte[] buffer, int offset, int length)
    {
        int end = offset;
        while (end < offset + length && buffer[end] != 0) end++;
        return System.Text.Encoding.ASCII.GetString(buffer, offset, end - offset);
    }
}

// -----------------------------------------------------------------------
// Module cache (downloads from proxy.golang.org)
// -----------------------------------------------------------------------

static class ModuleCache
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ngo", "mod", "cache");

    private static readonly HttpClient Http = new();

    public static string EnsureModule(string module, string version, string? subPkg = null)
    {
        var cacheKey = $"{module}@{version}";
        var moduleDir = Path.Combine(CacheDir, cacheKey.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(moduleDir) ||
            Directory.GetFiles(moduleDir, "*.go", SearchOption.AllDirectories).Length == 0)
        {
            var escaped = EscapeModulePath(module);
            var url = $"https://proxy.golang.org/{escaped}/@v/{version}.zip";
            Console.WriteLine($"  Downloading {url}...");

            var zipBytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
            using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
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
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                using var entryStream = entry.Open();
                using var fileStream = File.Create(targetPath);
                entryStream.CopyTo(fileStream);
            }
        }

        return subPkg != null
            ? Path.Combine(moduleDir, subPkg.Replace('/', Path.DirectorySeparatorChar))
            : moduleDir;
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
                chars.Add(c);
        }
        return new string(chars.ToArray());
    }
}

// -----------------------------------------------------------------------
// Analyzer (semantic analysis of Go package directories)
// -----------------------------------------------------------------------

static class Analyzer
{
    private static readonly string[] PlatformSuffixes =
    {
        "_windows.go", "_darwin.go", "_freebsd.go", "_openbsd.go", "_netbsd.go",
        "_solaris.go", "_plan9.go", "_aix.go", "_ios.go", "_js.go", "_wasip1.go",
        "_android.go", "_illumos.go", "_dragonfly.go", "_hurd.go",
        "_386.go", "_arm.go", "_arm64.go", "_mips.go", "_mips64.go",
        "_mipsle.go", "_mips64le.go", "_ppc64.go", "_ppc64le.go",
        "_riscv64.go", "_s390x.go", "_wasm.go", "_loong64.go", "_nacl.go", "_zos.go",
    };

    // Non-target operating systems (we target linux)
    private static readonly HashSet<string> InactiveOS = new(new[]
    {
        "windows", "darwin", "freebsd", "openbsd", "netbsd",
        "solaris", "plan9", "aix", "ios", "js", "wasip1",
        "android", "illumos", "dragonfly", "hurd", "zos", "nacl",
    });

    private static readonly string[] Platforms =
    {
        "windows", "darwin", "freebsd", "openbsd", "netbsd",
        "solaris", "plan9", "aix", "ios", "js", "wasip1",
        "android", "illumos", "dragonfly", "hurd", "cgo",
        "ignore", "generate",
    };

    // Architectures that are NOT our target (amd64) — these should evaluate to false
    private static readonly string[] InactiveArchitectures =
    {
        "386", "arm", "arm64", "mips", "mips64", "mipsle", "mips64le",
        "ppc64", "ppc64le", "riscv64", "s390x", "wasm", "loong64",
    };

    public static IReadOnlyList<CompileError> AnalyzePackageDir(string dir)
    {
        if (!Directory.Exists(dir))
            return Array.Empty<CompileError>();

        var trees = new List<SyntaxTree>();
        foreach (var file in Directory.GetFiles(dir, "*.go"))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                continue;
            if (PlatformSuffixes.Any(s => fileName.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
                continue;
            // Handle compound GOOS_GOARCH suffixes: name_darwin_amd64.go
            // If the second-to-last part is a non-target OS, exclude it.
            if (HasInactiveCompoundSuffix(fileName))
                continue;

            var source = File.ReadAllText(file);
            if (HasPlatformBuildTag(source))
                continue;

            trees.Add(SyntaxTree.Parse(source, file));
        }

        if (trees.Count == 0)
            return Array.Empty<CompileError>();

        // Inject synthetic stubs for generated files that only exist after `go generate`
        InjectSyntheticSources(dir, trees);

        var moduleRoot = FindModuleRoot(dir);
        var compilation = new CompilationContext(moduleRoot ?? dir);

        var result = SemanticAnalyzer.Analyze(trees, compilation);
        return result.Errors.Where(e => e.Severity == ErrorSeverity.Error).ToList();
    }

    private static string? FindModuleRoot(string dir)
    {
        var current = dir;
        while (current != null)
        {
            var goModPath = Path.Combine(current, "go.mod");
            if (File.Exists(goModPath))
                return current;
            var parent = Path.GetDirectoryName(current);
            if (parent == current || string.IsNullOrEmpty(parent)) break;
            current = parent;
        }
        return null;
    }

    private static bool HasInactiveCompoundSuffix(string fileName)
    {
        // Parse compound suffix: name_OS_ARCH.go
        // If _OS_ARCH.go where OS is inactive (not linux), exclude.
        var name = fileName.Substring(0, fileName.Length - 3); // strip .go
        int last = name.LastIndexOf('_');
        if (last <= 0) return false;
        int secondLast = name.LastIndexOf('_', last - 1);
        if (secondLast <= 0) return false;
        var osPart = name.Substring(secondLast + 1, last - secondLast - 1);
        return InactiveOS.Contains(osPart);
    }

    private static void InjectSyntheticSources(string dir, List<SyntaxTree> trees)
    {
        var dirName = dir.Replace('\\', '/');
        // go/build/zcgo.go — generated by cmd/dist, defines defaultCGO_ENABLED
        if (dirName.EndsWith("/go/build"))
        {
            trees.Add(SyntaxTree.Parse("package build\n\nconst defaultCGO_ENABLED = \"0\"\n"));
        }
        // time/tzdata/zipdata.go — generated, contains embedded timezone database
        if (dirName.EndsWith("/time/tzdata"))
        {
            trees.Add(SyntaxTree.Parse("package tzdata\n\nconst zipdata = \"\"\n"));
        }
    }

    private static bool HasPlatformBuildTag(string source)
    {
        // If //go:build is present, it is authoritative — ignore // +build lines.
        string? goBuildExpr = null;
        var oldBuildTags = new List<string>();

        var lines = source.Split('\n');
        for (int i = 0; i < Math.Min(lines.Length, 30); i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("package ")) break;

            if (line.StartsWith("//go:build "))
            {
                goBuildExpr = line.Substring(11).Trim();
            }
            else if (line.StartsWith("// +build ") || line.StartsWith("//+build "))
            {
                var tagStart = line.IndexOf("+build ") + 7;
                oldBuildTags.Add(line.Substring(tagStart).Trim());
            }
        }

        if (goBuildExpr != null)
        {
            return !EvalBuildExpression(goBuildExpr);
        }

        // Old-style: each // +build line must be satisfied (AND across lines)
        // Within a line: spaces = OR, commas = AND
        foreach (var tag in oldBuildTags)
        {
            var orGroups = tag.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool anyGroupSatisfied = false;
            foreach (var group in orGroups)
            {
                var andTerms = group.Split(',', StringSplitOptions.RemoveEmptyEntries);
                bool groupSatisfied = andTerms.All(t => EvalBuildTerm(t.Trim()));
                if (groupSatisfied) { anyGroupSatisfied = true; break; }
            }
            if (!anyGroupSatisfied)
            {
                return true;
            }
        }

        return false;
    }

    // Recursive descent parser for //go:build expression syntax
    private static bool EvalBuildExpression(string expr)
    {
        int pos = 0;
        return ParseOr(expr, ref pos);
    }

    private static bool ParseOr(string expr, ref int pos)
    {
        bool result = ParseAnd(expr, ref pos);
        while (true)
        {
            SkipSpaces(expr, ref pos);
            if (pos + 1 < expr.Length && expr[pos] == '|' && expr[pos + 1] == '|')
            {
                pos += 2;
                bool right = ParseAnd(expr, ref pos);
                result = result || right;
            }
            else break;
        }
        return result;
    }

    private static bool ParseAnd(string expr, ref int pos)
    {
        bool result = ParseUnary(expr, ref pos);
        while (true)
        {
            SkipSpaces(expr, ref pos);
            if (pos + 1 < expr.Length && expr[pos] == '&' && expr[pos + 1] == '&')
            {
                pos += 2;
                bool right = ParseUnary(expr, ref pos);
                result = result && right;
            }
            else break;
        }
        return result;
    }

    private static bool ParseUnary(string expr, ref int pos)
    {
        SkipSpaces(expr, ref pos);
        if (pos < expr.Length && expr[pos] == '!')
        {
            pos++;
            return !ParseUnary(expr, ref pos);
        }
        if (pos < expr.Length && expr[pos] == '(')
        {
            pos++; // skip '('
            bool result = ParseOr(expr, ref pos);
            SkipSpaces(expr, ref pos);
            if (pos < expr.Length && expr[pos] == ')')
                pos++; // skip ')'
            return result;
        }
        // Parse identifier (tag name)
        int start = pos;
        while (pos < expr.Length && expr[pos] != ' ' && expr[pos] != ')' && expr[pos] != '&' && expr[pos] != '|' && expr[pos] != '!')
            pos++;
        string term = expr.Substring(start, pos - start);
        return EvalBuildTerm(term);
    }

    private static void SkipSpaces(string expr, ref int pos)
    {
        while (pos < expr.Length && expr[pos] == ' ')
            pos++;
    }

    private static bool EvalBuildTerm(string term)
    {
        bool negated = term.StartsWith("!");
        var name = negated ? term.Substring(1) : term;

        bool active;
        if (name is "linux" or "amd64" or "unix")
            active = true;
        else if (name is "gc")
            active = true;
        else if (name is "cgo")
            active = true;
        else if (name.StartsWith("go1.") && int.TryParse(name.AsSpan(4), out int ver))
            active = ver <= Ngo.Compiler.Semantics.CompilationContext.LatestGoVersion;
        else
            active = false;

        return negated ? !active : active;
    }
}
