// -----------------------------------------------------------------------
// <copyright file="PackageRegistry.cs" company="Ziad">
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
using Ngo.Compiler.Language;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;

namespace Ngo.Compiler.Semantics
{
    public static class PackageRegistry
    {
        private static string? _projectRoot;
        private static readonly GoModuleResolver _moduleResolver = new();
        private static readonly Dictionary<string, AnalysisResult> _userPackageCache = new();

        public static void SetProjectRoot(string? root)
        {
            _projectRoot = root;
            _userPackageCache.Clear();

            if (root != null)
            {
                _moduleResolver.LoadGoMod(root);
            }
        }

        public static GoModuleResolver ModuleResolver => _moduleResolver;

        private static readonly Dictionary<string, Func<PackageSymbol>> _packages = new()
        {
            ["fmt"] = CreateFmtPackage,
            ["strconv"] = CreateStrconvPackage,
            ["strings"] = CreateStringsPackage,
            ["errors"] = CreateErrorsPackage,
            ["math"] = CreateMathPackage,
            ["math/bits"] = CreateMathBitsPackage,
            ["sync"] = CreateSyncPackage,
            ["os"] = CreateOsPackage,
            ["os/signal"] = CreateOsSignalPackage,
            ["time"] = CreateTimePackage,
            ["sort"] = CreateSortPackage,
            ["math/rand"] = CreateMathRandPackage,
            ["log"] = CreateLogPackage,
            ["io"] = CreateIoPackage,
            ["bufio"] = CreateBufioPackage,
            ["path/filepath"] = CreateFilepathPackage,
            ["regexp"] = CreateRegexpPackage,
            ["unicode"] = CreateUnicodePackage,
            ["unicode/utf8"] = CreateUtf8Package,
            ["unicode/utf16"] = CreateUtf16Package,
            ["bytes"] = CreateBytesPackage,
            ["path"] = CreatePathPackage,
            ["dotnet"] = CreateDotnetPackage,
            ["context"] = CreateContextPackage,
            ["compress/gzip"] = CreateCompressGzipPackage,
            ["encoding/json"] = CreateJsonPackage,
            ["io/ioutil"] = CreateIoutilPackage,
            ["testing"] = CreateTestingPackage,
            ["encoding/base64"] = CreateBase64Package,
            ["encoding/hex"] = CreateHexPackage,
            ["encoding/csv"] = CreateCsvPackage,
            ["flag"] = CreateFlagPackage,
            ["crypto/sha256"] = CreateSha256Package,
            ["crypto/rand"] = CreateCryptoRandPackage,
            ["net/http"] = CreateHttpPackage,
            ["reflect"] = CreateReflectPackage,
            ["runtime"] = CreateRuntimePackage,
            ["runtime/debug"] = CreateRuntimeDebugPackage,
            ["unsafe"] = CreateUnsafePackage,
            ["internal/reflectlite"] = CreateReflectlitePackage,
            ["net/url"] = CreateNetUrlPackage,
            ["net/http/httptest"] = CreateHttptestPackage,
            ["sync/atomic"] = CreateSyncAtomicPackage,
            ["os/exec"] = CreateOsExecPackage,
            ["container/list"] = CreateContainerListPackage,
            ["database/sql/driver"] = CreateDatabaseSqlDriverPackage,
            ["database/sql"] = CreateDatabaseSqlPackage,
            ["encoding"] = CreateEncodingPackage,
            ["text/tabwriter"] = CreateTabwriterPackage,
            ["text/template"] = CreateTextTemplatePackage,
            ["html"] = CreateHtmlPackage,
            ["html/template"] = CreateHtmlTemplatePackage,
            ["encoding/binary"] = CreateEncodingBinaryPackage,
            ["encoding/gob"] = CreateEncodingGobPackage,
            ["hash"] = CreateHashPackage,
            ["hash/fnv"] = CreateHashFnvPackage,
            ["crypto/sha1"] = CreateCryptoSha1Package,
            ["crypto/md5"] = CreateCryptoMd5Package,
            ["net"] = CreateNetPackage,
            ["syscall"] = CreateSyscallPackage,
            ["math/big"] = CreateMathBigPackage,
            ["image/color"] = CreateImageColorPackage,
            ["os/user"] = CreateOsUserPackage,
            ["io/fs"] = CreateIoFsPackage,
            ["hash/crc32"] = CreateHashCrc32Package,
            ["hash/crc64"] = CreateHashCrc64Package,
            ["compress/zlib"] = CreateCompressZlibPackage,
            ["compress/flate"] = CreateCompressFlatePackage,
            ["crypto/hmac"] = CreateCryptoHmacPackage,
            ["crypto/sha256"] = CreateCryptoSha256Package,
            ["crypto/sha512"] = CreateCryptoSha512Package,
            ["crypto/subtle"] = CreateCryptoSubtlePackage,
            ["net/mail"] = CreateNetMailPackage,
            ["cmp"] = CreateCmpPackage,
            ["slices"] = CreateSlicesPackage,
        };

        public static PackageSymbol? Resolve(string importPath)
        {
            if (_packages.TryGetValue(importPath, out var factory))
            {
                return factory();
            }

            // Try resolving as a user-defined package from the filesystem
            return ResolveUserPackage(importPath);
        }

        public static AnalysisResult? GetUserPackageResult(string importPath)
        {
            _userPackageCache.TryGetValue(importPath, out var result);
            return result;
        }

        public static IReadOnlyDictionary<string, AnalysisResult> GetAllUserPackages()
        {
            return _userPackageCache;
        }

        private static PackageSymbol? ResolveUserPackage(string importPath)
        {
            if (_projectRoot == null)
                return null;

            // Check cache first
            if (_userPackageCache.TryGetValue(importPath, out var cached))
            {
                return cached.Root.Package.Symbol;
            }

            // Resolve package directory
            string? pkgDir = null;

            // Try go.mod module-relative resolution first (local packages within the module)
            if (_moduleResolver.ModuleName != null && _moduleResolver.ModuleRoot != null
                && importPath.StartsWith(_moduleResolver.ModuleName))
            {
                var relativePath = importPath.Substring(_moduleResolver.ModuleName.Length).TrimStart('/');
                pkgDir = string.IsNullOrEmpty(relativePath)
                    ? _moduleResolver.ModuleRoot
                    : Path.Combine(_moduleResolver.ModuleRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            // Fall back to project-root-relative resolution
            if (pkgDir == null || !Directory.Exists(pkgDir))
            {
                pkgDir = Path.Combine(_projectRoot, importPath.Replace('/', Path.DirectorySeparatorChar));
            }

            // Try external module resolution via go.mod requirements
            if (!Directory.Exists(pkgDir))
            {
                pkgDir = ResolveExternalModule(importPath);
            }

            if (pkgDir == null || !Directory.Exists(pkgDir))
                return null;

            // Find all .go files in the directory (excluding _test.go and platform-specific files)
            var goFiles = Directory.GetFiles(pkgDir, "*.go");
            if (goFiles.Length == 0)
                return null;

            // Parse all files
            var trees = new List<SyntaxTree>();
            foreach (var file in goFiles)
            {
                var fileName = Path.GetFileName(file);

                // Skip test files
                if (fileName.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip platform-specific files (e.g., file_windows.go, file_darwin.go)
                if (ShouldSkipPlatformFile(fileName))
                    continue;

                var source = File.ReadAllText(file);

                // Skip files with unsupported build tags
                if (HasUnsupportedBuildTag(source))
                    continue;

                trees.Add(SyntaxTree.Parse(source));
            }

            if (trees.Count == 0)
                return null;

            // Analyze the package
            var result = SemanticAnalyzer.Analyze(trees);

            // Cache the result — even with errors, partial results may be usable as dependencies
            _userPackageCache[importPath] = result;

            // Build a PackageSymbol with exports from the analysis result
            var pkgName = result.Root.Package.Symbol.Name;
            var pkg = new PackageSymbol(pkgName, importPath);

            // Export all uppercase-named functions
            foreach (var func in result.Root.Functions)
            {
                if (func.Symbol.Name.Length > 0 && char.IsUpper(func.Symbol.Name[0]))
                {
                    pkg.AddExport(func.Symbol);
                }
            }

            // Export all uppercase-named types
            foreach (var typeDecl in result.Root.Types)
            {
                if (typeDecl.Symbol.Name.Length > 0 && char.IsUpper(typeDecl.Symbol.Name[0]))
                {
                    pkg.AddExport(typeDecl.Symbol);
                }
            }

            // Export all uppercase-named constants
            foreach (var constDecl in result.Root.Constants)
            {
                if (constDecl.Symbol.Name.Length > 0 && char.IsUpper(constDecl.Symbol.Name[0]))
                {
                    pkg.AddExport(constDecl.Symbol);
                }
            }

            // Export all uppercase-named package variables
            foreach (var varDecl in result.Root.Variables)
            {
                if (varDecl.Symbol.Name.Length > 0 && char.IsUpper(varDecl.Symbol.Name[0]))
                {
                    pkg.AddExport(varDecl.Symbol);
                }
            }

            // Export methods on exported types
            foreach (var method in result.Root.Methods)
            {
                if (method.Symbol.Name.Length > 0 && char.IsUpper(method.Symbol.Name[0]))
                {
                    // Methods are already registered on their receiver types
                }
            }

            // Store the package symbol on the analysis result's root
            result.Root.Package.Symbol.CopyExportsFrom(pkg);

            return pkg;
        }

        public static bool ShouldSkipFile(string filePath)
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            if (fileName.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                return true;
            if (ShouldSkipPlatformFile(fileName))
                return true;
            var source = System.IO.File.ReadAllText(filePath);
            return HasUnsupportedBuildTag(source);
        }

        private static bool ShouldSkipPlatformFile(string fileName)
        {
            // Skip OS-specific files that aren't for the current "platform" (we target .NET, treat as linux-like)
            var platformSuffixes = new[]
            {
                "_windows.go", "_darwin.go", "_freebsd.go", "_openbsd.go", "_netbsd.go",
                "_solaris.go", "_plan9.go", "_aix.go", "_ios.go", "_js.go", "_wasip1.go",
                "_android.go", "_illumos.go", "_dragonfly.go", "_hurd.go",
                // Architecture-specific
                "_386.go", "_arm.go", "_arm64.go", "_mips.go", "_mips64.go",
                "_mipsle.go", "_mips64le.go", "_ppc64.go", "_ppc64le.go",
                "_riscv64.go", "_s390x.go", "_wasm.go", "_loong64.go",
            };

            foreach (var suffix in platformSuffixes)
            {
                if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasUnsupportedBuildTag(string source)
        {
            // Check first few lines for build constraints
            // Go build constraints appear before the package clause
            var lines = source.Split('\n');
            for (int i = 0; i < Math.Min(lines.Length, 20); i++)
            {
                var line = lines[i].Trim();

                // Stop at package clause — build tags must come before it
                if (line.StartsWith("package "))
                    break;

                // Old-style: // +build windows
                if (line.StartsWith("// +build "))
                {
                    var tags = line.Substring(10).Trim();
                    // If it contains only platform names, skip
                    // If it's something like "go1.13", allow it (version constraints)
                    if (IsPlatformBuildTag(tags))
                        return true;
                }

                // New-style: //go:build windows
                if (line.StartsWith("//go:build "))
                {
                    var expr = line.Substring(11).Trim();
                    if (IsPlatformBuildTag(expr))
                        return true;
                }
            }

            return false;
        }

        private static bool IsPlatformBuildTag(string tag)
        {
            // Evaluate like the test base: old-style build tags use spaces for OR, commas for AND.
            // Our environment: linux/amd64, not appengine, not cgo, go1.22.
            // Return true if the constraint is NOT satisfied (meaning we should skip the file).
            var platformNames = new[]
            {
                "windows", "darwin", "freebsd", "openbsd", "netbsd",
                "solaris", "plan9", "aix", "ios", "js", "wasip1",
                "android", "illumos", "dragonfly", "hurd",
                "386", "arm", "arm64", "mips", "mips64",
                "mipsle", "mips64le", "ppc64", "ppc64le",
                "riscv64", "s390x", "wasm", "loong64",
                "cgo", "appengine",
            };

            // Handle new-style //go:build expressions by normalizing
            // Simple new-style: "!windows && !appengine" → split by && and ||
            // For now, handle the common cases; fall back to old-style evaluation
            var orGroups = tag.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Filter out logical operators from new-style syntax
            var filteredGroups = new List<string>();
            foreach (var g in orGroups)
            {
                if (g == "&&" || g == "||" || g == "(" || g == ")")
                    continue;
                filteredGroups.Add(g);
            }

            // For old-style: spaces = OR groups, commas = AND within group
            // For simple new-style without parens: treat && as comma (AND), || as space (OR)
            bool anyGroupSatisfied = false;

            if (tag.Contains("||"))
            {
                // New-style with OR: split by ||, each is an AND group
                var parts = tag.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var andTerms = part.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
                    bool groupOk = true;
                    foreach (var term in andTerms)
                    {
                        if (!EvalBuildTerm(term.Trim().Trim('(', ')'), platformNames))
                        {
                            groupOk = false;
                            break;
                        }
                    }
                    if (groupOk) { anyGroupSatisfied = true; break; }
                }
            }
            else if (tag.Contains("&&"))
            {
                // New-style AND only
                var andTerms = tag.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
                bool groupOk = true;
                foreach (var term in andTerms)
                {
                    if (!EvalBuildTerm(term.Trim().Trim('(', ')'), platformNames))
                    {
                        groupOk = false;
                        break;
                    }
                }
                anyGroupSatisfied = groupOk;
            }
            else
            {
                // Old-style: spaces = OR, commas = AND
                foreach (var group in orGroups)
                {
                    var andTerms = group.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    bool groupOk = true;
                    foreach (var term in andTerms)
                    {
                        if (!EvalBuildTerm(term.Trim(), platformNames))
                        {
                            groupOk = false;
                            break;
                        }
                    }
                    if (groupOk) { anyGroupSatisfied = true; break; }
                }
            }

            // If no group is satisfied, the file should be skipped
            return !anyGroupSatisfied;
        }

        private static bool EvalBuildTerm(string term, string[] platformNames)
        {
            bool negated = term.StartsWith("!");
            var name = negated ? term.Substring(1) : term;

            bool active;
            if (name == "linux" || name == "amd64" || name == "unix")
                active = true;
            else if (name == "safe" || name == "disableunsafe")
                active = true;
            else if (Array.IndexOf(platformNames, name) >= 0)
                active = false;
            else if (name.StartsWith("go1."))
            {
                if (int.TryParse(name.Substring(4), out int ver))
                    active = ver <= 22;
                else
                    active = true;
            }
            else if (name == "ignore" || name == "generate" || name == "tools"
                     || name == "none" || name == "example" || name == "protolegacy")
                active = false;
            else
                active = true; // unknown tags default to satisfied

            return negated ? !active : active;
        }

        private static string? ResolveExternalModule(string importPath)
        {
            var match = _moduleResolver.FindModule(importPath);
            if (match == null)
                return null;

            var (module, version) = match.Value;
            var dir = _moduleResolver.ResolvePackageDir(importPath, module, version);
            return dir;
        }

        public static string GetDefaultName(string importPath)
        {
            // Last element of path: "fmt" → "fmt", "math/rand" → "rand"
            var lastSlash = importPath.LastIndexOf('/');
            return lastSlash >= 0 ? importPath.Substring(lastSlash + 1) : importPath;
        }

        private static PackageSymbol CreateFmtPackage()
        {
            var pkg = new PackageSymbol("fmt", "fmt");

            // Println(a ...interface{}) (n int, err error)
            var i64 = BuiltinTypes.Int;
            var err = BuiltinTypes.Error;
            var intErr = new TypeSymbol[] { i64, err };
            pkg.AddExport(new FunctionSymbol("Println", Array.Empty<ParameterSymbol>(), intErr, isVariadic: true, packageName: "fmt"));
            pkg.AddExport(new FunctionSymbol("Print", Array.Empty<ParameterSymbol>(), intErr, isVariadic: true, packageName: "fmt"));
            pkg.AddExport(new FunctionSymbol("Printf",
                new[] { new ParameterSymbol("format", BuiltinTypes.String, 0) },
                intErr, isVariadic: true, packageName: "fmt"));
            pkg.AddExport(CreateFormatFunc("Sprintf", BuiltinTypes.String));
            pkg.AddExport(CreateFormatFunc("Errorf", BuiltinTypes.Error));
            pkg.AddExport(CreateVariadicPrintFunc("Sprint", BuiltinTypes.String));
            pkg.AddExport(CreateVariadicPrintFunc("Sprintln", BuiltinTypes.String));

            // Fprintf(w io.Writer, format string, a ...interface{}) (n int, err error)
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var s = BuiltinTypes.String;
            pkg.AddExport(new FunctionSymbol("Fprintf",
                new[] { new ParameterSymbol("w", iface, 0),
                        new ParameterSymbol("format", s, 1) },
                new TypeSymbol[] { i64, err }, isVariadic: true, packageName: "fmt"));

            // Fprintln(w io.Writer, a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Fprintln",
                new[] { new ParameterSymbol("w", iface, 0) },
                new TypeSymbol[] { i64, err }, isVariadic: true, packageName: "fmt"));

            // Fprint(w io.Writer, a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Fprint",
                new[] { new ParameterSymbol("w", iface, 0) },
                new TypeSymbol[] { i64, err }, isVariadic: true, packageName: "fmt"));

            // Scan(a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Scan",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { i64, iface }, isVariadic: true, packageName: "fmt"));

            // Scanf(format string, a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Scanf",
                new[] { new ParameterSymbol("format", s, 0) },
                new TypeSymbol[] { i64, iface }, isVariadic: true, packageName: "fmt"));

            // Scanln(a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Scanln",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { i64, iface }, isVariadic: true, packageName: "fmt"));

            // Sscan(str string, a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Sscan",
                new[] { new ParameterSymbol("str", s, 0) },
                new TypeSymbol[] { i64, iface }, isVariadic: true, packageName: "fmt"));

            // Sscanf(str string, format string, a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Sscanf",
                new[] { new ParameterSymbol("str", s, 0),
                        new ParameterSymbol("format", s, 1) },
                new TypeSymbol[] { i64, iface }, isVariadic: true, packageName: "fmt"));

            // Sscanln(str string, a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Sscanln",
                new[] { new ParameterSymbol("str", s, 0) },
                new TypeSymbol[] { i64, iface }, isVariadic: true, packageName: "fmt"));

            // fmt.State interface — used by custom formatters
            var stateType = new InterfaceTypeSymbol("State", new[]
            {
                new MethodSymbol("Write", null!, false,
                    new[] { new ParameterSymbol("b", new SliceTypeSymbol(BuiltinTypes.Uint8), 0) },
                    new TypeSymbol[] { i64, err }),
                new MethodSymbol("Width", null!, false,
                    Array.Empty<ParameterSymbol>(),
                    new TypeSymbol[] { i64, BuiltinTypes.Bool }),
                new MethodSymbol("Precision", null!, false,
                    Array.Empty<ParameterSymbol>(),
                    new TypeSymbol[] { i64, BuiltinTypes.Bool }),
                new MethodSymbol("Flag", null!, false,
                    new[] { new ParameterSymbol("c", BuiltinTypes.Int, 0) },
                    BuiltinTypes.Bool),
            });
            pkg.AddExport(stateType);

            // fmt.Formatter interface
            var formatterType = new InterfaceTypeSymbol("Formatter", new[]
            {
                new MethodSymbol("Format", null!, false,
                    new[] { new ParameterSymbol("f", stateType, 0),
                            new ParameterSymbol("verb", BuiltinTypes.Int32, 1) },
                    BuiltinTypes.Void),
            });
            pkg.AddExport(formatterType);

            // fmt.Stringer interface
            var stringerType = new InterfaceTypeSymbol("Stringer", new[]
            {
                new MethodSymbol("String", null!, false,
                    Array.Empty<ParameterSymbol>(), s),
            });
            pkg.AddExport(stringerType);

            // fmt.GoStringer interface
            var goStringerType = new InterfaceTypeSymbol("GoStringer", new[]
            {
                new MethodSymbol("GoString", null!, false,
                    Array.Empty<ParameterSymbol>(), s),
            });
            pkg.AddExport(goStringerType);

            return pkg;
        }

        private static PackageSymbol CreateStrconvPackage()
        {
            var pkg = new PackageSymbol("strconv", "strconv");

            // Itoa(i int) string
            pkg.AddExport(new FunctionSymbol("Itoa",
                new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0) },
                new[] { BuiltinTypes.String }));

            // Atoi(s string) (int, error)
            pkg.AddExport(new FunctionSymbol("Atoi",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));

            // FormatInt(i int64, base int) string
            pkg.AddExport(new FunctionSymbol("FormatInt",
                new[] { new ParameterSymbol("i", BuiltinTypes.Int64, 0),
                        new ParameterSymbol("base", BuiltinTypes.Int, 1) },
                new[] { BuiltinTypes.String }));

            // FormatBool(b bool) string
            pkg.AddExport(new FunctionSymbol("FormatBool",
                new[] { new ParameterSymbol("b", BuiltinTypes.Bool, 0) },
                new[] { BuiltinTypes.String }));

            // ParseInt(s string, base int, bitSize int) (int64, error)
            pkg.AddExport(new FunctionSymbol("ParseInt",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0),
                        new ParameterSymbol("base", BuiltinTypes.Int, 1),
                        new ParameterSymbol("bitSize", BuiltinTypes.Int, 2) },
                new TypeSymbol[] { BuiltinTypes.Int64, BuiltinTypes.Error }));

            // ParseFloat(s string, bitSize int) (float64, error)
            pkg.AddExport(new FunctionSymbol("ParseFloat",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0),
                        new ParameterSymbol("bitSize", BuiltinTypes.Int, 1) },
                new TypeSymbol[] { BuiltinTypes.Float64, BuiltinTypes.Error }));

            // FormatFloat(f float64, fmt byte, prec int, bitSize int) string
            pkg.AddExport(new FunctionSymbol("FormatFloat",
                new[] { new ParameterSymbol("f", BuiltinTypes.Float64, 0),
                        new ParameterSymbol("fmt", BuiltinTypes.Uint8, 1),
                        new ParameterSymbol("prec", BuiltinTypes.Int, 2),
                        new ParameterSymbol("bitSize", BuiltinTypes.Int, 3) },
                new[] { BuiltinTypes.String }));

            // ParseBool(str string) (bool, error)
            pkg.AddExport(new FunctionSymbol("ParseBool",
                new[] { new ParameterSymbol("str", BuiltinTypes.String, 0) },
                new TypeSymbol[] { BuiltinTypes.Bool, BuiltinTypes.Error }));

            // ParseUint(s string, base int, bitSize int) (uint64, error)
            pkg.AddExport(new FunctionSymbol("ParseUint",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0),
                        new ParameterSymbol("base", BuiltinTypes.Int, 1),
                        new ParameterSymbol("bitSize", BuiltinTypes.Int, 2) },
                new TypeSymbol[] { BuiltinTypes.Uint64, BuiltinTypes.Error }));

            // FormatUint(i uint64, base int) string
            pkg.AddExport(new FunctionSymbol("FormatUint",
                new[] { new ParameterSymbol("i", BuiltinTypes.Uint64, 0),
                        new ParameterSymbol("base", BuiltinTypes.Int, 1) },
                new[] { BuiltinTypes.String }));

            // Quote(s string) string
            pkg.AddExport(new FunctionSymbol("Quote",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.String }));

            // Unquote(s string) (string, error)
            pkg.AddExport(new FunctionSymbol("Unquote",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { BuiltinTypes.String, BuiltinTypes.Error }));

            // AppendInt(dst []byte, i int64, base int) []byte
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            pkg.AddExport(new FunctionSymbol("AppendInt",
                new[] { new ParameterSymbol("dst", byteSlice, 0),
                        new ParameterSymbol("i", BuiltinTypes.Int64, 1),
                        new ParameterSymbol("base", BuiltinTypes.Int, 2) },
                new[] { byteSlice }));

            // AppendBool(dst []byte, b bool) []byte
            pkg.AddExport(new FunctionSymbol("AppendBool",
                new[] { new ParameterSymbol("dst", byteSlice, 0),
                        new ParameterSymbol("b", BuiltinTypes.Bool, 1) },
                new[] { byteSlice }));

            // AppendUint(dst []byte, i uint64, base int) []byte
            pkg.AddExport(new FunctionSymbol("AppendUint",
                new[] { new ParameterSymbol("dst", byteSlice, 0),
                        new ParameterSymbol("i", BuiltinTypes.Uint64, 1),
                        new ParameterSymbol("base", BuiltinTypes.Int, 2) },
                new[] { byteSlice }));

            // AppendQuote(dst []byte, s string) []byte
            pkg.AddExport(new FunctionSymbol("AppendQuote",
                new[] { new ParameterSymbol("dst", byteSlice, 0),
                        new ParameterSymbol("s", BuiltinTypes.String, 1) },
                new[] { byteSlice }));

            // CanBackquote(s string) bool
            pkg.AddExport(new FunctionSymbol("CanBackquote",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.Bool }));

            // IsPrint(r rune) bool
            pkg.AddExport(new FunctionSymbol("IsPrint",
                new[] { new ParameterSymbol("r", BuiltinTypes.Rune, 0) },
                new[] { BuiltinTypes.Bool }));

            // QuoteRune(r rune) string
            pkg.AddExport(new FunctionSymbol("QuoteRune",
                new[] { new ParameterSymbol("r", BuiltinTypes.Rune, 0) },
                new[] { BuiltinTypes.String }));

            // QuoteToASCII(s string) string
            pkg.AddExport(new FunctionSymbol("QuoteToASCII",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.String }));

            // QuoteRuneToASCII(r rune) string
            pkg.AddExport(new FunctionSymbol("QuoteRuneToASCII",
                new[] { new ParameterSymbol("r", BuiltinTypes.Rune, 0) },
                new[] { BuiltinTypes.String }));

            // AppendFloat(dst []byte, f float64, fmt byte, prec int, bitSize int) []byte
            pkg.AddExport(new FunctionSymbol("AppendFloat",
                new[] { new ParameterSymbol("dst", byteSlice, 0),
                        new ParameterSymbol("f", BuiltinTypes.Float64, 1),
                        new ParameterSymbol("fmt", BuiltinTypes.Uint8, 2),
                        new ParameterSymbol("prec", BuiltinTypes.Int, 3),
                        new ParameterSymbol("bitSize", BuiltinTypes.Int, 4) },
                new[] { byteSlice }));

            // AppendQuoteToASCII(dst []byte, s string) []byte
            pkg.AddExport(new FunctionSymbol("AppendQuoteToASCII",
                new[] { new ParameterSymbol("dst", byteSlice, 0),
                        new ParameterSymbol("s", BuiltinTypes.String, 1) },
                new[] { byteSlice }));

            // NumError type — struct with Func, Num string and Err error fields
            var numErrStruct = new StructTypeSymbol("NumError", new[]
            {
                new FieldSymbol("Func", BuiltinTypes.String, 0),
                new FieldSymbol("Num", BuiltinTypes.String, 1),
                new FieldSymbol("Err", BuiltinTypes.Error, 2),
            });
            numErrStruct.AddMethod(new MethodSymbol("Error", numErrStruct, false,
                System.Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            numErrStruct.AddMethod(new MethodSymbol("Unwrap", numErrStruct, false,
                System.Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            pkg.AddExport(numErrStruct);

            // ErrRange — sentinel error
            pkg.AddExport(new PackageVarSymbol("ErrRange", BuiltinTypes.Error,
                typeof(object), "ErrRange"));

            // ErrSyntax — sentinel error
            pkg.AddExport(new PackageVarSymbol("ErrSyntax", BuiltinTypes.Error,
                typeof(object), "ErrSyntax"));

            // IntSize — int constant
            pkg.AddExport(new ConstantSymbol("IntSize", BuiltinTypes.Int, (long)64));

            return pkg;
        }

        private static PackageSymbol CreateStringsPackage()
        {
            var pkg = new PackageSymbol("strings", "strings");

            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;

            // strings.Compare(a, b string) int
            pkg.AddExport(new FunctionSymbol("Compare",
                new[] { P("a", s, 0), P("b", s, 1) }, new[] { i }));

            // strings.Contains(s, substr string) bool
            pkg.AddExport(new FunctionSymbol("Contains",
                new[] { P("s", s, 0), P("substr", s, 1) }, new[] { b }));

            // strings.HasPrefix(s, prefix string) bool
            pkg.AddExport(new FunctionSymbol("HasPrefix",
                new[] { P("s", s, 0), P("prefix", s, 1) }, new[] { b }));

            // strings.HasSuffix(s, suffix string) bool
            pkg.AddExport(new FunctionSymbol("HasSuffix",
                new[] { P("s", s, 0), P("suffix", s, 1) }, new[] { b }));

            // strings.Join(elems []string, sep string) string
            var sliceString = new SliceTypeSymbol(s);
            pkg.AddExport(new FunctionSymbol("Join",
                new[] { new ParameterSymbol("elems", sliceString, 0), P("sep", s, 1) }, new[] { s }));

            // strings.Split(s, sep string) []string
            pkg.AddExport(new FunctionSymbol("Split",
                new[] { P("s", s, 0), P("sep", s, 1) }, new TypeSymbol[] { sliceString }));

            // strings.Replace(s, old, new string, n int) string
            pkg.AddExport(new FunctionSymbol("Replace",
                new[] { P("s", s, 0), P("old", s, 1), P("new_", s, 2), P("n", i, 3) }, new[] { s }));

            // strings.TrimSpace(s string) string
            pkg.AddExport(new FunctionSymbol("TrimSpace",
                new[] { P("s", s, 0) }, new[] { s }));

            // strings.ToUpper(s string) string
            pkg.AddExport(new FunctionSymbol("ToUpper",
                new[] { P("s", s, 0) }, new[] { s }));

            // strings.ToLower(s string) string
            pkg.AddExport(new FunctionSymbol("ToLower",
                new[] { P("s", s, 0) }, new[] { s }));

            // strings.Index(s, substr string) int
            pkg.AddExport(new FunctionSymbol("Index",
                new[] { P("s", s, 0), P("substr", s, 1) }, new[] { i }));

            // strings.Repeat(s string, count int) string
            pkg.AddExport(new FunctionSymbol("Repeat",
                new[] { P("s", s, 0), P("count", i, 1) }, new[] { s }));

            // strings.ReplaceAll(s, old, new string) string
            pkg.AddExport(new FunctionSymbol("ReplaceAll",
                new[] { P("s", s, 0), P("old", s, 1), P("new_", s, 2) }, new[] { s }));

            // strings.Trim(s, cutset string) string
            pkg.AddExport(new FunctionSymbol("Trim",
                new[] { P("s", s, 0), P("cutset", s, 1) }, new[] { s }));

            // strings.TrimPrefix(s, prefix string) string
            pkg.AddExport(new FunctionSymbol("TrimPrefix",
                new[] { P("s", s, 0), P("prefix", s, 1) }, new[] { s }));

            // strings.TrimSuffix(s, suffix string) string
            pkg.AddExport(new FunctionSymbol("TrimSuffix",
                new[] { P("s", s, 0), P("suffix", s, 1) }, new[] { s }));

            // strings.TrimLeft(s, cutset string) string
            pkg.AddExport(new FunctionSymbol("TrimLeft",
                new[] { P("s", s, 0), P("cutset", s, 1) }, new[] { s }));

            // strings.TrimRight(s, cutset string) string
            pkg.AddExport(new FunctionSymbol("TrimRight",
                new[] { P("s", s, 0), P("cutset", s, 1) }, new[] { s }));

            // strings.Count(s, substr string) int
            pkg.AddExport(new FunctionSymbol("Count",
                new[] { P("s", s, 0), P("substr", s, 1) }, new[] { i }));

            // strings.EqualFold(s, t string) bool
            pkg.AddExport(new FunctionSymbol("EqualFold",
                new[] { P("s", s, 0), P("t", s, 1) }, new[] { b }));

            // strings.Fields(s string) []string
            pkg.AddExport(new FunctionSymbol("Fields",
                new[] { P("s", s, 0) }, new TypeSymbol[] { sliceString }));

            // strings.LastIndex(s, substr string) int
            pkg.AddExport(new FunctionSymbol("LastIndex",
                new[] { P("s", s, 0), P("substr", s, 1) }, new[] { i }));

            // strings.LastIndexByte(s string, c byte) int
            pkg.AddExport(new FunctionSymbol("LastIndexByte",
                new[] { P("s", s, 0), P("c", BuiltinTypes.Uint8, 1) }, new[] { i }));

            // strings.ContainsRune(s string, r rune) bool
            pkg.AddExport(new FunctionSymbol("ContainsRune",
                new[] { P("s", s, 0), P("r", BuiltinTypes.Rune, 1) }, new[] { b }));

            // strings.ContainsAny(s, chars string) bool
            pkg.AddExport(new FunctionSymbol("ContainsAny",
                new[] { P("s", s, 0), P("chars", s, 1) }, new[] { b }));

            // strings.NewReader(s string) *strings.Reader (returns io.Reader)
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { P("s", s, 0) }, new TypeSymbol[] { emptyIface }));

            // strings.Cut(s, sep string) (before, after string, found bool)
            pkg.AddExport(new FunctionSymbol("Cut",
                new[] { P("s", s, 0), P("sep", s, 1) },
                new TypeSymbol[] { s, s, b }));

            // strings.SplitN(s, sep string, n int) []string
            pkg.AddExport(new FunctionSymbol("SplitN",
                new[] { P("s", s, 0), P("sep", s, 1), P("n", i, 2) },
                new TypeSymbol[] { sliceString }));

            // strings.SplitAfter(s, sep string) []string
            pkg.AddExport(new FunctionSymbol("SplitAfter",
                new[] { P("s", s, 0), P("sep", s, 1) },
                new TypeSymbol[] { sliceString }));

            // strings.SplitAfterN(s, sep string, n int) []string
            pkg.AddExport(new FunctionSymbol("SplitAfterN",
                new[] { P("s", s, 0), P("sep", s, 1), P("n", i, 2) },
                new TypeSymbol[] { sliceString }));

            // strings.Title(s string) string
            pkg.AddExport(new FunctionSymbol("Title",
                new[] { P("s", s, 0) }, new[] { s }));

            // strings.ToTitle(s string) string
            pkg.AddExport(new FunctionSymbol("ToTitle",
                new[] { P("s", s, 0) }, new[] { s }));

            // strings.IndexByte(s string, c byte) int
            pkg.AddExport(new FunctionSymbol("IndexByte",
                new[] { P("s", s, 0), P("c", BuiltinTypes.Uint8, 1) }, new[] { i }));

            // strings.IndexRune(s string, r rune) int
            pkg.AddExport(new FunctionSymbol("IndexRune",
                new[] { P("s", s, 0), P("r", BuiltinTypes.Rune, 1) }, new[] { i }));

            // strings.IndexAny(s, chars string) int
            pkg.AddExport(new FunctionSymbol("IndexAny",
                new[] { P("s", s, 0), P("chars", s, 1) }, new[] { i }));

            // strings.TrimFunc(s string, f func(rune) bool) string
            var trimFuncType = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.Rune },
                new TypeSymbol[] { b });
            pkg.AddExport(new FunctionSymbol("TrimFunc",
                new[] { P("s", s, 0), new ParameterSymbol("f", trimFuncType, 1) }, new[] { s }));

            // strings.TrimRightFunc(s string, f func(rune) bool) string
            pkg.AddExport(new FunctionSymbol("TrimRightFunc",
                new[] { P("s", s, 0), new ParameterSymbol("f", trimFuncType, 1) }, new[] { s }));

            // strings.TrimLeftFunc(s string, f func(rune) bool) string
            pkg.AddExport(new FunctionSymbol("TrimLeftFunc",
                new[] { P("s", s, 0), new ParameterSymbol("f", trimFuncType, 1) }, new[] { s }));

            // strings.IndexFunc(s string, f func(rune) bool) int
            pkg.AddExport(new FunctionSymbol("IndexFunc",
                new[] { P("s", s, 0), new ParameterSymbol("f", trimFuncType, 1) }, new[] { i }));

            // strings.LastIndexFunc(s string, f func(rune) bool) int
            pkg.AddExport(new FunctionSymbol("LastIndexFunc",
                new[] { P("s", s, 0), new ParameterSymbol("f", trimFuncType, 1) }, new[] { i }));

            // strings.FieldsFunc(s string, f func(rune) bool) []string
            pkg.AddExport(new FunctionSymbol("FieldsFunc",
                new[] { P("s", s, 0), new ParameterSymbol("f", trimFuncType, 1) }, new[] { sliceString }));

            // strings.Map(mapping func(rune) rune, s string) string
            var mapFuncType = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.Rune },
                new TypeSymbol[] { BuiltinTypes.Rune });
            pkg.AddExport(new FunctionSymbol("Map",
                new[] { new ParameterSymbol("mapping", mapFuncType, 0), P("s", s, 1) },
                new[] { s }));

            // strings.NewReplacer(oldnew ...string) *Replacer
            var replacerType = new StructTypeSymbol("Replacer", Array.Empty<FieldSymbol>());
            replacerType.AddMethod(new MethodSymbol("Replace", replacerType, false,
                new[] { P("s", s, 0) }, s));
            replacerType.AddMethod(new MethodSymbol("WriteString", replacerType, false,
                new[] { P("w", new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>()), 0), P("s", s, 1) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            pkg.AddExport(replacerType);
            pkg.AddExport(new FunctionSymbol("NewReplacer",
                Array.Empty<ParameterSymbol>(), new[] { replacerType },
                isVariadic: true));

            // strings.Builder type
            var builderType = new StructTypeSymbol("Builder", Array.Empty<FieldSymbol>());
            builderType.AddMethod(new MethodSymbol("WriteString", builderType, false,
                new[] { P("s", s, 0) }, BuiltinTypes.Void));
            builderType.AddMethod(new MethodSymbol("WriteByte", builderType, false,
                new[] { P("c", BuiltinTypes.Uint8, 0) }, BuiltinTypes.Void));
            builderType.AddMethod(new MethodSymbol("WriteRune", builderType, false,
                new[] { P("r", BuiltinTypes.Rune, 0) }, BuiltinTypes.Void));
            builderType.AddMethod(new MethodSymbol("Reset", builderType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            builderType.AddMethod(new MethodSymbol("Len", builderType, false,
                Array.Empty<ParameterSymbol>(), i));
            builderType.AddMethod(new MethodSymbol("String", builderType, false,
                Array.Empty<ParameterSymbol>(), s));
            builderType.AddMethod(new MethodSymbol("Grow", builderType, false,
                new[] { P("n", i, 0) }, BuiltinTypes.Void));
            builderType.AddMethod(new MethodSymbol("Cap", builderType, false,
                Array.Empty<ParameterSymbol>(), i));
            builderType.AddMethod(new MethodSymbol("Write", builderType, false,
                new[] { P("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            pkg.AddExport(builderType);

            return pkg;
        }

        private static PackageSymbol CreateErrorsPackage()
        {
            var pkg = new PackageSymbol("errors", "errors");

            // errors.New(text string) error
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("text", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.Error }, packageName: "errors"));

            // errors.Unwrap(err error) error
            pkg.AddExport(new FunctionSymbol("Unwrap",
                new[] { P("err", BuiltinTypes.Error, 0) },
                new[] { BuiltinTypes.Error }, packageName: "errors"));

            // errors.Is(err, target error) bool
            pkg.AddExport(new FunctionSymbol("Is",
                new[] { P("err", BuiltinTypes.EmptyInterface, 0), P("target", BuiltinTypes.EmptyInterface, 1) },
                new[] { BuiltinTypes.Bool }, packageName: "errors"));

            // errors.As(err error, target interface{}) bool
            pkg.AddExport(new FunctionSymbol("As",
                new[] { P("err", BuiltinTypes.EmptyInterface, 0), P("target", BuiltinTypes.EmptyInterface, 1) },
                new[] { BuiltinTypes.Bool }, packageName: "errors"));

            // errors.Join(errs ...error) error
            pkg.AddExport(new FunctionSymbol("Join",
                new[] { new ParameterSymbol("errs", new SliceTypeSymbol(BuiltinTypes.Error), 0) },
                new[] { BuiltinTypes.Error }, isVariadic: true, packageName: "errors"));

            return pkg;
        }

        private static PackageSymbol CreateMathPackage()
        {
            var pkg = new PackageSymbol("math", "math");

            var f = BuiltinTypes.Float64;
            var i = BuiltinTypes.Int;

            pkg.AddExport(new FunctionSymbol("Abs",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Max",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Min",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Sqrt",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Floor",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Ceil",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Round",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Pow",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Log",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Log2",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Log10",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Exp",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Mod",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Sin",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Cos",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Tan",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Atan",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Atan2",
                new[] { P("y", f, 0), P("x", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Inf",
                new[] { P("sign", i, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("IsNaN",
                new[] { P("x", f, 0) }, new[] { BuiltinTypes.Bool }));
            pkg.AddExport(new FunctionSymbol("IsInf",
                new[] { P("x", f, 0), P("sign", i, 1) }, new[] { BuiltinTypes.Bool }));
            pkg.AddExport(new FunctionSymbol("NaN",
                Array.Empty<ParameterSymbol>(), new[] { f }));
            pkg.AddExport(new FunctionSymbol("Remainder",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Trunc",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Pow10",
                new[] { P("n", i, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Asin",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Acos",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Sinh",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Cosh",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Tanh",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Cbrt",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Hypot",
                new[] { P("p", f, 0), P("q", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Dim",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Copysign",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Ldexp",
                new[] { P("frac", f, 0), P("exp", i, 1) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Logb",
                new[] { P("x", f, 0) }, new[] { f }));
            pkg.AddExport(new FunctionSymbol("Ilogb",
                new[] { P("x", f, 0) }, new[] { i }));
            pkg.AddExport(new FunctionSymbol("Modf",
                new[] { P("f", f, 0) }, new[] { f, f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Frexp",
                new[] { P("f", f, 0) }, new TypeSymbol[] { f, i }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Mod",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Remainder",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Signbit",
                new[] { P("x", f, 0) }, new TypeSymbol[] { BuiltinTypes.Bool }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("IsInf",
                new[] { P("f", f, 0), P("sign", i, 1) }, new TypeSymbol[] { BuiltinTypes.Bool }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("IsNaN",
                new[] { P("f", f, 0) }, new TypeSymbol[] { BuiltinTypes.Bool }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Inf",
                new[] { P("sign", i, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("NaN",
                Array.Empty<ParameterSymbol>(), new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Round",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("RoundToEven",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Trunc",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Erf",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Erfc",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Gamma",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Lgamma",
                new[] { P("x", f, 0) }, new TypeSymbol[] { f, i }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("J0",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("J1",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Y0",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Y1",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Jn",
                new[] { P("n", i, 0), P("x", f, 1) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Yn",
                new[] { P("n", i, 0), P("x", f, 1) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Log1p",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Expm1",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Atanh",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Asinh",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Acosh",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Log2",
                new[] { P("x", f, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("FMA",
                new[] { P("x", f, 0), P("y", f, 1), P("z", f, 2) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Float64bits",
                new[] { P("f", f, 0) }, new TypeSymbol[] { BuiltinTypes.Uint64 }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Float64frombits",
                new[] { P("b", BuiltinTypes.Uint64, 0) }, new[] { f }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Float32bits",
                new[] { P("f", BuiltinTypes.Float32, 0) }, new TypeSymbol[] { BuiltinTypes.Uint32 }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Float32frombits",
                new[] { P("b", BuiltinTypes.Uint32, 0) }, new[] { BuiltinTypes.Float32 }, packageName: "math"));

            // math constants as package variables (untyped for numeric constant assignability)
            var ui = BuiltinTypes.UntypedInt;
            var uf = BuiltinTypes.UntypedFloat;
            pkg.AddExport(new PackageVarSymbol("Pi", uf, typeof(GoMath), "Pi"));
            pkg.AddExport(new PackageVarSymbol("E", uf, typeof(GoMath), "E"));
            pkg.AddExport(new PackageVarSymbol("MaxFloat64", uf, typeof(GoMath), "MaxFloat64"));
            pkg.AddExport(new PackageVarSymbol("SmallestNonzeroFloat64", uf, typeof(GoMath), "SmallestNonzeroFloat64"));
            pkg.AddExport(new PackageVarSymbol("MaxInt", ui, typeof(GoMath), "MaxInt"));
            pkg.AddExport(new PackageVarSymbol("MinInt", ui, typeof(GoMath), "MinInt"));
            pkg.AddExport(new PackageVarSymbol("MaxInt8", ui, typeof(GoMath), "MaxInt8"));
            pkg.AddExport(new PackageVarSymbol("MinInt8", ui, typeof(GoMath), "MinInt8"));
            pkg.AddExport(new PackageVarSymbol("MaxInt16", ui, typeof(GoMath), "MaxInt16"));
            pkg.AddExport(new PackageVarSymbol("MinInt16", ui, typeof(GoMath), "MinInt16"));
            pkg.AddExport(new PackageVarSymbol("MaxInt32", ui, typeof(GoMath), "MaxInt32"));
            pkg.AddExport(new PackageVarSymbol("MinInt32", ui, typeof(GoMath), "MinInt32"));
            pkg.AddExport(new PackageVarSymbol("MaxInt64", ui, typeof(GoMath), "MaxInt64"));
            pkg.AddExport(new PackageVarSymbol("MinInt64", ui, typeof(GoMath), "MinInt64"));
            pkg.AddExport(new PackageVarSymbol("MaxUint8", ui, typeof(GoMath), "MaxUint8"));
            pkg.AddExport(new PackageVarSymbol("MaxUint16", ui, typeof(GoMath), "MaxUint16"));
            pkg.AddExport(new PackageVarSymbol("MaxUint32", ui, typeof(GoMath), "MaxUint32"));
            pkg.AddExport(new PackageVarSymbol("MaxUint64", ui, typeof(GoMath), "MaxUint64"));
            pkg.AddExport(new PackageVarSymbol("MaxFloat32", uf, typeof(GoMath), "MaxFloat32"));
            pkg.AddExport(new PackageVarSymbol("Phi", uf, typeof(GoMath), "Phi"));
            pkg.AddExport(new PackageVarSymbol("Sqrt2", uf, typeof(GoMath), "Sqrt2"));
            pkg.AddExport(new PackageVarSymbol("SqrtE", uf, typeof(GoMath), "SqrtE"));
            pkg.AddExport(new PackageVarSymbol("SqrtPi", uf, typeof(GoMath), "SqrtPi"));
            pkg.AddExport(new PackageVarSymbol("SqrtPhi", uf, typeof(GoMath), "SqrtPhi"));
            pkg.AddExport(new PackageVarSymbol("Ln2", uf, typeof(GoMath), "Ln2"));
            pkg.AddExport(new PackageVarSymbol("Log2E", uf, typeof(GoMath), "Log2E"));
            pkg.AddExport(new PackageVarSymbol("Ln10", uf, typeof(GoMath), "Ln10"));
            pkg.AddExport(new PackageVarSymbol("Log10E", uf, typeof(GoMath), "Log10E"));

            // math.Float32bits(f float32) uint32
            var f32 = BuiltinTypes.Float32;
            var u32 = BuiltinTypes.Uint32;
            var u64 = BuiltinTypes.Uint64;
            pkg.AddExport(new FunctionSymbol("Float32bits",
                new[] { P("f", f32, 0) }, new TypeSymbol[] { u32 }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Float32frombits",
                new[] { P("b", u32, 0) }, new TypeSymbol[] { f32 }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Float64bits",
                new[] { P("f", f, 0) }, new TypeSymbol[] { u64 }, packageName: "math"));
            pkg.AddExport(new FunctionSymbol("Float64frombits",
                new[] { P("b", u64, 0) }, new TypeSymbol[] { f }, packageName: "math"));

            return pkg;
        }

        private static PackageSymbol CreateSyncPackage()
        {
            var pkg = new PackageSymbol("sync", "sync");

            // sync.WaitGroup
            var waitGroupType = new StructTypeSymbol("WaitGroup", Array.Empty<FieldSymbol>());
            waitGroupType.AddMethod(new MethodSymbol("Add", waitGroupType, false,
                new[] { new ParameterSymbol("delta", BuiltinTypes.Int, 0) },
                BuiltinTypes.Void));
            waitGroupType.AddMethod(new MethodSymbol("Done", waitGroupType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            waitGroupType.AddMethod(new MethodSymbol("Wait", waitGroupType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            pkg.AddExport(waitGroupType);

            // sync.Mutex
            var mutexType = new StructTypeSymbol("Mutex", Array.Empty<FieldSymbol>());
            mutexType.AddMethod(new MethodSymbol("Lock", mutexType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            mutexType.AddMethod(new MethodSymbol("Unlock", mutexType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            pkg.AddExport(mutexType);

            // sync.Once
            var onceType = new StructTypeSymbol("Once", Array.Empty<FieldSymbol>());
            var funcType = new FunctionTypeSymbol(Array.Empty<TypeSymbol>(), Array.Empty<TypeSymbol>());
            onceType.AddMethod(new MethodSymbol("Do", onceType, false,
                new[] { new ParameterSymbol("f", funcType, 0) }, BuiltinTypes.Void));
            pkg.AddExport(onceType);

            // sync.RWMutex
            var rwMutexType = new StructTypeSymbol("RWMutex", Array.Empty<FieldSymbol>());
            rwMutexType.AddMethod(new MethodSymbol("RLock", rwMutexType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            rwMutexType.AddMethod(new MethodSymbol("RUnlock", rwMutexType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            rwMutexType.AddMethod(new MethodSymbol("Lock", rwMutexType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            rwMutexType.AddMethod(new MethodSymbol("Unlock", rwMutexType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            pkg.AddExport(rwMutexType);

            // sync.Map
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var syncMapType = new StructTypeSymbol("Map", Array.Empty<FieldSymbol>());
            syncMapType.AddMethod(new MethodSymbol("Store", syncMapType, false,
                new[] { new ParameterSymbol("key", emptyIface, 0),
                        new ParameterSymbol("value", emptyIface, 1) },
                BuiltinTypes.Void));
            syncMapType.AddMethod(new MethodSymbol("Load", syncMapType, false,
                new[] { new ParameterSymbol("key", emptyIface, 0) },
                new TypeSymbol[] { emptyIface, BuiltinTypes.Bool }));
            syncMapType.AddMethod(new MethodSymbol("Delete", syncMapType, false,
                new[] { new ParameterSymbol("key", emptyIface, 0) },
                BuiltinTypes.Void));
            syncMapType.AddMethod(new MethodSymbol("LoadOrStore", syncMapType, false,
                new[] { new ParameterSymbol("key", emptyIface, 0),
                        new ParameterSymbol("value", emptyIface, 1) },
                new TypeSymbol[] { emptyIface, BuiltinTypes.Bool }));
            syncMapType.AddMethod(new MethodSymbol("LoadAndDelete", syncMapType, false,
                new[] { new ParameterSymbol("key", emptyIface, 0) },
                new TypeSymbol[] { emptyIface, BuiltinTypes.Bool }));
            // Range(f func(key, value any) bool)
            var rangeFuncType = new FunctionTypeSymbol(
                new TypeSymbol[] { emptyIface, emptyIface },
                new TypeSymbol[] { BuiltinTypes.Bool });
            syncMapType.AddMethod(new MethodSymbol("Range", syncMapType, false,
                new[] { new ParameterSymbol("f", rangeFuncType, 0) },
                BuiltinTypes.Void));
            pkg.AddExport(syncMapType);

            // sync.Pool
            var poolType = new StructTypeSymbol("Pool", new[]
            {
                new FieldSymbol("New", new FunctionTypeSymbol(
                    Array.Empty<TypeSymbol>(),
                    new TypeSymbol[] { emptyIface }), 0),
            });
            poolType.AddMethod(new MethodSymbol("Get", poolType, false,
                Array.Empty<ParameterSymbol>(), emptyIface));
            poolType.AddMethod(new MethodSymbol("Put", poolType, false,
                new[] { new ParameterSymbol("x", emptyIface, 0) },
                BuiltinTypes.Void));
            pkg.AddExport(poolType);

            // sync.Locker interface
            var lockerIface = new InterfaceTypeSymbol("Locker", new[]
            {
                new MethodSymbol("Lock", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void),
                new MethodSymbol("Unlock", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void),
            });
            pkg.AddExport(lockerIface);

            // sync.Cond
            var condType = new StructTypeSymbol("Cond", new[]
            {
                new FieldSymbol("L", lockerIface, 0),
            });
            condType.AddMethod(new MethodSymbol("Wait", condType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            condType.AddMethod(new MethodSymbol("Signal", condType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            condType.AddMethod(new MethodSymbol("Broadcast", condType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            pkg.AddExport(condType);

            // sync.NewCond(l Locker) *Cond
            pkg.AddExport(new FunctionSymbol("NewCond",
                new[] { new ParameterSymbol("l", lockerIface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(condType) }, packageName: "sync"));

            return pkg;
        }

        private static PackageSymbol CreateOsPackage()
        {
            var pkg = new PackageSymbol("os", "os");

            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var osErr = BuiltinTypes.Error;

            // File type with methods
            var fileType = new StructTypeSymbol("File", Array.Empty<FieldSymbol>());
            fileType.AddMethod(new MethodSymbol("Close", fileType, false,
                Array.Empty<ParameterSymbol>(), osErr));
            fileType.AddMethod(new MethodSymbol("Name", fileType, false,
                Array.Empty<ParameterSymbol>(), s));
            fileType.AddMethod(new MethodSymbol("Write", fileType, false,
                new[] { P("b", byteSlice, 0) },
                new TypeSymbol[] { i, osErr }));
            fileType.AddMethod(new MethodSymbol("Read", fileType, false,
                new[] { P("b", byteSlice, 0) },
                new TypeSymbol[] { i, osErr }));
            fileType.AddMethod(new MethodSymbol("WriteString", fileType, false,
                new[] { P("s", s, 0) },
                new TypeSymbol[] { i, osErr }));
            fileType.AddMethod(new MethodSymbol("ReadAt", fileType, false,
                new[] { P("b", byteSlice, 0), P("off", i, 1) },
                new TypeSymbol[] { i, osErr }));
            fileType.AddMethod(new MethodSymbol("WriteAt", fileType, false,
                new[] { P("b", byteSlice, 0), P("off", i, 1) },
                new TypeSymbol[] { i, osErr }));
            fileType.AddMethod(new MethodSymbol("Seek", fileType, false,
                new[] { P("offset", i, 0), P("whence", i, 1) },
                new TypeSymbol[] { i, osErr }));
            fileType.AddMethod(new MethodSymbol("Sync", fileType, false,
                Array.Empty<ParameterSymbol>(), osErr));
            fileType.AddMethod(new MethodSymbol("Truncate", fileType, false,
                new[] { P("size", i, 0) }, osErr));
            fileType.AddMethod(new MethodSymbol("Fd", fileType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uintptr));

            // os.FileMode — named type backed by uint32
            var fileModeType = new TypeSymbol("FileMode", TypeKind.Uint32, BuiltinTypes.Uint32);
            fileModeType.AddMethod(new MethodSymbol("IsDir", fileModeType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            fileModeType.AddMethod(new MethodSymbol("IsRegular", fileModeType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            fileModeType.AddMethod(new MethodSymbol("Perm", fileModeType, false,
                Array.Empty<ParameterSymbol>(), fileModeType));
            fileModeType.AddMethod(new MethodSymbol("String", fileModeType, false,
                Array.Empty<ParameterSymbol>(), s));
            fileModeType.AddMethod(new MethodSymbol("Type", fileModeType, false,
                Array.Empty<ParameterSymbol>(), fileModeType));
            pkg.AddExport(fileModeType);

            fileType.AddMethod(new MethodSymbol("Chmod", fileType, false,
                new[] { P("mode", fileModeType, 0) }, osErr));
            pkg.AddExport(fileType);

            // os.Exit(code int)
            pkg.AddExport(new FunctionSymbol("Exit",
                new[] { P("code", i, 0) },
                Array.Empty<TypeSymbol>(), packageName: "os"));

            // os.Getenv(key string) string
            pkg.AddExport(new FunctionSymbol("Getenv",
                new[] { P("key", s, 0) },
                new[] { s }, packageName: "os"));

            // os.Setenv(key, value string)
            pkg.AddExport(new FunctionSymbol("Setenv",
                new[] { P("key", s, 0), P("value", s, 1) },
                Array.Empty<TypeSymbol>(), packageName: "os"));

            // os.Unsetenv(key string) error
            pkg.AddExport(new FunctionSymbol("Unsetenv",
                new[] { P("key", s, 0) },
                new[] { BuiltinTypes.Error }, packageName: "os"));

            // os.Clearenv()
            pkg.AddExport(new FunctionSymbol("Clearenv",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "os"));

            var err = BuiltinTypes.Error;

            var ptrFileType = new PointerTypeSymbol(fileType);

            // os.Create(name string) (*File, error)
            pkg.AddExport(new FunctionSymbol("Create",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { ptrFileType, err }, packageName: "os"));

            // os.Open(name string) (*File, error)
            pkg.AddExport(new FunctionSymbol("Open",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { ptrFileType, err }, packageName: "os"));

            // os.ReadFile(name string) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("ReadFile",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { byteSlice, err }, packageName: "os"));

            // os.WriteFile(name string, data []byte, perm FileMode) error
            pkg.AddExport(new FunctionSymbol("WriteFile",
                new[] { P("name", s, 0), new ParameterSymbol("data", byteSlice, 1),
                        P("perm", fileModeType, 2) },
                new[] { err }, packageName: "os"));

            // os.Remove(name string) error
            pkg.AddExport(new FunctionSymbol("Remove",
                new[] { P("name", s, 0) },
                new[] { err }, packageName: "os"));

            // os.MkdirAll(path string, perm FileMode) error
            pkg.AddExport(new FunctionSymbol("MkdirAll",
                new[] { P("path", s, 0), P("perm", fileModeType, 1) },
                new[] { err }, packageName: "os"));

            // os.Getwd() (string, error)
            pkg.AddExport(new FunctionSymbol("Getwd",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s, err }, packageName: "os"));

            // os.Getpagesize() int
            pkg.AddExport(new FunctionSymbol("Getpagesize",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { i }, packageName: "os"));

            // os.Args []string
            pkg.AddExport(new PackageVarSymbol("Args",
                new SliceTypeSymbol(s),
                typeof(GoOs), "Args"));

            // os.Rename(oldpath, newpath string) error
            pkg.AddExport(new FunctionSymbol("Rename",
                new[] { P("oldpath", s, 0), P("newpath", s, 1) },
                new[] { err }, packageName: "os"));

            // os.FileInfo interface (same as fs.FileInfo)
            var fileInfoMethods = new List<MethodSymbol>();
            var fileInfoType = new InterfaceTypeSymbol("FileInfo", fileInfoMethods);
            fileInfoMethods.Add(new MethodSymbol("Name", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), s));
            fileInfoMethods.Add(new MethodSymbol("Size", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64));
            fileInfoMethods.Add(new MethodSymbol("IsDir", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            fileInfoMethods.Add(new MethodSymbol("Mode", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), fileModeType));
            fileInfoMethods.Add(new MethodSymbol("ModTime", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), i));
            fileInfoMethods.Add(new MethodSymbol("Sys", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.EmptyInterface));
            pkg.AddExport(fileInfoType);

            // File.Readdir(n int) ([]FileInfo, error) — add after fileInfoType is defined
            var fileInfoSlice = new SliceTypeSymbol(fileInfoType);
            fileType.AddMethod(new MethodSymbol("Readdir", fileType, false,
                new[] { P("n", i, 0) },
                new TypeSymbol[] { fileInfoSlice, err }));
            fileType.AddMethod(new MethodSymbol("Readdirnames", fileType, false,
                new[] { P("n", i, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(s), err }));

            pkg.AddExport(new FunctionSymbol("Stat",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { fileInfoType, err }, packageName: "os"));

            // os.Lstat(name string) (FileInfo, error)
            pkg.AddExport(new FunctionSymbol("Lstat",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { fileInfoType, err }, packageName: "os"));

            // os.TempDir() string
            pkg.AddExport(new FunctionSymbol("TempDir",
                Array.Empty<ParameterSymbol>(),
                new[] { s }, packageName: "os"));

            // os.UserHomeDir() (string, error)
            pkg.AddExport(new FunctionSymbol("UserHomeDir",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s, err }, packageName: "os"));

            // os.Stdin, os.Stdout, os.Stderr
            pkg.AddExport(new PackageVarSymbol("Stdin", ptrFileType, typeof(GoOs), "Stdin"));
            pkg.AddExport(new PackageVarSymbol("Stdout", ptrFileType, typeof(GoOs), "Stdout"));
            pkg.AddExport(new PackageVarSymbol("Stderr", ptrFileType, typeof(GoOs), "Stderr"));

            // os.LookupEnv(key string) (string, bool)
            pkg.AddExport(new FunctionSymbol("LookupEnv",
                new[] { P("key", s, 0) },
                new TypeSymbol[] { s, BuiltinTypes.Bool }, packageName: "os"));

            // os.Environ() []string
            pkg.AddExport(new FunctionSymbol("Environ",
                Array.Empty<ParameterSymbol>(),
                new[] { new SliceTypeSymbol(s) as TypeSymbol }, packageName: "os"));

            // os.IsNotExist(err error) bool
            pkg.AddExport(new FunctionSymbol("IsNotExist",
                new[] { P("err", err, 0) },
                new[] { BuiltinTypes.Bool }, packageName: "os"));

            // os.IsExist(err error) bool
            pkg.AddExport(new FunctionSymbol("IsExist",
                new[] { P("err", err, 0) },
                new[] { BuiltinTypes.Bool }, packageName: "os"));

            // os.IsPermission(err error) bool
            pkg.AddExport(new FunctionSymbol("IsPermission",
                new[] { P("err", err, 0) },
                new[] { BuiltinTypes.Bool }, packageName: "os"));

            // os.DirEntry type
            var dirEntryType = new StructTypeSymbol("DirEntry", Array.Empty<FieldSymbol>());
            dirEntryType.AddMethod(new MethodSymbol("Name", dirEntryType, false,
                Array.Empty<ParameterSymbol>(), s));
            dirEntryType.AddMethod(new MethodSymbol("IsDir", dirEntryType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            pkg.AddExport(dirEntryType);

            // os.ReadDir(name string) ([]DirEntry, error)
            pkg.AddExport(new FunctionSymbol("ReadDir",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(dirEntryType), err },
                packageName: "os"));

            // os.Chmod(name string, mode FileMode) error
            pkg.AddExport(new FunctionSymbol("Chmod",
                new[] { P("name", s, 0), P("mode", fileModeType, 1) },
                new[] { err }, packageName: "os"));

            // os.Getuid() int
            pkg.AddExport(new FunctionSymbol("Getuid",
                Array.Empty<ParameterSymbol>(),
                new[] { i }, packageName: "os"));

            // os.Getgid() int
            pkg.AddExport(new FunctionSymbol("Getgid",
                Array.Empty<ParameterSymbol>(),
                new[] { i }, packageName: "os"));

            // os.Getpid() int
            pkg.AddExport(new FunctionSymbol("Getpid",
                Array.Empty<ParameterSymbol>(),
                new[] { i }, packageName: "os"));

            // os.Symlink(oldname, newname string) error
            pkg.AddExport(new FunctionSymbol("Symlink",
                new[] { P("oldname", s, 0), P("newname", s, 1) },
                new[] { err }, packageName: "os"));

            // os.RemoveAll(path string) error
            pkg.AddExport(new FunctionSymbol("RemoveAll",
                new[] { P("path", s, 0) },
                new[] { err }, packageName: "os"));

            // os.Link(oldname, newname string) error
            pkg.AddExport(new FunctionSymbol("Link",
                new[] { P("oldname", s, 0), P("newname", s, 1) },
                new[] { err }, packageName: "os"));

            // os.OpenFile(name string, flag int, perm FileMode) (*File, error)
            pkg.AddExport(new FunctionSymbol("OpenFile",
                new[] { P("name", s, 0), P("flag", i, 1), P("perm", fileModeType, 2) },
                new TypeSymbol[] { ptrFileType, err }, packageName: "os"));

            // os.Chown(name string, uid, gid int) error
            pkg.AddExport(new FunctionSymbol("Chown",
                new[] { P("name", s, 0), P("uid", i, 1), P("gid", i, 2) },
                new[] { err }, packageName: "os"));

            // os.Hostname() (string, error)
            pkg.AddExport(new FunctionSymbol("Hostname",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s, err }, packageName: "os"));

            // os.MkdirTemp(dir, pattern string) (string, error)
            pkg.AddExport(new FunctionSymbol("MkdirTemp",
                new[] { P("dir", s, 0), P("pattern", s, 1) },
                new TypeSymbol[] { s, err }, packageName: "os"));

            // os.CreateTemp(dir, pattern string) (*File, error)
            pkg.AddExport(new FunctionSymbol("CreateTemp",
                new[] { P("dir", s, 0), P("pattern", s, 1) },
                new TypeSymbol[] { ptrFileType, err }, packageName: "os"));

            // os.Readlink(name string) (string, error)
            pkg.AddExport(new FunctionSymbol("Readlink",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { s, err }, packageName: "os"));

            // os.Executable() (string, error)
            pkg.AddExport(new FunctionSymbol("Executable",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s, err }, packageName: "os"));

            // File flag constants
            pkg.AddExport(new ConstantSymbol("O_RDONLY", i, (long)0));
            pkg.AddExport(new ConstantSymbol("O_WRONLY", i, (long)1));
            pkg.AddExport(new ConstantSymbol("O_RDWR", i, (long)2));
            pkg.AddExport(new ConstantSymbol("O_APPEND", i, (long)0x400));
            pkg.AddExport(new ConstantSymbol("O_CREATE", i, (long)0x40));
            pkg.AddExport(new ConstantSymbol("O_EXCL", i, (long)0x80));
            pkg.AddExport(new ConstantSymbol("O_SYNC", i, (long)0x101000));
            pkg.AddExport(new ConstantSymbol("O_TRUNC", i, (long)0x200));

            // os.PathSeparator, os.PathListSeparator
            pkg.AddExport(new ConstantSymbol("PathSeparator", BuiltinTypes.UntypedRune, (long)'/'));
            pkg.AddExport(new ConstantSymbol("PathListSeparator", BuiltinTypes.UntypedRune, (long)':'));

            // os.ErrNotExist, os.ErrExist, os.ErrPermission, os.ErrClosed
            pkg.AddExport(new PackageVarSymbol("ErrNotExist", err, typeof(object), "ErrNotExist"));
            pkg.AddExport(new PackageVarSymbol("ErrExist", err, typeof(object), "ErrExist"));
            pkg.AddExport(new PackageVarSymbol("ErrPermission", err, typeof(object), "ErrPermission"));
            pkg.AddExport(new PackageVarSymbol("ErrClosed", err, typeof(object), "ErrClosed"));
            pkg.AddExport(new PackageVarSymbol("ErrInvalid", err, typeof(object), "ErrInvalid"));

            // os.DevNull
            pkg.AddExport(new ConstantSymbol("DevNull", s, "/dev/null"));

            // os.Signal interface
            var signalIface = new InterfaceTypeSymbol("Signal", new[]
            {
                new MethodSymbol("Signal", null!, false, Array.Empty<ParameterSymbol>(), BuiltinTypes.Void),
                new MethodSymbol("String", null!, false, Array.Empty<ParameterSymbol>(), s),
            });
            pkg.AddExport(signalIface);

            // os.Interrupt, os.Kill signals
            pkg.AddExport(new PackageVarSymbol("Interrupt", signalIface, typeof(object), "Interrupt"));
            pkg.AddExport(new PackageVarSymbol("Kill", signalIface, typeof(object), "Kill"));

            // os.Process type
            var processType = new StructTypeSymbol("Process", new[]
            {
                new FieldSymbol("Pid", i, 0),
            });
            processType.AddMethod(new MethodSymbol("Kill", processType, false,
                Array.Empty<ParameterSymbol>(), err));
            processType.AddMethod(new MethodSymbol("Signal", processType, false,
                new[] { P("sig", signalIface, 0) }, err));
            processType.AddMethod(new MethodSymbol("Wait", processType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new PointerTypeSymbol(new StructTypeSymbol("ProcessState", Array.Empty<FieldSymbol>())), err }));
            pkg.AddExport(processType);

            // os.FindProcess(pid int) (*Process, error)
            var ptrProcessType = new PointerTypeSymbol(processType);
            pkg.AddExport(new FunctionSymbol("FindProcess",
                new[] { P("pid", i, 0) },
                new TypeSymbol[] { ptrProcessType, err }, packageName: "os"));

            // Expand(s string, mapping func(string) string) string
            var expandFunc = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.String },
                new TypeSymbol[] { BuiltinTypes.String });
            pkg.AddExport(new FunctionSymbol("Expand",
                new[] { P("s", BuiltinTypes.String, 0), new ParameterSymbol("mapping", expandFunc, 1) },
                new[] { BuiltinTypes.String }, packageName: "os"));

            // ExpandEnv(s string) string
            pkg.AddExport(new FunctionSymbol("ExpandEnv",
                new[] { P("s", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.String }, packageName: "os"));

            // os.PathError struct
            var pathError = new StructTypeSymbol("PathError", new[]
            {
                new FieldSymbol("Op", s, 0),
                new FieldSymbol("Path", s, 1),
                new FieldSymbol("Err", err, 2),
            });
            pathError.AddMethod(new MethodSymbol("Error", pathError, false,
                Array.Empty<ParameterSymbol>(), s));
            pathError.AddMethod(new MethodSymbol("Unwrap", pathError, false,
                Array.Empty<ParameterSymbol>(), err));
            pkg.AddExport(pathError);

            // os.LinkError struct
            var linkError = new StructTypeSymbol("LinkError", new[]
            {
                new FieldSymbol("Op", s, 0),
                new FieldSymbol("Old", s, 1),
                new FieldSymbol("New", s, 2),
                new FieldSymbol("Err", err, 3),
            });
            linkError.AddMethod(new MethodSymbol("Error", linkError, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(linkError);

            // os.SameFile(fi1, fi2 FileInfo) bool
            pkg.AddExport(new FunctionSymbol("SameFile",
                new[] { P("fi1", fileInfoType, 0), P("fi2", fileInfoType, 1) },
                new[] { BuiltinTypes.Bool }, packageName: "os"));

            // os.IsPathSeparator(c uint8) bool
            pkg.AddExport(new FunctionSymbol("IsPathSeparator",
                new[] { P("c", BuiltinTypes.Uint8, 0) },
                new[] { BuiltinTypes.Bool }, packageName: "os"));

            // os.NewSyscallError(syscall string, err error) error
            pkg.AddExport(new FunctionSymbol("NewSyscallError",
                new[] { P("name", s, 0), P("err", err, 1) },
                new[] { err }, packageName: "os"));

            // os.Symlink(oldname, newname string) error
            pkg.AddExport(new FunctionSymbol("Symlink",
                new[] { P("oldname", s, 0), P("newname", s, 1) },
                new[] { err }, packageName: "os"));

            // FileMode constants (same as io/fs but in os package)
            pkg.AddExport(new ConstantSymbol("ModeDir", fileModeType, (long)0x80000000));
            pkg.AddExport(new ConstantSymbol("ModeAppend", fileModeType, (long)0x40000000));
            pkg.AddExport(new ConstantSymbol("ModeExclusive", fileModeType, (long)0x20000000));
            pkg.AddExport(new ConstantSymbol("ModeTemporary", fileModeType, (long)0x10000000));
            pkg.AddExport(new ConstantSymbol("ModeSymlink", fileModeType, (long)0x08000000));
            pkg.AddExport(new ConstantSymbol("ModeDevice", fileModeType, (long)0x04000000));
            pkg.AddExport(new ConstantSymbol("ModeNamedPipe", fileModeType, (long)0x02000000));
            pkg.AddExport(new ConstantSymbol("ModeSocket", fileModeType, (long)0x01000000));
            pkg.AddExport(new ConstantSymbol("ModeSetuid", fileModeType, (long)0x00800000));
            pkg.AddExport(new ConstantSymbol("ModeSetgid", fileModeType, (long)0x00400000));
            pkg.AddExport(new ConstantSymbol("ModeCharDevice", fileModeType, (long)0x00200000));
            pkg.AddExport(new ConstantSymbol("ModeSticky", fileModeType, (long)0x00100000));
            pkg.AddExport(new ConstantSymbol("ModeIrregular", fileModeType, (long)0x00080000));
            pkg.AddExport(new ConstantSymbol("ModeType", fileModeType, unchecked((long)0xFF000000)));
            pkg.AddExport(new ConstantSymbol("ModePerm", fileModeType, (long)0x1FF));

            return pkg;
        }

        private static PackageSymbol CreateOsSignalPackage()
        {
            var pkg = new PackageSymbol("signal", "os/signal");
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // signal.Notify(c chan<- os.Signal, sig ...os.Signal)
            pkg.AddExport(new FunctionSymbol("Notify",
                new[] { new ParameterSymbol("c", iface, 0) },
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "signal"));

            // signal.Stop(c chan<- os.Signal)
            pkg.AddExport(new FunctionSymbol("Stop",
                new[] { new ParameterSymbol("c", iface, 0) },
                Array.Empty<TypeSymbol>(), packageName: "signal"));

            // signal.Reset(sig ...os.Signal)
            pkg.AddExport(new FunctionSymbol("Reset",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "signal"));

            // signal.Ignore(sig ...os.Signal)
            pkg.AddExport(new FunctionSymbol("Ignore",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "signal"));

            // signal.NotifyContext(parent context.Context, signals ...os.Signal) (context.Context, context.CancelFunc)
            pkg.AddExport(new FunctionSymbol("NotifyContext",
                new[] { new ParameterSymbol("parent", iface, 0) },
                new TypeSymbol[] { iface, iface }, isVariadic: true, packageName: "signal"));

            return pkg;
        }

        private static PackageSymbol CreateOsExecPackage()
        {
            var pkg = new PackageSymbol("exec", "os/exec");
            var s = BuiltinTypes.String;
            var err = BuiltinTypes.Error;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var stringSlice = new SliceTypeSymbol(s);

            // exec.Cmd type
            var cmdType = new StructTypeSymbol("Cmd", new[]
            {
                new FieldSymbol("Path", s, 0),
                new FieldSymbol("Args", stringSlice, 1),
                new FieldSymbol("Env", stringSlice, 2),
                new FieldSymbol("Dir", s, 3),
                new FieldSymbol("Stdin", iface, 4),
                new FieldSymbol("Stdout", iface, 5),
                new FieldSymbol("Stderr", iface, 6),
            });
            cmdType.AddMethod(new MethodSymbol("Run", cmdType, false,
                Array.Empty<ParameterSymbol>(), err));
            cmdType.AddMethod(new MethodSymbol("Start", cmdType, false,
                Array.Empty<ParameterSymbol>(), err));
            cmdType.AddMethod(new MethodSymbol("Wait", cmdType, false,
                Array.Empty<ParameterSymbol>(), err));
            cmdType.AddMethod(new MethodSymbol("Output", cmdType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { byteSlice, err }));
            cmdType.AddMethod(new MethodSymbol("CombinedOutput", cmdType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { byteSlice, err }));
            cmdType.AddMethod(new MethodSymbol("StdinPipe", cmdType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { iface, err }));
            cmdType.AddMethod(new MethodSymbol("StdoutPipe", cmdType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { iface, err }));
            cmdType.AddMethod(new MethodSymbol("StderrPipe", cmdType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { iface, err }));
            cmdType.AddMethod(new MethodSymbol("String", cmdType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(cmdType);

            // exec.Command(name string, arg ...string) *Cmd
            pkg.AddExport(new FunctionSymbol("Command",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(cmdType) }, isVariadic: true, packageName: "exec"));

            // exec.CommandContext(ctx context.Context, name string, arg ...string) *Cmd
            pkg.AddExport(new FunctionSymbol("CommandContext",
                new[] { P("ctx", iface, 0), P("name", s, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(cmdType) }, isVariadic: true, packageName: "exec"));

            // exec.LookPath(file string) (string, error)
            pkg.AddExport(new FunctionSymbol("LookPath",
                new[] { P("file", s, 0) },
                new TypeSymbol[] { s, err }, packageName: "exec"));

            // exec.Error type
            var execErrType = new StructTypeSymbol("Error", new[]
            {
                new FieldSymbol("Name", s, 0),
                new FieldSymbol("Err", err, 1),
            });
            execErrType.AddMethod(new MethodSymbol("Error", execErrType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(execErrType);

            // exec.ErrNotFound
            pkg.AddExport(new PackageVarSymbol("ErrNotFound", err, typeof(object), "ErrNotFound"));

            // exec.ExitError type
            var exitErrType = new StructTypeSymbol("ExitError", new[]
            {
                new FieldSymbol("Stderr", byteSlice, 0),
            });
            exitErrType.AddMethod(new MethodSymbol("Error", exitErrType, false,
                Array.Empty<ParameterSymbol>(), s));
            exitErrType.AddMethod(new MethodSymbol("ExitCode", exitErrType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            pkg.AddExport(exitErrType);

            return pkg;
        }

        private static PackageSymbol CreateTimePackage()
        {
            var pkg = new PackageSymbol("time", "time");

            var i = BuiltinTypes.Int;
            var i64 = BuiltinTypes.Int64;
            var s = BuiltinTypes.String;
            var b = BuiltinTypes.Bool;

            // time.Duration — named type backed by int64
            var durationType = new TypeSymbol("Duration", TypeKind.Int64, BuiltinTypes.Int64);
            durationType.AddMethod(new MethodSymbol("String", durationType, false,
                Array.Empty<ParameterSymbol>(), s));
            durationType.AddMethod(new MethodSymbol("Nanoseconds", durationType, false,
                Array.Empty<ParameterSymbol>(), i64));
            durationType.AddMethod(new MethodSymbol("Microseconds", durationType, false,
                Array.Empty<ParameterSymbol>(), i64));
            durationType.AddMethod(new MethodSymbol("Milliseconds", durationType, false,
                Array.Empty<ParameterSymbol>(), i64));
            durationType.AddMethod(new MethodSymbol("Seconds", durationType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Float64));
            durationType.AddMethod(new MethodSymbol("Minutes", durationType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Float64));
            durationType.AddMethod(new MethodSymbol("Hours", durationType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Float64));
            durationType.AddMethod(new MethodSymbol("Truncate", durationType, false,
                new[] { P("m", durationType, 0) }, durationType));
            durationType.AddMethod(new MethodSymbol("Round", durationType, false,
                new[] { P("m", durationType, 0) }, durationType));
            durationType.AddMethod(new MethodSymbol("Abs", durationType, false,
                Array.Empty<ParameterSymbol>(), durationType));
            pkg.AddExport(durationType);

            // time.Month — named type backed by int
            var monthType = new TypeSymbol("Month", TypeKind.Int, BuiltinTypes.Int);
            monthType.AddMethod(new MethodSymbol("String", monthType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(monthType);

            // time.Weekday — named type backed by int
            var weekdayType = new TypeSymbol("Weekday", TypeKind.Int, BuiltinTypes.Int);
            weekdayType.AddMethod(new MethodSymbol("String", weekdayType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(weekdayType);

            // time.Location type
            var locationType = new StructTypeSymbol("Location", Array.Empty<FieldSymbol>());
            locationType.AddMethod(new MethodSymbol("String", locationType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(locationType);

            // time.Time type
            var timeType = new StructTypeSymbol("Time", Array.Empty<FieldSymbol>());
            timeType.AddMethod(new MethodSymbol("Unix", timeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            timeType.AddMethod(new MethodSymbol("UnixMilli", timeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            timeType.AddMethod(new MethodSymbol("UnixNano", timeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            timeType.AddMethod(new MethodSymbol("String", timeType, false,
                Array.Empty<ParameterSymbol>(), s));
            timeType.AddMethod(new MethodSymbol("Format", timeType, false,
                new[] { P("layout", s, 0) }, s));
            timeType.AddMethod(new MethodSymbol("Sub", timeType, false,
                new[] { P("u", timeType, 0) }, durationType));
            timeType.AddMethod(new MethodSymbol("Add", timeType, false,
                new[] { P("d", durationType, 0) }, new[] { timeType }));
            timeType.AddMethod(new MethodSymbol("Before", timeType, false,
                new[] { P("u", timeType, 0) }, b));
            timeType.AddMethod(new MethodSymbol("After", timeType, false,
                new[] { P("u", timeType, 0) }, b));
            timeType.AddMethod(new MethodSymbol("Equal", timeType, false,
                new[] { P("u", timeType, 0) }, b));
            timeType.AddMethod(new MethodSymbol("IsZero", timeType, false,
                Array.Empty<ParameterSymbol>(), b));
            timeType.AddMethod(new MethodSymbol("Year", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("Month", timeType, false,
                Array.Empty<ParameterSymbol>(), monthType));
            timeType.AddMethod(new MethodSymbol("Day", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("Hour", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("Minute", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("Second", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("Nanosecond", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("Weekday", timeType, false,
                Array.Empty<ParameterSymbol>(), weekdayType));
            timeType.AddMethod(new MethodSymbol("Location", timeType, false,
                Array.Empty<ParameterSymbol>(), new PointerTypeSymbol(locationType)));
            timeType.AddMethod(new MethodSymbol("UTC", timeType, false,
                Array.Empty<ParameterSymbol>(), timeType));
            timeType.AddMethod(new MethodSymbol("Local", timeType, false,
                Array.Empty<ParameterSymbol>(), timeType));
            timeType.AddMethod(new MethodSymbol("In", timeType, false,
                new[] { P("loc", new PointerTypeSymbol(locationType), 0) }, timeType));
            timeType.AddMethod(new MethodSymbol("Truncate", timeType, false,
                new[] { P("d", durationType, 0) }, timeType));
            timeType.AddMethod(new MethodSymbol("Round", timeType, false,
                new[] { P("d", durationType, 0) }, timeType));
            timeType.AddMethod(new MethodSymbol("AddDate", timeType, false,
                new[] { P("years", i, 0), P("months", i, 1), P("days", i, 2) }, timeType));
            timeType.AddMethod(new MethodSymbol("Clock", timeType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { i, i, i }));
            timeType.AddMethod(new MethodSymbol("Date", timeType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { i, monthType, i }));
            timeType.AddMethod(new MethodSymbol("YearDay", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("ISOWeek", timeType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { i, i }));
            timeType.AddMethod(new MethodSymbol("Zone", timeType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s, i }));
            timeType.AddMethod(new MethodSymbol("AppendFormat", timeType, false,
                new[] { P("b", new SliceTypeSymbol(BuiltinTypes.Byte), 0), P("layout", s, 1) },
                new SliceTypeSymbol(BuiltinTypes.Byte)));
            timeType.AddMethod(new MethodSymbol("GoString", timeType, false,
                Array.Empty<ParameterSymbol>(), s));
            timeType.AddMethod(new MethodSymbol("GobEncode", timeType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error }));
            timeType.AddMethod(new MethodSymbol("MarshalJSON", timeType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error }));
            timeType.AddMethod(new MethodSymbol("MarshalText", timeType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error }));
            timeType.AddMethod(new MethodSymbol("MarshalBinary", timeType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error }));
            timeType.AddMethod(new MethodSymbol("UnmarshalBinary", timeType, false,
                new[] { P("data", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                BuiltinTypes.Error));
            pkg.AddExport(timeType);

            // time.Timer type
            var timeChan = new ChannelTypeSymbol(timeType);
            var timerType = new StructTypeSymbol("Timer", new[]
            {
                new FieldSymbol("C", timeChan, 0),
            });
            timerType.AddMethod(new MethodSymbol("Stop", timerType, false,
                Array.Empty<ParameterSymbol>(), b));
            timerType.AddMethod(new MethodSymbol("Reset", timerType, false,
                new[] { P("d", durationType, 0) }, b));
            pkg.AddExport(timerType);

            // time.Ticker type
            var tickerType = new StructTypeSymbol("Ticker", new[]
            {
                new FieldSymbol("C", timeChan, 0),
            });
            tickerType.AddMethod(new MethodSymbol("Stop", tickerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            tickerType.AddMethod(new MethodSymbol("Reset", tickerType, false,
                new[] { P("d", durationType, 0) }, BuiltinTypes.Void));
            pkg.AddExport(tickerType);

            // time.Sleep(d Duration)
            pkg.AddExport(new FunctionSymbol("Sleep",
                new[] { P("d", durationType, 0) },
                Array.Empty<TypeSymbol>(), packageName: "time"));

            // time.Now() Time
            pkg.AddExport(new FunctionSymbol("Now",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { timeType }, packageName: "time"));

            // time.Since(t Time) Duration
            pkg.AddExport(new FunctionSymbol("Since",
                new[] { P("t", timeType, 0) },
                new[] { durationType }, packageName: "time"));

            // time.Until(t Time) Duration
            pkg.AddExport(new FunctionSymbol("Until",
                new[] { P("t", timeType, 0) },
                new[] { durationType }, packageName: "time"));

            // time.Parse(layout, value string) (Time, error)
            pkg.AddExport(new FunctionSymbol("Parse",
                new[] { P("layout", s, 0), P("value", s, 1) },
                new TypeSymbol[] { timeType, BuiltinTypes.Error },
                packageName: "time"));

            // time.ParseInLocation(layout, value string, loc *Location) (Time, error)
            pkg.AddExport(new FunctionSymbol("ParseInLocation",
                new[] { P("layout", s, 0), P("value", s, 1), P("loc", new PointerTypeSymbol(locationType), 2) },
                new TypeSymbol[] { timeType, BuiltinTypes.Error },
                packageName: "time"));

            // time.ParseDuration(s string) (Duration, error)
            pkg.AddExport(new FunctionSymbol("ParseDuration",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { durationType, BuiltinTypes.Error },
                packageName: "time"));

            // time.Unix(sec, nsec int64) Time
            pkg.AddExport(new FunctionSymbol("Unix",
                new[] { P("sec", BuiltinTypes.Int64, 0), P("nsec", BuiltinTypes.Int64, 1) },
                new TypeSymbol[] { timeType }, packageName: "time"));

            // time.Date(year int, month Month, day, hour, min, sec, nsec int, loc *Location) Time
            pkg.AddExport(new FunctionSymbol("Date",
                new[] { P("year", i, 0), P("month", i, 1), P("day", i, 2),
                        P("hour", i, 3), P("min", i, 4), P("sec", i, 5),
                        P("nsec", i, 6), P("loc", new PointerTypeSymbol(locationType), 7) },
                new TypeSymbol[] { timeType }, packageName: "time"));

            // time.NewTimer(d Duration) *Timer
            pkg.AddExport(new FunctionSymbol("NewTimer",
                new[] { P("d", durationType, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(timerType) }, packageName: "time"));

            // time.NewTicker(d Duration) *Ticker
            pkg.AddExport(new FunctionSymbol("NewTicker",
                new[] { P("d", durationType, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tickerType) }, packageName: "time"));

            // time.After(d Duration) <-chan Time
            pkg.AddExport(new FunctionSymbol("After",
                new[] { P("d", durationType, 0) },
                new TypeSymbol[] { new ChannelTypeSymbol(timeType) }, packageName: "time"));

            // time.Tick(d Duration) <-chan Time
            pkg.AddExport(new FunctionSymbol("Tick",
                new[] { P("d", durationType, 0) },
                new TypeSymbol[] { new ChannelTypeSymbol(timeType) }, packageName: "time"));

            // time.AfterFunc(d Duration, f func()) *Timer
            pkg.AddExport(new FunctionSymbol("AfterFunc",
                new[] { P("d", durationType, 0),
                        P("f", new FunctionTypeSymbol(Array.Empty<TypeSymbol>(), Array.Empty<TypeSymbol>()), 1) },
                new TypeSymbol[] { new PointerTypeSymbol(timerType) }, packageName: "time"));

            // time.LoadLocation(name string) (*Location, error)
            pkg.AddExport(new FunctionSymbol("LoadLocation",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(locationType), BuiltinTypes.Error },
                packageName: "time"));

            // Duration constants
            pkg.AddExport(new ConstantSymbol("Nanosecond", durationType, (long)1));
            pkg.AddExport(new ConstantSymbol("Microsecond", durationType, (long)1000));
            pkg.AddExport(new ConstantSymbol("Millisecond", durationType, (long)1_000_000));
            pkg.AddExport(new ConstantSymbol("Second", durationType, (long)1_000_000_000));
            pkg.AddExport(new ConstantSymbol("Minute", durationType, (long)60_000_000_000));
            pkg.AddExport(new ConstantSymbol("Hour", durationType, (long)3_600_000_000_000));

            // Layout constants
            pkg.AddExport(new ConstantSymbol("RFC3339", s, "2006-01-02T15:04:05Z07:00"));
            pkg.AddExport(new ConstantSymbol("RFC3339Nano", s, "2006-01-02T15:04:05.999999999Z07:00"));
            pkg.AddExport(new ConstantSymbol("RFC822", s, "02 Jan 06 15:04 MST"));
            pkg.AddExport(new ConstantSymbol("RFC822Z", s, "02 Jan 06 15:04 -0700"));
            pkg.AddExport(new ConstantSymbol("RFC850", s, "Monday, 02-Jan-06 15:04:05 MST"));
            pkg.AddExport(new ConstantSymbol("RFC1123", s, "Mon, 02 Jan 2006 15:04:05 MST"));
            pkg.AddExport(new ConstantSymbol("RFC1123Z", s, "Mon, 02 Jan 2006 15:04:05 -0700"));
            pkg.AddExport(new ConstantSymbol("Kitchen", s, "3:04PM"));
            pkg.AddExport(new ConstantSymbol("Stamp", s, "Jan _2 15:04:05"));
            pkg.AddExport(new ConstantSymbol("StampMilli", s, "Jan _2 15:04:05.000"));
            pkg.AddExport(new ConstantSymbol("StampMicro", s, "Jan _2 15:04:05.000000"));
            pkg.AddExport(new ConstantSymbol("StampNano", s, "Jan _2 15:04:05.000000000"));
            pkg.AddExport(new ConstantSymbol("DateTime", s, "2006-01-02 15:04:05"));
            pkg.AddExport(new ConstantSymbol("DateOnly", s, "2006-01-02"));
            pkg.AddExport(new ConstantSymbol("TimeOnly", s, "15:04:05"));
            pkg.AddExport(new ConstantSymbol("ANSIC", s, "Mon Jan _2 15:04:05 2006"));
            pkg.AddExport(new ConstantSymbol("UnixDate", s, "Mon Jan _2 15:04:05 MST 2006"));
            pkg.AddExport(new ConstantSymbol("RubyDate", s, "Mon Jan 02 15:04:05 -0700 2006"));

            // Month constants
            pkg.AddExport(new ConstantSymbol("January", monthType, (long)1));
            pkg.AddExport(new ConstantSymbol("February", monthType, (long)2));
            pkg.AddExport(new ConstantSymbol("March", monthType, (long)3));
            pkg.AddExport(new ConstantSymbol("April", monthType, (long)4));
            pkg.AddExport(new ConstantSymbol("May", monthType, (long)5));
            pkg.AddExport(new ConstantSymbol("June", monthType, (long)6));
            pkg.AddExport(new ConstantSymbol("July", monthType, (long)7));
            pkg.AddExport(new ConstantSymbol("August", monthType, (long)8));
            pkg.AddExport(new ConstantSymbol("September", monthType, (long)9));
            pkg.AddExport(new ConstantSymbol("October", monthType, (long)10));
            pkg.AddExport(new ConstantSymbol("November", monthType, (long)11));
            pkg.AddExport(new ConstantSymbol("December", monthType, (long)12));

            // Weekday constants
            pkg.AddExport(new ConstantSymbol("Sunday", weekdayType, (long)0));
            pkg.AddExport(new ConstantSymbol("Monday", weekdayType, (long)1));
            pkg.AddExport(new ConstantSymbol("Tuesday", weekdayType, (long)2));
            pkg.AddExport(new ConstantSymbol("Wednesday", weekdayType, (long)3));
            pkg.AddExport(new ConstantSymbol("Thursday", weekdayType, (long)4));
            pkg.AddExport(new ConstantSymbol("Friday", weekdayType, (long)5));
            pkg.AddExport(new ConstantSymbol("Saturday", weekdayType, (long)6));

            // time.UTC (a nil placeholder for Location)
            pkg.AddExport(new PackageVarSymbol("UTC", new PointerTypeSymbol(locationType),
                typeof(Ngo.Runtime.GoTime), "UTC"));
            pkg.AddExport(new PackageVarSymbol("Local", new PointerTypeSymbol(locationType),
                typeof(Ngo.Runtime.GoTime), "Local"));

            return pkg;
        }

        private static PackageSymbol CreateSortPackage()
        {
            var pkg = new PackageSymbol("sort", "sort");

            var sliceInt = new SliceTypeSymbol(BuiltinTypes.Int);
            var sliceString = new SliceTypeSymbol(BuiltinTypes.String);
            var sliceFloat64 = new SliceTypeSymbol(BuiltinTypes.Float64);

            // sort.Ints(a []int)
            pkg.AddExport(new FunctionSymbol("Ints",
                new[] { new ParameterSymbol("a", sliceInt, 0) },
                Array.Empty<TypeSymbol>()));

            // sort.Strings(a []string)
            pkg.AddExport(new FunctionSymbol("Strings",
                new[] { new ParameterSymbol("a", sliceString, 0) },
                Array.Empty<TypeSymbol>()));

            // sort.Float64s(a []float64)
            pkg.AddExport(new FunctionSymbol("Float64s",
                new[] { new ParameterSymbol("a", sliceFloat64, 0) },
                Array.Empty<TypeSymbol>()));

            // sort.IntsAreSorted(a []int) bool
            pkg.AddExport(new FunctionSymbol("IntsAreSorted",
                new[] { new ParameterSymbol("a", sliceInt, 0) },
                new[] { BuiltinTypes.Bool }));

            // sort.StringsAreSorted(a []string) bool
            pkg.AddExport(new FunctionSymbol("StringsAreSorted",
                new[] { new ParameterSymbol("a", sliceString, 0) },
                new[] { BuiltinTypes.Bool }));

            // sort.Float64sAreSorted(a []float64) bool
            pkg.AddExport(new FunctionSymbol("Float64sAreSorted",
                new[] { new ParameterSymbol("a", sliceFloat64, 0) },
                new[] { BuiltinTypes.Bool }));

            // sort.SearchInts(a []int, x int) int
            pkg.AddExport(new FunctionSymbol("SearchInts",
                new[] { new ParameterSymbol("a", sliceInt, 0),
                        new ParameterSymbol("x", BuiltinTypes.Int, 1) },
                new[] { BuiltinTypes.Int }));

            // sort.SearchStrings(a []string, x string) int
            pkg.AddExport(new FunctionSymbol("SearchStrings",
                new[] { new ParameterSymbol("a", sliceString, 0),
                        new ParameterSymbol("x", BuiltinTypes.String, 1) },
                new[] { BuiltinTypes.Int }));

            // sort.Interface { Len() int; Less(i, j int) bool; Swap(i, j int) }
            var sortIface = new InterfaceTypeSymbol("Interface", new[]
            {
                new MethodSymbol("Len", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
                new MethodSymbol("Less", null!, false,
                    new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                            new ParameterSymbol("j", BuiltinTypes.Int, 1) },
                    BuiltinTypes.Bool),
                new MethodSymbol("Swap", null!, false,
                    new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                            new ParameterSymbol("j", BuiltinTypes.Int, 1) },
                    BuiltinTypes.Void),
            });
            pkg.AddExport(sortIface);

            // sort.Sort(data Interface)
            pkg.AddExport(new FunctionSymbol("Sort",
                new[] { new ParameterSymbol("data", sortIface, 0) },
                Array.Empty<TypeSymbol>()));

            // sort.Stable(data Interface)
            pkg.AddExport(new FunctionSymbol("Stable",
                new[] { new ParameterSymbol("data", sortIface, 0) },
                Array.Empty<TypeSymbol>()));

            // sort.Reverse(data Interface) Interface
            pkg.AddExport(new FunctionSymbol("Reverse",
                new[] { new ParameterSymbol("data", sortIface, 0) },
                new[] { (TypeSymbol)sortIface }));

            // sort.IsSorted(data Interface) bool
            pkg.AddExport(new FunctionSymbol("IsSorted",
                new[] { new ParameterSymbol("data", sortIface, 0) },
                new[] { BuiltinTypes.Bool }));

            // sort.Search(n int, f func(int) bool) int
            var searchFunc = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.Int },
                new TypeSymbol[] { BuiltinTypes.Bool });
            pkg.AddExport(new FunctionSymbol("Search",
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0),
                        new ParameterSymbol("f", searchFunc, 1) },
                new[] { BuiltinTypes.Int }));

            // sort.Slice(x interface{}, less func(i, j int) bool)
            var lessFunc = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Int },
                new TypeSymbol[] { BuiltinTypes.Bool });
            pkg.AddExport(new FunctionSymbol("Slice",
                new[] { new ParameterSymbol("x", BuiltinTypes.EmptyInterface, 0),
                        new ParameterSymbol("less", lessFunc, 1) },
                Array.Empty<TypeSymbol>()));

            // sort.SliceStable(x interface{}, less func(i, j int) bool)
            pkg.AddExport(new FunctionSymbol("SliceStable",
                new[] { new ParameterSymbol("x", BuiltinTypes.EmptyInterface, 0),
                        new ParameterSymbol("less", lessFunc, 1) },
                Array.Empty<TypeSymbol>()));

            // sort.SliceIsSorted(x interface{}, less func(i, j int) bool) bool
            pkg.AddExport(new FunctionSymbol("SliceIsSorted",
                new[] { new ParameterSymbol("x", BuiltinTypes.EmptyInterface, 0),
                        new ParameterSymbol("less", lessFunc, 1) },
                new[] { BuiltinTypes.Bool }));

            // sort.StringSlice — named type []string with Sort/Len/Less/Swap/Search
            var stringSliceType = new StructTypeSymbol("StringSlice", Array.Empty<FieldSymbol>());
            stringSliceType.UnderlyingType = new SliceTypeSymbol(BuiltinTypes.String);
            stringSliceType.AddMethod(new MethodSymbol("Sort", stringSliceType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            stringSliceType.AddMethod(new MethodSymbol("Len", stringSliceType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            stringSliceType.AddMethod(new MethodSymbol("Less", stringSliceType, false,
                new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                        new ParameterSymbol("j", BuiltinTypes.Int, 1) },
                BuiltinTypes.Bool));
            stringSliceType.AddMethod(new MethodSymbol("Swap", stringSliceType, false,
                new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                        new ParameterSymbol("j", BuiltinTypes.Int, 1) },
                BuiltinTypes.Void));
            stringSliceType.AddMethod(new MethodSymbol("Search", stringSliceType, false,
                new[] { new ParameterSymbol("x", BuiltinTypes.String, 0) },
                BuiltinTypes.Int));
            pkg.AddExport(stringSliceType);

            // sort.IntSlice — named type []int with Sort/Len/Less/Swap/Search
            var intSliceType = new StructTypeSymbol("IntSlice", Array.Empty<FieldSymbol>());
            intSliceType.UnderlyingType = new SliceTypeSymbol(BuiltinTypes.Int);
            intSliceType.AddMethod(new MethodSymbol("Sort", intSliceType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            intSliceType.AddMethod(new MethodSymbol("Len", intSliceType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            intSliceType.AddMethod(new MethodSymbol("Less", intSliceType, false,
                new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                        new ParameterSymbol("j", BuiltinTypes.Int, 1) },
                BuiltinTypes.Bool));
            intSliceType.AddMethod(new MethodSymbol("Swap", intSliceType, false,
                new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                        new ParameterSymbol("j", BuiltinTypes.Int, 1) },
                BuiltinTypes.Void));
            intSliceType.AddMethod(new MethodSymbol("Search", intSliceType, false,
                new[] { new ParameterSymbol("x", BuiltinTypes.Int, 0) },
                BuiltinTypes.Int));
            pkg.AddExport(intSliceType);

            // sort.Float64Slice — named type []float64 with Sort/Len/Less/Swap/Search
            var float64SliceType = new StructTypeSymbol("Float64Slice", Array.Empty<FieldSymbol>());
            float64SliceType.UnderlyingType = new SliceTypeSymbol(BuiltinTypes.Float64);
            float64SliceType.AddMethod(new MethodSymbol("Sort", float64SliceType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            float64SliceType.AddMethod(new MethodSymbol("Len", float64SliceType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            float64SliceType.AddMethod(new MethodSymbol("Less", float64SliceType, false,
                new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                        new ParameterSymbol("j", BuiltinTypes.Int, 1) },
                BuiltinTypes.Bool));
            float64SliceType.AddMethod(new MethodSymbol("Swap", float64SliceType, false,
                new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                        new ParameterSymbol("j", BuiltinTypes.Int, 1) },
                BuiltinTypes.Void));
            float64SliceType.AddMethod(new MethodSymbol("Search", float64SliceType, false,
                new[] { new ParameterSymbol("x", BuiltinTypes.Float64, 0) },
                BuiltinTypes.Int));
            pkg.AddExport(float64SliceType);

            return pkg;
        }

        private static PackageSymbol CreateMathBitsPackage()
        {
            var pkg = new PackageSymbol("bits", "math/bits");

            var u = BuiltinTypes.Uint;
            var u8 = BuiltinTypes.Uint8;
            var u16 = BuiltinTypes.Uint16;
            var u32 = BuiltinTypes.Uint32;
            var u64 = BuiltinTypes.Uint64;
            var i = BuiltinTypes.Int;

            // RotateLeft functions
            pkg.AddExport(new FunctionSymbol("RotateLeft", new[] { P("x", u, 0), P("k", i, 1) }, new TypeSymbol[] { u }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("RotateLeft8", new[] { P("x", u8, 0), P("k", i, 1) }, new TypeSymbol[] { u8 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("RotateLeft16", new[] { P("x", u16, 0), P("k", i, 1) }, new TypeSymbol[] { u16 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("RotateLeft32", new[] { P("x", u32, 0), P("k", i, 1) }, new TypeSymbol[] { u32 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("RotateLeft64", new[] { P("x", u64, 0), P("k", i, 1) }, new TypeSymbol[] { u64 }, packageName: "bits"));

            // Counting functions
            pkg.AddExport(new FunctionSymbol("OnesCount", new[] { P("x", u, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("OnesCount8", new[] { P("x", u8, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("OnesCount16", new[] { P("x", u16, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("OnesCount32", new[] { P("x", u32, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("OnesCount64", new[] { P("x", u64, 0) }, new TypeSymbol[] { i }, packageName: "bits"));

            // Leading zeros
            pkg.AddExport(new FunctionSymbol("LeadingZeros", new[] { P("x", u, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("LeadingZeros8", new[] { P("x", u8, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("LeadingZeros16", new[] { P("x", u16, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("LeadingZeros32", new[] { P("x", u32, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("LeadingZeros64", new[] { P("x", u64, 0) }, new TypeSymbol[] { i }, packageName: "bits"));

            // Trailing zeros
            pkg.AddExport(new FunctionSymbol("TrailingZeros", new[] { P("x", u, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("TrailingZeros8", new[] { P("x", u8, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("TrailingZeros16", new[] { P("x", u16, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("TrailingZeros32", new[] { P("x", u32, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("TrailingZeros64", new[] { P("x", u64, 0) }, new TypeSymbol[] { i }, packageName: "bits"));

            // Bit length
            pkg.AddExport(new FunctionSymbol("Len", new[] { P("x", u, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Len8", new[] { P("x", u8, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Len16", new[] { P("x", u16, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Len32", new[] { P("x", u32, 0) }, new TypeSymbol[] { i }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Len64", new[] { P("x", u64, 0) }, new TypeSymbol[] { i }, packageName: "bits"));

            // Reverse bits
            pkg.AddExport(new FunctionSymbol("Reverse", new[] { P("x", u, 0) }, new TypeSymbol[] { u }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Reverse8", new[] { P("x", u8, 0) }, new TypeSymbol[] { u8 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Reverse16", new[] { P("x", u16, 0) }, new TypeSymbol[] { u16 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Reverse32", new[] { P("x", u32, 0) }, new TypeSymbol[] { u32 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Reverse64", new[] { P("x", u64, 0) }, new TypeSymbol[] { u64 }, packageName: "bits"));

            // Reverse bytes
            pkg.AddExport(new FunctionSymbol("ReverseBytes16", new[] { P("x", u16, 0) }, new TypeSymbol[] { u16 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("ReverseBytes32", new[] { P("x", u32, 0) }, new TypeSymbol[] { u32 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("ReverseBytes64", new[] { P("x", u64, 0) }, new TypeSymbol[] { u64 }, packageName: "bits"));

            // Arithmetic
            pkg.AddExport(new FunctionSymbol("Add", new[] { P("x", u, 0), P("y", u, 1), P("carry", u, 2) },
                new TypeSymbol[] { u, u }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Add32", new[] { P("x", u32, 0), P("y", u32, 1), P("carry", u32, 2) },
                new TypeSymbol[] { u32, u32 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Add64", new[] { P("x", u64, 0), P("y", u64, 1), P("carry", u64, 2) },
                new TypeSymbol[] { u64, u64 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Sub", new[] { P("x", u, 0), P("y", u, 1), P("borrow", u, 2) },
                new TypeSymbol[] { u, u }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Sub32", new[] { P("x", u32, 0), P("y", u32, 1), P("borrow", u32, 2) },
                new TypeSymbol[] { u32, u32 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Sub64", new[] { P("x", u64, 0), P("y", u64, 1), P("borrow", u64, 2) },
                new TypeSymbol[] { u64, u64 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Mul", new[] { P("x", u, 0), P("y", u, 1) },
                new TypeSymbol[] { u, u }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Mul32", new[] { P("x", u32, 0), P("y", u32, 1) },
                new TypeSymbol[] { u64 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Mul64", new[] { P("x", u64, 0), P("y", u64, 1) },
                new TypeSymbol[] { u64, u64 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Div", new[] { P("hi", u, 0), P("lo", u, 1), P("y", u, 2) },
                new TypeSymbol[] { u, u }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Div32", new[] { P("hi", u32, 0), P("lo", u32, 1), P("y", u32, 2) },
                new TypeSymbol[] { u32, u32 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Div64", new[] { P("hi", u64, 0), P("lo", u64, 1), P("y", u64, 2) },
                new TypeSymbol[] { u64, u64 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Rem", new[] { P("hi", u, 0), P("lo", u, 1), P("y", u, 2) },
                new TypeSymbol[] { u }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Rem32", new[] { P("hi", u32, 0), P("lo", u32, 1), P("y", u32, 2) },
                new TypeSymbol[] { u32 }, packageName: "bits"));
            pkg.AddExport(new FunctionSymbol("Rem64", new[] { P("hi", u64, 0), P("lo", u64, 1), P("y", u64, 2) },
                new TypeSymbol[] { u64 }, packageName: "bits"));

            // Constants
            pkg.AddExport(new ConstantSymbol("UintSize", i, (long)64));

            return pkg;
        }

        private static PackageSymbol CreateMathRandPackage()
        {
            var pkg = new PackageSymbol("rand", "math/rand");

            // rand.Intn(n int) int
            pkg.AddExport(new FunctionSymbol("Intn",
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0) },
                new[] { BuiltinTypes.Int }));

            // rand.Float64() float64
            pkg.AddExport(new FunctionSymbol("Float64",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Float64 }));

            // rand.Int() int
            pkg.AddExport(new FunctionSymbol("Int",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Int }));

            // rand.Seed(seed int64)
            pkg.AddExport(new FunctionSymbol("Seed",
                new[] { new ParameterSymbol("seed", BuiltinTypes.Int64, 0) },
                Array.Empty<TypeSymbol>()));

            // rand.Int31() int32
            pkg.AddExport(new FunctionSymbol("Int31",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Int32 }));

            // rand.Int31n(n int32) int32
            pkg.AddExport(new FunctionSymbol("Int31n",
                new[] { new ParameterSymbol("n", BuiltinTypes.Int32, 0) },
                new[] { BuiltinTypes.Int32 }));

            // rand.Int63() int64
            pkg.AddExport(new FunctionSymbol("Int63",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Int64 }));

            // rand.Int63n(n int64) int64
            pkg.AddExport(new FunctionSymbol("Int63n",
                new[] { new ParameterSymbol("n", BuiltinTypes.Int64, 0) },
                new[] { BuiltinTypes.Int64 }));

            // rand.Float32() float32
            pkg.AddExport(new FunctionSymbol("Float32",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Float32 }));

            // rand.Perm(n int) []int
            pkg.AddExport(new FunctionSymbol("Perm",
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0) },
                new[] { new SliceTypeSymbol(BuiltinTypes.Int) }));

            // rand.Uint32() uint32
            pkg.AddExport(new FunctionSymbol("Uint32",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Uint32 }));

            // rand.Uint64() uint64
            pkg.AddExport(new FunctionSymbol("Uint64",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Uint64 }));

            // rand.NormFloat64() float64
            pkg.AddExport(new FunctionSymbol("NormFloat64",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Float64 }));

            // rand.ExpFloat64() float64
            pkg.AddExport(new FunctionSymbol("ExpFloat64",
                Array.Empty<ParameterSymbol>(),
                new[] { BuiltinTypes.Float64 }));

            // rand.Read(p []byte) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Read",
                new[] { new ParameterSymbol("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));

            // rand.Shuffle(n int, swap func(i, j int))
            var swapFunc = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Int },
                Array.Empty<TypeSymbol>());
            pkg.AddExport(new FunctionSymbol("Shuffle",
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0),
                        new ParameterSymbol("swap", swapFunc, 1) },
                Array.Empty<TypeSymbol>()));

            // rand.New(src Source) *Rand  — simplified
            var sourceIface = new InterfaceTypeSymbol("Source", Array.Empty<MethodSymbol>());
            var randType = new StructTypeSymbol("Rand", Array.Empty<FieldSymbol>());
            randType.AddMethod(new MethodSymbol("Intn", randType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0) }, BuiltinTypes.Int));
            randType.AddMethod(new MethodSymbol("Int63n", randType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int64, 0) }, BuiltinTypes.Int64));
            randType.AddMethod(new MethodSymbol("Float64", randType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Float64));
            randType.AddMethod(new MethodSymbol("Float32", randType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Float32));
            randType.AddMethod(new MethodSymbol("Perm", randType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0) },
                new SliceTypeSymbol(BuiltinTypes.Int)));
            randType.AddMethod(new MethodSymbol("Uint32", randType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint32));
            randType.AddMethod(new MethodSymbol("Uint64", randType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint64));
            randType.AddMethod(new MethodSymbol("Int31", randType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int32));
            randType.AddMethod(new MethodSymbol("Int31n", randType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int32, 0) }, BuiltinTypes.Int32));
            randType.AddMethod(new MethodSymbol("Int63", randType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64));
            randType.AddMethod(new MethodSymbol("Int", randType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            randType.AddMethod(new MethodSymbol("Seed", randType, false,
                new[] { new ParameterSymbol("seed", BuiltinTypes.Int64, 0) },
                Array.Empty<TypeSymbol>()));
            randType.AddMethod(new MethodSymbol("Shuffle", randType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0),
                        new ParameterSymbol("swap", swapFunc, 1) },
                Array.Empty<TypeSymbol>()));
            randType.AddMethod(new MethodSymbol("Read", randType, false,
                new[] { new ParameterSymbol("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            pkg.AddExport(randType);
            pkg.AddExport(sourceIface);

            // Source64 interface (extends Source with Uint64 method)
            var source64Iface = new InterfaceTypeSymbol("Source64", Array.Empty<MethodSymbol>());
            source64Iface.SetMethods(new[]
            {
                new MethodSymbol("Uint64", source64Iface, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint64),
                new MethodSymbol("Int63", source64Iface, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64),
                new MethodSymbol("Seed", source64Iface, false,
                    new[] { new ParameterSymbol("seed", BuiltinTypes.Int64, 0) },
                    Array.Empty<TypeSymbol>()),
            });
            pkg.AddExport(source64Iface);

            pkg.AddExport(new FunctionSymbol("New",
                new[] { new ParameterSymbol("src", sourceIface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(randType) }));
            pkg.AddExport(new FunctionSymbol("NewSource",
                new[] { new ParameterSymbol("seed", BuiltinTypes.Int64, 0) },
                new TypeSymbol[] { sourceIface }));

            return pkg;
        }

        private static PackageSymbol CreateLogPackage()
        {
            var pkg = new PackageSymbol("log", "log");

            pkg.AddExport(new FunctionSymbol("Println", Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Print", Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Printf",
                new[] { new ParameterSymbol("format", BuiltinTypes.String, 0) },
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Fatal", Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Fatalf",
                new[] { new ParameterSymbol("format", BuiltinTypes.String, 0) },
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Fatalln", Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Panic", Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Panicf",
                new[] { new ParameterSymbol("format", BuiltinTypes.String, 0) },
                Array.Empty<TypeSymbol>(), isVariadic: true, packageName: "log"));

            // Package-level log functions
            pkg.AddExport(new FunctionSymbol("SetFlags",
                new[] { new ParameterSymbol("flag", BuiltinTypes.Int, 0) },
                Array.Empty<TypeSymbol>(), packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Flags",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Int }, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("SetOutput",
                new[] { new ParameterSymbol("w", new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>()), 0) },
                Array.Empty<TypeSymbol>(), packageName: "log"));
            pkg.AddExport(new FunctionSymbol("SetPrefix",
                new[] { new ParameterSymbol("prefix", BuiltinTypes.String, 0) },
                Array.Empty<TypeSymbol>(), packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Prefix",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.String }, packageName: "log"));
            pkg.AddExport(new FunctionSymbol("Output",
                new[] { new ParameterSymbol("calldepth", BuiltinTypes.Int, 0),
                        new ParameterSymbol("s", BuiltinTypes.String, 1) },
                new TypeSymbol[] { BuiltinTypes.Error }, packageName: "log"));

            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // Logger type
            var loggerType = new StructTypeSymbol("Logger", Array.Empty<FieldSymbol>());
            loggerType.AddMethod(new MethodSymbol("Println", loggerType, false,
                Array.Empty<TypeParameterSymbol>(), Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true));
            loggerType.AddMethod(new MethodSymbol("Printf", loggerType, false,
                Array.Empty<TypeParameterSymbol>(),
                new[] { new ParameterSymbol("format", BuiltinTypes.String, 0) },
                Array.Empty<TypeSymbol>(), isVariadic: true));
            loggerType.AddMethod(new MethodSymbol("Print", loggerType, false,
                Array.Empty<TypeParameterSymbol>(), Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true));
            loggerType.AddMethod(new MethodSymbol("Fatal", loggerType, false,
                Array.Empty<TypeParameterSymbol>(), Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), isVariadic: true));
            loggerType.AddMethod(new MethodSymbol("Fatalf", loggerType, false,
                Array.Empty<TypeParameterSymbol>(),
                new[] { new ParameterSymbol("format", BuiltinTypes.String, 0) },
                Array.Empty<TypeSymbol>(), isVariadic: true));
            loggerType.AddMethod(new MethodSymbol("SetOutput", loggerType, false,
                new[] { new ParameterSymbol("w", iface, 0) },
                BuiltinTypes.Void));
            loggerType.AddMethod(new MethodSymbol("SetPrefix", loggerType, false,
                new[] { new ParameterSymbol("prefix", BuiltinTypes.String, 0) },
                BuiltinTypes.Void));
            loggerType.AddMethod(new MethodSymbol("SetFlags", loggerType, false,
                new[] { new ParameterSymbol("flag", BuiltinTypes.Int, 0) },
                BuiltinTypes.Void));
            loggerType.AddMethod(new MethodSymbol("Output", loggerType, false,
                new[] { new ParameterSymbol("calldepth", BuiltinTypes.Int, 0),
                        new ParameterSymbol("s", BuiltinTypes.String, 1) },
                new TypeSymbol[] { BuiltinTypes.Error }));
            pkg.AddExport(loggerType);

            // log.New(out Writer, prefix string, flag int) *Logger
            var ptrLogger = new PointerTypeSymbol(loggerType);
            pkg.AddExport(new FunctionSymbol("New",
                new[] { new ParameterSymbol("out", iface, 0),
                        new ParameterSymbol("prefix", BuiltinTypes.String, 1),
                        new ParameterSymbol("flag", BuiltinTypes.Int, 2) },
                new TypeSymbol[] { ptrLogger }, packageName: "log"));

            // log flag constants
            pkg.AddExport(new PackageVarSymbol("Ldate", BuiltinTypes.UntypedInt, typeof(object), "Ldate"));
            pkg.AddExport(new PackageVarSymbol("Ltime", BuiltinTypes.UntypedInt, typeof(object), "Ltime"));
            pkg.AddExport(new PackageVarSymbol("Lmicroseconds", BuiltinTypes.UntypedInt, typeof(object), "Lmicroseconds"));
            pkg.AddExport(new PackageVarSymbol("Llongfile", BuiltinTypes.UntypedInt, typeof(object), "Llongfile"));
            pkg.AddExport(new PackageVarSymbol("Lshortfile", BuiltinTypes.UntypedInt, typeof(object), "Lshortfile"));
            pkg.AddExport(new PackageVarSymbol("LstdFlags", BuiltinTypes.UntypedInt, typeof(object), "LstdFlags"));

            return pkg;
        }

        private static PackageSymbol CreateIoPackage()
        {
            var pkg = new PackageSymbol("io", "io");

            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;
            var err = BuiltinTypes.Error;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            // Use empty interface type for Reader/Writer params (mapped to object)
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // io.Writer interface { Write(p []byte) (n int, err error) }
            var writerIface = new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>());
            var writeMethod = new MethodSymbol("Write", writerIface, false,
                new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err });
            writerIface.SetMethods(new[] { writeMethod });
            pkg.AddExport(writerIface);

            // io.Reader interface { Read(p []byte) (n int, err error) }
            var readerIface = new InterfaceTypeSymbol("Reader", Array.Empty<MethodSymbol>());
            var readMethod = new MethodSymbol("Read", readerIface, false,
                new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err });
            readerIface.SetMethods(new[] { readMethod });
            pkg.AddExport(readerIface);

            // io.Closer interface { Close() error }
            var closerIface = new InterfaceTypeSymbol("Closer", Array.Empty<MethodSymbol>());
            var closeMethod = new MethodSymbol("Close", closerIface, false,
                Array.Empty<ParameterSymbol>(), err);
            closerIface.SetMethods(new[] { closeMethod });
            pkg.AddExport(closerIface);

            // io.ReadCloser = Reader + Closer
            var readCloserIface = new InterfaceTypeSymbol("ReadCloser", Array.Empty<MethodSymbol>());
            readCloserIface.SetMethods(new[]
            {
                new MethodSymbol("Read", readCloserIface, false,
                    new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err }),
                new MethodSymbol("Close", readCloserIface, false,
                    Array.Empty<ParameterSymbol>(), err),
            });
            pkg.AddExport(readCloserIface);

            // io.WriteCloser = Writer + Closer
            var writeCloserIface = new InterfaceTypeSymbol("WriteCloser", Array.Empty<MethodSymbol>());
            writeCloserIface.SetMethods(new[]
            {
                new MethodSymbol("Write", writeCloserIface, false,
                    new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err }),
                new MethodSymbol("Close", writeCloserIface, false,
                    Array.Empty<ParameterSymbol>(), err),
            });
            pkg.AddExport(writeCloserIface);

            // io.ReadWriter = Reader + Writer
            var readWriterIface = new InterfaceTypeSymbol("ReadWriter", Array.Empty<MethodSymbol>());
            readWriterIface.SetMethods(new[]
            {
                new MethodSymbol("Read", readWriterIface, false,
                    new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err }),
                new MethodSymbol("Write", readWriterIface, false,
                    new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err }),
            });
            pkg.AddExport(readWriterIface);

            // io.Copy(dst Writer, src Reader) (int64, error)
            pkg.AddExport(new FunctionSymbol("Copy",
                new[] { new ParameterSymbol("dst", iface, 0),
                        new ParameterSymbol("src", iface, 1) },
                new TypeSymbol[] { i64, err }, packageName: "io"));

            // io.ReadAll(r Reader) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("ReadAll",
                new[] { new ParameterSymbol("r", iface, 0) },
                new TypeSymbol[] { byteSlice, err }, packageName: "io"));

            // io.ReadFull(r Reader, buf []byte) (int, error)
            pkg.AddExport(new FunctionSymbol("ReadFull",
                new[] { new ParameterSymbol("r", iface, 0),
                        new ParameterSymbol("buf", byteSlice, 1) },
                new TypeSymbol[] { i64, err }, packageName: "io"));

            // io.ReadAtLeast(r Reader, buf []byte, min int) (int, error)
            pkg.AddExport(new FunctionSymbol("ReadAtLeast",
                new[] { new ParameterSymbol("r", iface, 0),
                        new ParameterSymbol("buf", byteSlice, 1),
                        new ParameterSymbol("min", i64, 2) },
                new TypeSymbol[] { i64, err }, packageName: "io"));

            // io.WriteString(w Writer, s string) (int, error)
            pkg.AddExport(new FunctionSymbol("WriteString",
                new[] { new ParameterSymbol("w", iface, 0),
                        new ParameterSymbol("s", s, 1) },
                new TypeSymbol[] { i64, err }, packageName: "io"));

            // io.NopCloser(r Reader) ReadCloser
            pkg.AddExport(new FunctionSymbol("NopCloser",
                new[] { new ParameterSymbol("r", iface, 0) },
                new[] { iface }, packageName: "io"));

            // io.LimitReader(r Reader, n int64) Reader
            pkg.AddExport(new FunctionSymbol("LimitReader",
                new[] { new ParameterSymbol("r", iface, 0),
                        new ParameterSymbol("n", i64, 1) },
                new[] { iface }, packageName: "io"));

            // io.MultiReader(readers ...Reader) Reader
            pkg.AddExport(new FunctionSymbol("MultiReader",
                Array.Empty<ParameterSymbol>(),
                new[] { iface }, isVariadic: true, packageName: "io"));

            // io.MultiWriter(writers ...Writer) Writer
            pkg.AddExport(new FunctionSymbol("MultiWriter",
                Array.Empty<ParameterSymbol>(),
                new[] { iface }, isVariadic: true, packageName: "io"));

            // io.EOF — sentinel error value
            pkg.AddExport(new PackageVarSymbol("EOF", err, typeof(GoIo), "EOF"));

            // io.ErrUnexpectedEOF
            pkg.AddExport(new PackageVarSymbol("ErrUnexpectedEOF", err, typeof(GoIo), "EOF"));

            // io.ErrClosedPipe
            pkg.AddExport(new PackageVarSymbol("ErrClosedPipe", err, typeof(GoIo), "EOF"));

            // io.ErrShortWrite
            pkg.AddExport(new PackageVarSymbol("ErrShortWrite", err, typeof(GoIo), "EOF"));

            // io.ErrNoProgress
            pkg.AddExport(new PackageVarSymbol("ErrNoProgress", err, typeof(GoIo), "EOF"));

            // io.ErrShortBuffer
            pkg.AddExport(new PackageVarSymbol("ErrShortBuffer", err, typeof(GoIo), "EOF"));

            // io.Discard — Writer that discards all data
            pkg.AddExport(new PackageVarSymbol("Discard", iface,
                typeof(DiscardWriter), "Instance"));

            // io.WriterTo interface { WriteTo(w Writer) (int64, error) }
            var writerToIface = new InterfaceTypeSymbol("WriterTo", Array.Empty<MethodSymbol>());
            writerToIface.SetMethods(new[]
            {
                new MethodSymbol("WriteTo", writerToIface, false,
                    new[] { P("w", writerIface, 0) }, new TypeSymbol[] { BuiltinTypes.Int64, err }),
            });
            pkg.AddExport(writerToIface);

            // io.ReaderFrom interface { ReadFrom(r Reader) (int64, error) }
            var readerFromIface = new InterfaceTypeSymbol("ReaderFrom", Array.Empty<MethodSymbol>());
            readerFromIface.SetMethods(new[]
            {
                new MethodSymbol("ReadFrom", readerFromIface, false,
                    new[] { P("r", readerIface, 0) }, new TypeSymbol[] { BuiltinTypes.Int64, err }),
            });
            pkg.AddExport(readerFromIface);

            // io.ReaderAt interface { ReadAt(p []byte, off int64) (n int, err error) }
            var readerAtIface = new InterfaceTypeSymbol("ReaderAt", Array.Empty<MethodSymbol>());
            readerAtIface.SetMethods(new[]
            {
                new MethodSymbol("ReadAt", readerAtIface, false,
                    new[] { P("p", byteSlice, 0), P("off", BuiltinTypes.Int64, 1) },
                    new TypeSymbol[] { i64, err }),
            });
            pkg.AddExport(readerAtIface);

            // io.WriterAt interface { WriteAt(p []byte, off int64) (n int, err error) }
            var writerAtIface = new InterfaceTypeSymbol("WriterAt", Array.Empty<MethodSymbol>());
            writerAtIface.SetMethods(new[]
            {
                new MethodSymbol("WriteAt", writerAtIface, false,
                    new[] { P("p", byteSlice, 0), P("off", BuiltinTypes.Int64, 1) },
                    new TypeSymbol[] { i64, err }),
            });
            pkg.AddExport(writerAtIface);

            // io.Seeker interface { Seek(offset int64, whence int) (int64, error) }
            var seekerIface = new InterfaceTypeSymbol("Seeker", Array.Empty<MethodSymbol>());
            seekerIface.SetMethods(new[]
            {
                new MethodSymbol("Seek", seekerIface, false,
                    new[] { P("offset", BuiltinTypes.Int64, 0), P("whence", i64, 1) },
                    new TypeSymbol[] { BuiltinTypes.Int64, err }),
            });
            pkg.AddExport(seekerIface);

            // io.ReadSeeker = Reader + Seeker
            var readSeekerIface = new InterfaceTypeSymbol("ReadSeeker", Array.Empty<MethodSymbol>());
            readSeekerIface.SetMethods(new[]
            {
                new MethodSymbol("Read", readSeekerIface, false,
                    new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err }),
                new MethodSymbol("Seek", readSeekerIface, false,
                    new[] { P("offset", BuiltinTypes.Int64, 0), P("whence", i64, 1) },
                    new TypeSymbol[] { BuiltinTypes.Int64, err }),
            });
            pkg.AddExport(readSeekerIface);

            // io.ReadWriteCloser = Reader + Writer + Closer
            var readWriteCloserIface = new InterfaceTypeSymbol("ReadWriteCloser", Array.Empty<MethodSymbol>());
            readWriteCloserIface.SetMethods(new[]
            {
                new MethodSymbol("Read", readWriteCloserIface, false,
                    new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err }),
                new MethodSymbol("Write", readWriteCloserIface, false,
                    new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err }),
                new MethodSymbol("Close", readWriteCloserIface, false,
                    Array.Empty<ParameterSymbol>(), err),
            });
            pkg.AddExport(readWriteCloserIface);

            // io.ByteReader interface { ReadByte() (byte, error) }
            var byteReaderIface = new InterfaceTypeSymbol("ByteReader", Array.Empty<MethodSymbol>());
            byteReaderIface.SetMethods(new[]
            {
                new MethodSymbol("ReadByte", byteReaderIface, false,
                    Array.Empty<ParameterSymbol>(), new TypeSymbol[] { BuiltinTypes.Byte, err }),
            });
            pkg.AddExport(byteReaderIface);

            // io.ByteWriter interface { WriteByte(c byte) error }
            var byteWriterIface = new InterfaceTypeSymbol("ByteWriter", Array.Empty<MethodSymbol>());
            byteWriterIface.SetMethods(new[]
            {
                new MethodSymbol("WriteByte", byteWriterIface, false,
                    new[] { P("c", BuiltinTypes.Byte, 0) }, err),
            });
            pkg.AddExport(byteWriterIface);

            // io.StringWriter interface { WriteString(s string) (n int, err error) }
            var stringWriterIface = new InterfaceTypeSymbol("StringWriter", Array.Empty<MethodSymbol>());
            stringWriterIface.SetMethods(new[]
            {
                new MethodSymbol("WriteString", stringWriterIface, false,
                    new[] { P("s", s, 0) }, new TypeSymbol[] { i64, err }),
            });
            pkg.AddExport(stringWriterIface);

            // io.Pipe() (*PipeReader, *PipeWriter)
            var pipeReaderType = new StructTypeSymbol("PipeReader", Array.Empty<FieldSymbol>());
            pipeReaderType.AddMethod(new MethodSymbol("Read", pipeReaderType, false,
                new[] { P("data", byteSlice, 0) }, new TypeSymbol[] { i64, err }));
            pipeReaderType.AddMethod(new MethodSymbol("Close", pipeReaderType, false,
                Array.Empty<ParameterSymbol>(), err));
            pipeReaderType.AddMethod(new MethodSymbol("CloseWithError", pipeReaderType, false,
                new[] { P("err", err, 0) }, err));
            pkg.AddExport(pipeReaderType);

            var pipeWriterType = new StructTypeSymbol("PipeWriter", Array.Empty<FieldSymbol>());
            pipeWriterType.AddMethod(new MethodSymbol("Write", pipeWriterType, false,
                new[] { P("data", byteSlice, 0) }, new TypeSymbol[] { i64, err }));
            pipeWriterType.AddMethod(new MethodSymbol("Close", pipeWriterType, false,
                Array.Empty<ParameterSymbol>(), err));
            pipeWriterType.AddMethod(new MethodSymbol("CloseWithError", pipeWriterType, false,
                new[] { P("err", err, 0) }, err));
            pkg.AddExport(pipeWriterType);

            pkg.AddExport(new FunctionSymbol("Pipe",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new PointerTypeSymbol(pipeReaderType), new PointerTypeSymbol(pipeWriterType) },
                packageName: "io"));

            // io.TeeReader(r Reader, w Writer) Reader
            pkg.AddExport(new FunctionSymbol("TeeReader",
                new[] { P("r", iface, 0), P("w", iface, 1) },
                new[] { iface }, packageName: "io"));

            // io.CopyN(dst Writer, src Reader, n int64) (written int64, err error)
            pkg.AddExport(new FunctionSymbol("CopyN",
                new[] { P("dst", iface, 0), P("src", iface, 1), P("n", BuiltinTypes.Int64, 2) },
                new TypeSymbol[] { BuiltinTypes.Int64, err }, packageName: "io"));

            // io.CopyBuffer(dst Writer, src Reader, buf []byte) (written int64, err error)
            pkg.AddExport(new FunctionSymbol("CopyBuffer",
                new[] { P("dst", iface, 0), P("src", iface, 1), P("buf", byteSlice, 2) },
                new TypeSymbol[] { BuiltinTypes.Int64, err }, packageName: "io"));

            // io.NewSectionReader(r ReaderAt, off int64, n int64) *SectionReader
            var sectionReaderType = new StructTypeSymbol("SectionReader", Array.Empty<FieldSymbol>());
            sectionReaderType.AddMethod(new MethodSymbol("Read", sectionReaderType, false,
                new[] { P("p", byteSlice, 0) }, new TypeSymbol[] { i64, err }));
            sectionReaderType.AddMethod(new MethodSymbol("ReadAt", sectionReaderType, false,
                new[] { P("p", byteSlice, 0), P("off", BuiltinTypes.Int64, 1) },
                new TypeSymbol[] { i64, err }));
            sectionReaderType.AddMethod(new MethodSymbol("Seek", sectionReaderType, false,
                new[] { P("offset", BuiltinTypes.Int64, 0), P("whence", i64, 1) },
                new TypeSymbol[] { BuiltinTypes.Int64, err }));
            sectionReaderType.AddMethod(new MethodSymbol("Size", sectionReaderType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64));
            pkg.AddExport(sectionReaderType);

            pkg.AddExport(new FunctionSymbol("NewSectionReader",
                new[] { P("r", iface, 0), P("off", BuiltinTypes.Int64, 1), P("n", BuiltinTypes.Int64, 2) },
                new TypeSymbol[] { new PointerTypeSymbol(sectionReaderType) }, packageName: "io"));

            // Seek constants
            pkg.AddExport(new ConstantSymbol("SeekStart", BuiltinTypes.Int, (long)0));
            pkg.AddExport(new ConstantSymbol("SeekCurrent", BuiltinTypes.Int, (long)1));
            pkg.AddExport(new ConstantSymbol("SeekEnd", BuiltinTypes.Int, (long)2));

            return pkg;
        }

        private static PackageSymbol CreateBufioPackage()
        {
            var pkg = new PackageSymbol("bufio", "bufio");

            var s = BuiltinTypes.String;

            // Use empty interface for Reader/Writer params
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // Scanner type with Scan() and Text() methods
            var scannerType = new StructTypeSymbol("Scanner", Array.Empty<FieldSymbol>());
            scannerType.AddMethod(new MethodSymbol("Scan", scannerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            scannerType.AddMethod(new MethodSymbol("Text", scannerType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(scannerType);

            // Reader type with methods
            var readerType = new StructTypeSymbol("Reader", Array.Empty<FieldSymbol>());
            readerType.AddMethod(new MethodSymbol("ReadString", readerType, false,
                new[] { new ParameterSymbol("delim", BuiltinTypes.Uint8, 0) },
                new TypeSymbol[] { s, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("ReadLine", readerType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Bool, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("ReadByte", readerType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Byte, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("ReadRune", readerType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Rune, BuiltinTypes.Int, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("UnreadByte", readerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            readerType.AddMethod(new MethodSymbol("Peek", readerType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("Buffered", readerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            readerType.AddMethod(new MethodSymbol("Read", readerType, false,
                new[] { new ParameterSymbol("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("Reset", readerType, false,
                new[] { new ParameterSymbol("r", iface, 0) },
                BuiltinTypes.Void));
            readerType.AddMethod(new MethodSymbol("ReadBytes", readerType, false,
                new[] { new ParameterSymbol("delim", BuiltinTypes.Uint8, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("Discard", readerType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("UnreadRune", readerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            readerType.AddMethod(new MethodSymbol("WriteTo", readerType, false,
                new[] { new ParameterSymbol("w", iface, 0) },
                new TypeSymbol[] { BuiltinTypes.Int64, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("Size", readerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            readerType.AddMethod(new MethodSymbol("ReadSlice", readerType, false,
                new[] { new ParameterSymbol("delim", BuiltinTypes.Uint8, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error }));
            pkg.AddExport(readerType);

            // Writer type with methods
            var writerType = new StructTypeSymbol("Writer", Array.Empty<FieldSymbol>());
            writerType.AddMethod(new MethodSymbol("Flush", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            writerType.AddMethod(new MethodSymbol("Write", writerType, false,
                new[] { new ParameterSymbol("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            writerType.AddMethod(new MethodSymbol("WriteString", writerType, false,
                new[] { new ParameterSymbol("s", s, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            writerType.AddMethod(new MethodSymbol("WriteByte", writerType, false,
                new[] { new ParameterSymbol("c", BuiltinTypes.Byte, 0) },
                BuiltinTypes.Error));
            writerType.AddMethod(new MethodSymbol("WriteRune", writerType, false,
                new[] { new ParameterSymbol("r", BuiltinTypes.Rune, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            writerType.AddMethod(new MethodSymbol("Buffered", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            writerType.AddMethod(new MethodSymbol("Available", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            writerType.AddMethod(new MethodSymbol("Reset", writerType, false,
                new[] { new ParameterSymbol("w", iface, 0) },
                BuiltinTypes.Void));
            pkg.AddExport(writerType);

            // bufio.NewScanner(r Reader) *Scanner
            var ptrScannerType = new PointerTypeSymbol(scannerType);
            pkg.AddExport(new FunctionSymbol("NewScanner",
                new[] { new ParameterSymbol("r", iface, 0) },
                new TypeSymbol[] { ptrScannerType }, packageName: "bufio"));

            // bufio.NewReader(r Reader) *Reader
            var ptrReaderType = new PointerTypeSymbol(readerType);
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { new ParameterSymbol("r", iface, 0) },
                new TypeSymbol[] { ptrReaderType }, packageName: "bufio"));

            // bufio.NewReaderSize(rd Reader, size int) *Reader
            pkg.AddExport(new FunctionSymbol("NewReaderSize",
                new[] { new ParameterSymbol("rd", iface, 0),
                        new ParameterSymbol("size", BuiltinTypes.Int, 1) },
                new TypeSymbol[] { ptrReaderType }, packageName: "bufio"));

            // bufio.NewWriter(w Writer) *Writer
            var ptrWriterType = new PointerTypeSymbol(writerType);
            pkg.AddExport(new FunctionSymbol("NewWriter",
                new[] { new ParameterSymbol("w", iface, 0) },
                new TypeSymbol[] { ptrWriterType }, packageName: "bufio"));

            // Scanner split functions
            var splitFunc = new FunctionTypeSymbol(
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Bool },
                new TypeSymbol[] { BuiltinTypes.Int, new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error });

            scannerType.AddMethod(new MethodSymbol("Split", scannerType, false,
                new[] { new ParameterSymbol("split", splitFunc, 0) }, BuiltinTypes.Void));
            scannerType.AddMethod(new MethodSymbol("Bytes", scannerType, false,
                Array.Empty<ParameterSymbol>(), new SliceTypeSymbol(BuiltinTypes.Byte)));
            scannerType.AddMethod(new MethodSymbol("Err", scannerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            scannerType.AddMethod(new MethodSymbol("Buffer", scannerType, false,
                new[] { new ParameterSymbol("buf", new SliceTypeSymbol(BuiltinTypes.Byte), 0),
                        new ParameterSymbol("max", BuiltinTypes.Int, 1) },
                BuiltinTypes.Void));

            // Split function vars
            pkg.AddExport(new PackageVarSymbol("ScanLines", splitFunc,
                typeof(Ngo.Runtime.GoBufio), "ScanLines"));
            pkg.AddExport(new PackageVarSymbol("ScanWords", splitFunc,
                typeof(Ngo.Runtime.GoBufio), "ScanWords"));
            pkg.AddExport(new PackageVarSymbol("ScanBytes", splitFunc,
                typeof(Ngo.Runtime.GoBufio), "ScanBytes"));
            pkg.AddExport(new PackageVarSymbol("ScanRunes", splitFunc,
                typeof(Ngo.Runtime.GoBufio), "ScanRunes"));

            // MaxScanTokenSize constant
            pkg.AddExport(new ConstantSymbol("MaxScanTokenSize", BuiltinTypes.Int, (long)64 * 1024));

            // ErrBufferFull is returned when the buffer is full
            pkg.AddExport(new PackageVarSymbol("ErrBufferFull", BuiltinTypes.Error));

            // ErrFinalToken is a sentinel error for Scanner.Split functions
            pkg.AddExport(new PackageVarSymbol("ErrFinalToken", BuiltinTypes.Error));

            // ErrTooLong is returned when a token is too large
            pkg.AddExport(new PackageVarSymbol("ErrTooLong", BuiltinTypes.Error));

            // ErrNegativeAdvance, ErrAdvanceTooFar, ErrBadReadCount
            pkg.AddExport(new PackageVarSymbol("ErrNegativeAdvance", BuiltinTypes.Error));
            pkg.AddExport(new PackageVarSymbol("ErrAdvanceTooFar", BuiltinTypes.Error));
            pkg.AddExport(new PackageVarSymbol("ErrBadReadCount", BuiltinTypes.Error));

            // bufio.NewWriterSize(w Writer, size int) *Writer
            pkg.AddExport(new FunctionSymbol("NewWriterSize",
                new[] { new ParameterSymbol("w", iface, 0),
                        new ParameterSymbol("size", BuiltinTypes.Int, 1) },
                new TypeSymbol[] { ptrWriterType }, packageName: "bufio"));

            return pkg;
        }

        private static PackageSymbol CreateFilepathPackage()
        {
            var pkg = new PackageSymbol("filepath", "path/filepath");

            var s = BuiltinTypes.String;
            var b = BuiltinTypes.Bool;

            // filepath.Join(elem ...string) string — variadic
            pkg.AddExport(new FunctionSymbol("Join",
                Array.Empty<ParameterSymbol>(),
                new[] { s }, isVariadic: true, packageName: "filepath"));

            // filepath.Dir(path string) string
            pkg.AddExport(new FunctionSymbol("Dir",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "filepath"));

            // filepath.Base(path string) string
            pkg.AddExport(new FunctionSymbol("Base",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "filepath"));

            // filepath.Ext(path string) string
            pkg.AddExport(new FunctionSymbol("Ext",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "filepath"));

            // filepath.IsAbs(path string) bool
            pkg.AddExport(new FunctionSymbol("IsAbs",
                new[] { P("path", s, 0) }, new[] { b }, packageName: "filepath"));

            // filepath.Abs(path string) (string, error)
            pkg.AddExport(new FunctionSymbol("Abs",
                new[] { P("path", s, 0) },
                new TypeSymbol[] { s, BuiltinTypes.Error }, packageName: "filepath"));

            // filepath.Rel(basepath, targpath string) (string, error)
            pkg.AddExport(new FunctionSymbol("Rel",
                new[] { P("basepath", s, 0), P("targpath", s, 1) },
                new TypeSymbol[] { s, BuiltinTypes.Error }, packageName: "filepath"));

            // filepath.Match(pattern, name string) (bool, error)
            pkg.AddExport(new FunctionSymbol("Match",
                new[] { P("pattern", s, 0), P("name", s, 1) },
                new TypeSymbol[] { b, BuiltinTypes.Error }, packageName: "filepath"));

            // filepath.Glob(pattern string) ([]string, error)
            var sliceString = new SliceTypeSymbol(s);
            pkg.AddExport(new FunctionSymbol("Glob",
                new[] { P("pattern", s, 0) },
                new TypeSymbol[] { sliceString, BuiltinTypes.Error }, packageName: "filepath"));

            // filepath.Clean(path string) string
            pkg.AddExport(new FunctionSymbol("Clean",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "filepath"));

            // filepath.Split(path string) (dir, file string)
            pkg.AddExport(new FunctionSymbol("Split",
                new[] { P("path", s, 0) },
                new TypeSymbol[] { s, s }, packageName: "filepath"));

            // filepath.ToSlash(path string) string
            pkg.AddExport(new FunctionSymbol("ToSlash",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "filepath"));

            // filepath.FromSlash(path string) string
            pkg.AddExport(new FunctionSymbol("FromSlash",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "filepath"));

            // filepath.HasPrefix(p, prefix string) bool
            pkg.AddExport(new FunctionSymbol("HasPrefix",
                new[] { P("p", s, 0), P("prefix", s, 1) },
                new[] { b }, packageName: "filepath"));

            // filepath.VolumeName(path string) string
            pkg.AddExport(new FunctionSymbol("VolumeName",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "filepath"));

            // filepath.EvalSymlinks(path string) (string, error)
            pkg.AddExport(new FunctionSymbol("EvalSymlinks",
                new[] { P("path", s, 0) },
                new TypeSymbol[] { s, BuiltinTypes.Error }, packageName: "filepath"));

            // filepath.Walk(root string, fn WalkFunc) error
            var walkFuncType = new FunctionTypeSymbol(
                new TypeSymbol[] { s, BuiltinTypes.EmptyInterface, BuiltinTypes.Error },
                new TypeSymbol[] { BuiltinTypes.Error });
            pkg.AddExport(new FunctionSymbol("Walk",
                new[] { P("root", s, 0), P("fn", walkFuncType, 1) },
                new TypeSymbol[] { BuiltinTypes.Error }, packageName: "filepath"));

            // filepath.WalkDir(root string, fn WalkDirFunc) error
            pkg.AddExport(new FunctionSymbol("WalkDir",
                new[] { P("root", s, 0), P("fn", walkFuncType, 1) },
                new TypeSymbol[] { BuiltinTypes.Error }, packageName: "filepath"));

            // filepath.Separator and ListSeparator constants
            pkg.AddExport(new ConstantSymbol("Separator", BuiltinTypes.Rune, null));
            pkg.AddExport(new ConstantSymbol("ListSeparator", BuiltinTypes.Rune, null));

            // filepath.SkipDir — sentinel error to skip directory in Walk
            pkg.AddExport(new PackageVarSymbol("SkipDir", BuiltinTypes.Error));

            // filepath.SkipAll — sentinel error to stop Walk entirely (Go 1.20)
            pkg.AddExport(new PackageVarSymbol("SkipAll", BuiltinTypes.Error));

            // filepath.ErrBadPattern
            pkg.AddExport(new PackageVarSymbol("ErrBadPattern", BuiltinTypes.Error));

            // filepath.SplitList(path string) []string
            pkg.AddExport(new FunctionSymbol("SplitList",
                new[] { P("path", s, 0) }, new[] { sliceString }, packageName: "filepath"));

            return pkg;
        }

        private static PackageSymbol CreateRegexpPackage()
        {
            var pkg = new PackageSymbol("regexp", "regexp");

            var s = BuiltinTypes.String;
            var b = BuiltinTypes.Bool;
            var i = BuiltinTypes.Int;
            var sliceString = new SliceTypeSymbol(s);

            // Regexp type with methods
            var regexpType = new StructTypeSymbol("Regexp", Array.Empty<FieldSymbol>());
            regexpType.AddMethod(new MethodSymbol("MatchString", regexpType, false,
                new[] { P("s", s, 0) }, b));
            regexpType.AddMethod(new MethodSymbol("FindString", regexpType, false,
                new[] { P("s", s, 0) }, s));
            regexpType.AddMethod(new MethodSymbol("FindAllString", regexpType, false,
                new[] { P("s", s, 0), P("n", i, 1) },
                new TypeSymbol[] { sliceString }));
            regexpType.AddMethod(new MethodSymbol("ReplaceAllString", regexpType, false,
                new[] { P("src", s, 0), P("repl", s, 1) }, s));
            regexpType.AddMethod(new MethodSymbol("ReplaceAllLiteralString", regexpType, false,
                new[] { P("src", s, 0), P("repl", s, 1) }, s));
            regexpType.AddMethod(new MethodSymbol("Split", regexpType, false,
                new[] { P("s", s, 0), P("n", i, 1) },
                new TypeSymbol[] { sliceString }));
            regexpType.AddMethod(new MethodSymbol("FindStringSubmatch", regexpType, false,
                new[] { P("s", s, 0) },
                new TypeSymbol[] { sliceString }));
            regexpType.AddMethod(new MethodSymbol("FindStringIndex", regexpType, false,
                new[] { P("s", s, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(i) }));
            regexpType.AddMethod(new MethodSymbol("FindAllStringSubmatch", regexpType, false,
                new[] { P("s", s, 0), P("n", i, 1) },
                new TypeSymbol[] { new SliceTypeSymbol(sliceString) }));
            regexpType.AddMethod(new MethodSymbol("ReplaceAllStringFunc", regexpType, false,
                new[] { P("src", s, 0), P("repl", new FunctionTypeSymbol(
                    new TypeSymbol[] { s }, new TypeSymbol[] { s }), 1) }, s));
            regexpType.AddMethod(new MethodSymbol("FindAllStringIndex", regexpType, false,
                new[] { P("s", s, 0), P("n", i, 1) },
                new TypeSymbol[] { new SliceTypeSymbol(new SliceTypeSymbol(i)) }));
            regexpType.AddMethod(new MethodSymbol("NumSubexp", regexpType, false,
                Array.Empty<ParameterSymbol>(), i));
            regexpType.AddMethod(new MethodSymbol("SubexpNames", regexpType, false,
                Array.Empty<ParameterSymbol>(), sliceString));
            regexpType.AddMethod(new MethodSymbol("LiteralPrefix", regexpType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s, b }));

            // Byte-oriented methods
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var intSlice = new SliceTypeSymbol(i);
            regexpType.AddMethod(new MethodSymbol("Match", regexpType, false,
                new[] { new ParameterSymbol("b", byteSlice, 0) }, b));
            regexpType.AddMethod(new MethodSymbol("Find", regexpType, false,
                new[] { new ParameterSymbol("b", byteSlice, 0) }, byteSlice));
            regexpType.AddMethod(new MethodSymbol("FindIndex", regexpType, false,
                new[] { new ParameterSymbol("b", byteSlice, 0) },
                new TypeSymbol[] { intSlice }));
            regexpType.AddMethod(new MethodSymbol("FindAll", regexpType, false,
                new[] { new ParameterSymbol("b", byteSlice, 0), new ParameterSymbol("n", i, 1) },
                new TypeSymbol[] { new SliceTypeSymbol(byteSlice) }));
            regexpType.AddMethod(new MethodSymbol("FindAllIndex", regexpType, false,
                new[] { new ParameterSymbol("b", byteSlice, 0), new ParameterSymbol("n", i, 1) },
                new TypeSymbol[] { new SliceTypeSymbol(intSlice) }));
            regexpType.AddMethod(new MethodSymbol("FindSubmatch", regexpType, false,
                new[] { new ParameterSymbol("b", byteSlice, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(byteSlice) }));
            regexpType.AddMethod(new MethodSymbol("FindAllSubmatch", regexpType, false,
                new[] { new ParameterSymbol("b", byteSlice, 0), new ParameterSymbol("n", i, 1) },
                new TypeSymbol[] { new SliceTypeSymbol(new SliceTypeSymbol(byteSlice)) }));
            regexpType.AddMethod(new MethodSymbol("ReplaceAll", regexpType, false,
                new[] { new ParameterSymbol("src", byteSlice, 0), new ParameterSymbol("repl", byteSlice, 1) },
                byteSlice));
            regexpType.AddMethod(new MethodSymbol("ReplaceAllLiteral", regexpType, false,
                new[] { new ParameterSymbol("src", byteSlice, 0), new ParameterSymbol("repl", byteSlice, 1) },
                byteSlice));
            regexpType.AddMethod(new MethodSymbol("ReplaceAllFunc", regexpType, false,
                new[] { new ParameterSymbol("src", byteSlice, 0), new ParameterSymbol("repl",
                    new FunctionTypeSymbol(new TypeSymbol[] { byteSlice }, new TypeSymbol[] { byteSlice }), 1) },
                byteSlice));
            regexpType.AddMethod(new MethodSymbol("String", regexpType, false,
                Array.Empty<ParameterSymbol>(), s));
            regexpType.AddMethod(new MethodSymbol("Longest", regexpType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            regexpType.AddMethod(new MethodSymbol("ExpandString", regexpType, false,
                new[] { new ParameterSymbol("dst", byteSlice, 0), new ParameterSymbol("template", s, 1),
                        new ParameterSymbol("src", s, 2), new ParameterSymbol("match", intSlice, 3) },
                byteSlice));
            regexpType.AddMethod(new MethodSymbol("SubexpIndex", regexpType, false,
                new[] { new ParameterSymbol("name", s, 0) }, i));

            pkg.AddExport(regexpType);

            // regexp.Compile(expr string) (*Regexp, error)
            var ptrRegexp = new PointerTypeSymbol(regexpType);
            pkg.AddExport(new FunctionSymbol("Compile",
                new[] { P("expr", s, 0) },
                new TypeSymbol[] { ptrRegexp, BuiltinTypes.Error }, packageName: "regexp"));

            // regexp.MustCompile(expr string) *Regexp
            pkg.AddExport(new FunctionSymbol("MustCompile",
                new[] { P("expr", s, 0) },
                new TypeSymbol[] { ptrRegexp }, packageName: "regexp"));

            // regexp.MatchString(pattern, s string) (bool, error)
            pkg.AddExport(new FunctionSymbol("MatchString",
                new[] { P("pattern", s, 0), P("s", s, 1) },
                new TypeSymbol[] { b, BuiltinTypes.Error }, packageName: "regexp"));

            // regexp.QuoteMeta(s string) string
            pkg.AddExport(new FunctionSymbol("QuoteMeta",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { s }, packageName: "regexp"));

            return pkg;
        }

        private static PackageSymbol CreateUnicodePackage()
        {
            var pkg = new PackageSymbol("unicode", "unicode");

            var r = BuiltinTypes.Rune;
            var b = BuiltinTypes.Bool;

            pkg.AddExport(new FunctionSymbol("IsLetter",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsDigit",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsSpace",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsUpper",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsLower",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsPunct",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsControl",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("ToUpper",
                new[] { P("r", r, 0) }, new[] { r }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("ToLower",
                new[] { P("r", r, 0) }, new[] { r }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsNumber",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsGraphic",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsPrint",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsTitle",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsSymbol",
                new[] { P("r", r, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("ToTitle",
                new[] { P("r", r, 0) }, new[] { r }, packageName: "unicode"));

            // unicode.In(r rune, ranges ...*RangeTable) bool
            var rangeTableType = new StructTypeSymbol("RangeTable", new List<FieldSymbol>());
            pkg.AddExport(rangeTableType);
            var rangeTablePtr = new PointerTypeSymbol(rangeTableType);
            pkg.AddExport(new FunctionSymbol("In",
                new[] { P("r", r, 0), P("ranges", rangeTablePtr, 1) },
                new[] { b }, packageName: "unicode", isVariadic: true));
            pkg.AddExport(new FunctionSymbol("Is",
                new[] { P("rangeTab", rangeTablePtr, 0), P("r", r, 1) },
                new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsOneOf",
                new[] { P("ranges", new SliceTypeSymbol(rangeTablePtr), 0), P("r", r, 1) },
                new[] { b }, packageName: "unicode"));

            // Common RangeTable vars
            foreach (var name in new[] {
                "Letter", "Upper", "Lower", "Title", "Number", "Digit",
                "Mark", "Punct", "Symbol", "Space", "Cc", "Cf",
                "Co", "Cs", "Nd", "Nl", "No", "Mn", "Me", "Mc",
                "Ll", "Lu", "Lt", "Lm", "Lo",
                "Pc", "Pd", "Pe", "Pf", "Pi", "Po", "Ps",
                "Sc", "Sk", "Sm", "So",
                "Zl", "Zp", "Zs",
                "Latin", "Greek", "Cyrillic", "Han", "Hiragana", "Katakana",
                "Arabic", "Hebrew", "Thai", "Devanagari",
                "Common", "Inherited" })
            {
                pkg.AddExport(new PackageVarSymbol(name, rangeTablePtr, typeof(GoUnicode), name));
            }

            // unicode.MaxASCII = '\u007F' (127)
            pkg.AddExport(new ConstantSymbol("MaxASCII", r, (long)0x7F));
            // unicode.MaxRune = '\U0010FFFF'
            pkg.AddExport(new ConstantSymbol("MaxRune", r, (long)0x10FFFF));
            // unicode.MaxLatin1 = '\u00FF' (255)
            pkg.AddExport(new ConstantSymbol("MaxLatin1", r, (long)0xFF));
            // unicode.ReplacementChar
            pkg.AddExport(new ConstantSymbol("ReplacementChar", r, (long)0xFFFD));

            return pkg;
        }

        private static PackageSymbol CreateUtf8Package()
        {
            var pkg = new PackageSymbol("utf8", "unicode/utf8");

            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;
            var r = BuiltinTypes.Rune;
            var b = BuiltinTypes.Bool;

            // utf8.RuneCount(p []byte) int
            pkg.AddExport(new FunctionSymbol("RuneCount",
                new[] { P("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new[] { i }, packageName: "utf8"));

            // utf8.RuneCountInString(s string) int
            pkg.AddExport(new FunctionSymbol("RuneCountInString",
                new[] { P("s", s, 0) }, new[] { i }, packageName: "utf8"));

            // utf8.ValidString(s string) bool
            pkg.AddExport(new FunctionSymbol("ValidString",
                new[] { P("s", s, 0) }, new[] { b }, packageName: "utf8"));

            // utf8.DecodeRuneInString(s string) (rune, int)
            pkg.AddExport(new FunctionSymbol("DecodeRuneInString",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { r, i }, packageName: "utf8"));

            // utf8.DecodeRune(p []byte) (rune, int)
            pkg.AddExport(new FunctionSymbol("DecodeRune",
                new[] { P("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new TypeSymbol[] { r, i }, packageName: "utf8"));

            // utf8.DecodeLastRune(p []byte) (rune, int)
            pkg.AddExport(new FunctionSymbol("DecodeLastRune",
                new[] { P("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new TypeSymbol[] { r, i }, packageName: "utf8"));

            // utf8.DecodeLastRuneInString(s string) (rune, int)
            pkg.AddExport(new FunctionSymbol("DecodeLastRuneInString",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { r, i }, packageName: "utf8"));

            // utf8.EncodeRune(p []byte, r rune) int
            pkg.AddExport(new FunctionSymbol("EncodeRune",
                new[] { P("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0), P("r", r, 1) },
                new[] { i }, packageName: "utf8"));

            // utf8.Valid(p []byte) bool
            pkg.AddExport(new FunctionSymbol("Valid",
                new[] { P("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new[] { b }, packageName: "utf8"));

            // utf8.RuneLen(r rune) int
            pkg.AddExport(new FunctionSymbol("RuneLen",
                new[] { P("r", r, 0) }, new[] { i }, packageName: "utf8"));

            // utf8.FullRune(p []byte) bool
            pkg.AddExport(new FunctionSymbol("FullRune",
                new[] { P("p", new SliceTypeSymbol(BuiltinTypes.Byte), 0) },
                new[] { b }, packageName: "utf8"));

            // utf8.FullRuneInString(s string) bool
            pkg.AddExport(new FunctionSymbol("FullRuneInString",
                new[] { P("s", s, 0) },
                new[] { b }, packageName: "utf8"));

            // utf8.ValidRune(r rune) bool
            pkg.AddExport(new FunctionSymbol("ValidRune",
                new[] { P("r", r, 0) },
                new[] { b }, packageName: "utf8"));

            // Constants
            pkg.AddExport(new ConstantSymbol("RuneError", r, (long)0xFFFD));
            pkg.AddExport(new ConstantSymbol("MaxRune", r, (long)0x10FFFF));
            pkg.AddExport(new ConstantSymbol("UTFMax", i, (long)4));
            pkg.AddExport(new ConstantSymbol("RuneSelf", i, (long)0x80));

            return pkg;
        }

        private static PackageSymbol CreateUtf16Package()
        {
            var pkg = new PackageSymbol("utf16", "unicode/utf16");

            var i = BuiltinTypes.Int;
            var r = BuiltinTypes.Rune;

            // Decode(s []uint16) []rune
            pkg.AddExport(new FunctionSymbol("Decode",
                new[] { P("s", new SliceTypeSymbol(BuiltinTypes.Uint16), 0) },
                new TypeSymbol[] { new SliceTypeSymbol(r) }, packageName: "utf16"));
            // Encode(s []rune) []uint16
            pkg.AddExport(new FunctionSymbol("Encode",
                new[] { P("s", new SliceTypeSymbol(r), 0) },
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Uint16) }, packageName: "utf16"));
            // DecodeRune(r1, r2 rune) rune
            pkg.AddExport(new FunctionSymbol("DecodeRune",
                new[] { P("r1", r, 0), P("r2", r, 1) },
                new TypeSymbol[] { r }, packageName: "utf16"));
            // EncodeRune(r rune) (r1, r2 rune)
            pkg.AddExport(new FunctionSymbol("EncodeRune",
                new[] { P("r", r, 0) },
                new TypeSymbol[] { r, r }, packageName: "utf16"));
            // IsSurrogate(r rune) bool
            pkg.AddExport(new FunctionSymbol("IsSurrogate",
                new[] { P("r", r, 0) },
                new TypeSymbol[] { BuiltinTypes.Bool }, packageName: "utf16"));

            return pkg;
        }

        private static ParameterSymbol P(string name, TypeSymbol type, int ordinal) =>
            new ParameterSymbol(name, type, ordinal);

        private static MethodSymbol[] CreateHashMethods()
        {
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            return new[]
            {
                new MethodSymbol("Write", null!, false,
                    new[] { P("p", byteSlice, 0) },
                    new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }),
                new MethodSymbol("Sum", null!, false,
                    new[] { P("b", byteSlice, 0) }, byteSlice),
                new MethodSymbol("Reset", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void),
                new MethodSymbol("Size", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
                new MethodSymbol("BlockSize", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
            };
        }

        private static InterfaceTypeSymbol CreateHashType(string name = "Hash")
        {
            return new InterfaceTypeSymbol(name, CreateHashMethods());
        }

        private static FunctionSymbol CreateVariadicPrintFunc(string name, TypeSymbol? returnType = null)
        {
            var returnTypes = returnType != null
                ? new[] { returnType }
                : Array.Empty<TypeSymbol>();
            return new FunctionSymbol(name, Array.Empty<ParameterSymbol>(), returnTypes, isVariadic: true);
        }

        private static FunctionSymbol CreateFormatFunc(string name, TypeSymbol? returnType = null)
        {
            var formatParam = new ParameterSymbol("format", BuiltinTypes.String, 0);
            var returnTypes = returnType != null
                ? new[] { returnType }
                : Array.Empty<TypeSymbol>();
            return new FunctionSymbol(name, new[] { formatParam }, returnTypes, isVariadic: true);
        }

        private static PackageSymbol CreateBytesPackage()
        {
            var pkg = new PackageSymbol("bytes", "bytes");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var b = BuiltinTypes.Bool;
            var i = BuiltinTypes.Int;

            pkg.AddExport(new FunctionSymbol("Contains",
                new[] { P("b", byteSlice, 0), P("subslice", byteSlice, 1) },
                new[] { b }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("HasPrefix",
                new[] { P("s", byteSlice, 0), P("prefix", byteSlice, 1) },
                new[] { b }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("HasSuffix",
                new[] { P("s", byteSlice, 0), P("suffix", byteSlice, 1) },
                new[] { b }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("Index",
                new[] { P("s", byteSlice, 0), P("sep", byteSlice, 1) },
                new[] { i }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("Equal",
                new[] { P("a", byteSlice, 0), P("b", byteSlice, 1) },
                new[] { b }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("Compare",
                new[] { P("a", byteSlice, 0), P("b", byteSlice, 1) },
                new[] { i }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("Repeat",
                new[] { P("b", byteSlice, 0), P("count", i, 1) },
                new[] { byteSlice }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("ToUpper",
                new[] { P("s", byteSlice, 0) },
                new[] { byteSlice }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("ToLower",
                new[] { P("s", byteSlice, 0) },
                new[] { byteSlice }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("TrimSpace",
                new[] { P("s", byteSlice, 0) },
                new[] { byteSlice }, packageName: "bytes"));
            pkg.AddExport(new FunctionSymbol("ReplaceAll",
                new[] { P("s", byteSlice, 0), P("old", byteSlice, 1), P("new", byteSlice, 2) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.Replace(s, old, new []byte, n int) []byte
            pkg.AddExport(new FunctionSymbol("Replace",
                new[] { P("s", byteSlice, 0), P("old", byteSlice, 1), P("new", byteSlice, 2), P("n", i, 3) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.IndexFunc(s []byte, f func(rune) bool) int
            var runeToBool = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.Rune },
                new TypeSymbol[] { BuiltinTypes.Bool });
            pkg.AddExport(new FunctionSymbol("IndexFunc",
                new[] { P("s", byteSlice, 0), new ParameterSymbol("f", runeToBool, 1) },
                new[] { i }, packageName: "bytes"));

            // bytes.IndexByte(s []byte, c byte) int
            pkg.AddExport(new FunctionSymbol("IndexByte",
                new[] { P("s", byteSlice, 0), P("c", BuiltinTypes.Byte, 1) },
                new[] { i }, packageName: "bytes"));

            // bytes.TrimLeftFunc(s []byte, f func(rune) bool) []byte
            pkg.AddExport(new FunctionSymbol("TrimLeftFunc",
                new[] { P("s", byteSlice, 0), new ParameterSymbol("f", runeToBool, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.TrimRightFunc(s []byte, f func(rune) bool) []byte
            pkg.AddExport(new FunctionSymbol("TrimRightFunc",
                new[] { P("s", byteSlice, 0), new ParameterSymbol("f", runeToBool, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.TrimPrefix(s, prefix []byte) []byte
            pkg.AddExport(new FunctionSymbol("TrimPrefix",
                new[] { P("s", byteSlice, 0), P("prefix", byteSlice, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.TrimSuffix(s, suffix []byte) []byte
            pkg.AddExport(new FunctionSymbol("TrimSuffix",
                new[] { P("s", byteSlice, 0), P("suffix", byteSlice, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.TrimFunc(s []byte, f func(rune) bool) []byte
            pkg.AddExport(new FunctionSymbol("TrimFunc",
                new[] { P("s", byteSlice, 0), new ParameterSymbol("f", runeToBool, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.Trim(s []byte, cutset string) []byte
            pkg.AddExport(new FunctionSymbol("Trim",
                new[] { P("s", byteSlice, 0), P("cutset", BuiltinTypes.String, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.ContainsRune(s []byte, r rune) bool
            pkg.AddExport(new FunctionSymbol("ContainsRune",
                new[] { P("s", byteSlice, 0), P("r", BuiltinTypes.Rune, 1) },
                new[] { BuiltinTypes.Bool }, packageName: "bytes"));

            // bytes.Split(s, sep []byte) [][]byte
            var byteSliceSlice = new SliceTypeSymbol(byteSlice);
            pkg.AddExport(new FunctionSymbol("Split",
                new[] { P("s", byteSlice, 0), P("sep", byteSlice, 1) },
                new[] { byteSliceSlice }, packageName: "bytes"));

            // bytes.Join(s [][]byte, sep []byte) []byte
            pkg.AddExport(new FunctionSymbol("Join",
                new[] { P("s", byteSliceSlice, 0), P("sep", byteSlice, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.IndexAny(s []byte, chars string) int
            pkg.AddExport(new FunctionSymbol("IndexAny",
                new[] { P("s", byteSlice, 0), P("chars", BuiltinTypes.String, 1) },
                new[] { i }, packageName: "bytes"));

            // bytes.SplitN(s, sep []byte, n int) [][]byte
            pkg.AddExport(new FunctionSymbol("SplitN",
                new[] { P("s", byteSlice, 0), P("sep", byteSlice, 1), P("n", i, 2) },
                new[] { byteSliceSlice }, packageName: "bytes"));

            // bytes.SplitAfter(s, sep []byte) [][]byte
            pkg.AddExport(new FunctionSymbol("SplitAfter",
                new[] { P("s", byteSlice, 0), P("sep", byteSlice, 1) },
                new[] { byteSliceSlice }, packageName: "bytes"));

            // bytes.Runes(s []byte) []rune
            pkg.AddExport(new FunctionSymbol("Runes",
                new[] { P("s", byteSlice, 0) },
                new[] { new SliceTypeSymbol(BuiltinTypes.Rune) }, packageName: "bytes"));

            // bytes.EqualFold(s, t []byte) bool
            pkg.AddExport(new FunctionSymbol("EqualFold",
                new[] { P("s", byteSlice, 0), P("t", byteSlice, 1) },
                new[] { b }, packageName: "bytes"));

            // bytes.Count(s, sep []byte) int
            pkg.AddExport(new FunctionSymbol("Count",
                new[] { P("s", byteSlice, 0), P("sep", byteSlice, 1) },
                new[] { i }, packageName: "bytes"));

            // bytes.Fields(s []byte) [][]byte
            pkg.AddExport(new FunctionSymbol("Fields",
                new[] { P("s", byteSlice, 0) },
                new[] { byteSliceSlice }, packageName: "bytes"));

            // bytes.Map(mapping func(rune) rune, s []byte) []byte
            var runeToRune = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.Rune },
                new TypeSymbol[] { BuiltinTypes.Rune });
            pkg.AddExport(new FunctionSymbol("Map",
                new[] { new ParameterSymbol("mapping", runeToRune, 0), P("s", byteSlice, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.Title(s []byte) []byte
            pkg.AddExport(new FunctionSymbol("Title",
                new[] { P("s", byteSlice, 0) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.LastIndex(s, sep []byte) int
            pkg.AddExport(new FunctionSymbol("LastIndex",
                new[] { P("s", byteSlice, 0), P("sep", byteSlice, 1) },
                new[] { i }, packageName: "bytes"));

            // bytes.LastIndexByte(s []byte, c byte) int
            pkg.AddExport(new FunctionSymbol("LastIndexByte",
                new[] { P("s", byteSlice, 0), P("c", BuiltinTypes.Byte, 1) },
                new[] { i }, packageName: "bytes"));

            // bytes.TrimLeft(s []byte, cutset string) []byte
            pkg.AddExport(new FunctionSymbol("TrimLeft",
                new[] { P("s", byteSlice, 0), P("cutset", BuiltinTypes.String, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.TrimRight(s []byte, cutset string) []byte
            pkg.AddExport(new FunctionSymbol("TrimRight",
                new[] { P("s", byteSlice, 0), P("cutset", BuiltinTypes.String, 1) },
                new[] { byteSlice }, packageName: "bytes"));

            // bytes.ContainsAny(b []byte, chars string) bool
            pkg.AddExport(new FunctionSymbol("ContainsAny",
                new[] { P("b", byteSlice, 0), P("chars", BuiltinTypes.String, 1) },
                new[] { b }, packageName: "bytes"));

            // bytes.LastIndexAny(s []byte, chars string) int
            pkg.AddExport(new FunctionSymbol("LastIndexAny",
                new[] { P("s", byteSlice, 0), P("chars", BuiltinTypes.String, 1) },
                new[] { i }, packageName: "bytes"));

            // bytes.Buffer type
            var bufferType = new StructTypeSymbol("Buffer", Array.Empty<FieldSymbol>());
            bufferType.AddMethod(new MethodSymbol("Write", bufferType, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("WriteString", bufferType, false,
                new[] { P("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("WriteByte", bufferType, false,
                new[] { P("c", BuiltinTypes.Byte, 0) },
                new TypeSymbol[] { BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("WriteRune", bufferType, false,
                new[] { P("r", BuiltinTypes.Int32, 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("Read", bufferType, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("ReadByte", bufferType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Byte, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("ReadRune", bufferType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Int32, i, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("UnreadByte", bufferType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("UnreadRune", bufferType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("Truncate", bufferType, false,
                new[] { P("n", i, 0) },
                BuiltinTypes.Void));
            bufferType.AddMethod(new MethodSymbol("Grow", bufferType, false,
                new[] { P("n", i, 0) },
                BuiltinTypes.Void));
            bufferType.AddMethod(new MethodSymbol("Next", bufferType, false,
                new[] { P("n", i, 0) },
                byteSlice));
            bufferType.AddMethod(new MethodSymbol("Bytes", bufferType, false,
                Array.Empty<ParameterSymbol>(), byteSlice));
            bufferType.AddMethod(new MethodSymbol("String", bufferType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            bufferType.AddMethod(new MethodSymbol("Len", bufferType, false,
                Array.Empty<ParameterSymbol>(), i));
            bufferType.AddMethod(new MethodSymbol("Cap", bufferType, false,
                Array.Empty<ParameterSymbol>(), i));
            bufferType.AddMethod(new MethodSymbol("Reset", bufferType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            bufferType.AddMethod(new MethodSymbol("ReadFrom", bufferType, false,
                new[] { P("r", BuiltinTypes.EmptyInterface, 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("WriteTo", bufferType, false,
                new[] { P("w", BuiltinTypes.EmptyInterface, 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("ReadString", bufferType, false,
                new[] { P("delim", BuiltinTypes.Byte, 0) },
                new TypeSymbol[] { BuiltinTypes.String, BuiltinTypes.Error }));
            bufferType.AddMethod(new MethodSymbol("ReadBytes", bufferType, false,
                new[] { P("delim", BuiltinTypes.Byte, 0) },
                new TypeSymbol[] { byteSlice, BuiltinTypes.Error }));
            pkg.AddExport(bufferType);

            // bytes.NewBuffer(buf []byte) *Buffer
            var ptrBufferType = new PointerTypeSymbol(bufferType);
            pkg.AddExport(new FunctionSymbol("NewBuffer",
                new[] { P("buf", byteSlice, 0) },
                new TypeSymbol[] { ptrBufferType }, packageName: "bytes"));

            // bytes.NewBufferString(s string) *Buffer
            pkg.AddExport(new FunctionSymbol("NewBufferString",
                new[] { P("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { ptrBufferType }, packageName: "bytes"));

            // bytes.Reader type
            var readerType = new StructTypeSymbol("Reader", Array.Empty<FieldSymbol>());
            readerType.AddMethod(new MethodSymbol("Read", readerType, false,
                new[] { P("b", byteSlice, 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("ReadByte", readerType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Byte, BuiltinTypes.Error }));
            readerType.AddMethod(new MethodSymbol("UnreadByte", readerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            readerType.AddMethod(new MethodSymbol("Len", readerType, false,
                Array.Empty<ParameterSymbol>(), i));
            readerType.AddMethod(new MethodSymbol("Size", readerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64));
            readerType.AddMethod(new MethodSymbol("Reset", readerType, false,
                new[] { P("b", byteSlice, 0) }, BuiltinTypes.Void));
            pkg.AddExport(readerType);

            // bytes.NewReader(b []byte) *Reader
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { P("b", byteSlice, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(readerType) }, packageName: "bytes"));

            return pkg;
        }

        private static PackageSymbol CreatePathPackage()
        {
            var pkg = new PackageSymbol("path", "path");

            var s = BuiltinTypes.String;
            var b = BuiltinTypes.Bool;

            pkg.AddExport(new FunctionSymbol("Base",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("Dir",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("Ext",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("Join",
                Array.Empty<ParameterSymbol>(), new[] { s }, isVariadic: true, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("Clean",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("IsAbs",
                new[] { P("path", s, 0) }, new[] { b }, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("Split",
                new[] { P("path", s, 0) }, new[] { s, s }, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("Match",
                new[] { P("pattern", s, 0), P("name", s, 1) },
                new[] { b, BuiltinTypes.Error }, packageName: "path"));
            pkg.AddExport(new PackageVarSymbol("ErrBadPattern", BuiltinTypes.Error));

            return pkg;
        }

        private static PackageSymbol CreateDotnetPackage()
        {
            var pkg = new PackageSymbol("dotnet", "dotnet");

            var s = BuiltinTypes.String;
            var iface = BuiltinTypes.EmptyInterface;

            // dotnet.CallStatic(typeName string, methodName string, args ...interface{}) interface{}
            pkg.AddExport(new FunctionSymbol("CallStatic",
                new[] { P("typeName", s, 0), P("methodName", s, 1) },
                new TypeSymbol[] { iface }, isVariadic: true, packageName: "dotnet"));

            // dotnet.GetStaticProperty(typeName string, propertyName string) interface{}
            pkg.AddExport(new FunctionSymbol("GetStaticProperty",
                new[] { P("typeName", s, 0), P("propertyName", s, 1) },
                new TypeSymbol[] { iface }, packageName: "dotnet"));

            // dotnet.New(typeName string, args ...interface{}) interface{}
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("typeName", s, 0) },
                new TypeSymbol[] { iface }, isVariadic: true, packageName: "dotnet"));

            // dotnet.CallMethod(instance interface{}, methodName string, args ...interface{}) interface{}
            pkg.AddExport(new FunctionSymbol("CallMethod",
                new[] { P("instance", iface, 0), P("methodName", s, 1) },
                new TypeSymbol[] { iface }, isVariadic: true, packageName: "dotnet"));

            // dotnet.GetProperty(instance interface{}, propertyName string) interface{}
            pkg.AddExport(new FunctionSymbol("GetProperty",
                new[] { P("instance", iface, 0), P("propertyName", s, 1) },
                new TypeSymbol[] { iface }, packageName: "dotnet"));

            // dotnet.SetProperty(instance interface{}, propertyName string, value interface{})
            pkg.AddExport(new FunctionSymbol("SetProperty",
                new[] { P("instance", iface, 0), P("propertyName", s, 1), P("value", iface, 2) },
                Array.Empty<TypeSymbol>(), packageName: "dotnet"));

            // dotnet.TypeName(instance interface{}) string
            pkg.AddExport(new FunctionSymbol("TypeName",
                new[] { P("instance", iface, 0) },
                new[] { s }, packageName: "dotnet"));

            return pkg;
        }

        private static PackageSymbol CreateContextPackage()
        {
            var pkg = new PackageSymbol("context", "context");
            var iface = BuiltinTypes.EmptyInterface;

            // Deadline time type
            var durationType = BuiltinTypes.Int;
            var timeType = new StructTypeSymbol("Time", Array.Empty<FieldSymbol>());
            timeType.AddMethod(new MethodSymbol("Sub", timeType, false,
                new[] { P("u", timeType, 0) }, durationType));
            timeType.AddMethod(new MethodSymbol("Add", timeType, false,
                new[] { P("d", durationType, 0) }, new[] { timeType }));
            timeType.AddMethod(new MethodSymbol("Before", timeType, false,
                new[] { P("u", timeType, 0) }, BuiltinTypes.Bool));
            timeType.AddMethod(new MethodSymbol("After", timeType, false,
                new[] { P("u", timeType, 0) }, BuiltinTypes.Bool));
            timeType.AddMethod(new MethodSymbol("IsZero", timeType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            timeType.AddMethod(new MethodSymbol("Unix", timeType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            timeType.AddMethod(new MethodSymbol("String", timeType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));

            // context.Context interface
            var ctxType = new InterfaceTypeSymbol("Context", new[]
            {
                new MethodSymbol("Value", null!, false,
                    new[] { P("key", iface, 0) }, iface),
                new MethodSymbol("Err", null!, false,
                    Array.Empty<ParameterSymbol>(), iface),
                new MethodSymbol("Done", null!, false,
                    Array.Empty<ParameterSymbol>(),
                    new[] { new ChannelTypeSymbol(iface) }),
                new MethodSymbol("Deadline", null!, false,
                    Array.Empty<ParameterSymbol>(),
                    new TypeSymbol[] { timeType, BuiltinTypes.Bool }),
            });
            pkg.AddExport(ctxType);

            // CancelFunc type — just Action
            var cancelFunc = new FunctionTypeSymbol(
                Array.Empty<TypeSymbol>(), Array.Empty<TypeSymbol>());

            // context.Background() Context
            pkg.AddExport(new FunctionSymbol("Background",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { ctxType }, packageName: "context"));

            // context.TODO() Context
            pkg.AddExport(new FunctionSymbol("TODO",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { ctxType }, packageName: "context"));

            // context.WithCancel(parent Context) (Context, CancelFunc)
            pkg.AddExport(new FunctionSymbol("WithCancel",
                new[] { P("parent", ctxType, 0) },
                new TypeSymbol[] { ctxType, cancelFunc }, packageName: "context"));

            // context.WithTimeout(parent Context, timeout Duration) (Context, CancelFunc)
            pkg.AddExport(new FunctionSymbol("WithTimeout",
                new[] { P("parent", ctxType, 0), P("timeout", BuiltinTypes.Int, 1) },
                new TypeSymbol[] { ctxType, cancelFunc }, packageName: "context"));

            // context.WithDeadline(parent Context, d Time) (Context, CancelFunc)
            pkg.AddExport(new FunctionSymbol("WithDeadline",
                new[] { P("parent", ctxType, 0), P("d", timeType, 1) },
                new TypeSymbol[] { ctxType, cancelFunc }, packageName: "context"));

            // context.WithValue(parent Context, key, val interface{}) Context
            pkg.AddExport(new FunctionSymbol("WithValue",
                new[] { P("parent", ctxType, 0), P("key", iface, 1), P("val", iface, 2) },
                new TypeSymbol[] { ctxType }, packageName: "context"));

            // context.WithCancelCause(parent Context) (Context, CancelCauseFunc)
            // CancelCauseFunc is func(error)
            var cancelCauseFunc = new FunctionTypeSymbol(
                new TypeSymbol[] { BuiltinTypes.Error }, Array.Empty<TypeSymbol>());
            pkg.AddExport(new FunctionSymbol("WithCancelCause",
                new[] { P("parent", ctxType, 0) },
                new TypeSymbol[] { ctxType, cancelCauseFunc }, packageName: "context"));

            // context.CancelFunc type — type CancelFunc func()
            var cancelFuncType = new TypeSymbol("CancelFunc", TypeKind.Function, cancelFunc);
            pkg.AddExport(cancelFuncType);

            // context.Canceled var
            pkg.AddExport(new PackageVarSymbol("Canceled", BuiltinTypes.Error, typeof(GoContext), "Canceled"));

            // context.DeadlineExceeded var
            pkg.AddExport(new PackageVarSymbol("DeadlineExceeded", BuiltinTypes.Error, typeof(GoContext), "DeadlineExceeded"));

            // context.Cause(c Context) error
            pkg.AddExport(new FunctionSymbol("Cause",
                new[] { P("c", ctxType, 0) },
                new TypeSymbol[] { BuiltinTypes.Error }, packageName: "context"));

            // context.AfterFunc(ctx Context, f func()) (stop func() bool)
            var stopFunc = new FunctionTypeSymbol(
                Array.Empty<TypeSymbol>(), new TypeSymbol[] { BuiltinTypes.Bool });
            pkg.AddExport(new FunctionSymbol("AfterFunc",
                new[] { P("ctx", ctxType, 0), P("f", new FunctionTypeSymbol(
                    Array.Empty<TypeSymbol>(), Array.Empty<TypeSymbol>()), 1) },
                new TypeSymbol[] { stopFunc }, packageName: "context"));

            return pkg;
        }

        private static PackageSymbol CreateCompressGzipPackage()
        {
            var pkg = new PackageSymbol("gzip", "compress/gzip");
            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;
            var err = BuiltinTypes.Error;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // gzip.Writer
            var writerType = new StructTypeSymbol("Writer", Array.Empty<FieldSymbol>());
            writerType.AddMethod(new MethodSymbol("Write", writerType, false,
                new[] { new ParameterSymbol("p", byteSlice, 0) },
                new TypeSymbol[] { i, err }));
            writerType.AddMethod(new MethodSymbol("Close", writerType, false,
                Array.Empty<ParameterSymbol>(), err));
            writerType.AddMethod(new MethodSymbol("Flush", writerType, false,
                Array.Empty<ParameterSymbol>(), err));
            writerType.AddMethod(new MethodSymbol("Reset", writerType, false,
                new[] { new ParameterSymbol("w", iface, 0) },
                BuiltinTypes.Void));
            pkg.AddExport(writerType);

            // gzip.Reader
            var readerType = new StructTypeSymbol("Reader", new[]
            {
                new FieldSymbol("Header", iface, 0),
            });
            readerType.AddMethod(new MethodSymbol("Read", readerType, false,
                new[] { new ParameterSymbol("p", byteSlice, 0) },
                new TypeSymbol[] { i, err }));
            readerType.AddMethod(new MethodSymbol("Close", readerType, false,
                Array.Empty<ParameterSymbol>(), err));
            readerType.AddMethod(new MethodSymbol("Reset", readerType, false,
                new[] { new ParameterSymbol("r", iface, 0) },
                err));
            pkg.AddExport(readerType);

            // gzip.NewWriter(w io.Writer) *Writer
            pkg.AddExport(new FunctionSymbol("NewWriter",
                new[] { new ParameterSymbol("w", iface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(writerType) }, packageName: "gzip"));

            // gzip.NewWriterLevel(w io.Writer, level int) (*Writer, error)
            pkg.AddExport(new FunctionSymbol("NewWriterLevel",
                new[] { new ParameterSymbol("w", iface, 0), new ParameterSymbol("level", i, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(writerType), err }, packageName: "gzip"));

            // gzip.NewReader(r io.Reader) (*Reader, error)
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { new ParameterSymbol("r", iface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(readerType), err }, packageName: "gzip"));

            // Compression level constants
            pkg.AddExport(new ConstantSymbol("NoCompression", i, (long)0));
            pkg.AddExport(new ConstantSymbol("BestSpeed", i, (long)1));
            pkg.AddExport(new ConstantSymbol("BestCompression", i, (long)9));
            pkg.AddExport(new ConstantSymbol("DefaultCompression", i, (long)-1));
            pkg.AddExport(new ConstantSymbol("HuffmanOnly", i, (long)-2));

            return pkg;
        }

        private static PackageSymbol CreateJsonPackage()
        {
            var pkg = new PackageSymbol("json", "encoding/json");

            var iface = BuiltinTypes.EmptyInterface;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var s = BuiltinTypes.String;

            // json.Marshal(v interface{}) ([]byte, error)
            var err = BuiltinTypes.Error;
            pkg.AddExport(new FunctionSymbol("Marshal",
                new[] { P("v", iface, 0) },
                new TypeSymbol[] { byteSlice, err }, packageName: "json"));

            // json.MarshalIndent(v interface{}, prefix, indent string) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("MarshalIndent",
                new[] { P("v", iface, 0), P("prefix", s, 1), P("indent", s, 2) },
                new TypeSymbol[] { byteSlice, err }, packageName: "json"));

            // json.Unmarshal(data []byte, v interface{}) error
            pkg.AddExport(new FunctionSymbol("Unmarshal",
                new[] { P("data", byteSlice, 0), P("v", iface, 1) },
                new TypeSymbol[] { BuiltinTypes.Error }, packageName: "json"));

            // json.Valid(data []byte) bool
            pkg.AddExport(new FunctionSymbol("Valid",
                new[] { P("data", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Bool }, packageName: "json"));

            // json.Decoder type
            var decoderType = new StructTypeSymbol("Decoder", Array.Empty<FieldSymbol>());
            decoderType.AddMethod(new MethodSymbol("Decode", decoderType, false,
                new[] { P("v", iface, 0) }, BuiltinTypes.Error));
            decoderType.AddMethod(new MethodSymbol("More", decoderType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            decoderType.AddMethod(new MethodSymbol("UseNumber", decoderType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            decoderType.AddMethod(new MethodSymbol("DisallowUnknownFields", decoderType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            decoderType.AddMethod(new MethodSymbol("Buffered", decoderType, false,
                Array.Empty<ParameterSymbol>(), iface));
            pkg.AddExport(decoderType);

            // json.NewDecoder(r io.Reader) *Decoder
            pkg.AddExport(new FunctionSymbol("NewDecoder",
                new[] { P("r", iface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(decoderType) }, packageName: "json"));

            // json.Encoder type
            var encoderType = new StructTypeSymbol("Encoder", Array.Empty<FieldSymbol>());
            encoderType.AddMethod(new MethodSymbol("Encode", encoderType, false,
                new[] { P("v", iface, 0) }, BuiltinTypes.Error));
            encoderType.AddMethod(new MethodSymbol("SetIndent", encoderType, false,
                new[] { P("prefix", s, 0), P("indent", s, 1) }, BuiltinTypes.Void));
            encoderType.AddMethod(new MethodSymbol("SetEscapeHTML", encoderType, false,
                new[] { P("on", BuiltinTypes.Bool, 0) }, BuiltinTypes.Void));
            pkg.AddExport(encoderType);

            // json.NewEncoder(w io.Writer) *Encoder
            pkg.AddExport(new FunctionSymbol("NewEncoder",
                new[] { P("w", iface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(encoderType) }, packageName: "json"));

            // json.Number type (string alias with methods)
            var numberType = new StructTypeSymbol("Number", Array.Empty<FieldSymbol>());
            numberType.AddMethod(new MethodSymbol("String", numberType, false,
                Array.Empty<ParameterSymbol>(), s));
            numberType.AddMethod(new MethodSymbol("Float64", numberType, false,
                Array.Empty<ParameterSymbol>(), new TypeSymbol[] { BuiltinTypes.Float64, err }));
            numberType.AddMethod(new MethodSymbol("Int64", numberType, false,
                Array.Empty<ParameterSymbol>(), new TypeSymbol[] { BuiltinTypes.Int64, err }));
            // json.Number is 'type Number string' in Go — set underlying type for conversions
            numberType.UnderlyingType = BuiltinTypes.String;
            pkg.AddExport(numberType);

            // json.RawMessage type ([]byte alias)
            var rawMessageType = new StructTypeSymbol("RawMessage", Array.Empty<FieldSymbol>());
            rawMessageType.AddMethod(new MethodSymbol("MarshalJSON", rawMessageType, false,
                Array.Empty<ParameterSymbol>(), new TypeSymbol[] { byteSlice, err }));
            rawMessageType.AddMethod(new MethodSymbol("UnmarshalJSON", rawMessageType, false,
                new[] { P("data", byteSlice, 0) }, BuiltinTypes.Error));
            pkg.AddExport(rawMessageType);

            // json.Marshaler interface { MarshalJSON() ([]byte, error) }
            var marshalerIface = new InterfaceTypeSymbol("Marshaler", Array.Empty<MethodSymbol>());
            marshalerIface.SetMethods(new[]
            {
                new MethodSymbol("MarshalJSON", marshalerIface, false,
                    Array.Empty<ParameterSymbol>(), new TypeSymbol[] { byteSlice, err }),
            });
            pkg.AddExport(marshalerIface);

            // json.Unmarshaler interface { UnmarshalJSON([]byte) error }
            var unmarshalerIface = new InterfaceTypeSymbol("Unmarshaler", Array.Empty<MethodSymbol>());
            unmarshalerIface.SetMethods(new[]
            {
                new MethodSymbol("UnmarshalJSON", unmarshalerIface, false,
                    new[] { P("data", byteSlice, 0) }, BuiltinTypes.Error),
            });
            pkg.AddExport(unmarshalerIface);

            // json.UnsupportedTypeError
            var unsupportedTypeError = new StructTypeSymbol("UnsupportedTypeError", new[]
            {
                new FieldSymbol("Type", iface, 0),
            });
            unsupportedTypeError.AddMethod(new MethodSymbol("Error", unsupportedTypeError, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(unsupportedTypeError);

            // json.SyntaxError
            var syntaxError = new StructTypeSymbol("SyntaxError", new[]
            {
                new FieldSymbol("Offset", BuiltinTypes.Int64, 0),
            });
            syntaxError.AddMethod(new MethodSymbol("Error", syntaxError, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(syntaxError);

            // json.InvalidUnmarshalError
            var invalidUnmarshalError = new StructTypeSymbol("InvalidUnmarshalError", new[]
            {
                new FieldSymbol("Type", iface, 0),
            });
            invalidUnmarshalError.AddMethod(new MethodSymbol("Error", invalidUnmarshalError, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(invalidUnmarshalError);

            // json.UnmarshalTypeError
            var unmarshalTypeError = new StructTypeSymbol("UnmarshalTypeError", new[]
            {
                new FieldSymbol("Value", s, 0),
                new FieldSymbol("Type", iface, 1),
                new FieldSymbol("Offset", BuiltinTypes.Int64, 2),
                new FieldSymbol("Struct", s, 3),
                new FieldSymbol("Field", s, 4),
            });
            unmarshalTypeError.AddMethod(new MethodSymbol("Error", unmarshalTypeError, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(unmarshalTypeError);

            // json.Token type (interface{})
            var tokenType = new TypeSymbol("Token", TypeKind.Interface, iface);
            pkg.AddExport(tokenType);

            // json.Delim type (rune)
            var delimType = new TypeSymbol("Delim", TypeKind.Int32, BuiltinTypes.Rune);
            pkg.AddExport(delimType);

            return pkg;
        }

        private static PackageSymbol CreateIoutilPackage()
        {
            var pkg = new PackageSymbol("ioutil", "io/ioutil");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var s = BuiltinTypes.String;
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // ioutil.ReadAll(r Reader) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("ReadAll",
                new[] { P("r", iface, 0) },
                new TypeSymbol[] { byteSlice, BuiltinTypes.Error }, packageName: "ioutil"));

            // ioutil.ReadFile(filename string) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("ReadFile",
                new[] { P("filename", s, 0) },
                new TypeSymbol[] { byteSlice, BuiltinTypes.Error }, packageName: "ioutil"));

            // ioutil.WriteFile(filename string, data []byte, perm os.FileMode) error
            pkg.AddExport(new FunctionSymbol("WriteFile",
                new[] { P("filename", s, 0), P("data", byteSlice, 1),
                        P("perm", BuiltinTypes.Int, 2) },
                new TypeSymbol[] { BuiltinTypes.Error }, packageName: "ioutil"));

            // ioutil.TempDir(dir, pattern string) (string, error)
            pkg.AddExport(new FunctionSymbol("TempDir",
                new[] { P("dir", s, 0), P("pattern", s, 1) },
                new TypeSymbol[] { s, BuiltinTypes.Error }, packageName: "ioutil"));

            // ioutil.TempFile(dir, pattern string) (*os.File, error)
            var osFileType = CreateOsFileType();
            pkg.AddExport(new FunctionSymbol("TempFile",
                new[] { P("dir", s, 0), P("pattern", s, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(osFileType), BuiltinTypes.Error }, packageName: "ioutil"));

            // ioutil.NopCloser(r Reader) ReadCloser
            pkg.AddExport(new FunctionSymbol("NopCloser",
                new[] { P("r", iface, 0) },
                new[] { iface }, packageName: "ioutil"));

            // ioutil.ReadDir(dirname string) ([]os.FileInfo, error)
            var fileInfoIface = new InterfaceTypeSymbol("FileInfo", new[]
            {
                new MethodSymbol("Name", null!, false, Array.Empty<ParameterSymbol>(), s),
                new MethodSymbol("Size", null!, false, Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64),
                new MethodSymbol("Mode", null!, false, Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint32),
                new MethodSymbol("IsDir", null!, false, Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool),
            });
            pkg.AddExport(new FunctionSymbol("ReadDir",
                new[] { P("dirname", s, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(fileInfoIface), BuiltinTypes.Error }, packageName: "ioutil"));

            // ioutil.Discard — Writer that discards all data
            pkg.AddExport(new PackageVarSymbol("Discard", iface,
                typeof(DiscardWriter), "Instance"));

            return pkg;
        }

        private static PackageSymbol CreateTestingPackage()
        {
            var pkg = new PackageSymbol("testing", "testing");

            var s = BuiltinTypes.String;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // testing.T type
            var tType = new StructTypeSymbol("T", Array.Empty<FieldSymbol>());

            // Name() string
            tType.AddMethod(new MethodSymbol("Name", tType, false,
                Array.Empty<ParameterSymbol>(), s));

            // Failed() bool
            tType.AddMethod(new MethodSymbol("Failed", tType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));

            // Skipped() bool
            tType.AddMethod(new MethodSymbol("Skipped", tType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));

            // Log(args ...interface{})
            tType.AddMethod(new MethodSymbol("Log", tType, false,
                new[] { new ParameterSymbol("msg", emptyIface, 0) },
                BuiltinTypes.Void));

            // Logf(format string, args ...interface{})
            tType.AddMethod(new MethodSymbol("Logf", tType, false,
                new[] { new ParameterSymbol("format", s, 0),
                        new ParameterSymbol("args", emptyIface, 1) },
                BuiltinTypes.Void));

            // Error(args ...interface{})
            tType.AddMethod(new MethodSymbol("Error", tType, false,
                new[] { new ParameterSymbol("msg", emptyIface, 0) },
                BuiltinTypes.Void));

            // Errorf(format string, args ...interface{})
            tType.AddMethod(new MethodSymbol("Errorf", tType, false,
                new[] { new ParameterSymbol("format", s, 0),
                        new ParameterSymbol("args", emptyIface, 1) },
                BuiltinTypes.Void));

            // Fatal(args ...interface{})
            tType.AddMethod(new MethodSymbol("Fatal", tType, false,
                new[] { new ParameterSymbol("msg", emptyIface, 0) },
                BuiltinTypes.Void));

            // Fatalf(format string, args ...interface{})
            tType.AddMethod(new MethodSymbol("Fatalf", tType, false,
                new[] { new ParameterSymbol("format", s, 0),
                        new ParameterSymbol("args", emptyIface, 1) },
                BuiltinTypes.Void));

            // Fail()
            tType.AddMethod(new MethodSymbol("Fail", tType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));

            // FailNow()
            tType.AddMethod(new MethodSymbol("FailNow", tType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));

            // Skip(args ...interface{})
            tType.AddMethod(new MethodSymbol("Skip", tType, false,
                new[] { new ParameterSymbol("msg", emptyIface, 0) },
                BuiltinTypes.Void));

            // Skipf(format string, args ...interface{})
            tType.AddMethod(new MethodSymbol("Skipf", tType, false,
                new[] { new ParameterSymbol("format", s, 0),
                        new ParameterSymbol("args", emptyIface, 1) },
                BuiltinTypes.Void));

            // SkipNow()
            tType.AddMethod(new MethodSymbol("SkipNow", tType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));

            // Helper()
            tType.AddMethod(new MethodSymbol("Helper", tType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));

            // TempDir() string
            tType.AddMethod(new MethodSymbol("TempDir", tType, false,
                Array.Empty<ParameterSymbol>(), s));

            // Run(name string, f func(*T)) bool
            var funcType = new FunctionTypeSymbol(
                new TypeSymbol[] { tType },
                Array.Empty<TypeSymbol>());
            tType.AddMethod(new MethodSymbol("Run", tType, false,
                new[] { new ParameterSymbol("name", s, 0),
                        new ParameterSymbol("f", funcType, 1) },
                BuiltinTypes.Bool));

            pkg.AddExport(tType);

            return pkg;
        }

        private static PackageSymbol CreateBase64Package()
        {
            var pkg = new PackageSymbol("base64", "encoding/base64");

            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Uint8);
            var s = BuiltinTypes.String;

            // Encoding type with EncodeToString and DecodeString methods
            var encodingType = new StructTypeSymbol("Encoding", Array.Empty<FieldSymbol>());
            encodingType.AddMethod(new MethodSymbol("EncodeToString", encodingType, false,
                new[] { new ParameterSymbol("src", byteSlice, 0) },
                s));
            encodingType.AddMethod(new MethodSymbol("DecodeString", encodingType, false,
                new[] { new ParameterSymbol("s", s, 0) },
                new TypeSymbol[] { byteSlice, emptyIface }));
            encodingType.AddMethod(new MethodSymbol("EncodedLen", encodingType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0) },
                BuiltinTypes.Int));
            encodingType.AddMethod(new MethodSymbol("DecodedLen", encodingType, false,
                new[] { new ParameterSymbol("n", BuiltinTypes.Int, 0) },
                BuiltinTypes.Int));
            encodingType.AddMethod(new MethodSymbol("Encode", encodingType, false,
                new[] { new ParameterSymbol("dst", byteSlice, 0), new ParameterSymbol("src", byteSlice, 1) },
                BuiltinTypes.Void));
            encodingType.AddMethod(new MethodSymbol("Decode", encodingType, false,
                new[] { new ParameterSymbol("dst", byteSlice, 0), new ParameterSymbol("src", byteSlice, 1) },
                new TypeSymbol[] { BuiltinTypes.Int, emptyIface }));
            encodingType.AddMethod(new MethodSymbol("Strict", encodingType, false,
                Array.Empty<ParameterSymbol>(), new PointerTypeSymbol(encodingType)));
            encodingType.AddMethod(new MethodSymbol("WithPadding", encodingType, false,
                new[] { new ParameterSymbol("padding", BuiltinTypes.Rune, 0) },
                new PointerTypeSymbol(encodingType)));
            pkg.AddExport(encodingType);

            // Package vars: StdEncoding, URLEncoding, RawStdEncoding, RawURLEncoding
            pkg.AddExport(new PackageVarSymbol("StdEncoding", encodingType,
                typeof(Ngo.Runtime.GoBase64), "StdEncoding"));
            pkg.AddExport(new PackageVarSymbol("URLEncoding", encodingType,
                typeof(Ngo.Runtime.GoBase64), "URLEncoding"));
            pkg.AddExport(new PackageVarSymbol("RawStdEncoding", encodingType,
                typeof(Ngo.Runtime.GoBase64), "RawStdEncoding"));
            pkg.AddExport(new PackageVarSymbol("RawURLEncoding", encodingType,
                typeof(Ngo.Runtime.GoBase64), "RawURLEncoding"));

            // NoPadding constant
            pkg.AddExport(new ConstantSymbol("NoPadding", BuiltinTypes.Rune, (long)-1));
            // StdPadding constant
            pkg.AddExport(new ConstantSymbol("StdPadding", BuiltinTypes.Rune, (long)'='));

            // base64.NewEncoder(enc *Encoding, w io.Writer) io.WriteCloser
            pkg.AddExport(new FunctionSymbol("NewEncoder",
                new[] { P("enc", encodingType, 0), P("w", emptyIface, 1) },
                new TypeSymbol[] { emptyIface }, packageName: "base64"));

            // base64.NewDecoder(enc *Encoding, r io.Reader) io.Reader
            pkg.AddExport(new FunctionSymbol("NewDecoder",
                new[] { P("enc", encodingType, 0), P("r", emptyIface, 1) },
                new TypeSymbol[] { emptyIface }, packageName: "base64"));

            return pkg;
        }

        private static PackageSymbol CreateHexPackage()
        {
            var pkg = new PackageSymbol("hex", "encoding/hex");

            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Uint8);
            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;

            // EncodeToString(src []byte) string
            pkg.AddExport(new FunctionSymbol("EncodeToString",
                new[] { new ParameterSymbol("src", byteSlice, 0) },
                new[] { s }, packageName: "hex"));

            // DecodeString(s string) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("DecodeString",
                new[] { new ParameterSymbol("s", s, 0) },
                new TypeSymbol[] { byteSlice, emptyIface }, packageName: "hex"));

            // EncodedLen(n int) int
            pkg.AddExport(new FunctionSymbol("EncodedLen",
                new[] { new ParameterSymbol("n", i64, 0) },
                new[] { i64 }, packageName: "hex"));

            // DecodedLen(n int) int
            pkg.AddExport(new FunctionSymbol("DecodedLen",
                new[] { new ParameterSymbol("n", i64, 0) },
                new[] { i64 }, packageName: "hex"));

            // Dump(data []byte) string
            pkg.AddExport(new FunctionSymbol("Dump",
                new[] { new ParameterSymbol("data", byteSlice, 0) },
                new[] { s }, packageName: "hex"));

            // Encode(dst, src []byte) int
            pkg.AddExport(new FunctionSymbol("Encode",
                new[] { new ParameterSymbol("dst", byteSlice, 0), new ParameterSymbol("src", byteSlice, 1) },
                new[] { i64 }, packageName: "hex"));

            // Decode(dst, src []byte) (int, error)
            pkg.AddExport(new FunctionSymbol("Decode",
                new[] { new ParameterSymbol("dst", byteSlice, 0), new ParameterSymbol("src", byteSlice, 1) },
                new TypeSymbol[] { i64, emptyIface }, packageName: "hex"));

            // Dumper(w io.Writer) io.WriteCloser
            pkg.AddExport(new FunctionSymbol("Dumper",
                new[] { new ParameterSymbol("w", emptyIface, 0) },
                new[] { emptyIface }, packageName: "hex"));

            return pkg;
        }

        private static PackageSymbol CreateCsvPackage()
        {
            var pkg = new PackageSymbol("csv", "encoding/csv");

            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var s = BuiltinTypes.String;
            var stringSlice = new SliceTypeSymbol(s);

            // Reader type
            var readerType = new StructTypeSymbol("Reader", Array.Empty<FieldSymbol>());
            readerType.AddMethod(new MethodSymbol("Read", readerType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { stringSlice, emptyIface }));
            readerType.AddMethod(new MethodSymbol("ReadAll", readerType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(stringSlice), emptyIface }));
            pkg.AddExport(readerType);

            // Writer type
            var writerType = new StructTypeSymbol("Writer", Array.Empty<FieldSymbol>());
            writerType.AddMethod(new MethodSymbol("Write", writerType, false,
                new[] { new ParameterSymbol("record", stringSlice, 0) },
                BuiltinTypes.Void));
            writerType.AddMethod(new MethodSymbol("WriteAll", writerType, false,
                new[] { new ParameterSymbol("records", new SliceTypeSymbol(stringSlice), 0) },
                BuiltinTypes.Void));
            writerType.AddMethod(new MethodSymbol("Flush", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            pkg.AddExport(writerType);

            // NewReader(r io.Reader) *Reader
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { new ParameterSymbol("r", emptyIface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(readerType) }, packageName: "csv"));

            // NewWriter(w io.Writer) *Writer
            pkg.AddExport(new FunctionSymbol("NewWriter",
                new[] { new ParameterSymbol("w", emptyIface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(writerType) }, packageName: "csv"));

            return pkg;
        }

        private static PackageSymbol CreateFlagPackage()
        {
            var pkg = new PackageSymbol("flag", "flag");

            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;
            var f64 = BuiltinTypes.Float64;
            var ptrS = new PointerTypeSymbol(s);
            var ptrI = new PointerTypeSymbol(i64);
            var ptrB = new PointerTypeSymbol(b);
            var ptrF = new PointerTypeSymbol(f64);

            // String(name string, value string, usage string) *string
            pkg.AddExport(new FunctionSymbol("String",
                new[] { P("name", s, 0), P("value", s, 1), P("usage", s, 2) },
                new[] { ptrS }, packageName: "flag"));

            // Int(name string, value int, usage string) *int
            pkg.AddExport(new FunctionSymbol("Int",
                new[] { P("name", s, 0), P("value", i64, 1), P("usage", s, 2) },
                new[] { ptrI }, packageName: "flag"));

            // Bool(name string, value bool, usage string) *bool
            pkg.AddExport(new FunctionSymbol("Bool",
                new[] { P("name", s, 0), P("value", b, 1), P("usage", s, 2) },
                new[] { ptrB }, packageName: "flag"));

            // Float64(name string, value float64, usage string) *float64
            pkg.AddExport(new FunctionSymbol("Float64",
                new[] { P("name", s, 0), P("value", f64, 1), P("usage", s, 2) },
                new[] { ptrF }, packageName: "flag"));

            // Parse()
            pkg.AddExport(new FunctionSymbol("Parse",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "flag"));

            // Parsed() bool
            pkg.AddExport(new FunctionSymbol("Parsed",
                Array.Empty<ParameterSymbol>(),
                new[] { b }, packageName: "flag"));

            // Args() []string
            pkg.AddExport(new FunctionSymbol("Args",
                Array.Empty<ParameterSymbol>(),
                new[] { (TypeSymbol)new SliceTypeSymbol(s) }, packageName: "flag"));

            // NArg() int
            pkg.AddExport(new FunctionSymbol("NArg",
                Array.Empty<ParameterSymbol>(),
                new[] { i64 }, packageName: "flag"));

            // Arg(i int) string
            pkg.AddExport(new FunctionSymbol("Arg",
                new[] { P("i", i64, 0) },
                new[] { s }, packageName: "flag"));

            // NFlag() int
            pkg.AddExport(new FunctionSymbol("NFlag",
                Array.Empty<ParameterSymbol>(),
                new[] { i64 }, packageName: "flag"));

            // StringVar(p *string, name string, value string, usage string)
            pkg.AddExport(new FunctionSymbol("StringVar",
                new[] { P("p", ptrS, 0), P("name", s, 1), P("value", s, 2), P("usage", s, 3) },
                Array.Empty<TypeSymbol>(), packageName: "flag"));

            // IntVar(p *int, name string, value int, usage string)
            pkg.AddExport(new FunctionSymbol("IntVar",
                new[] { P("p", ptrI, 0), P("name", s, 1), P("value", i64, 2), P("usage", s, 3) },
                Array.Empty<TypeSymbol>(), packageName: "flag"));

            // BoolVar(p *bool, name string, value bool, usage string)
            pkg.AddExport(new FunctionSymbol("BoolVar",
                new[] { P("p", ptrB, 0), P("name", s, 1), P("value", b, 2), P("usage", s, 3) },
                Array.Empty<TypeSymbol>(), packageName: "flag"));

            // Float64Var(p *float64, name string, value float64, usage string)
            pkg.AddExport(new FunctionSymbol("Float64Var",
                new[] { P("p", ptrF, 0), P("name", s, 1), P("value", f64, 2), P("usage", s, 3) },
                Array.Empty<TypeSymbol>(), packageName: "flag"));

            // Var(value Value, name string, usage string)
            var flagValueIface = new InterfaceTypeSymbol("Value", Array.Empty<MethodSymbol>());
            pkg.AddExport(flagValueIface);
            pkg.AddExport(new FunctionSymbol("Var",
                new[] { P("value", flagValueIface, 0), P("name", s, 1), P("usage", s, 2) },
                Array.Empty<TypeSymbol>(), packageName: "flag"));

            // Usage (var of type func())
            pkg.AddExport(new FunctionSymbol("PrintDefaults",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "flag"));

            // CommandLine *FlagSet
            var flagSetType = new StructTypeSymbol("FlagSet", Array.Empty<FieldSymbol>());
            flagSetType.AddMethod(new MethodSymbol("Parse", flagSetType, false,
                new[] { P("arguments", new SliceTypeSymbol(s), 0) }, BuiltinTypes.Error));
            flagSetType.AddMethod(new MethodSymbol("Args", flagSetType, false,
                Array.Empty<ParameterSymbol>(), new SliceTypeSymbol(s)));
            flagSetType.AddMethod(new MethodSymbol("NArg", flagSetType, false,
                Array.Empty<ParameterSymbol>(), i64));
            pkg.AddExport(flagSetType);

            pkg.AddExport(new FunctionSymbol("NewFlagSet",
                new[] { P("name", s, 0), P("errorHandling", i64, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(flagSetType) }, packageName: "flag"));

            // Flag type
            var flagType = new StructTypeSymbol("Flag", new[]
            {
                new FieldSymbol("Name", s, 0),
                new FieldSymbol("Usage", s, 1),
                new FieldSymbol("Value", flagValueIface, 2),
                new FieldSymbol("DefValue", s, 3),
            });
            pkg.AddExport(flagType);

            var ptrFlagType = new PointerTypeSymbol(flagType);
            var flagVisitorFunc = new FunctionTypeSymbol(
                new TypeSymbol[] { ptrFlagType }, System.Array.Empty<TypeSymbol>());

            // FlagSet.Visit / VisitAll
            flagSetType.AddMethod(new MethodSymbol("Visit", flagSetType, false,
                new[] { P("fn", flagVisitorFunc, 0) }, BuiltinTypes.Void));
            flagSetType.AddMethod(new MethodSymbol("VisitAll", flagSetType, false,
                new[] { P("fn", flagVisitorFunc, 0) }, BuiltinTypes.Void));
            flagSetType.AddMethod(new MethodSymbol("Lookup", flagSetType, false,
                new[] { P("name", s, 0) }, ptrFlagType));
            flagSetType.AddMethod(new MethodSymbol("Set", flagSetType, false,
                new[] { P("name", s, 0), P("value", s, 1) }, BuiltinTypes.Error));

            // Package-level Visit / VisitAll
            pkg.AddExport(new FunctionSymbol("Visit",
                new[] { P("fn", flagVisitorFunc, 0) },
                System.Array.Empty<TypeSymbol>(), packageName: "flag"));
            pkg.AddExport(new FunctionSymbol("VisitAll",
                new[] { P("fn", flagVisitorFunc, 0) },
                System.Array.Empty<TypeSymbol>(), packageName: "flag"));
            pkg.AddExport(new FunctionSymbol("Lookup",
                new[] { P("name", s, 0) },
                new[] { ptrFlagType }, packageName: "flag"));
            pkg.AddExport(new FunctionSymbol("Set",
                new[] { P("name", s, 0), P("value", s, 1) },
                new[] { BuiltinTypes.Error }, packageName: "flag"));

            // CommandLine var
            pkg.AddExport(new PackageVarSymbol("CommandLine",
                new PointerTypeSymbol(flagSetType)));

            // ErrorHandling constants
            pkg.AddExport(new ConstantSymbol("ContinueOnError", i64, (long)0));
            pkg.AddExport(new ConstantSymbol("ExitOnError", i64, (long)1));
            pkg.AddExport(new ConstantSymbol("PanicOnError", i64, (long)2));

            return pkg;
        }

        private static PackageSymbol CreateSha256Package()
        {
            var pkg = new PackageSymbol("sha256", "crypto/sha256");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Uint8);
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // Sum256(data []byte) [32]byte — we return []byte for simplicity
            pkg.AddExport(new FunctionSymbol("Sum256",
                new[] { new ParameterSymbol("data", byteSlice, 0) },
                new[] { (TypeSymbol)byteSlice }, packageName: "sha256"));

            // Hash type (via New())
            var hashType = new StructTypeSymbol("Hash", Array.Empty<FieldSymbol>());
            hashType.AddMethod(new MethodSymbol("Write", hashType, false,
                new[] { new ParameterSymbol("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, emptyIface }));
            hashType.AddMethod(new MethodSymbol("Sum", hashType, false,
                new[] { new ParameterSymbol("b", byteSlice, 0) },
                byteSlice));
            hashType.AddMethod(new MethodSymbol("Reset", hashType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            hashType.AddMethod(new MethodSymbol("Size", hashType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            hashType.AddMethod(new MethodSymbol("BlockSize", hashType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            pkg.AddExport(hashType);

            // New() hash.Hash
            pkg.AddExport(new FunctionSymbol("New",
                Array.Empty<ParameterSymbol>(),
                new[] { (TypeSymbol)hashType }, packageName: "sha256"));

            return pkg;
        }

        private static PackageSymbol CreateCryptoRandPackage()
        {
            var pkg = new PackageSymbol("rand", "crypto/rand");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Uint8);
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var i64 = BuiltinTypes.Int;

            // Read(b []byte) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Read",
                new[] { new ParameterSymbol("b", byteSlice, 0) },
                new TypeSymbol[] { i64, BuiltinTypes.Error }, packageName: "crand"));

            // Reader — global Reader var (io.Reader interface with Read method)
            var readerIface = new InterfaceTypeSymbol("Reader", Array.Empty<MethodSymbol>());
            readerIface.AddMethod(new MethodSymbol("Read", readerIface, false,
                new[] { new ParameterSymbol("p", byteSlice, 0) },
                new TypeSymbol[] { i64, BuiltinTypes.Error }));
            pkg.AddExport(new PackageVarSymbol("Reader", readerIface,
                typeof(object), "Reader"));

            // Minimal big.Int type for return types
            var bigIntType = new StructTypeSymbol("Int", Array.Empty<FieldSymbol>());
            var ptrBigInt = new PointerTypeSymbol(bigIntType);
            bigIntType.AddMethod(new MethodSymbol("Int64", bigIntType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64));
            bigIntType.AddMethod(new MethodSymbol("Uint64", bigIntType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint64));
            bigIntType.AddMethod(new MethodSymbol("String", bigIntType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));

            // Int(rand io.Reader, max *big.Int) (*big.Int, error)
            pkg.AddExport(new FunctionSymbol("Int",
                new[] { new ParameterSymbol("rand", emptyIface, 0),
                        new ParameterSymbol("max", ptrBigInt, 1) },
                new TypeSymbol[] { ptrBigInt, BuiltinTypes.Error }, packageName: "crand"));

            // Prime(rand io.Reader, bits int) (*big.Int, error)
            pkg.AddExport(new FunctionSymbol("Prime",
                new[] { new ParameterSymbol("rand", emptyIface, 0),
                        new ParameterSymbol("bits", i64, 1) },
                new TypeSymbol[] { ptrBigInt, BuiltinTypes.Error }, packageName: "crand"));

            return pkg;
        }

        private static PackageSymbol CreateHttpPackage()
        {
            var pkg = new PackageSymbol("http", "net/http");

            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Uint8);

            // Response type
            var responseType = new StructTypeSymbol("Response",
                new[] { new FieldSymbol("StatusCode", i64, 0),
                        new FieldSymbol("Status", s, 1),
                        new FieldSymbol("Header", emptyIface, 2),
                        new FieldSymbol("Body", emptyIface, 3),
                        new FieldSymbol("ContentLength", BuiltinTypes.Int64, 4),
                        new FieldSymbol("Proto", s, 5),
                        new FieldSymbol("ProtoMajor", i64, 6),
                        new FieldSymbol("ProtoMinor", i64, 7),
                });

            // Response.Body — has Read and Close
            var bodyType = new StructTypeSymbol("Body", Array.Empty<FieldSymbol>());
            bodyType.AddMethod(new MethodSymbol("Read", bodyType, false,
                new[] { new ParameterSymbol("p", byteSlice, 0) },
                new TypeSymbol[] { i64, emptyIface }));
            bodyType.AddMethod(new MethodSymbol("Close", bodyType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));

            responseType.AddMethod(new MethodSymbol("Read", responseType, false,
                new[] { new ParameterSymbol("p", byteSlice, 0) },
                new TypeSymbol[] { i64, emptyIface }));

            pkg.AddExport(responseType);
            pkg.AddExport(bodyType);

            // http.Get(url string) (*Response, error)
            pkg.AddExport(new FunctionSymbol("Get",
                new[] { P("url", s, 0) },
                new TypeSymbol[] { responseType, emptyIface }, packageName: "http"));

            // http.Post(url, contentType string, body io.Reader) (*Response, error)
            pkg.AddExport(new FunctionSymbol("Post",
                new[] { P("url", s, 0), P("contentType", s, 1), P("body", emptyIface, 2) },
                new TypeSymbol[] { responseType, emptyIface }, packageName: "http"));

            // URL type (net/url.URL)
            var urlType = new StructTypeSymbol("URL", new[]
            {
                new FieldSymbol("Scheme", s, 0),
                new FieldSymbol("Host", s, 1),
                new FieldSymbol("Path", s, 2),
                new FieldSymbol("RawPath", s, 3),
                new FieldSymbol("RawQuery", s, 4),
                new FieldSymbol("Fragment", s, 5),
                new FieldSymbol("User", emptyIface, 6),
                new FieldSymbol("Opaque", s, 7),
                new FieldSymbol("ForceQuery", BuiltinTypes.Bool, 8),
            });
            urlType.AddMethod(new MethodSymbol("String", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("Query", urlType, false,
                Array.Empty<ParameterSymbol>(), emptyIface));
            urlType.AddMethod(new MethodSymbol("RequestURI", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("Hostname", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("Port", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("EscapedPath", urlType, false,
                Array.Empty<ParameterSymbol>(), s));

            // Request type
            var requestType = new StructTypeSymbol("Request",
                new[]
                {
                    new FieldSymbol("Method", s, 0),
                    new FieldSymbol("URL", new PointerTypeSymbol(urlType), 1),
                    new FieldSymbol("Header", emptyIface, 2),
                    new FieldSymbol("Body", emptyIface, 3),
                    new FieldSymbol("ContentLength", i64, 4),
                    new FieldSymbol("Host", s, 5),
                    new FieldSymbol("RemoteAddr", s, 6),
                    new FieldSymbol("RequestURI", s, 7),
                    new FieldSymbol("Proto", s, 8),
                });
            requestType.AddMethod(new MethodSymbol("Clone", requestType, false,
                new[] { P("ctx", emptyIface, 0) },
                new PointerTypeSymbol(requestType)));
            requestType.AddMethod(new MethodSymbol("WithContext", requestType, false,
                new[] { P("ctx", emptyIface, 0) },
                new PointerTypeSymbol(requestType)));
            requestType.AddMethod(new MethodSymbol("Context", requestType, false,
                System.Array.Empty<ParameterSymbol>(), emptyIface));
            pkg.AddExport(requestType);

            // NewRequest(method, url string, body io.Reader) (*Request, error)
            pkg.AddExport(new FunctionSymbol("NewRequest",
                new[] { P("method", s, 0), P("url", s, 1), P("body", emptyIface, 2) },
                new TypeSymbol[] { new PointerTypeSymbol(requestType), emptyIface }, packageName: "http"));

            // HandlerFunc type — func(ResponseWriter, *Request)
            var responseWriterIface = new InterfaceTypeSymbol("ResponseWriter", Array.Empty<MethodSymbol>());
            pkg.AddExport(responseWriterIface);

            var handlerFuncType = new FunctionTypeSymbol(
                new TypeSymbol[] { responseWriterIface, new PointerTypeSymbol(requestType) },
                Array.Empty<TypeSymbol>());
            // Export as a named type
            var namedHandlerFunc = new TypeSymbol("HandlerFunc", TypeKind.Function, handlerFuncType);
            // HandlerFunc implements Handler via ServeHTTP
            namedHandlerFunc.AddMethod(new MethodSymbol("ServeHTTP", namedHandlerFunc, false,
                new[] { new ParameterSymbol("w", responseWriterIface, 0),
                        new ParameterSymbol("r", new PointerTypeSymbol(requestType), 1) },
                BuiltinTypes.Void));
            pkg.AddExport(namedHandlerFunc);

            // Handler interface
            var handlerIface = new InterfaceTypeSymbol("Handler", new[]
            {
                new MethodSymbol("ServeHTTP", null!, false,
                    new[] { new ParameterSymbol("w", responseWriterIface, 0),
                            new ParameterSymbol("r", new PointerTypeSymbol(requestType), 1) },
                    BuiltinTypes.Void),
            });
            pkg.AddExport(handlerIface);

            // ResponseWriter methods
            responseWriterIface.SetMethods(new[]
            {
                new MethodSymbol("Header", responseWriterIface, false,
                    Array.Empty<ParameterSymbol>(), emptyIface),
                new MethodSymbol("Write", responseWriterIface, false,
                    new[] { new ParameterSymbol("b", byteSlice, 0) },
                    new TypeSymbol[] { i64, emptyIface }),
                new MethodSymbol("WriteHeader", responseWriterIface, false,
                    new[] { new ParameterSymbol("statusCode", i64, 0) },
                    BuiltinTypes.Void),
            });

            // Transport type
            var tlsConfigType = new StructTypeSymbol("TLSClientConfig", Array.Empty<FieldSymbol>());
            var transportType = new StructTypeSymbol("Transport",
                new[]
                {
                    new FieldSymbol("TLSClientConfig", emptyIface, 0),
                    new FieldSymbol("DisableKeepAlives", BuiltinTypes.Bool, 1),
                    new FieldSymbol("DisableCompression", BuiltinTypes.Bool, 2),
                    new FieldSymbol("MaxIdleConns", i64, 3),
                    new FieldSymbol("MaxIdleConnsPerHost", i64, 4),
                    new FieldSymbol("IdleConnTimeout", emptyIface, 5),
                    new FieldSymbol("TLSHandshakeTimeout", emptyIface, 6),
                    new FieldSymbol("ExpectContinueTimeout", emptyIface, 7),
                    new FieldSymbol("Proxy", emptyIface, 8),
                    new FieldSymbol("DialContext", emptyIface, 9),
                    new FieldSymbol("ForceAttemptHTTP2", BuiltinTypes.Bool, 10),
                    new FieldSymbol("ResponseHeaderTimeout", emptyIface, 11),
                    new FieldSymbol("Dial", emptyIface, 12),
                    new FieldSymbol("DialTLS", emptyIface, 13),
                });
            transportType.AddMethod(new MethodSymbol("RoundTrip", transportType, false,
                new[] { new ParameterSymbol("req", new PointerTypeSymbol(requestType), 0) },
                new TypeSymbol[] { new PointerTypeSymbol(responseType), emptyIface }));
            transportType.AddMethod(new MethodSymbol("CloseIdleConnections", transportType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            pkg.AddExport(transportType);

            // RoundTripper interface
            var roundTripperIface = new InterfaceTypeSymbol("RoundTripper", new[]
            {
                new MethodSymbol("RoundTrip", BuiltinTypes.EmptyInterface, false,
                    new[] { new ParameterSymbol("req", new PointerTypeSymbol(requestType), 0) },
                    new TypeSymbol[] { new PointerTypeSymbol(responseType), BuiltinTypes.Error }),
            });
            pkg.AddExport(roundTripperIface);

            // Client type
            var clientType = new StructTypeSymbol("Client",
                new[]
                {
                    new FieldSymbol("Transport", emptyIface, 0),
                    new FieldSymbol("Timeout", emptyIface, 1),
                    new FieldSymbol("Jar", emptyIface, 2),
                    new FieldSymbol("CheckRedirect", emptyIface, 3),
                });
            clientType.AddMethod(new MethodSymbol("Do", clientType, false,
                new[] { new ParameterSymbol("req", new PointerTypeSymbol(requestType), 0) },
                new TypeSymbol[] { new PointerTypeSymbol(responseType), emptyIface }));
            clientType.AddMethod(new MethodSymbol("Get", clientType, false,
                new[] { P("url", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(responseType), emptyIface }));
            clientType.AddMethod(new MethodSymbol("Post", clientType, false,
                new[] { P("url", s, 0), P("contentType", s, 1), P("body", emptyIface, 2) },
                new TypeSymbol[] { new PointerTypeSymbol(responseType), emptyIface }));
            clientType.AddMethod(new MethodSymbol("Head", clientType, false,
                new[] { P("url", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(responseType), emptyIface }));
            pkg.AddExport(clientType);

            // http.DefaultTransport
            pkg.AddExport(new PackageVarSymbol("DefaultTransport", emptyIface,
                typeof(object), "DefaultTransport"));

            // http.DefaultClient
            pkg.AddExport(new PackageVarSymbol("DefaultClient", new PointerTypeSymbol(clientType),
                typeof(object), "DefaultClient"));

            // http.ProxyFromEnvironment(req *Request) (*url.URL, error)
            pkg.AddExport(new FunctionSymbol("ProxyFromEnvironment",
                new[] { P("req", new PointerTypeSymbol(requestType), 0) },
                new TypeSymbol[] { emptyIface, emptyIface }, packageName: "http"));

            // Status code constants
            pkg.AddExport(new ConstantSymbol("StatusOK", i64, (long)200));
            pkg.AddExport(new ConstantSymbol("StatusCreated", i64, (long)201));
            pkg.AddExport(new ConstantSymbol("StatusAccepted", i64, (long)202));
            pkg.AddExport(new ConstantSymbol("StatusNoContent", i64, (long)204));
            pkg.AddExport(new ConstantSymbol("StatusPartialContent", i64, (long)206));
            pkg.AddExport(new ConstantSymbol("StatusMultipleChoices", i64, (long)300));
            pkg.AddExport(new ConstantSymbol("StatusMovedPermanently", i64, (long)301));
            pkg.AddExport(new ConstantSymbol("StatusFound", i64, (long)302));
            pkg.AddExport(new ConstantSymbol("StatusSeeOther", i64, (long)303));
            pkg.AddExport(new ConstantSymbol("StatusNotModified", i64, (long)304));
            pkg.AddExport(new ConstantSymbol("StatusTemporaryRedirect", i64, (long)307));
            pkg.AddExport(new ConstantSymbol("StatusPermanentRedirect", i64, (long)308));
            pkg.AddExport(new ConstantSymbol("StatusBadRequest", i64, (long)400));
            pkg.AddExport(new ConstantSymbol("StatusUnauthorized", i64, (long)401));
            pkg.AddExport(new ConstantSymbol("StatusForbidden", i64, (long)403));
            pkg.AddExport(new ConstantSymbol("StatusNotFound", i64, (long)404));
            pkg.AddExport(new ConstantSymbol("StatusMethodNotAllowed", i64, (long)405));
            pkg.AddExport(new ConstantSymbol("StatusConflict", i64, (long)409));
            pkg.AddExport(new ConstantSymbol("StatusGone", i64, (long)410));
            pkg.AddExport(new ConstantSymbol("StatusInternalServerError", i64, (long)500));
            pkg.AddExport(new ConstantSymbol("StatusNotImplemented", i64, (long)501));
            pkg.AddExport(new ConstantSymbol("StatusBadGateway", i64, (long)502));
            pkg.AddExport(new ConstantSymbol("StatusServiceUnavailable", i64, (long)503));

            // Method constants
            pkg.AddExport(new ConstantSymbol("MethodGet", s, "GET"));
            pkg.AddExport(new ConstantSymbol("MethodPost", s, "POST"));
            pkg.AddExport(new ConstantSymbol("MethodPut", s, "PUT"));
            pkg.AddExport(new ConstantSymbol("MethodDelete", s, "DELETE"));
            pkg.AddExport(new ConstantSymbol("MethodPatch", s, "PATCH"));
            pkg.AddExport(new ConstantSymbol("MethodHead", s, "HEAD"));
            pkg.AddExport(new ConstantSymbol("MethodOptions", s, "OPTIONS"));
            pkg.AddExport(new ConstantSymbol("MethodConnect", s, "CONNECT"));
            pkg.AddExport(new ConstantSymbol("MethodTrace", s, "TRACE"));

            // More status codes
            pkg.AddExport(new ConstantSymbol("StatusTooManyRequests", i64, (long)429));
            pkg.AddExport(new ConstantSymbol("StatusRequestEntityTooLarge", i64, (long)413));
            pkg.AddExport(new ConstantSymbol("StatusUnprocessableEntity", i64, (long)422));
            pkg.AddExport(new ConstantSymbol("StatusTeapot", i64, (long)418));

            // Header type (map[string][]string)
            var headerType = new TypeSymbol("Header", TypeKind.Map,
                new MapTypeSymbol(s, new SliceTypeSymbol(s)));
            headerType.AddMethod(new MethodSymbol("Get", headerType, false,
                new[] { P("key", s, 0) }, s));
            headerType.AddMethod(new MethodSymbol("Set", headerType, false,
                new[] { P("key", s, 0), P("value", s, 1) }, BuiltinTypes.Void));
            headerType.AddMethod(new MethodSymbol("Add", headerType, false,
                new[] { P("key", s, 0), P("value", s, 1) }, BuiltinTypes.Void));
            headerType.AddMethod(new MethodSymbol("Del", headerType, false,
                new[] { P("key", s, 0) }, BuiltinTypes.Void));
            headerType.AddMethod(new MethodSymbol("Values", headerType, false,
                new[] { P("key", s, 0) }, new SliceTypeSymbol(s)));
            pkg.AddExport(headerType);

            // NotFound handler
            pkg.AddExport(new FunctionSymbol("NotFound",
                new[] { P("w", emptyIface, 0), P("r", emptyIface, 1) },
                Array.Empty<TypeSymbol>(), packageName: "http"));

            // NewRequestWithContext(ctx context.Context, method, url string, body io.Reader) (*Request, error)
            pkg.AddExport(new FunctionSymbol("NewRequestWithContext",
                new[] { P("ctx", emptyIface, 0), P("method", s, 1),
                        P("url", s, 2), P("body", emptyIface, 3) },
                new TypeSymbol[] { new PointerTypeSymbol(requestType), BuiltinTypes.Error },
                packageName: "http"));

            return pkg;
        }

        private static PackageSymbol CreateReflectPackage()
        {
            var pkg = new PackageSymbol("reflect", "reflect");

            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // reflect.Kind — named type backed by int
            var kindType = new TypeSymbol("Kind", TypeKind.Int, BuiltinTypes.Int);
            kindType.AddMethod(new MethodSymbol("String", kindType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            pkg.AddExport(kindType);

            // reflect.SliceHeader type (deprecated but still used)
            var uintptrType = BuiltinTypes.Uintptr;
            var sliceHeaderType = new StructTypeSymbol("SliceHeader",
                new[]
                {
                    new FieldSymbol("Data", uintptrType, 0),
                    new FieldSymbol("Len", i64, 1),
                    new FieldSymbol("Cap", i64, 2),
                });
            pkg.AddExport(sliceHeaderType);

            // reflect.StringHeader type (deprecated but still used)
            var stringHeaderType = new StructTypeSymbol("StringHeader",
                new[]
                {
                    new FieldSymbol("Data", uintptrType, 0),
                    new FieldSymbol("Len", i64, 1),
                });
            pkg.AddExport(stringHeaderType);

            // reflect.StructTag type (named string with Get/Lookup methods)
            var structTagType = new TypeSymbol("StructTag", TypeKind.String, s);
            structTagType.AddMethod(new MethodSymbol("Get", structTagType, false,
                new[] { P("key", s, 0) }, s));
            structTagType.AddMethod(new MethodSymbol("Lookup", structTagType, false,
                new[] { P("key", s, 0) },
                new TypeSymbol[] { s, b }));
            pkg.AddExport(structTagType);

            // reflect.StructField type (declared early so Type.Field can return it)
            var structFieldType = new StructTypeSymbol("StructField",
                new[]
                {
                    new FieldSymbol("Name", s, 0),
                    new FieldSymbol("PkgPath", s, 1),
                    new FieldSymbol("Tag", structTagType, 2),
                    new FieldSymbol("Index", new SliceTypeSymbol(i64), 3),
                    new FieldSymbol("Anonymous", b, 4),
                    new FieldSymbol("Offset", BuiltinTypes.Uintptr, 5),
                });
            // Type field added after typeType is created

            // reflect.Type type (interface with methods)
            var typeType = new InterfaceTypeSymbol("Type", Array.Empty<MethodSymbol>());
            typeType.AddMethod(new MethodSymbol("Name", typeType, false,
                Array.Empty<ParameterSymbol>(), s));
            typeType.AddMethod(new MethodSymbol("Kind", typeType, false,
                Array.Empty<ParameterSymbol>(), kindType));
            typeType.AddMethod(new MethodSymbol("String", typeType, false,
                Array.Empty<ParameterSymbol>(), s));
            typeType.AddMethod(new MethodSymbol("NumField", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("Field", typeType, false,
                new[] { P("i", i64, 0) },
                new TypeSymbol[] { structFieldType }));
            typeType.AddMethod(new MethodSymbol("FieldByName", typeType, false,
                new[] { P("name", s, 0) },
                new TypeSymbol[] { structFieldType, b }));
            typeType.AddMethod(new MethodSymbol("NumMethod", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));

            // reflect.Method struct — use interface{} as placeholder for Func (Value declared later)
            var methodType = new StructTypeSymbol("Method", new List<FieldSymbol>
            {
                new FieldSymbol("Name", s, 0),
                new FieldSymbol("PkgPath", s, 1),
                new FieldSymbol("Type", typeType, 2),
                new FieldSymbol("Index", i64, 3),
            });
            pkg.AddExport(methodType);

            typeType.AddMethod(new MethodSymbol("Method", typeType, false,
                new[] { P("i", i64, 0) }, methodType));
            typeType.AddMethod(new MethodSymbol("MethodByName", typeType, false,
                new[] { P("name", s, 0) },
                new TypeSymbol[] { methodType, b }));
            typeType.AddMethod(new MethodSymbol("FieldByNameFunc", typeType, false,
                new[] { P("match", new FunctionTypeSymbol(
                    new TypeSymbol[] { s }, new TypeSymbol[] { b }), 0) },
                new TypeSymbol[] { structFieldType, b }));
            typeType.AddMethod(new MethodSymbol("Elem", typeType, false,
                Array.Empty<ParameterSymbol>(), typeType));
            typeType.AddMethod(new MethodSymbol("Key", typeType, false,
                Array.Empty<ParameterSymbol>(), typeType));
            typeType.AddMethod(new MethodSymbol("Len", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("Comparable", typeType, false,
                Array.Empty<ParameterSymbol>(), b));
            typeType.AddMethod(new MethodSymbol("Size", typeType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uintptr));
            typeType.AddMethod(new MethodSymbol("AssignableTo", typeType, false,
                new[] { P("u", typeType, 0) }, b));
            typeType.AddMethod(new MethodSymbol("Implements", typeType, false,
                new[] { P("u", typeType, 0) }, b));
            typeType.AddMethod(new MethodSymbol("ConvertibleTo", typeType, false,
                new[] { P("u", typeType, 0) }, b));
            typeType.AddMethod(new MethodSymbol("PkgPath", typeType, false,
                Array.Empty<ParameterSymbol>(), s));
            typeType.AddMethod(new MethodSymbol("NumIn", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("NumOut", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("In", typeType, false,
                new[] { P("i", i64, 0) }, typeType));
            typeType.AddMethod(new MethodSymbol("Out", typeType, false,
                new[] { P("i", i64, 0) }, typeType));
            typeType.AddMethod(new MethodSymbol("Bits", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("Align", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("ChanDir", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("IsVariadic", typeType, false,
                Array.Empty<ParameterSymbol>(), b));
            typeType.AddMethod(new MethodSymbol("FieldAlign", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            pkg.AddExport(typeType);

            // Now add Type field to StructField
            structFieldType.SetFields(new[]
            {
                new FieldSymbol("Name", s, 0),
                new FieldSymbol("PkgPath", s, 1),
                new FieldSymbol("Type", typeType, 2),
                new FieldSymbol("Tag", structTagType, 3),
                new FieldSymbol("Index", new SliceTypeSymbol(i64), 4),
                new FieldSymbol("Anonymous", b, 5),
                new FieldSymbol("Offset", BuiltinTypes.Uintptr, 6),
                new FieldSymbol("IsExported", b, 7),
            });
            structFieldType.AddMethod(new MethodSymbol("IsExported", structFieldType, false,
                Array.Empty<ParameterSymbol>(), b));
            pkg.AddExport(structFieldType);

            // reflect.Value type
            var valueType = new StructTypeSymbol("Value", Array.Empty<FieldSymbol>());

            // Now add Func field to Method struct (needs valueType)
            methodType.SetFields(new List<FieldSymbol>
            {
                new FieldSymbol("Name", s, 0),
                new FieldSymbol("PkgPath", s, 1),
                new FieldSymbol("Type", typeType, 2),
                new FieldSymbol("Func", valueType, 3),
                new FieldSymbol("Index", i64, 4),
            });
            valueType.AddMethod(new MethodSymbol("Kind", valueType, false,
                Array.Empty<ParameterSymbol>(), kindType));
            valueType.AddMethod(new MethodSymbol("Type", valueType, false,
                Array.Empty<ParameterSymbol>(), typeType));
            valueType.AddMethod(new MethodSymbol("IsValid", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("IsNil", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("IsZero", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("Interface", valueType, false,
                Array.Empty<ParameterSymbol>(), emptyIface));
            valueType.AddMethod(new MethodSymbol("Int", valueType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64));
            valueType.AddMethod(new MethodSymbol("Uint", valueType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint64));
            valueType.AddMethod(new MethodSymbol("Float", valueType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Float64));
            valueType.AddMethod(new MethodSymbol("Complex", valueType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Complex128));
            valueType.AddMethod(new MethodSymbol("Bool", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("String", valueType, false,
                Array.Empty<ParameterSymbol>(), s));
            valueType.AddMethod(new MethodSymbol("Bytes", valueType, false,
                Array.Empty<ParameterSymbol>(), new SliceTypeSymbol(BuiltinTypes.Byte)));
            valueType.AddMethod(new MethodSymbol("Len", valueType, false,
                Array.Empty<ParameterSymbol>(), i64));
            valueType.AddMethod(new MethodSymbol("Cap", valueType, false,
                Array.Empty<ParameterSymbol>(), i64));
            valueType.AddMethod(new MethodSymbol("Index", valueType, false,
                new[] { P("i", i64, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("Slice", valueType, false,
                new[] { P("i", i64, 0), P("j", i64, 1) }, valueType));
            valueType.AddMethod(new MethodSymbol("MapKeys", valueType, false,
                Array.Empty<ParameterSymbol>(),
                new SliceTypeSymbol(valueType)));
            valueType.AddMethod(new MethodSymbol("MapIndex", valueType, false,
                new[] { P("key", valueType, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("SetMapIndex", valueType, false,
                new[] { P("key", valueType, 0), P("elem", valueType, 1) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("NumField", valueType, false,
                Array.Empty<ParameterSymbol>(), i64));
            valueType.AddMethod(new MethodSymbol("Field", valueType, false,
                new[] { P("i", i64, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("FieldByName", valueType, false,
                new[] { P("name", s, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("FieldByIndex", valueType, false,
                new[] { P("index", new SliceTypeSymbol(i64), 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("FieldByNameFunc", valueType, false,
                new[] { P("match", new FunctionTypeSymbol(
                    new TypeSymbol[] { s }, new TypeSymbol[] { b }), 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("MethodByName", valueType, false,
                new[] { P("name", s, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("Call", valueType, false,
                new[] { P("in", new SliceTypeSymbol(valueType), 0) },
                new SliceTypeSymbol(valueType)));
            valueType.AddMethod(new MethodSymbol("Elem", valueType, false,
                Array.Empty<ParameterSymbol>(), valueType));
            valueType.AddMethod(new MethodSymbol("Addr", valueType, false,
                Array.Empty<ParameterSymbol>(), valueType));
            valueType.AddMethod(new MethodSymbol("Pointer", valueType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uintptr));
            valueType.AddMethod(new MethodSymbol("CanAddr", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("CanSet", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("CanInterface", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("CanConvert", valueType, false,
                new[] { P("t", typeType, 0) }, b));
            valueType.AddMethod(new MethodSymbol("Convert", valueType, false,
                new[] { P("t", typeType, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("Set", valueType, false,
                new[] { P("x", valueType, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetInt", valueType, false,
                new[] { P("x", BuiltinTypes.Int64, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetUint", valueType, false,
                new[] { P("x", BuiltinTypes.Uint64, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetFloat", valueType, false,
                new[] { P("x", BuiltinTypes.Float64, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetString", valueType, false,
                new[] { P("x", s, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetBool", valueType, false,
                new[] { P("x", b, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetBytes", valueType, false,
                new[] { P("x", new SliceTypeSymbol(BuiltinTypes.Byte), 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("NumMethod", valueType, false,
                Array.Empty<ParameterSymbol>(), i64));
            valueType.AddMethod(new MethodSymbol("Method", valueType, false,
                new[] { P("i", i64, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("IsExported", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("UnsafeAddr", valueType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uintptr));
            valueType.AddMethod(new MethodSymbol("SetLen", valueType, false,
                new[] { P("n", i64, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetCap", valueType, false,
                new[] { P("n", i64, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("OverflowInt", valueType, false,
                new[] { P("x", i64, 0) }, b));
            valueType.AddMethod(new MethodSymbol("OverflowUint", valueType, false,
                new[] { P("x", BuiltinTypes.Uint64, 0) }, b));
            valueType.AddMethod(new MethodSymbol("OverflowFloat", valueType, false,
                new[] { P("x", BuiltinTypes.Float64, 0) }, b));
            valueType.AddMethod(new MethodSymbol("SetComplex", valueType, false,
                new[] { P("x", BuiltinTypes.Complex128, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("MapRange", valueType, false,
                Array.Empty<ParameterSymbol>(), new StructTypeSymbol("MapIter", Array.Empty<FieldSymbol>())));
            pkg.AddExport(valueType);

            // Top-level functions
            pkg.AddExport(new FunctionSymbol("TypeOf",
                new[] { P("v", emptyIface, 0) },
                new TypeSymbol[] { typeType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("ValueOf",
                new[] { P("v", emptyIface, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("DeepEqual",
                new[] { P("x", emptyIface, 0), P("y", emptyIface, 1) },
                new TypeSymbol[] { b }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("Zero",
                new[] { P("typ", typeType, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("typ", typeType, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("NewAt",
                new[] { P("typ", typeType, 0), P("p", emptyIface, 1) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("MakeSlice",
                new[] { P("typ", typeType, 0), P("len", i64, 1), P("cap", i64, 2) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("MakeMap",
                new[] { P("typ", typeType, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("MakeMapWithSize",
                new[] { P("typ", typeType, 0), P("n", i64, 1) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("Indirect",
                new[] { P("v", valueType, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("Copy",
                new[] { P("dst", valueType, 0), P("src", valueType, 1) },
                new TypeSymbol[] { i64 }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("Append",
                new[] { P("s", valueType, 0), P("x", valueType, 1) },
                new TypeSymbol[] { valueType }, isVariadic: true, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("AppendSlice",
                new[] { P("s", valueType, 0), P("t", valueType, 1) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("SliceOf",
                new[] { P("t", typeType, 0) },
                new TypeSymbol[] { typeType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("MapOf",
                new[] { P("key", typeType, 0), P("elem", typeType, 1) },
                new TypeSymbol[] { typeType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("ArrayOf",
                new[] { P("count", BuiltinTypes.Int, 0), P("elem", typeType, 1) },
                new TypeSymbol[] { typeType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("PtrTo",
                new[] { P("t", typeType, 0) },
                new TypeSymbol[] { typeType }, packageName: "reflect"));
            pkg.AddExport(new FunctionSymbol("PointerTo",
                new[] { P("t", typeType, 0) },
                new TypeSymbol[] { typeType }, packageName: "reflect"));

            // Kind constants — iota values (Invalid=0, Bool=1, ..., String=24, Struct=25, UnsafePointer=26)
            pkg.AddExport(new ConstantSymbol("Invalid", kindType, (long)0));
            pkg.AddExport(new ConstantSymbol("Bool", kindType, (long)1));
            pkg.AddExport(new ConstantSymbol("Int", kindType, (long)2));
            pkg.AddExport(new ConstantSymbol("Int8", kindType, (long)3));
            pkg.AddExport(new ConstantSymbol("Int16", kindType, (long)4));
            pkg.AddExport(new ConstantSymbol("Int32", kindType, (long)5));
            pkg.AddExport(new ConstantSymbol("Int64", kindType, (long)6));
            pkg.AddExport(new ConstantSymbol("Uint", kindType, (long)7));
            pkg.AddExport(new ConstantSymbol("Uint8", kindType, (long)8));
            pkg.AddExport(new ConstantSymbol("Uint16", kindType, (long)9));
            pkg.AddExport(new ConstantSymbol("Uint32", kindType, (long)10));
            pkg.AddExport(new ConstantSymbol("Uint64", kindType, (long)11));
            pkg.AddExport(new ConstantSymbol("Uintptr", kindType, (long)12));
            pkg.AddExport(new ConstantSymbol("Float32", kindType, (long)13));
            pkg.AddExport(new ConstantSymbol("Float64", kindType, (long)14));
            pkg.AddExport(new ConstantSymbol("Complex64", kindType, (long)15));
            pkg.AddExport(new ConstantSymbol("Complex128", kindType, (long)16));
            pkg.AddExport(new ConstantSymbol("Array", kindType, (long)17));
            pkg.AddExport(new ConstantSymbol("Chan", kindType, (long)18));
            pkg.AddExport(new ConstantSymbol("Func", kindType, (long)19));
            pkg.AddExport(new ConstantSymbol("Interface", kindType, (long)20));
            pkg.AddExport(new ConstantSymbol("Map", kindType, (long)21));
            pkg.AddExport(new ConstantSymbol("Pointer", kindType, (long)22));
            pkg.AddExport(new ConstantSymbol("Ptr", kindType, (long)22));
            pkg.AddExport(new ConstantSymbol("Slice", kindType, (long)23));
            pkg.AddExport(new ConstantSymbol("String", kindType, (long)24));
            pkg.AddExport(new ConstantSymbol("Struct", kindType, (long)25));
            pkg.AddExport(new ConstantSymbol("UnsafePointer", kindType, (long)26));

            // ChanDir constants
            var chanDirType = new StructTypeSymbol("ChanDir", Array.Empty<FieldSymbol>());
            pkg.AddExport(chanDirType);
            pkg.AddExport(new ConstantSymbol("RecvDir", chanDirType, (long)1));
            pkg.AddExport(new ConstantSymbol("SendDir", chanDirType, (long)2));
            pkg.AddExport(new ConstantSymbol("BothDir", chanDirType, (long)3));

            // SelectDir type and constants
            var selectDirType = new StructTypeSymbol("SelectDir", Array.Empty<FieldSymbol>());
            pkg.AddExport(selectDirType);
            pkg.AddExport(new ConstantSymbol("SelectSend", selectDirType, (long)1));
            pkg.AddExport(new ConstantSymbol("SelectRecv", selectDirType, (long)2));
            pkg.AddExport(new ConstantSymbol("SelectDefault", selectDirType, (long)3));

            return pkg;
        }

        private static PackageSymbol CreateRuntimePackage()
        {
            var pkg = new PackageSymbol("runtime", "runtime");

            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // Func type — represents a runtime function
            var funcType = new StructTypeSymbol("Func", Array.Empty<FieldSymbol>());
            funcType.AddMethod(new MethodSymbol("Name", funcType, false,
                Array.Empty<ParameterSymbol>(), s));
            funcType.AddMethod(new MethodSymbol("Entry", funcType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uintptr));
            funcType.AddMethod(new MethodSymbol("FileLine", funcType, false,
                new[] { P("pc", BuiltinTypes.Uintptr, 0) },
                new TypeSymbol[] { s, i64 }));
            pkg.AddExport(funcType);

            // FuncForPC(pc uintptr) *Func
            var ptrFuncType = new PointerTypeSymbol(funcType);
            pkg.AddExport(new FunctionSymbol("FuncForPC",
                new[] { P("pc", BuiltinTypes.Uintptr, 0) },
                new TypeSymbol[] { ptrFuncType }, packageName: "runtime"));

            // Callers(skip int, pc []uintptr) int
            pkg.AddExport(new FunctionSymbol("Callers",
                new[] { P("skip", i64, 0), P("pc", new SliceTypeSymbol(BuiltinTypes.Uintptr), 1) },
                new TypeSymbol[] { i64 }, packageName: "runtime"));

            // Caller(skip int) (pc uintptr, file string, line int, ok bool)
            pkg.AddExport(new FunctionSymbol("Caller",
                new[] { P("skip", i64, 0) },
                new TypeSymbol[] { BuiltinTypes.Uintptr, s, i64, BuiltinTypes.Bool }, packageName: "runtime"));

            // GC()
            pkg.AddExport(new FunctionSymbol("GC",
                Array.Empty<ParameterSymbol>(), Array.Empty<TypeSymbol>(), packageName: "runtime"));

            // Gosched()
            pkg.AddExport(new FunctionSymbol("Gosched",
                Array.Empty<ParameterSymbol>(), Array.Empty<TypeSymbol>(), packageName: "runtime"));

            // NumCPU() int
            pkg.AddExport(new FunctionSymbol("NumCPU",
                Array.Empty<ParameterSymbol>(), new TypeSymbol[] { i64 }, packageName: "runtime"));

            // NumGoroutine() int
            pkg.AddExport(new FunctionSymbol("NumGoroutine",
                Array.Empty<ParameterSymbol>(), new TypeSymbol[] { i64 }, packageName: "runtime"));

            // GOMAXPROCS(n int) int
            pkg.AddExport(new FunctionSymbol("GOMAXPROCS",
                new[] { P("n", i64, 0) }, new TypeSymbol[] { i64 }, packageName: "runtime"));

            // SetFinalizer(obj interface{}, finalizer interface{})
            pkg.AddExport(new FunctionSymbol("SetFinalizer",
                new[] { P("obj", emptyIface, 0), P("finalizer", emptyIface, 1) },
                Array.Empty<TypeSymbol>(), packageName: "runtime"));

            // runtime.Frame type
            var frameType = new StructTypeSymbol("Frame", new[]
            {
                new FieldSymbol("PC", BuiltinTypes.Uintptr, 0),
                new FieldSymbol("Func", ptrFuncType, 1),
                new FieldSymbol("Function", s, 2),
                new FieldSymbol("File", s, 3),
                new FieldSymbol("Line", i64, 4),
                new FieldSymbol("Entry", BuiltinTypes.Uintptr, 5),
            });
            pkg.AddExport(frameType);

            // runtime.Frames type (iterator)
            var framesType = new StructTypeSymbol("Frames", Array.Empty<FieldSymbol>());
            framesType.AddMethod(new MethodSymbol("Next", framesType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { frameType, BuiltinTypes.Bool }));
            pkg.AddExport(framesType);

            // runtime.CallersFrames(callers []uintptr) *Frames
            pkg.AddExport(new FunctionSymbol("CallersFrames",
                new[] { P("callers", new SliceTypeSymbol(BuiltinTypes.Uintptr), 0) },
                new TypeSymbol[] { new PointerTypeSymbol(framesType) }, packageName: "runtime"));

            // runtime.KeepAlive(x interface{})
            pkg.AddExport(new FunctionSymbol("KeepAlive",
                new[] { P("x", emptyIface, 0) },
                Array.Empty<TypeSymbol>(), packageName: "runtime"));

            // runtime.Version() string
            pkg.AddExport(new FunctionSymbol("Version",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s }, packageName: "runtime"));

            // runtime.Stack(buf []byte, all bool) int
            pkg.AddExport(new FunctionSymbol("Stack",
                new[] { P("buf", new SliceTypeSymbol(BuiltinTypes.Byte), 0), P("all", BuiltinTypes.Bool, 1) },
                new TypeSymbol[] { i64 }, packageName: "runtime"));

            // runtime.Goexit()
            pkg.AddExport(new FunctionSymbol("Goexit",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "runtime"));

            // runtime.Gosched()
            pkg.AddExport(new FunctionSymbol("Gosched",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "runtime"));

            // runtime.LockOSThread() / runtime.UnlockOSThread()
            pkg.AddExport(new FunctionSymbol("LockOSThread",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "runtime"));
            pkg.AddExport(new FunctionSymbol("UnlockOSThread",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "runtime"));

            // GOOS, GOARCH string constants
            pkg.AddExport(new PackageVarSymbol("GOOS", s, typeof(Ngo.Runtime.GoRuntime), "GOOS"));
            pkg.AddExport(new PackageVarSymbol("GOARCH", s, typeof(Ngo.Runtime.GoRuntime), "GOARCH"));

            // runtime.Error interface { Error() string ; RuntimeError() }
            var runtimeErrorIface = new InterfaceTypeSymbol("Error", new[]
            {
                new MethodSymbol("Error", null!, false,
                    Array.Empty<ParameterSymbol>(), s),
                new MethodSymbol("RuntimeError", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void),
            });
            pkg.AddExport(runtimeErrorIface);

            // runtime.MemStats type (commonly used fields)
            var u64 = BuiltinTypes.Uint64;
            var u32 = BuiltinTypes.Uint32;
            var memStatsType = new StructTypeSymbol("MemStats", new[]
            {
                new FieldSymbol("Alloc", u64, 0),
                new FieldSymbol("TotalAlloc", u64, 1),
                new FieldSymbol("Sys", u64, 2),
                new FieldSymbol("Lookups", u64, 3),
                new FieldSymbol("Mallocs", u64, 4),
                new FieldSymbol("Frees", u64, 5),
                new FieldSymbol("HeapAlloc", u64, 6),
                new FieldSymbol("HeapSys", u64, 7),
                new FieldSymbol("HeapIdle", u64, 8),
                new FieldSymbol("HeapInuse", u64, 9),
                new FieldSymbol("HeapReleased", u64, 10),
                new FieldSymbol("HeapObjects", u64, 11),
                new FieldSymbol("StackInuse", u64, 12),
                new FieldSymbol("StackSys", u64, 13),
                new FieldSymbol("NumGC", u32, 14),
                new FieldSymbol("GCCPUFraction", BuiltinTypes.Float64, 15),
                new FieldSymbol("PauseTotalNs", u64, 16),
            });
            pkg.AddExport(memStatsType);

            // runtime.ReadMemStats(m *MemStats)
            pkg.AddExport(new FunctionSymbol("ReadMemStats",
                new[] { P("m", new PointerTypeSymbol(memStatsType), 0) },
                Array.Empty<TypeSymbol>(), packageName: "runtime"));

            return pkg;
        }

        private static PackageSymbol CreateUnsafePackage()
        {
            var pkg = new PackageSymbol("unsafe", "unsafe");

            var i64 = BuiltinTypes.Int;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // Pointer type — we model as uintptr for now
            // In Go, unsafe.Pointer is its own type, but for compatibility we treat it like uintptr
            var pointerType = new StructTypeSymbol("Pointer", Array.Empty<FieldSymbol>());
            pkg.AddExport(pointerType);

            // Sizeof(x ArbitraryType) uintptr
            pkg.AddExport(new FunctionSymbol("Sizeof",
                new[] { P("x", emptyIface, 0) },
                new TypeSymbol[] { BuiltinTypes.Uintptr }, packageName: "unsafe"));

            // Offsetof(x ArbitraryType) uintptr
            pkg.AddExport(new FunctionSymbol("Offsetof",
                new[] { P("x", emptyIface, 0) },
                new TypeSymbol[] { BuiltinTypes.Uintptr }, packageName: "unsafe"));

            // Alignof(x ArbitraryType) uintptr
            pkg.AddExport(new FunctionSymbol("Alignof",
                new[] { P("x", emptyIface, 0) },
                new TypeSymbol[] { BuiltinTypes.Uintptr }, packageName: "unsafe"));

            return pkg;
        }

        private static PackageSymbol CreateReflectlitePackage()
        {
            // internal/reflectlite is a subset of reflect used by stdlib packages like errors
            var pkg = new PackageSymbol("reflectlite", "internal/reflectlite");

            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // Type interface (same as reflect.Type subset)
            var typeType = new InterfaceTypeSymbol("Type", new[]
            {
                new MethodSymbol("Name", null!, false, Array.Empty<ParameterSymbol>(), s),
                new MethodSymbol("Kind", null!, false, Array.Empty<ParameterSymbol>(), i64),
                new MethodSymbol("String", null!, false, Array.Empty<ParameterSymbol>(), s),
                new MethodSymbol("Comparable", null!, false, Array.Empty<ParameterSymbol>(), b),
                new MethodSymbol("Elem", null!, false, Array.Empty<ParameterSymbol>(), emptyIface),
                new MethodSymbol("Implements", null!, false,
                    new[] { P("u", emptyIface, 0) }, b),
                new MethodSymbol("AssignableTo", null!, false,
                    new[] { P("u", emptyIface, 0) }, b),
            });
            pkg.AddExport(typeType);

            // Value struct (same as reflect.Value subset)
            var valueType = new StructTypeSymbol("Value", Array.Empty<FieldSymbol>());
            valueType.AddMethod(new MethodSymbol("Kind", valueType, false,
                Array.Empty<ParameterSymbol>(), i64));
            valueType.AddMethod(new MethodSymbol("Type", valueType, false,
                Array.Empty<ParameterSymbol>(), typeType));
            valueType.AddMethod(new MethodSymbol("IsValid", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("IsNil", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("Elem", valueType, false,
                Array.Empty<ParameterSymbol>(), valueType));
            valueType.AddMethod(new MethodSymbol("Set", valueType, false,
                new[] { P("x", valueType, 0) }, BuiltinTypes.Void));
            pkg.AddExport(valueType);

            // TypeOf(v interface{}) Type
            pkg.AddExport(new FunctionSymbol("TypeOf",
                new[] { P("v", emptyIface, 0) },
                new TypeSymbol[] { typeType }, packageName: "reflectlite"));

            // ValueOf(v interface{}) Value
            pkg.AddExport(new FunctionSymbol("ValueOf",
                new[] { P("v", emptyIface, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflectlite"));

            // Kind constants
            pkg.AddExport(new PackageVarSymbol("Invalid", i64, typeof(Ngo.Runtime.GoReflectKinds), "Invalid"));
            pkg.AddExport(new PackageVarSymbol("Ptr", i64, typeof(Ngo.Runtime.GoReflectKinds), "Ptr"));
            pkg.AddExport(new PackageVarSymbol("Interface", i64, typeof(Ngo.Runtime.GoReflectKinds), "Interface"));

            return pkg;
        }

        private static PackageSymbol CreateRuntimeDebugPackage()
        {
            var pkg = new PackageSymbol("debug", "runtime/debug");

            var s = BuiltinTypes.String;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            // debug.Stack() []byte
            pkg.AddExport(new FunctionSymbol("Stack",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { byteSlice }, packageName: "debug"));

            // debug.PrintStack()
            pkg.AddExport(new FunctionSymbol("PrintStack",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "debug"));

            // debug.SetGCPercent(percent int) int
            pkg.AddExport(new FunctionSymbol("SetGCPercent",
                new[] { P("percent", BuiltinTypes.Int, 0) },
                new TypeSymbol[] { BuiltinTypes.Int }, packageName: "debug"));

            // debug.FreeOSMemory()
            pkg.AddExport(new FunctionSymbol("FreeOSMemory",
                Array.Empty<ParameterSymbol>(),
                Array.Empty<TypeSymbol>(), packageName: "debug"));

            // debug.ReadBuildInfo() (*BuildInfo, bool)
            var buildInfoType = new StructTypeSymbol("BuildInfo",
                new[]
                {
                    new FieldSymbol("GoVersion", s, 0),
                    new FieldSymbol("Path", s, 1),
                    new FieldSymbol("Main", BuiltinTypes.EmptyInterface, 2),
                });
            pkg.AddExport(buildInfoType);
            pkg.AddExport(new FunctionSymbol("ReadBuildInfo",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new PointerTypeSymbol(buildInfoType), BuiltinTypes.Bool },
                packageName: "debug"));

            return pkg;
        }

        private static PackageSymbol CreateNetUrlPackage()
        {
            var pkg = new PackageSymbol("url", "net/url");

            var s = BuiltinTypes.String;
            var err = BuiltinTypes.Error;

            // Values type — map[string][]string (defined before URL so URL.Query() can return it)
            var valuesType = new TypeSymbol("Values", TypeKind.Map,
                new MapTypeSymbol(BuiltinTypes.String, new SliceTypeSymbol(BuiltinTypes.String)));
            valuesType.AddMethod(new MethodSymbol("Get", valuesType, false,
                new[] { P("key", s, 0) }, s));
            valuesType.AddMethod(new MethodSymbol("Set", valuesType, false,
                new[] { P("key", s, 0), P("value", s, 1) }, BuiltinTypes.Void));
            valuesType.AddMethod(new MethodSymbol("Add", valuesType, false,
                new[] { P("key", s, 0), P("value", s, 1) }, BuiltinTypes.Void));
            valuesType.AddMethod(new MethodSymbol("Del", valuesType, false,
                new[] { P("key", s, 0) }, BuiltinTypes.Void));
            valuesType.AddMethod(new MethodSymbol("Encode", valuesType, false,
                Array.Empty<ParameterSymbol>(), s));
            valuesType.AddMethod(new MethodSymbol("Has", valuesType, false,
                new[] { P("key", s, 0) }, BuiltinTypes.Bool));
            pkg.AddExport(valuesType);

            // url.URL type
            var urlType = new StructTypeSymbol("URL",
                new[]
                {
                    new FieldSymbol("Scheme", s, 0),
                    new FieldSymbol("Opaque", s, 1),
                    new FieldSymbol("Host", s, 2),
                    new FieldSymbol("Path", s, 3),
                    new FieldSymbol("RawPath", s, 4),
                    new FieldSymbol("RawQuery", s, 5),
                    new FieldSymbol("Fragment", s, 6),
                    new FieldSymbol("RawFragment", s, 7),
                    new FieldSymbol("User", BuiltinTypes.EmptyInterface, 8),
                    new FieldSymbol("ForceQuery", BuiltinTypes.Bool, 9),
                    new FieldSymbol("OmitHost", BuiltinTypes.Bool, 10),
                });
            urlType.AddMethod(new MethodSymbol("String", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("Query", urlType, false,
                Array.Empty<ParameterSymbol>(), valuesType));
            urlType.AddMethod(new MethodSymbol("Hostname", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("Port", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("RequestURI", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("EscapedPath", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("EscapedFragment", urlType, false,
                Array.Empty<ParameterSymbol>(), s));
            urlType.AddMethod(new MethodSymbol("ResolveReference", urlType, false,
                new[] { P("ref", new PointerTypeSymbol(urlType), 0) },
                new PointerTypeSymbol(urlType)));
            urlType.AddMethod(new MethodSymbol("Parse", urlType, false,
                new[] { P("ref", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(urlType), err }));
            pkg.AddExport(urlType);

            // url.Parse(rawURL string) (*URL, error)
            pkg.AddExport(new FunctionSymbol("Parse",
                new[] { P("rawurl", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(urlType), err },
                packageName: "url"));

            // url.ParseRequestURI(rawURL string) (*URL, error)
            pkg.AddExport(new FunctionSymbol("ParseRequestURI",
                new[] { P("rawurl", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(urlType), err },
                packageName: "url"));

            // url.PathEscape(s string) string
            pkg.AddExport(new FunctionSymbol("PathEscape",
                new[] { P("s", s, 0) }, new[] { s }, packageName: "url"));

            // url.PathUnescape(s string) (string, error)
            pkg.AddExport(new FunctionSymbol("PathUnescape",
                new[] { P("s", s, 0) }, new TypeSymbol[] { s, err }, packageName: "url"));

            // url.QueryEscape(s string) string
            pkg.AddExport(new FunctionSymbol("QueryEscape",
                new[] { P("s", s, 0) }, new[] { s }, packageName: "url"));

            // url.QueryUnescape(s string) (string, error)
            pkg.AddExport(new FunctionSymbol("QueryUnescape",
                new[] { P("s", s, 0) }, new TypeSymbol[] { s, err }, packageName: "url"));

            // url.ParseQuery(query string) (Values, error)
            pkg.AddExport(new FunctionSymbol("ParseQuery",
                new[] { P("query", s, 0) },
                new TypeSymbol[] { valuesType, err }, packageName: "url"));

            return pkg;
        }

        private static PackageSymbol CreateHttptestPackage()
        {
            var pkg = new PackageSymbol("httptest", "net/http/httptest");

            var s = BuiltinTypes.String;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // httptest.Server type
            var serverType = new StructTypeSymbol("Server",
                new[]
                {
                    new FieldSymbol("URL", s, 0),
                });
            serverType.AddMethod(new MethodSymbol("Close", serverType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            serverType.AddMethod(new MethodSymbol("CloseClientConnections", serverType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            pkg.AddExport(serverType);

            // httptest.NewServer(handler http.Handler) *Server
            pkg.AddExport(new FunctionSymbol("NewServer",
                new[] { P("handler", emptyIface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(serverType) }, packageName: "httptest"));

            // httptest.ResponseRecorder type
            var recorderType = new StructTypeSymbol("ResponseRecorder", new[]
            {
                new FieldSymbol("Code", BuiltinTypes.Int, 0),
                new FieldSymbol("Body", emptyIface, 1),
                new FieldSymbol("Flushed", BuiltinTypes.Bool, 2),
            });
            recorderType.AddMethod(new MethodSymbol("Code", recorderType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            recorderType.AddMethod(new MethodSymbol("Result", recorderType, false,
                Array.Empty<ParameterSymbol>(), emptyIface));
            recorderType.AddMethod(new MethodSymbol("Flush", recorderType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            pkg.AddExport(recorderType);

            // httptest.NewRecorder() *ResponseRecorder
            pkg.AddExport(new FunctionSymbol("NewRecorder",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new PointerTypeSymbol(recorderType) }, packageName: "httptest"));

            // httptest.DefaultRemoteAddr
            pkg.AddExport(new ConstantSymbol("DefaultRemoteAddr", s, "1.2.3.4"));

            return pkg;
        }

        private static PackageSymbol CreateSyncAtomicPackage()
        {
            var pkg = new PackageSymbol("atomic", "sync/atomic");

            var i32 = BuiltinTypes.Int32;
            var i64 = BuiltinTypes.Int64;
            var u32 = BuiltinTypes.Uint32;
            var u64 = BuiltinTypes.Uint64;
            var b = BuiltinTypes.Bool;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // Load/Store/Add/Swap/CompareAndSwap for Int32
            pkg.AddExport(new FunctionSymbol("LoadInt32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i32), 0) },
                new[] { i32 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("StoreInt32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i32), 0), P("val", i32, 1) },
                Array.Empty<TypeSymbol>(), packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("AddInt32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i32), 0), P("delta", i32, 1) },
                new[] { i32 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("CompareAndSwapInt32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i32), 0), P("old", i32, 1), P("new_", i32, 2) },
                new[] { b }, packageName: "atomic"));

            // Load/Store/Add/Swap/CompareAndSwap for Int64
            pkg.AddExport(new FunctionSymbol("LoadInt64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i64), 0) },
                new[] { i64 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("StoreInt64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i64), 0), P("val", i64, 1) },
                Array.Empty<TypeSymbol>(), packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("AddInt64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i64), 0), P("delta", i64, 1) },
                new[] { i64 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("CompareAndSwapInt64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i64), 0), P("old", i64, 1), P("new_", i64, 2) },
                new[] { b }, packageName: "atomic"));

            // Swap for Int32/Int64
            pkg.AddExport(new FunctionSymbol("SwapInt32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i32), 0), P("new_", i32, 1) },
                new[] { i32 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("SwapInt64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(i64), 0), P("new_", i64, 1) },
                new[] { i64 }, packageName: "atomic"));

            // Uint32
            pkg.AddExport(new FunctionSymbol("LoadUint32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u32), 0) },
                new[] { u32 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("StoreUint32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u32), 0), P("val", u32, 1) },
                Array.Empty<TypeSymbol>(), packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("AddUint32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u32), 0), P("delta", u32, 1) },
                new[] { u32 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("SwapUint32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u32), 0), P("new_", u32, 1) },
                new[] { u32 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("CompareAndSwapUint32",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u32), 0), P("old", u32, 1), P("new_", u32, 2) },
                new[] { b }, packageName: "atomic"));

            // Uint64
            pkg.AddExport(new FunctionSymbol("LoadUint64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u64), 0) },
                new[] { u64 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("StoreUint64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u64), 0), P("val", u64, 1) },
                Array.Empty<TypeSymbol>(), packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("AddUint64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u64), 0), P("delta", u64, 1) },
                new[] { u64 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("SwapUint64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u64), 0), P("new_", u64, 1) },
                new[] { u64 }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("CompareAndSwapUint64",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(u64), 0), P("old", u64, 1), P("new_", u64, 2) },
                new[] { b }, packageName: "atomic"));

            // atomic.Value type
            var valueType = new StructTypeSymbol("Value", Array.Empty<FieldSymbol>());
            valueType.AddMethod(new MethodSymbol("Load", valueType, false,
                Array.Empty<ParameterSymbol>(), emptyIface));
            valueType.AddMethod(new MethodSymbol("Store", valueType, false,
                new[] { new ParameterSymbol("v", emptyIface, 0) },
                BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("Swap", valueType, false,
                new[] { new ParameterSymbol("new_", emptyIface, 0) },
                emptyIface));
            valueType.AddMethod(new MethodSymbol("CompareAndSwap", valueType, false,
                new[] { new ParameterSymbol("old", emptyIface, 0),
                        new ParameterSymbol("new_", emptyIface, 1) },
                b));
            pkg.AddExport(valueType);

            // atomic.Bool type (Go 1.19+)
            var boolType = new StructTypeSymbol("Bool", Array.Empty<FieldSymbol>());
            boolType.AddMethod(new MethodSymbol("Load", boolType, false,
                Array.Empty<ParameterSymbol>(), b));
            boolType.AddMethod(new MethodSymbol("Store", boolType, false,
                new[] { new ParameterSymbol("val", b, 0) }, BuiltinTypes.Void));
            boolType.AddMethod(new MethodSymbol("Swap", boolType, false,
                new[] { new ParameterSymbol("old", b, 0) }, b));
            boolType.AddMethod(new MethodSymbol("CompareAndSwap", boolType, false,
                new[] { new ParameterSymbol("old", b, 0), new ParameterSymbol("new_", b, 1) }, b));
            pkg.AddExport(boolType);

            // atomic.Int32 type (Go 1.19+)
            var int32Type = new StructTypeSymbol("Int32", Array.Empty<FieldSymbol>());
            int32Type.AddMethod(new MethodSymbol("Load", int32Type, false,
                Array.Empty<ParameterSymbol>(), i32));
            int32Type.AddMethod(new MethodSymbol("Store", int32Type, false,
                new[] { new ParameterSymbol("val", i32, 0) }, BuiltinTypes.Void));
            int32Type.AddMethod(new MethodSymbol("Add", int32Type, false,
                new[] { new ParameterSymbol("delta", i32, 0) }, i32));
            int32Type.AddMethod(new MethodSymbol("Swap", int32Type, false,
                new[] { new ParameterSymbol("old", i32, 0) }, i32));
            int32Type.AddMethod(new MethodSymbol("CompareAndSwap", int32Type, false,
                new[] { new ParameterSymbol("old", i32, 0), new ParameterSymbol("new_", i32, 1) }, b));
            pkg.AddExport(int32Type);

            // atomic.Int64 type (Go 1.19+)
            var int64Type = new StructTypeSymbol("Int64", Array.Empty<FieldSymbol>());
            int64Type.AddMethod(new MethodSymbol("Load", int64Type, false,
                Array.Empty<ParameterSymbol>(), i64));
            int64Type.AddMethod(new MethodSymbol("Store", int64Type, false,
                new[] { new ParameterSymbol("val", i64, 0) }, BuiltinTypes.Void));
            int64Type.AddMethod(new MethodSymbol("Add", int64Type, false,
                new[] { new ParameterSymbol("delta", i64, 0) }, i64));
            int64Type.AddMethod(new MethodSymbol("Swap", int64Type, false,
                new[] { new ParameterSymbol("old", i64, 0) }, i64));
            int64Type.AddMethod(new MethodSymbol("CompareAndSwap", int64Type, false,
                new[] { new ParameterSymbol("old", i64, 0), new ParameterSymbol("new_", i64, 1) }, b));
            pkg.AddExport(int64Type);

            // atomic.Uint32 type (Go 1.19+)
            var uint32Type = new StructTypeSymbol("Uint32", Array.Empty<FieldSymbol>());
            uint32Type.AddMethod(new MethodSymbol("Load", uint32Type, false,
                Array.Empty<ParameterSymbol>(), u32));
            uint32Type.AddMethod(new MethodSymbol("Store", uint32Type, false,
                new[] { new ParameterSymbol("val", u32, 0) }, BuiltinTypes.Void));
            uint32Type.AddMethod(new MethodSymbol("Add", uint32Type, false,
                new[] { new ParameterSymbol("delta", u32, 0) }, u32));
            uint32Type.AddMethod(new MethodSymbol("Swap", uint32Type, false,
                new[] { new ParameterSymbol("old", u32, 0) }, u32));
            uint32Type.AddMethod(new MethodSymbol("CompareAndSwap", uint32Type, false,
                new[] { new ParameterSymbol("old", u32, 0), new ParameterSymbol("new_", u32, 1) }, b));
            pkg.AddExport(uint32Type);

            // atomic.Uint64 type (Go 1.19+)
            var uint64Type = new StructTypeSymbol("Uint64", Array.Empty<FieldSymbol>());
            uint64Type.AddMethod(new MethodSymbol("Load", uint64Type, false,
                Array.Empty<ParameterSymbol>(), u64));
            uint64Type.AddMethod(new MethodSymbol("Store", uint64Type, false,
                new[] { new ParameterSymbol("val", u64, 0) }, BuiltinTypes.Void));
            uint64Type.AddMethod(new MethodSymbol("Add", uint64Type, false,
                new[] { new ParameterSymbol("delta", u64, 0) }, u64));
            uint64Type.AddMethod(new MethodSymbol("Swap", uint64Type, false,
                new[] { new ParameterSymbol("old", u64, 0) }, u64));
            uint64Type.AddMethod(new MethodSymbol("CompareAndSwap", uint64Type, false,
                new[] { new ParameterSymbol("old", u64, 0), new ParameterSymbol("new_", u64, 1) }, b));
            pkg.AddExport(uint64Type);

            // atomic.Uintptr type (Go 1.19+)
            var uintptrType = new StructTypeSymbol("Uintptr", Array.Empty<FieldSymbol>());
            uintptrType.AddMethod(new MethodSymbol("Load", uintptrType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uintptr));
            uintptrType.AddMethod(new MethodSymbol("Store", uintptrType, false,
                new[] { new ParameterSymbol("val", BuiltinTypes.Uintptr, 0) }, BuiltinTypes.Void));
            uintptrType.AddMethod(new MethodSymbol("Add", uintptrType, false,
                new[] { new ParameterSymbol("delta", BuiltinTypes.Uintptr, 0) }, BuiltinTypes.Uintptr));
            uintptrType.AddMethod(new MethodSymbol("Swap", uintptrType, false,
                new[] { new ParameterSymbol("old", BuiltinTypes.Uintptr, 0) }, BuiltinTypes.Uintptr));
            uintptrType.AddMethod(new MethodSymbol("CompareAndSwap", uintptrType, false,
                new[] { new ParameterSymbol("old", BuiltinTypes.Uintptr, 0),
                        new ParameterSymbol("new_", BuiltinTypes.Uintptr, 1) }, b));
            pkg.AddExport(uintptrType);

            // Pointer functions (unsafe.Pointer-based, use emptyIface as stand-in)
            var unsafePtr = BuiltinTypes.Uintptr; // simplified stand-in for unsafe.Pointer
            pkg.AddExport(new FunctionSymbol("LoadPointer",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(unsafePtr), 0) },
                new[] { unsafePtr }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("StorePointer",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(unsafePtr), 0),
                        P("val", unsafePtr, 1) },
                Array.Empty<TypeSymbol>(), packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("SwapPointer",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(unsafePtr), 0),
                        P("new_", unsafePtr, 1) },
                new[] { unsafePtr }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("CompareAndSwapPointer",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(unsafePtr), 0),
                        P("old", unsafePtr, 1), P("new_", unsafePtr, 2) },
                new[] { b }, packageName: "atomic"));

            // Uintptr functions
            pkg.AddExport(new FunctionSymbol("LoadUintptr",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(BuiltinTypes.Uintptr), 0) },
                new[] { BuiltinTypes.Uintptr }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("StoreUintptr",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(BuiltinTypes.Uintptr), 0),
                        P("val", BuiltinTypes.Uintptr, 1) },
                Array.Empty<TypeSymbol>(), packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("AddUintptr",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(BuiltinTypes.Uintptr), 0),
                        P("delta", BuiltinTypes.Uintptr, 1) },
                new[] { BuiltinTypes.Uintptr }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("SwapUintptr",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(BuiltinTypes.Uintptr), 0),
                        P("new_", BuiltinTypes.Uintptr, 1) },
                new[] { BuiltinTypes.Uintptr }, packageName: "atomic"));
            pkg.AddExport(new FunctionSymbol("CompareAndSwapUintptr",
                new[] { new ParameterSymbol("addr", new PointerTypeSymbol(BuiltinTypes.Uintptr), 0),
                        P("old", BuiltinTypes.Uintptr, 1), P("new_", BuiltinTypes.Uintptr, 2) },
                new[] { b }, packageName: "atomic"));

            return pkg;
        }

        private static PackageSymbol CreateContainerListPackage()
        {
            var pkg = new PackageSymbol("list", "container/list");
            var iface = BuiltinTypes.EmptyInterface;

            // list.Element type
            var elementType = new StructTypeSymbol("Element", new[]
            {
                new FieldSymbol("Value", iface, 0),
            });
            elementType.AddMethod(new MethodSymbol("Next", elementType, false,
                Array.Empty<ParameterSymbol>(),
                new[] { new PointerTypeSymbol(elementType) }));
            elementType.AddMethod(new MethodSymbol("Prev", elementType, false,
                Array.Empty<ParameterSymbol>(),
                new[] { new PointerTypeSymbol(elementType) }));
            pkg.AddExport(elementType);

            // list.List type
            var elemPtr = new PointerTypeSymbol(elementType);
            var listType = new StructTypeSymbol("List", Array.Empty<FieldSymbol>());
            listType.AddMethod(new MethodSymbol("Back", listType, false,
                Array.Empty<ParameterSymbol>(), new[] { elemPtr }));
            listType.AddMethod(new MethodSymbol("Front", listType, false,
                Array.Empty<ParameterSymbol>(), new[] { elemPtr }));
            listType.AddMethod(new MethodSymbol("Init", listType, false,
                Array.Empty<ParameterSymbol>(), new[] { new PointerTypeSymbol(listType) }));
            listType.AddMethod(new MethodSymbol("Len", listType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int));
            listType.AddMethod(new MethodSymbol("MoveToFront", listType, false,
                new[] { P("e", elemPtr, 0) }, BuiltinTypes.Void));
            listType.AddMethod(new MethodSymbol("MoveToBack", listType, false,
                new[] { P("e", elemPtr, 0) }, BuiltinTypes.Void));
            listType.AddMethod(new MethodSymbol("PushBack", listType, false,
                new[] { P("v", iface, 0) }, new[] { elemPtr }));
            listType.AddMethod(new MethodSymbol("PushFront", listType, false,
                new[] { P("v", iface, 0) }, new[] { elemPtr }));
            listType.AddMethod(new MethodSymbol("Remove", listType, false,
                new[] { P("e", elemPtr, 0) }, new[] { iface }));
            listType.AddMethod(new MethodSymbol("InsertAfter", listType, false,
                new[] { P("v", iface, 0), P("mark", elemPtr, 1) }, new[] { elemPtr }));
            listType.AddMethod(new MethodSymbol("InsertBefore", listType, false,
                new[] { P("v", iface, 0), P("mark", elemPtr, 1) }, new[] { elemPtr }));
            listType.AddMethod(new MethodSymbol("MoveBefore", listType, false,
                new[] { P("e", elemPtr, 0), P("mark", elemPtr, 1) }, BuiltinTypes.Void));
            listType.AddMethod(new MethodSymbol("MoveAfter", listType, false,
                new[] { P("e", elemPtr, 0), P("mark", elemPtr, 1) }, BuiltinTypes.Void));
            listType.AddMethod(new MethodSymbol("PushBackList", listType, false,
                new[] { P("other", new PointerTypeSymbol(listType), 0) }, BuiltinTypes.Void));
            listType.AddMethod(new MethodSymbol("PushFrontList", listType, false,
                new[] { P("other", new PointerTypeSymbol(listType), 0) }, BuiltinTypes.Void));
            pkg.AddExport(listType);

            // list.New() *List
            pkg.AddExport(new FunctionSymbol("New",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new PointerTypeSymbol(listType) }, packageName: "list"));

            return pkg;
        }

        private static PackageSymbol CreateDatabaseSqlDriverPackage()
        {
            var pkg = new PackageSymbol("driver", "database/sql/driver");

            // driver.Value is type Value interface{}
            var valueType = new InterfaceTypeSymbol("Value", Array.Empty<MethodSymbol>());
            pkg.AddExport(valueType);

            // driver.Valuer interface
            var valuerType = new InterfaceTypeSymbol("Valuer", new[]
            {
                new MethodSymbol("Value", null!, false,
                    Array.Empty<ParameterSymbol>(),
                    new TypeSymbol[] { valueType, BuiltinTypes.Error }),
            });
            pkg.AddExport(valuerType);

            return pkg;
        }

        private static PackageSymbol CreateDatabaseSqlPackage()
        {
            var pkg = new PackageSymbol("sql", "database/sql");

            var s = BuiltinTypes.String;
            var b = BuiltinTypes.Bool;
            var i64 = BuiltinTypes.Int64;
            var err = BuiltinTypes.Error;
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // sql.NullString
            var nullString = new StructTypeSymbol("NullString", new List<FieldSymbol>
            {
                new FieldSymbol("String", s, 0),
                new FieldSymbol("Valid", b, 1),
            });
            pkg.AddExport(nullString);

            // sql.NullInt64
            var nullInt64 = new StructTypeSymbol("NullInt64", new List<FieldSymbol>
            {
                new FieldSymbol("Int64", i64, 0),
                new FieldSymbol("Valid", b, 1),
            });
            pkg.AddExport(nullInt64);

            // sql.NullBool
            var nullBool = new StructTypeSymbol("NullBool", new List<FieldSymbol>
            {
                new FieldSymbol("Bool", b, 0),
                new FieldSymbol("Valid", b, 1),
            });
            pkg.AddExport(nullBool);

            // sql.NullFloat64
            var nullFloat64 = new StructTypeSymbol("NullFloat64", new List<FieldSymbol>
            {
                new FieldSymbol("Float64", BuiltinTypes.Float64, 0),
                new FieldSymbol("Valid", b, 1),
            });
            pkg.AddExport(nullFloat64);

            // sql.Scanner interface
            var scannerIface = new InterfaceTypeSymbol("Scanner", new[]
            {
                new MethodSymbol("Scan", null!, false,
                    new[] { P("src", iface, 0) },
                    new TypeSymbol[] { err }),
            });
            pkg.AddExport(scannerIface);

            return pkg;
        }

        private static PackageSymbol CreateEncodingPackage()
        {
            var pkg = new PackageSymbol("encoding", "encoding");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            // encoding.TextMarshaler interface
            var textMarshalerIface = new InterfaceTypeSymbol("TextMarshaler", new[]
            {
                new MethodSymbol("MarshalText", null!, false,
                    Array.Empty<ParameterSymbol>(),
                    new TypeSymbol[] { byteSlice, BuiltinTypes.Error }),
            });
            pkg.AddExport(textMarshalerIface);

            // encoding.TextUnmarshaler interface
            var textUnmarshalerIface = new InterfaceTypeSymbol("TextUnmarshaler", new[]
            {
                new MethodSymbol("UnmarshalText", null!, false,
                    new[] { P("text", byteSlice, 0) },
                    BuiltinTypes.Error),
            });
            pkg.AddExport(textUnmarshalerIface);

            // encoding.BinaryMarshaler interface
            var binaryMarshalerIface = new InterfaceTypeSymbol("BinaryMarshaler", new[]
            {
                new MethodSymbol("MarshalBinary", null!, false,
                    Array.Empty<ParameterSymbol>(),
                    new TypeSymbol[] { byteSlice, BuiltinTypes.Error }),
            });
            pkg.AddExport(binaryMarshalerIface);

            // encoding.BinaryUnmarshaler interface
            var binaryUnmarshalerIface = new InterfaceTypeSymbol("BinaryUnmarshaler", new[]
            {
                new MethodSymbol("UnmarshalBinary", null!, false,
                    new[] { P("data", byteSlice, 0) },
                    BuiltinTypes.Error),
            });
            pkg.AddExport(binaryUnmarshalerIface);

            return pkg;
        }

        private static PackageSymbol CreateTabwriterPackage()
        {
            var pkg = new PackageSymbol("tabwriter", "text/tabwriter");

            var iface = BuiltinTypes.EmptyInterface;
            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var writerIface = new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>());

            // tabwriter.Writer struct
            var writerType = new StructTypeSymbol("Writer", Array.Empty<FieldSymbol>());
            writerType.AddMethod(new MethodSymbol("Write", writerType, false,
                new[] { P("buf", byteSlice, 0) },
                new TypeSymbol[] { i, BuiltinTypes.Error }));
            writerType.AddMethod(new MethodSymbol("Flush", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            writerType.AddMethod(new MethodSymbol("Init", writerType, false,
                new[] { P("output", writerIface, 0), P("minwidth", i, 1), P("tabwidth", i, 2),
                        P("padding", i, 3), P("padchar", BuiltinTypes.Byte, 4), P("flags", BuiltinTypes.Uint, 5) },
                new PointerTypeSymbol(writerType)));
            pkg.AddExport(writerType);

            // tabwriter.NewWriter(output io.Writer, minwidth, tabwidth, padding int, padchar byte, flags uint) *Writer
            pkg.AddExport(new FunctionSymbol("NewWriter",
                new[] { P("output", writerIface, 0), P("minwidth", i, 1), P("tabwidth", i, 2),
                        P("padding", i, 3), P("padchar", BuiltinTypes.Byte, 4), P("flags", BuiltinTypes.Uint, 5) },
                new TypeSymbol[] { new PointerTypeSymbol(writerType) }, packageName: "tabwriter"));

            // Constants
            pkg.AddExport(new ConstantSymbol("FilterHTML", BuiltinTypes.Uint, (long)1));
            pkg.AddExport(new ConstantSymbol("StripEscape", BuiltinTypes.Uint, (long)2));
            pkg.AddExport(new ConstantSymbol("AlignRight", BuiltinTypes.Uint, (long)4));
            pkg.AddExport(new ConstantSymbol("DiscardEmptyColumns", BuiltinTypes.Uint, (long)8));
            pkg.AddExport(new ConstantSymbol("TabIndent", BuiltinTypes.Uint, (long)16));
            pkg.AddExport(new ConstantSymbol("Debug", BuiltinTypes.Uint, (long)32));

            // Escape byte
            pkg.AddExport(new ConstantSymbol("Escape", BuiltinTypes.Byte, (long)0xFF));

            return pkg;
        }

        private static PackageSymbol CreateTextTemplatePackage()
        {
            var pkg = new PackageSymbol("template", "text/template");

            var s = BuiltinTypes.String;
            var iface = BuiltinTypes.EmptyInterface;
            var writerIface = new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>());

            // template.Template type
            var tmplType = new StructTypeSymbol("Template", Array.Empty<FieldSymbol>());
            tmplType.AddMethod(new MethodSymbol("Execute", tmplType, false,
                new[] { P("wr", writerIface, 0), P("data", iface, 1) }, BuiltinTypes.Error));
            tmplType.AddMethod(new MethodSymbol("ExecuteTemplate", tmplType, false,
                new[] { P("wr", writerIface, 0), P("name", s, 1), P("data", iface, 2) }, BuiltinTypes.Error));
            tmplType.AddMethod(new MethodSymbol("Parse", tmplType, false,
                new[] { P("text", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType), BuiltinTypes.Error }));
            tmplType.AddMethod(new MethodSymbol("Name", tmplType, false,
                Array.Empty<ParameterSymbol>(), s));

            // template.FuncMap type (map[string]interface{})
            var funcMapType = new MapTypeSymbol(s, iface);
            var funcMapNamedType = new TypeSymbol("FuncMap", TypeKind.Map, funcMapType);
            pkg.AddExport(funcMapNamedType);

            tmplType.AddMethod(new MethodSymbol("Funcs", tmplType, false,
                new[] { P("funcMap", funcMapNamedType, 0) },
                new PointerTypeSymbol(tmplType)));
            tmplType.AddMethod(new MethodSymbol("Option", tmplType, false,
                new[] { P("opt", new SliceTypeSymbol(s), 0) },
                new PointerTypeSymbol(tmplType)));
            tmplType.AddMethod(new MethodSymbol("ParseFiles", tmplType, false,
                new[] { P("filenames", new SliceTypeSymbol(s), 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType), BuiltinTypes.Error }));
            tmplType.AddMethod(new MethodSymbol("ParseGlob", tmplType, false,
                new[] { P("pattern", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType), BuiltinTypes.Error }));
            tmplType.AddMethod(new MethodSymbol("Lookup", tmplType, false,
                new[] { P("name", s, 0) },
                new PointerTypeSymbol(tmplType)));
            tmplType.AddMethod(new MethodSymbol("Templates", tmplType, false,
                Array.Empty<ParameterSymbol>(),
                new SliceTypeSymbol(new PointerTypeSymbol(tmplType))));
            pkg.AddExport(tmplType);

            // template.New(name string) *Template
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType) }, packageName: "template"));

            // template.Must(t *Template, err error) *Template
            pkg.AddExport(new FunctionSymbol("Must",
                new[] { P("t", new PointerTypeSymbol(tmplType), 0), P("err", BuiltinTypes.Error, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType) }, packageName: "template"));

            // template.ParseFiles(filenames ...string) (*Template, error)
            pkg.AddExport(new FunctionSymbol("ParseFiles",
                new[] { P("filenames", new SliceTypeSymbol(s), 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType), BuiltinTypes.Error },
                isVariadic: true, packageName: "template"));

            // template.ParseGlob(pattern string) (*Template, error)
            pkg.AddExport(new FunctionSymbol("ParseGlob",
                new[] { P("pattern", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType), BuiltinTypes.Error },
                packageName: "template"));

            return pkg;
        }

        private static PackageSymbol CreateHtmlPackage()
        {
            var pkg = new PackageSymbol("html", "html");
            var s = BuiltinTypes.String;
            pkg.AddExport(new FunctionSymbol("EscapeString",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { s }, packageName: "html"));
            pkg.AddExport(new FunctionSymbol("UnescapeString",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { s }, packageName: "html"));
            return pkg;
        }

        private static PackageSymbol CreateHtmlTemplatePackage()
        {
            var pkg = new PackageSymbol("template", "html/template");

            var s = BuiltinTypes.String;
            var iface = BuiltinTypes.EmptyInterface;
            var writerIface = new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>());

            var tmplType = new StructTypeSymbol("Template", Array.Empty<FieldSymbol>());
            tmplType.AddMethod(new MethodSymbol("Execute", tmplType, false,
                new[] { P("wr", writerIface, 0), P("data", iface, 1) }, BuiltinTypes.Error));
            tmplType.AddMethod(new MethodSymbol("ExecuteTemplate", tmplType, false,
                new[] { P("wr", writerIface, 0), P("name", s, 1), P("data", iface, 2) }, BuiltinTypes.Error));
            tmplType.AddMethod(new MethodSymbol("Parse", tmplType, false,
                new[] { P("text", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType), BuiltinTypes.Error }));
            tmplType.AddMethod(new MethodSymbol("Name", tmplType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(tmplType);

            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType) }, packageName: "template"));
            pkg.AddExport(new FunctionSymbol("Must",
                new[] { P("t", new PointerTypeSymbol(tmplType), 0), P("err", BuiltinTypes.Error, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(tmplType) }, packageName: "template"));

            // Escape functions
            pkg.AddExport(new FunctionSymbol("HTMLEscapeString",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { s }, packageName: "template"));
            pkg.AddExport(new FunctionSymbol("HTMLEscaper",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s }, isVariadic: true, packageName: "template"));
            pkg.AddExport(new FunctionSymbol("JSEscapeString",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { s }, packageName: "template"));
            pkg.AddExport(new FunctionSymbol("URLQueryEscaper",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s }, isVariadic: true, packageName: "template"));

            // String alias types from html/template
            pkg.AddExport(new TypeSymbol("HTML", TypeKind.String, BuiltinTypes.String));
            pkg.AddExport(new TypeSymbol("URL", TypeKind.String, BuiltinTypes.String));
            pkg.AddExport(new TypeSymbol("JS", TypeKind.String, BuiltinTypes.String));
            pkg.AddExport(new TypeSymbol("CSS", TypeKind.String, BuiltinTypes.String));
            pkg.AddExport(new TypeSymbol("HTMLAttr", TypeKind.String, BuiltinTypes.String));
            pkg.AddExport(new TypeSymbol("JSStr", TypeKind.String, BuiltinTypes.String));
            pkg.AddExport(new TypeSymbol("Srcset", TypeKind.String, BuiltinTypes.String));

            return pkg;
        }

        private static PackageSymbol CreateEncodingBinaryPackage()
        {
            var pkg = new PackageSymbol("binary", "encoding/binary");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var iface = BuiltinTypes.EmptyInterface;
            var writerIface = new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>());
            var readerIface = new InterfaceTypeSymbol("Reader", Array.Empty<MethodSymbol>());

            // ByteOrder interface
            var byteOrderIface = new InterfaceTypeSymbol("ByteOrder", new[]
            {
                new MethodSymbol("Uint16", null!, false,
                    new[] { P("b", byteSlice, 0) }, BuiltinTypes.Uint16),
                new MethodSymbol("Uint32", null!, false,
                    new[] { P("b", byteSlice, 0) }, BuiltinTypes.Uint32),
                new MethodSymbol("Uint64", null!, false,
                    new[] { P("b", byteSlice, 0) }, BuiltinTypes.Uint64),
                new MethodSymbol("PutUint16", null!, false,
                    new[] { P("b", byteSlice, 0), P("v", BuiltinTypes.Uint16, 1) }, BuiltinTypes.Void),
                new MethodSymbol("PutUint32", null!, false,
                    new[] { P("b", byteSlice, 0), P("v", BuiltinTypes.Uint32, 1) }, BuiltinTypes.Void),
                new MethodSymbol("PutUint64", null!, false,
                    new[] { P("b", byteSlice, 0), P("v", BuiltinTypes.Uint64, 1) }, BuiltinTypes.Void),
                new MethodSymbol("String", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.String),
            });
            pkg.AddExport(byteOrderIface);

            // BigEndian and LittleEndian — exposed as constants for semantic analysis
            pkg.AddExport(new ConstantSymbol("BigEndian", byteOrderIface, (long)0));
            pkg.AddExport(new ConstantSymbol("LittleEndian", byteOrderIface, (long)1));

            // binary.Read, binary.Write
            pkg.AddExport(new FunctionSymbol("Read",
                new[] { P("r", readerIface, 0), P("order", byteOrderIface, 1), P("data", iface, 2) },
                new TypeSymbol[] { BuiltinTypes.Error }, packageName: "binary"));
            pkg.AddExport(new FunctionSymbol("Write",
                new[] { P("w", writerIface, 0), P("order", byteOrderIface, 1), P("data", iface, 2) },
                new TypeSymbol[] { BuiltinTypes.Error }, packageName: "binary"));

            // binary.PutVarint, PutUvarint, Varint, Uvarint
            pkg.AddExport(new FunctionSymbol("PutVarint",
                new[] { P("buf", byteSlice, 0), P("x", BuiltinTypes.Int64, 1) },
                new TypeSymbol[] { BuiltinTypes.Int }, packageName: "binary"));
            pkg.AddExport(new FunctionSymbol("PutUvarint",
                new[] { P("buf", byteSlice, 0), P("x", BuiltinTypes.Uint64, 1) },
                new TypeSymbol[] { BuiltinTypes.Int }, packageName: "binary"));
            pkg.AddExport(new FunctionSymbol("Varint",
                new[] { P("buf", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int64, BuiltinTypes.Int }, packageName: "binary"));
            pkg.AddExport(new FunctionSymbol("Uvarint",
                new[] { P("buf", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Uint64, BuiltinTypes.Int }, packageName: "binary"));

            // Constants
            pkg.AddExport(new ConstantSymbol("MaxVarintLen16", BuiltinTypes.Int, (long)3));
            pkg.AddExport(new ConstantSymbol("MaxVarintLen32", BuiltinTypes.Int, (long)5));
            pkg.AddExport(new ConstantSymbol("MaxVarintLen64", BuiltinTypes.Int, (long)10));

            return pkg;
        }

        private static PackageSymbol CreateEncodingGobPackage()
        {
            var pkg = new PackageSymbol("gob", "encoding/gob");

            var iface = BuiltinTypes.EmptyInterface;
            var readerIface = new InterfaceTypeSymbol("Reader", Array.Empty<MethodSymbol>());
            var writerIface = new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>());

            // gob.Encoder type
            var encoderType = new StructTypeSymbol("Encoder", Array.Empty<FieldSymbol>());
            encoderType.AddMethod(new MethodSymbol("Encode", encoderType, false,
                new[] { P("e", iface, 0) }, BuiltinTypes.Error));
            pkg.AddExport(encoderType);

            // gob.Decoder type
            var decoderType = new StructTypeSymbol("Decoder", Array.Empty<FieldSymbol>());
            decoderType.AddMethod(new MethodSymbol("Decode", decoderType, false,
                new[] { P("e", iface, 0) }, BuiltinTypes.Error));
            pkg.AddExport(decoderType);

            // gob.NewEncoder(w io.Writer) *Encoder
            pkg.AddExport(new FunctionSymbol("NewEncoder",
                new[] { P("w", writerIface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(encoderType) }, packageName: "gob"));

            // gob.NewDecoder(r io.Reader) *Decoder
            pkg.AddExport(new FunctionSymbol("NewDecoder",
                new[] { P("r", readerIface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(decoderType) }, packageName: "gob"));

            // gob.Register(value interface{})
            pkg.AddExport(new FunctionSymbol("Register",
                new[] { P("value", iface, 0) },
                Array.Empty<TypeSymbol>(), packageName: "gob"));

            // gob.RegisterName(name string, value interface{})
            pkg.AddExport(new FunctionSymbol("RegisterName",
                new[] { P("name", BuiltinTypes.String, 0), P("value", iface, 1) },
                Array.Empty<TypeSymbol>(), packageName: "gob"));

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            // gob.GobEncoder interface { GobEncode() ([]byte, error) }
            var gobEncoderIface = new InterfaceTypeSymbol("GobEncoder", Array.Empty<MethodSymbol>());
            gobEncoderIface.SetMethods(new[]
            {
                new MethodSymbol("GobEncode", gobEncoderIface, false,
                    Array.Empty<ParameterSymbol>(), new TypeSymbol[] { byteSlice, BuiltinTypes.Error }),
            });
            pkg.AddExport(gobEncoderIface);

            // gob.GobDecoder interface { GobDecode([]byte) error }
            var gobDecoderIface = new InterfaceTypeSymbol("GobDecoder", Array.Empty<MethodSymbol>());
            gobDecoderIface.SetMethods(new[]
            {
                new MethodSymbol("GobDecode", gobDecoderIface, false,
                    new[] { P("data", byteSlice, 0) }, BuiltinTypes.Error),
            });
            pkg.AddExport(gobDecoderIface);

            return pkg;
        }

        private static PackageSymbol CreateHashPackage()
        {
            var pkg = new PackageSymbol("hash", "hash");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            // hash.Hash interface
            var hashIface = new InterfaceTypeSymbol("Hash", new[]
            {
                new MethodSymbol("Write", null!, false,
                    new[] { P("p", byteSlice, 0) },
                    new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }),
                new MethodSymbol("Sum", null!, false,
                    new[] { P("b", byteSlice, 0) }, byteSlice),
                new MethodSymbol("Reset", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void),
                new MethodSymbol("Size", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
                new MethodSymbol("BlockSize", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
            });
            pkg.AddExport(hashIface);

            // hash.Hash32 interface (extends hash.Hash)
            var hash32Methods = new List<MethodSymbol>
            {
                new MethodSymbol("Write", null!, false,
                    new[] { P("p", byteSlice, 0) },
                    new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }),
                new MethodSymbol("Sum", null!, false,
                    new[] { P("b", byteSlice, 0) }, byteSlice),
                new MethodSymbol("Reset", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void),
                new MethodSymbol("Size", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
                new MethodSymbol("BlockSize", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
                new MethodSymbol("Sum32", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint32),
            };
            var hash32Iface = new InterfaceTypeSymbol("Hash32", hash32Methods);
            pkg.AddExport(hash32Iface);

            // hash.Hash64 interface (extends hash.Hash)
            var hash64Methods = new List<MethodSymbol>
            {
                new MethodSymbol("Write", null!, false,
                    new[] { P("p", byteSlice, 0) },
                    new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }),
                new MethodSymbol("Sum", null!, false,
                    new[] { P("b", byteSlice, 0) }, byteSlice),
                new MethodSymbol("Reset", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void),
                new MethodSymbol("Size", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
                new MethodSymbol("BlockSize", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int),
                new MethodSymbol("Sum64", null!, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint64),
            };
            var hash64Iface = new InterfaceTypeSymbol("Hash64", hash64Methods);
            pkg.AddExport(hash64Iface);

            return pkg;
        }

        private static PackageSymbol CreateHashFnvPackage()
        {
            var pkg = new PackageSymbol("fnv", "hash/fnv");

            var hashIface = CreateHashType();

            // Hash32 = Hash + Sum32() uint32
            var hash32Methods = new List<MethodSymbol>(CreateHashMethods());
            hash32Methods.Add(new MethodSymbol("Sum32", null!, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint32));
            var hash32Iface = new InterfaceTypeSymbol("Hash32", hash32Methods);

            // Hash64 = Hash + Sum64() uint64
            var hash64Methods = new List<MethodSymbol>(CreateHashMethods());
            hash64Methods.Add(new MethodSymbol("Sum64", null!, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint64));
            var hash64Iface = new InterfaceTypeSymbol("Hash64", hash64Methods);

            pkg.AddExport(new FunctionSymbol("New32",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hash32Iface }, packageName: "fnv"));
            pkg.AddExport(new FunctionSymbol("New32a",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hash32Iface }, packageName: "fnv"));
            pkg.AddExport(new FunctionSymbol("New64",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hash64Iface }, packageName: "fnv"));
            pkg.AddExport(new FunctionSymbol("New64a",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hash64Iface }, packageName: "fnv"));
            pkg.AddExport(new FunctionSymbol("New128",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "fnv"));
            pkg.AddExport(new FunctionSymbol("New128a",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "fnv"));

            return pkg;
        }

        private static PackageSymbol CreateCryptoSha1Package()
        {
            var pkg = new PackageSymbol("sha1", "crypto/sha1");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            // sha1.New() hash.Hash
            var hashIface = CreateHashType();
            pkg.AddExport(new FunctionSymbol("New",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "sha1"));

            // sha1.Sum(data []byte) [20]byte
            pkg.AddExport(new FunctionSymbol("Sum",
                new[] { P("data", byteSlice, 0) },
                new TypeSymbol[] { new ArrayTypeSymbol(BuiltinTypes.Byte, 20) }, packageName: "sha1"));

            // sha1.Size = 20, sha1.BlockSize = 64
            pkg.AddExport(new ConstantSymbol("Size", BuiltinTypes.Int, (long)20));
            pkg.AddExport(new ConstantSymbol("BlockSize", BuiltinTypes.Int, (long)64));

            return pkg;
        }

        private static PackageSymbol CreateCryptoMd5Package()
        {
            var pkg = new PackageSymbol("md5", "crypto/md5");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var hashIface = CreateHashType();
            pkg.AddExport(new FunctionSymbol("New",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "md5"));

            pkg.AddExport(new FunctionSymbol("Sum",
                new[] { P("data", byteSlice, 0) },
                new TypeSymbol[] { new ArrayTypeSymbol(BuiltinTypes.Byte, 16) }, packageName: "md5"));

            pkg.AddExport(new ConstantSymbol("Size", BuiltinTypes.Int, (long)16));
            pkg.AddExport(new ConstantSymbol("BlockSize", BuiltinTypes.Int, (long)64));

            return pkg;
        }

        private static PackageSymbol CreateHashCrc32Package()
        {
            var pkg = new PackageSymbol("crc32", "hash/crc32");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            // crc32.Table type
            var tableType = new StructTypeSymbol("Table", Array.Empty<FieldSymbol>());
            pkg.AddExport(tableType);

            // crc32.New(tab *Table) hash.Hash32
            var hashIface = CreateHashType();
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("tab", new PointerTypeSymbol(tableType), 0) },
                new TypeSymbol[] { hashIface }, packageName: "crc32"));

            // crc32.NewIEEE() hash.Hash32
            pkg.AddExport(new FunctionSymbol("NewIEEE",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "crc32"));

            // crc32.ChecksumIEEE(data []byte) uint32
            pkg.AddExport(new FunctionSymbol("ChecksumIEEE",
                new[] { P("data", byteSlice, 0) },
                new[] { BuiltinTypes.Uint32 }, packageName: "crc32"));

            // crc32.Checksum(data []byte, tab *Table) uint32
            pkg.AddExport(new FunctionSymbol("Checksum",
                new[] { P("data", byteSlice, 0), P("tab", new PointerTypeSymbol(tableType), 1) },
                new[] { BuiltinTypes.Uint32 }, packageName: "crc32"));

            // crc32.Update(crc uint32, tab *Table, p []byte) uint32
            pkg.AddExport(new FunctionSymbol("Update",
                new[] { P("crc", BuiltinTypes.Uint32, 0),
                        P("tab", new PointerTypeSymbol(tableType), 1),
                        P("p", byteSlice, 2) },
                new[] { BuiltinTypes.Uint32 }, packageName: "crc32"));

            // crc32.MakeTable(poly uint32) *Table
            pkg.AddExport(new FunctionSymbol("MakeTable",
                new[] { P("poly", BuiltinTypes.Uint32, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tableType) }, packageName: "crc32"));

            // Constants
            pkg.AddExport(new ConstantSymbol("Size", BuiltinTypes.Int, (long)4));
            pkg.AddExport(new ConstantSymbol("IEEE", BuiltinTypes.Uint32, (long)0xedb88320));
            pkg.AddExport(new ConstantSymbol("Castagnoli", BuiltinTypes.Uint32, (long)0x82f63b78));
            pkg.AddExport(new ConstantSymbol("Koopman", BuiltinTypes.Uint32, (long)0xeb31d82e));

            // Package var: IEEETable *Table
            pkg.AddExport(new PackageVarSymbol("IEEETable", new PointerTypeSymbol(tableType)));

            return pkg;
        }

        private static PackageSymbol CreateHashCrc64Package()
        {
            var pkg = new PackageSymbol("crc64", "hash/crc64");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            var tableType = new StructTypeSymbol("Table", Array.Empty<FieldSymbol>());
            pkg.AddExport(tableType);

            var hashIface = new InterfaceTypeSymbol("Hash64", Array.Empty<MethodSymbol>());
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("tab", new PointerTypeSymbol(tableType), 0) },
                new TypeSymbol[] { hashIface }, packageName: "crc64"));

            pkg.AddExport(new FunctionSymbol("Checksum",
                new[] { P("data", byteSlice, 0), P("tab", new PointerTypeSymbol(tableType), 1) },
                new[] { BuiltinTypes.Uint64 }, packageName: "crc64"));

            pkg.AddExport(new FunctionSymbol("MakeTable",
                new[] { P("poly", BuiltinTypes.Uint64, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(tableType) }, packageName: "crc64"));

            pkg.AddExport(new ConstantSymbol("Size", BuiltinTypes.Int, (long)8));
            pkg.AddExport(new ConstantSymbol("ISO", BuiltinTypes.Uint64, unchecked((long)0xD800000000000000)));
            pkg.AddExport(new ConstantSymbol("ECMA", BuiltinTypes.Uint64, unchecked((long)0xC96C5795D7870F42)));

            return pkg;
        }

        private static PackageSymbol CreateCompressZlibPackage()
        {
            var pkg = new PackageSymbol("zlib", "compress/zlib");
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var ioReaderIface = new InterfaceTypeSymbol("Reader", Array.Empty<MethodSymbol>());
            ioReaderIface.AddMethod(new MethodSymbol("Read", ioReaderIface, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            var ioWriterIface = new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>());
            ioWriterIface.AddMethod(new MethodSymbol("Write", ioWriterIface, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            var ioCloserIface = new InterfaceTypeSymbol("Closer", Array.Empty<MethodSymbol>());
            ioCloserIface.AddMethod(new MethodSymbol("Close", ioCloserIface, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));

            // zlib.NewReader(r io.Reader) (io.ReadCloser, error)
            var readCloserIface = new InterfaceTypeSymbol("ReadCloser", Array.Empty<MethodSymbol>());
            readCloserIface.AddMethod(new MethodSymbol("Read", readCloserIface, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            readCloserIface.AddMethod(new MethodSymbol("Close", readCloserIface, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { P("r", ioReaderIface, 0) },
                new TypeSymbol[] { readCloserIface, BuiltinTypes.Error }, packageName: "zlib"));

            // zlib.NewWriter(w io.Writer) *Writer
            var writerType = new StructTypeSymbol("Writer", Array.Empty<FieldSymbol>());
            writerType.AddMethod(new MethodSymbol("Write", writerType, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            writerType.AddMethod(new MethodSymbol("Close", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            writerType.AddMethod(new MethodSymbol("Flush", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            writerType.AddMethod(new MethodSymbol("Reset", writerType, false,
                new[] { P("w", ioWriterIface, 0) }, BuiltinTypes.Void));
            pkg.AddExport(writerType);
            pkg.AddExport(new FunctionSymbol("NewWriter",
                new[] { P("w", ioWriterIface, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(writerType) }, packageName: "zlib"));

            // zlib.NewWriterLevel(w io.Writer, level int) (*Writer, error)
            pkg.AddExport(new FunctionSymbol("NewWriterLevel",
                new[] { P("w", ioWriterIface, 0), P("level", BuiltinTypes.Int, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(writerType), BuiltinTypes.Error }, packageName: "zlib"));

            // Constants
            pkg.AddExport(new ConstantSymbol("NoCompression", BuiltinTypes.Int, (long)0));
            pkg.AddExport(new ConstantSymbol("BestSpeed", BuiltinTypes.Int, (long)1));
            pkg.AddExport(new ConstantSymbol("BestCompression", BuiltinTypes.Int, (long)9));
            pkg.AddExport(new ConstantSymbol("DefaultCompression", BuiltinTypes.Int, (long)-1));
            pkg.AddExport(new ConstantSymbol("HuffmanOnly", BuiltinTypes.Int, (long)-2));

            return pkg;
        }

        private static PackageSymbol CreateCompressFlatePackage()
        {
            var pkg = new PackageSymbol("flate", "compress/flate");
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var ioReaderIface = new InterfaceTypeSymbol("Reader", Array.Empty<MethodSymbol>());
            ioReaderIface.AddMethod(new MethodSymbol("Read", ioReaderIface, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            var ioWriterIface = new InterfaceTypeSymbol("Writer", Array.Empty<MethodSymbol>());
            ioWriterIface.AddMethod(new MethodSymbol("Write", ioWriterIface, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));

            // flate.NewReader(r io.Reader) io.ReadCloser
            var readCloserIface = new InterfaceTypeSymbol("ReadCloser", Array.Empty<MethodSymbol>());
            readCloserIface.AddMethod(new MethodSymbol("Read", readCloserIface, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            readCloserIface.AddMethod(new MethodSymbol("Close", readCloserIface, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { P("r", ioReaderIface, 0) },
                new TypeSymbol[] { readCloserIface }, packageName: "flate"));

            // flate.NewWriter(w io.Writer, level int) (*Writer, error)
            var writerType = new StructTypeSymbol("Writer", Array.Empty<FieldSymbol>());
            writerType.AddMethod(new MethodSymbol("Write", writerType, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            writerType.AddMethod(new MethodSymbol("Close", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            writerType.AddMethod(new MethodSymbol("Flush", writerType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Error));
            writerType.AddMethod(new MethodSymbol("Reset", writerType, false,
                new[] { P("w", ioWriterIface, 0) }, BuiltinTypes.Void));
            pkg.AddExport(writerType);
            pkg.AddExport(new FunctionSymbol("NewWriter",
                new[] { P("w", ioWriterIface, 0), P("level", BuiltinTypes.Int, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(writerType), BuiltinTypes.Error }, packageName: "flate"));

            // Constants
            pkg.AddExport(new ConstantSymbol("NoCompression", BuiltinTypes.Int, (long)0));
            pkg.AddExport(new ConstantSymbol("BestSpeed", BuiltinTypes.Int, (long)1));
            pkg.AddExport(new ConstantSymbol("BestCompression", BuiltinTypes.Int, (long)9));
            pkg.AddExport(new ConstantSymbol("DefaultCompression", BuiltinTypes.Int, (long)-1));
            pkg.AddExport(new ConstantSymbol("HuffmanOnly", BuiltinTypes.Int, (long)-2));

            return pkg;
        }

        private static PackageSymbol CreateCryptoSubtlePackage()
        {
            var pkg = new PackageSymbol("subtle", "crypto/subtle");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var i = BuiltinTypes.Int;

            // subtle.ConstantTimeCompare(x, y []byte) int
            pkg.AddExport(new FunctionSymbol("ConstantTimeCompare",
                new[] { P("x", byteSlice, 0), P("y", byteSlice, 1) },
                new TypeSymbol[] { i }, packageName: "subtle"));

            // subtle.ConstantTimeSelect(v, x, y int) int
            pkg.AddExport(new FunctionSymbol("ConstantTimeSelect",
                new[] { P("v", i, 0), P("x", i, 1), P("y", i, 2) },
                new TypeSymbol[] { i }, packageName: "subtle"));

            // subtle.ConstantTimeByteEq(x, y uint8) int
            pkg.AddExport(new FunctionSymbol("ConstantTimeByteEq",
                new[] { P("x", BuiltinTypes.Uint8, 0), P("y", BuiltinTypes.Uint8, 1) },
                new TypeSymbol[] { i }, packageName: "subtle"));

            // subtle.ConstantTimeEq(x, y int32) int
            pkg.AddExport(new FunctionSymbol("ConstantTimeEq",
                new[] { P("x", BuiltinTypes.Int32, 0), P("y", BuiltinTypes.Int32, 1) },
                new TypeSymbol[] { i }, packageName: "subtle"));

            // subtle.ConstantTimeCopy(v int, x, y []byte)
            pkg.AddExport(new FunctionSymbol("ConstantTimeCopy",
                new[] { P("v", i, 0), P("x", byteSlice, 1), P("y", byteSlice, 2) },
                Array.Empty<TypeSymbol>(), packageName: "subtle"));

            // subtle.ConstantTimeLessOrEq(x, y int) int
            pkg.AddExport(new FunctionSymbol("ConstantTimeLessOrEq",
                new[] { P("x", i, 0), P("y", i, 1) },
                new TypeSymbol[] { i }, packageName: "subtle"));

            // subtle.XORBytes(dst, x, y []byte) int
            pkg.AddExport(new FunctionSymbol("XORBytes",
                new[] { P("dst", byteSlice, 0), P("x", byteSlice, 1), P("y", byteSlice, 2) },
                new TypeSymbol[] { i }, packageName: "subtle"));

            return pkg;
        }

        private static PackageSymbol CreateCryptoHmacPackage()
        {
            var pkg = new PackageSymbol("hmac", "crypto/hmac");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var hashIface = CreateHashType();

            // hmac.New(h func() hash.Hash, key []byte) hash.Hash
            var hashFactory = new FunctionTypeSymbol(
                Array.Empty<TypeSymbol>(), new TypeSymbol[] { hashIface });
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("h", hashFactory, 0), P("key", byteSlice, 1) },
                new TypeSymbol[] { hashIface }, packageName: "hmac"));

            // hmac.Equal(mac1, mac2 []byte) bool
            pkg.AddExport(new FunctionSymbol("Equal",
                new[] { P("mac1", byteSlice, 0), P("mac2", byteSlice, 1) },
                new[] { BuiltinTypes.Bool }, packageName: "hmac"));

            return pkg;
        }

        private static PackageSymbol CreateCryptoSha256Package()
        {
            var pkg = new PackageSymbol("sha256", "crypto/sha256");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var hashIface = CreateHashType();

            pkg.AddExport(new FunctionSymbol("New",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "sha256"));

            pkg.AddExport(new FunctionSymbol("New224",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "sha256"));

            pkg.AddExport(new FunctionSymbol("Sum256",
                new[] { P("data", byteSlice, 0) },
                new TypeSymbol[] { new ArrayTypeSymbol(BuiltinTypes.Byte, 32) }, packageName: "sha256"));

            pkg.AddExport(new FunctionSymbol("Sum224",
                new[] { P("data", byteSlice, 0) },
                new TypeSymbol[] { new ArrayTypeSymbol(BuiltinTypes.Byte, 28) }, packageName: "sha256"));

            pkg.AddExport(new ConstantSymbol("Size", BuiltinTypes.Int, (long)32));
            pkg.AddExport(new ConstantSymbol("Size224", BuiltinTypes.Int, (long)28));
            pkg.AddExport(new ConstantSymbol("BlockSize", BuiltinTypes.Int, (long)64));

            return pkg;
        }

        private static PackageSymbol CreateCryptoSha512Package()
        {
            var pkg = new PackageSymbol("sha512", "crypto/sha512");

            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var hashIface = CreateHashType();

            pkg.AddExport(new FunctionSymbol("New",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "sha512"));

            pkg.AddExport(new FunctionSymbol("New384",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { hashIface }, packageName: "sha512"));

            pkg.AddExport(new FunctionSymbol("Sum512",
                new[] { P("data", byteSlice, 0) },
                new TypeSymbol[] { new ArrayTypeSymbol(BuiltinTypes.Byte, 64) }, packageName: "sha512"));

            pkg.AddExport(new FunctionSymbol("Sum384",
                new[] { P("data", byteSlice, 0) },
                new TypeSymbol[] { new ArrayTypeSymbol(BuiltinTypes.Byte, 48) }, packageName: "sha512"));

            pkg.AddExport(new ConstantSymbol("Size", BuiltinTypes.Int, (long)64));
            pkg.AddExport(new ConstantSymbol("Size384", BuiltinTypes.Int, (long)48));
            pkg.AddExport(new ConstantSymbol("BlockSize", BuiltinTypes.Int, (long)128));
            pkg.AddExport(new ConstantSymbol("Size224", BuiltinTypes.Int, (long)28));
            pkg.AddExport(new ConstantSymbol("Size256", BuiltinTypes.Int, (long)32));

            return pkg;
        }

        private static StructTypeSymbol CreateOsFileType()
        {
            var fileType = new StructTypeSymbol("File", Array.Empty<FieldSymbol>());
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            fileType.AddMethod(new MethodSymbol("Close", fileType, false,
                Array.Empty<ParameterSymbol>(), new[] { BuiltinTypes.Error }));
            fileType.AddMethod(new MethodSymbol("Name", fileType, false,
                Array.Empty<ParameterSymbol>(), new[] { BuiltinTypes.String }));
            fileType.AddMethod(new MethodSymbol("Write", fileType, false,
                new[] { P("b", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            fileType.AddMethod(new MethodSymbol("Read", fileType, false,
                new[] { P("b", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            fileType.AddMethod(new MethodSymbol("WriteString", fileType, false,
                new[] { P("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error }));
            fileType.AddMethod(new MethodSymbol("Sync", fileType, false,
                Array.Empty<ParameterSymbol>(), new[] { BuiltinTypes.Error }));
            fileType.AddMethod(new MethodSymbol("Chmod", fileType, false,
                new[] { P("mode", BuiltinTypes.Int, 0) },
                new[] { BuiltinTypes.Error }));
            return fileType;
        }

        private static PackageSymbol CreateNetPackage()
        {
            var pkg = new PackageSymbol("net", "net");

            // net.IP is []byte with methods
            var ipType = new SliceTypeSymbol(BuiltinTypes.Byte);
            var ipNamedType = new TypeSymbol("IP", TypeKind.Slice, ipType);
            ipNamedType.AddMethod(new MethodSymbol("String", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            ipNamedType.AddMethod(new MethodSymbol("To4", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), ipNamedType));
            ipNamedType.AddMethod(new MethodSymbol("To16", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), ipNamedType));
            ipNamedType.AddMethod(new MethodSymbol("IsLoopback", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            ipNamedType.AddMethod(new MethodSymbol("IsGlobalUnicast", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            ipNamedType.AddMethod(new MethodSymbol("IsLinkLocalUnicast", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            ipNamedType.AddMethod(new MethodSymbol("IsLinkLocalMulticast", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            ipNamedType.AddMethod(new MethodSymbol("IsMulticast", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            ipNamedType.AddMethod(new MethodSymbol("IsUnspecified", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            ipNamedType.AddMethod(new MethodSymbol("IsPrivate", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            ipNamedType.AddMethod(new MethodSymbol("Equal", ipNamedType, false,
                new[] { new ParameterSymbol("x", ipNamedType, 0) }, BuiltinTypes.Bool));
            ipNamedType.AddMethod(new MethodSymbol("Mask", ipNamedType, false,
                new[] { new ParameterSymbol("mask", ipType, 0) }, ipNamedType));
            ipNamedType.AddMethod(new MethodSymbol("DefaultMask", ipNamedType, false,
                Array.Empty<ParameterSymbol>(), ipType));
            ipNamedType.AddMethod(new MethodSymbol("MarshalText", ipNamedType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.Byte), BuiltinTypes.Error }));
            pkg.AddExport(ipNamedType);

            // net.IPMask is []byte
            var maskType = new SliceTypeSymbol(BuiltinTypes.Byte);
            pkg.AddExport(new TypeSymbol("IPMask", TypeKind.Slice, maskType));

            // net.IPNet struct
            var ipNetType = new StructTypeSymbol("IPNet", new[]
            {
                new FieldSymbol("IP", ipType, 0),
                new FieldSymbol("Mask", maskType, 1),
            });
            pkg.AddExport(ipNetType);

            // net.IPAddr struct
            var ipAddrType = new StructTypeSymbol("IPAddr", new[]
            {
                new FieldSymbol("IP", ipNamedType, 0),
                new FieldSymbol("Zone", BuiltinTypes.String, 1),
            });
            ipAddrType.AddMethod(new MethodSymbol("String", ipAddrType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            ipAddrType.AddMethod(new MethodSymbol("Network", ipAddrType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            pkg.AddExport(ipAddrType);

            // net.TCPAddr struct
            var tcpAddrType = new StructTypeSymbol("TCPAddr", new[]
            {
                new FieldSymbol("IP", ipNamedType, 0),
                new FieldSymbol("Port", BuiltinTypes.Int, 1),
                new FieldSymbol("Zone", BuiltinTypes.String, 2),
            });
            tcpAddrType.AddMethod(new MethodSymbol("String", tcpAddrType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            tcpAddrType.AddMethod(new MethodSymbol("Network", tcpAddrType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            pkg.AddExport(tcpAddrType);

            // net.UDPAddr struct
            var udpAddrType = new StructTypeSymbol("UDPAddr", new[]
            {
                new FieldSymbol("IP", ipNamedType, 0),
                new FieldSymbol("Port", BuiltinTypes.Int, 1),
                new FieldSymbol("Zone", BuiltinTypes.String, 2),
            });
            pkg.AddExport(udpAddrType);

            // net.HardwareAddr is []byte
            var hwAddrType = new SliceTypeSymbol(BuiltinTypes.Byte);
            pkg.AddExport(new TypeSymbol("HardwareAddr", TypeKind.Slice, hwAddrType));

            // net.IPv4(a, b, c, d byte) IP
            pkg.AddExport(new FunctionSymbol("IPv4",
                new[]
                {
                    new ParameterSymbol("a", BuiltinTypes.Byte, 0),
                    new ParameterSymbol("b", BuiltinTypes.Byte, 1),
                    new ParameterSymbol("c", BuiltinTypes.Byte, 2),
                    new ParameterSymbol("d", BuiltinTypes.Byte, 3),
                }, new TypeSymbol[] { ipType }, packageName: "net"));

            // net.CIDRMask(ones, bits int) IPMask
            pkg.AddExport(new FunctionSymbol("CIDRMask",
                new[]
                {
                    new ParameterSymbol("ones", BuiltinTypes.Int, 0),
                    new ParameterSymbol("bits", BuiltinTypes.Int, 1),
                }, new TypeSymbol[] { maskType }, packageName: "net"));

            // net.ParseIP(s string) IP
            pkg.AddExport(new FunctionSymbol("ParseIP",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { ipType }, packageName: "net"));

            // net.ParseCIDR(s string) (IP, *IPNet, error)
            pkg.AddExport(new FunctionSymbol("ParseCIDR",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { ipType, new PointerTypeSymbol(ipNetType), BuiltinTypes.Error },
                packageName: "net"));

            // Resolve functions
            pkg.AddExport(new FunctionSymbol("ResolveTCPAddr",
                new[] { P("network", BuiltinTypes.String, 0), P("address", BuiltinTypes.String, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(tcpAddrType), BuiltinTypes.Error },
                packageName: "net"));
            pkg.AddExport(new FunctionSymbol("ResolveIPAddr",
                new[] { P("network", BuiltinTypes.String, 0), P("address", BuiltinTypes.String, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(ipAddrType), BuiltinTypes.Error },
                packageName: "net"));
            pkg.AddExport(new FunctionSymbol("ResolveUDPAddr",
                new[] { P("network", BuiltinTypes.String, 0), P("address", BuiltinTypes.String, 1) },
                new TypeSymbol[] { new PointerTypeSymbol(udpAddrType), BuiltinTypes.Error },
                packageName: "net"));

            // IP constants
            pkg.AddExport(new PackageVarSymbol("IPv4zero", ipNamedType));
            pkg.AddExport(new PackageVarSymbol("IPv4bcast", ipNamedType));
            pkg.AddExport(new PackageVarSymbol("IPv6zero", ipNamedType));
            pkg.AddExport(new PackageVarSymbol("IPv6loopback", ipNamedType));
            pkg.AddExport(new PackageVarSymbol("IPv4len", BuiltinTypes.Int));
            pkg.AddExport(new PackageVarSymbol("IPv6len", BuiltinTypes.Int));

            // net.Interface struct
            var ifaceType = new StructTypeSymbol("Interface", new[]
            {
                new FieldSymbol("Index", BuiltinTypes.Int, 0),
                new FieldSymbol("MTU", BuiltinTypes.Int, 1),
                new FieldSymbol("Name", BuiltinTypes.String, 2),
                new FieldSymbol("HardwareAddr", hwAddrType, 3),
                new FieldSymbol("Flags", BuiltinTypes.Uint, 4),
            });
            ifaceType.AddMethod(new MethodSymbol("Addrs", ifaceType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.String), BuiltinTypes.Error }));
            pkg.AddExport(ifaceType);

            // net.Interfaces() ([]Interface, error)
            pkg.AddExport(new FunctionSymbol("Interfaces",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new SliceTypeSymbol(ifaceType), BuiltinTypes.Error },
                packageName: "net"));

            // net.InterfaceByName(name string) (*Interface, error)
            pkg.AddExport(new FunctionSymbol("InterfaceByName",
                new[] { new ParameterSymbol("name", BuiltinTypes.String, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(ifaceType), BuiltinTypes.Error },
                packageName: "net"));

            // net.Addr interface
            var addrIface = new InterfaceTypeSymbol("Addr", Array.Empty<MethodSymbol>());
            addrIface.AddMethod(new MethodSymbol("Network", addrIface, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            addrIface.AddMethod(new MethodSymbol("String", addrIface, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            pkg.AddExport(addrIface);

            // net.Error interface
            var netErrorIface = new InterfaceTypeSymbol("Error", Array.Empty<MethodSymbol>());
            netErrorIface.AddMethod(new MethodSymbol("Error", netErrorIface, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.String));
            netErrorIface.AddMethod(new MethodSymbol("Timeout", netErrorIface, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            netErrorIface.AddMethod(new MethodSymbol("Temporary", netErrorIface, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            pkg.AddExport(netErrorIface);

            // net.Dial(network, address string) (Conn, error)
            var connIface = new InterfaceTypeSymbol("Conn", Array.Empty<MethodSymbol>());
            pkg.AddExport(connIface);
            pkg.AddExport(new FunctionSymbol("Dial",
                new[] { new ParameterSymbol("network", BuiltinTypes.String, 0),
                        new ParameterSymbol("address", BuiltinTypes.String, 1) },
                new TypeSymbol[] { connIface, BuiltinTypes.Error },
                packageName: "net"));

            // net.Listen(network, address string) (Listener, error)
            var listenerIface = new InterfaceTypeSymbol("Listener", Array.Empty<MethodSymbol>());
            pkg.AddExport(listenerIface);
            pkg.AddExport(new FunctionSymbol("Listen",
                new[] { new ParameterSymbol("network", BuiltinTypes.String, 0),
                        new ParameterSymbol("address", BuiltinTypes.String, 1) },
                new TypeSymbol[] { listenerIface, BuiltinTypes.Error },
                packageName: "net"));

            // net.JoinHostPort(host, port string) string
            pkg.AddExport(new FunctionSymbol("JoinHostPort",
                new[] { new ParameterSymbol("host", BuiltinTypes.String, 0),
                        new ParameterSymbol("port", BuiltinTypes.String, 1) },
                new TypeSymbol[] { BuiltinTypes.String },
                packageName: "net"));

            // net.SplitHostPort(hostport string) (host, port string, err error)
            pkg.AddExport(new FunctionSymbol("SplitHostPort",
                new[] { new ParameterSymbol("hostport", BuiltinTypes.String, 0) },
                new TypeSymbol[] { BuiltinTypes.String, BuiltinTypes.String, BuiltinTypes.Error },
                packageName: "net"));

            // net.LookupHost(host string) (addrs []string, err error)
            pkg.AddExport(new FunctionSymbol("LookupHost",
                new[] { new ParameterSymbol("host", BuiltinTypes.String, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.String), BuiltinTypes.Error },
                packageName: "net"));

            // net.LookupIP(host string) ([]IP, error)
            pkg.AddExport(new FunctionSymbol("LookupIP",
                new[] { P("host", BuiltinTypes.String, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(ipNamedType), BuiltinTypes.Error },
                packageName: "net"));

            // net.Dialer type
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var dialerType = new StructTypeSymbol("Dialer", new[]
            {
                new FieldSymbol("Timeout", emptyIface, 0),
                new FieldSymbol("Deadline", emptyIface, 1),
                new FieldSymbol("LocalAddr", emptyIface, 2),
                new FieldSymbol("KeepAlive", emptyIface, 3),
                new FieldSymbol("FallbackDelay", emptyIface, 4),
                new FieldSymbol("Resolver", emptyIface, 5),
                new FieldSymbol("DualStack", BuiltinTypes.Bool, 6),
                new FieldSymbol("Control", emptyIface, 7),
            });
            dialerType.AddMethod(new MethodSymbol("Dial", dialerType, false,
                new[] { new ParameterSymbol("network", BuiltinTypes.String, 0),
                        new ParameterSymbol("address", BuiltinTypes.String, 1) },
                new TypeSymbol[] { connIface, BuiltinTypes.Error }));
            dialerType.AddMethod(new MethodSymbol("DialContext", dialerType, false,
                new[] { new ParameterSymbol("ctx", emptyIface, 0),
                        new ParameterSymbol("network", BuiltinTypes.String, 1),
                        new ParameterSymbol("address", BuiltinTypes.String, 2) },
                new TypeSymbol[] { connIface, BuiltinTypes.Error }));
            pkg.AddExport(dialerType);

            // net.ParseMAC(s string) (hw HardwareAddr, err error)
            pkg.AddExport(new FunctionSymbol("ParseMAC",
                new[] { P("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { hwAddrType, BuiltinTypes.Error },
                packageName: "net"));

            // net.LookupIP(host string) ([]IP, error)
            pkg.AddExport(new FunctionSymbol("LookupIP",
                new[] { P("host", BuiltinTypes.String, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(ipType), BuiltinTypes.Error },
                packageName: "net"));

            // net.MX struct
            var mxType = new StructTypeSymbol("MX", new[]
            {
                new FieldSymbol("Host", BuiltinTypes.String, 0),
                new FieldSymbol("Pref", BuiltinTypes.Uint16, 1),
            });
            pkg.AddExport(mxType);

            // net.LookupMX(name string) ([]*MX, error)
            pkg.AddExport(new FunctionSymbol("LookupMX",
                new[] { P("name", BuiltinTypes.String, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(new PointerTypeSymbol(mxType)), BuiltinTypes.Error },
                packageName: "net"));

            // net.LookupAddr(addr string) (names []string, err error)
            pkg.AddExport(new FunctionSymbol("LookupAddr",
                new[] { P("addr", BuiltinTypes.String, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.String), BuiltinTypes.Error },
                packageName: "net"));

            // net.LookupCNAME(host string) (cname string, err error)
            pkg.AddExport(new FunctionSymbol("LookupCNAME",
                new[] { P("host", BuiltinTypes.String, 0) },
                new TypeSymbol[] { BuiltinTypes.String, BuiltinTypes.Error },
                packageName: "net"));

            // net.LookupPort(network, service string) (port int, err error)
            pkg.AddExport(new FunctionSymbol("LookupPort",
                new[] { P("network", BuiltinTypes.String, 0), P("service", BuiltinTypes.String, 1) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.Error },
                packageName: "net"));

            // net.LookupTXT(name string) ([]string, error)
            pkg.AddExport(new FunctionSymbol("LookupTXT",
                new[] { P("name", BuiltinTypes.String, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(BuiltinTypes.String), BuiltinTypes.Error },
                packageName: "net"));

            // net.SplitHostPort(hostport string) (host, port string, err error)
            pkg.AddExport(new FunctionSymbol("SplitHostPort",
                new[] { P("hostport", BuiltinTypes.String, 0) },
                new TypeSymbol[] { BuiltinTypes.String, BuiltinTypes.String, BuiltinTypes.Error },
                packageName: "net"));

            // net.JoinHostPort(host, port string) string
            pkg.AddExport(new FunctionSymbol("JoinHostPort",
                new[] { P("host", BuiltinTypes.String, 0), P("port", BuiltinTypes.String, 1) },
                new TypeSymbol[] { BuiltinTypes.String },
                packageName: "net"));

            return pkg;
        }

        private static PackageSymbol CreateNetMailPackage()
        {
            var pkg = new PackageSymbol("mail", "net/mail");
            var s = BuiltinTypes.String;

            // mail.Address struct
            var addrType = new StructTypeSymbol("Address", new[]
            {
                new FieldSymbol("Name", s, 0),
                new FieldSymbol("Address", s, 1),
            });
            addrType.AddMethod(new MethodSymbol("String", addrType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(addrType);

            // mail.ParseAddress(address string) (*Address, error)
            pkg.AddExport(new FunctionSymbol("ParseAddress",
                new[] { P("address", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(addrType), BuiltinTypes.Error },
                packageName: "mail"));

            // mail.ParseAddressList(list string) ([]*Address, error)
            pkg.AddExport(new FunctionSymbol("ParseAddressList",
                new[] { P("list", s, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(new PointerTypeSymbol(addrType)), BuiltinTypes.Error },
                packageName: "mail"));

            return pkg;
        }

        private static PackageSymbol CreateSyscallPackage()
        {
            var pkg = new PackageSymbol("syscall", "syscall");

            var i = BuiltinTypes.Int;
            var s = BuiltinTypes.String;
            var err = BuiltinTypes.Error;

            // Common types
            var statType = new StructTypeSymbol("Stat_t", new[]
            {
                new FieldSymbol("Atim", new StructTypeSymbol("Timespec", new[]
                {
                    new FieldSymbol("Sec", BuiltinTypes.Int64, 0),
                    new FieldSymbol("Nsec", BuiltinTypes.Int64, 1),
                }), 0),
                new FieldSymbol("Mtim", new StructTypeSymbol("Timespec", new[]
                {
                    new FieldSymbol("Sec", BuiltinTypes.Int64, 0),
                    new FieldSymbol("Nsec", BuiltinTypes.Int64, 1),
                }), 1),
                new FieldSymbol("Ctim", new StructTypeSymbol("Timespec", new[]
                {
                    new FieldSymbol("Sec", BuiltinTypes.Int64, 0),
                    new FieldSymbol("Nsec", BuiltinTypes.Int64, 1),
                }), 2),
                new FieldSymbol("Size", BuiltinTypes.Int64, 3),
                new FieldSymbol("Mode", BuiltinTypes.Uint32, 4),
                new FieldSymbol("Uid", BuiltinTypes.Uint32, 5),
                new FieldSymbol("Gid", BuiltinTypes.Uint32, 6),
            });
            pkg.AddExport(statType);

            var timespecType = new StructTypeSymbol("Timespec", new[]
            {
                new FieldSymbol("Sec", BuiltinTypes.Int64, 0),
                new FieldSymbol("Nsec", BuiltinTypes.Int64, 1),
            });
            pkg.AddExport(timespecType);

            // Errno type
            var errnoType = new TypeSymbol("Errno", TypeKind.Uintptr, BuiltinTypes.Uintptr);
            errnoType.AddMethod(new MethodSymbol("Error", errnoType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(errnoType);

            // Signal type
            var signalType = new TypeSymbol("Signal", TypeKind.Int, i);
            signalType.AddMethod(new MethodSymbol("Signal", signalType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void));
            signalType.AddMethod(new MethodSymbol("String", signalType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(signalType);

            // Signal constants
            pkg.AddExport(new ConstantSymbol("SIGINT", signalType, (long)2));
            pkg.AddExport(new ConstantSymbol("SIGTERM", signalType, (long)15));
            pkg.AddExport(new ConstantSymbol("SIGKILL", signalType, (long)9));
            pkg.AddExport(new ConstantSymbol("SIGHUP", signalType, (long)1));
            pkg.AddExport(new ConstantSymbol("SIGQUIT", signalType, (long)3));
            pkg.AddExport(new ConstantSymbol("SIGPIPE", signalType, (long)13));

            // ProcAttr type
            var procAttrType = new StructTypeSymbol("ProcAttr", new[]
            {
                new FieldSymbol("Dir", s, 0),
                new FieldSymbol("Env", new SliceTypeSymbol(s), 1),
                new FieldSymbol("Files", new SliceTypeSymbol(BuiltinTypes.Uintptr), 2),
            });
            pkg.AddExport(procAttrType);

            // WaitStatus type
            var waitStatusType = new TypeSymbol("WaitStatus", TypeKind.Uint32, BuiltinTypes.Uint32);
            waitStatusType.AddMethod(new MethodSymbol("ExitStatus", waitStatusType, false,
                Array.Empty<ParameterSymbol>(), i));
            waitStatusType.AddMethod(new MethodSymbol("Exited", waitStatusType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            waitStatusType.AddMethod(new MethodSymbol("Signaled", waitStatusType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            pkg.AddExport(waitStatusType);

            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // ForkExec(argv0 string, argv []string, attr *ProcAttr) (pid int, err error)
            pkg.AddExport(new FunctionSymbol("ForkExec",
                new[] { new ParameterSymbol("argv0", s, 0),
                        new ParameterSymbol("argv", new SliceTypeSymbol(s), 1),
                        new ParameterSymbol("attr", new PointerTypeSymbol(procAttrType), 2) },
                new TypeSymbol[] { i, err }, packageName: "syscall"));

            // Wait4(pid int, wstatus *WaitStatus, options int, rusage *Rusage) (wpid int, err error)
            pkg.AddExport(new FunctionSymbol("Wait4",
                new[] { new ParameterSymbol("pid", i, 0),
                        new ParameterSymbol("wstatus", emptyIface, 1),
                        new ParameterSymbol("options", i, 2),
                        new ParameterSymbol("rusage", emptyIface, 3) },
                new TypeSymbol[] { i, err }, packageName: "syscall"));

            // Common errno constants
            pkg.AddExport(new ConstantSymbol("ENOENT", errnoType, (long)2));
            pkg.AddExport(new ConstantSymbol("EACCES", errnoType, (long)13));
            pkg.AddExport(new ConstantSymbol("EEXIST", errnoType, (long)17));
            pkg.AddExport(new ConstantSymbol("EINTR", errnoType, (long)4));
            pkg.AddExport(new ConstantSymbol("EPERM", errnoType, (long)1));

            // File descriptor constants
            pkg.AddExport(new ConstantSymbol("Stdin", BuiltinTypes.Int, (long)0));
            pkg.AddExport(new ConstantSymbol("Stdout", BuiltinTypes.Int, (long)1));
            pkg.AddExport(new ConstantSymbol("Stderr", BuiltinTypes.Int, (long)2));

            // Ioctl constants
            pkg.AddExport(new ConstantSymbol("SYS_IOCTL", BuiltinTypes.Uintptr, (long)16));

            // Syscall(trap, a1, a2, a3 uintptr) (r1, r2 uintptr, err Errno)
            pkg.AddExport(new FunctionSymbol("Syscall",
                new[] { P("trap", BuiltinTypes.Uintptr, 0),
                        P("a1", BuiltinTypes.Uintptr, 1),
                        P("a2", BuiltinTypes.Uintptr, 2),
                        P("a3", BuiltinTypes.Uintptr, 3) },
                new TypeSymbol[] { BuiltinTypes.Uintptr, BuiltinTypes.Uintptr, errnoType },
                packageName: "syscall"));

            // Syscall6(trap, a1, a2, a3, a4, a5, a6 uintptr) (r1, r2 uintptr, err Errno)
            pkg.AddExport(new FunctionSymbol("Syscall6",
                new[] { P("trap", BuiltinTypes.Uintptr, 0),
                        P("a1", BuiltinTypes.Uintptr, 1),
                        P("a2", BuiltinTypes.Uintptr, 2),
                        P("a3", BuiltinTypes.Uintptr, 3),
                        P("a4", BuiltinTypes.Uintptr, 4),
                        P("a5", BuiltinTypes.Uintptr, 5),
                        P("a6", BuiltinTypes.Uintptr, 6) },
                new TypeSymbol[] { BuiltinTypes.Uintptr, BuiltinTypes.Uintptr, errnoType },
                packageName: "syscall"));

            // LazyDLL type for Windows syscall
            var lazyProcType = new StructTypeSymbol("LazyProc", Array.Empty<FieldSymbol>());
            lazyProcType.AddMethod(new MethodSymbol("Call", lazyProcType, false,
                Array.Empty<TypeParameterSymbol>(),
                new[] { P("a", new SliceTypeSymbol(BuiltinTypes.Uintptr), 0) },
                new TypeSymbol[] { BuiltinTypes.Uintptr, BuiltinTypes.Uintptr, err },
                isVariadic: true));

            var lazyDllType = new StructTypeSymbol("LazyDLL", Array.Empty<FieldSymbol>());
            lazyDllType.AddMethod(new MethodSymbol("NewProc", lazyDllType, false,
                new[] { P("name", s, 0) },
                new PointerTypeSymbol(lazyProcType)));
            pkg.AddExport(lazyDllType);
            pkg.AddExport(lazyProcType);

            // NewLazyDLL(name string) *LazyDLL
            pkg.AddExport(new FunctionSymbol("NewLazyDLL",
                new[] { P("name", s, 0) },
                new[] { new PointerTypeSymbol(lazyDllType) },
                packageName: "syscall"));

            // Dirent type
            var direntType = new StructTypeSymbol("Dirent", new[]
            {
                new FieldSymbol("Ino", BuiltinTypes.Uint64, 0),
                new FieldSymbol("Off", BuiltinTypes.Int64, 1),
                new FieldSymbol("Reclen", BuiltinTypes.Uint16, 2),
                new FieldSymbol("Type", BuiltinTypes.Uint8, 3),
                new FieldSymbol("Name", new ArrayTypeSymbol(BuiltinTypes.Int8, 256), 4),
            });
            pkg.AddExport(direntType);

            // DT_* constants (dirent types)
            pkg.AddExport(new ConstantSymbol("DT_BLK", BuiltinTypes.Uint8, (long)6));
            pkg.AddExport(new ConstantSymbol("DT_CHR", BuiltinTypes.Uint8, (long)2));
            pkg.AddExport(new ConstantSymbol("DT_DIR", BuiltinTypes.Uint8, (long)4));
            pkg.AddExport(new ConstantSymbol("DT_FIFO", BuiltinTypes.Uint8, (long)1));
            pkg.AddExport(new ConstantSymbol("DT_LNK", BuiltinTypes.Uint8, (long)10));
            pkg.AddExport(new ConstantSymbol("DT_REG", BuiltinTypes.Uint8, (long)8));
            pkg.AddExport(new ConstantSymbol("DT_SOCK", BuiltinTypes.Uint8, (long)12));
            pkg.AddExport(new ConstantSymbol("DT_UNKNOWN", BuiltinTypes.Uint8, (long)0));

            // More errno constants
            pkg.AddExport(new ConstantSymbol("EINVAL", errnoType, (long)22));
            pkg.AddExport(new ConstantSymbol("ENOSYS", errnoType, (long)38));
            pkg.AddExport(new ConstantSymbol("ENOTDIR", errnoType, (long)20));

            // More signal constants
            pkg.AddExport(new ConstantSymbol("SIGUSR1", signalType, (long)10));
            pkg.AddExport(new ConstantSymbol("SIGUSR2", signalType, (long)12));

            // Open/Close/ReadDirent
            pkg.AddExport(new FunctionSymbol("Open",
                new[] { P("path", s, 0), P("mode", i, 1), P("perm", BuiltinTypes.Uint32, 2) },
                new TypeSymbol[] { i, err }, packageName: "syscall"));
            pkg.AddExport(new FunctionSymbol("Close",
                new[] { P("fd", i, 0) },
                new TypeSymbol[] { err }, packageName: "syscall"));
            pkg.AddExport(new FunctionSymbol("ReadDirent",
                new[] { P("fd", i, 0), P("buf", new SliceTypeSymbol(BuiltinTypes.Byte), 1) },
                new TypeSymbol[] { i, err }, packageName: "syscall"));
            pkg.AddExport(new FunctionSymbol("ParseDirent",
                new[] { P("buf", new SliceTypeSymbol(BuiltinTypes.Byte), 0),
                        P("max", i, 1),
                        P("names", new SliceTypeSymbol(s), 2) },
                new TypeSymbol[] { i, i, new SliceTypeSymbol(s) },
                packageName: "syscall"));

            // O_* constants
            pkg.AddExport(new ConstantSymbol("O_RDONLY", i, (long)0));
            pkg.AddExport(new ConstantSymbol("O_WRONLY", i, (long)1));
            pkg.AddExport(new ConstantSymbol("O_RDWR", i, (long)2));
            pkg.AddExport(new ConstantSymbol("O_CLOEXEC", i, (long)0x80000));
            pkg.AddExport(new ConstantSymbol("O_DIRECTORY", i, (long)0x10000));

            return pkg;
        }

        private static PackageSymbol CreateMathBigPackage()
        {
            var pkg = new PackageSymbol("big", "math/big");

            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;

            // Int type
            var intType = new StructTypeSymbol("Int", Array.Empty<FieldSymbol>());
            var ptrInt = new PointerTypeSymbol(intType);
            intType.AddMethod(new MethodSymbol("Int64", intType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64));
            intType.AddMethod(new MethodSymbol("String", intType, false,
                Array.Empty<ParameterSymbol>(), s));
            intType.AddMethod(new MethodSymbol("SetInt64", intType, false,
                new[] { P("x", BuiltinTypes.Int64, 0) }, ptrInt));
            intType.AddMethod(new MethodSymbol("SetString", intType, false,
                new[] { P("s", s, 0), P("base", i, 1) },
                new TypeSymbol[] { ptrInt, BuiltinTypes.Bool }));
            intType.AddMethod(new MethodSymbol("Add", intType, false,
                new[] { P("x", ptrInt, 0), P("y", ptrInt, 1) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Sub", intType, false,
                new[] { P("x", ptrInt, 0), P("y", ptrInt, 1) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Mul", intType, false,
                new[] { P("x", ptrInt, 0), P("y", ptrInt, 1) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Div", intType, false,
                new[] { P("x", ptrInt, 0), P("y", ptrInt, 1) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Cmp", intType, false,
                new[] { P("y", ptrInt, 0) }, i));
            intType.AddMethod(new MethodSymbol("Bytes", intType, false,
                Array.Empty<ParameterSymbol>(), new SliceTypeSymbol(BuiltinTypes.Uint8)));
            intType.AddMethod(new MethodSymbol("BitLen", intType, false,
                Array.Empty<ParameterSymbol>(), i));
            intType.AddMethod(new MethodSymbol("Sign", intType, false,
                Array.Empty<ParameterSymbol>(), i));
            intType.AddMethod(new MethodSymbol("Abs", intType, false,
                new[] { P("x", ptrInt, 0) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Set", intType, false,
                new[] { P("x", ptrInt, 0) }, ptrInt));
            intType.AddMethod(new MethodSymbol("DivMod", intType, false,
                new[] { P("x", ptrInt, 0), P("y", ptrInt, 1), P("m", ptrInt, 2) },
                new TypeSymbol[] { ptrInt, ptrInt }));
            intType.AddMethod(new MethodSymbol("Mod", intType, false,
                new[] { P("x", ptrInt, 0), P("y", ptrInt, 1) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Exp", intType, false,
                new[] { P("x", ptrInt, 0), P("y", ptrInt, 1), P("m", ptrInt, 2) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Neg", intType, false,
                new[] { P("x", ptrInt, 0) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Lsh", intType, false,
                new[] { P("x", ptrInt, 0), P("n", BuiltinTypes.Uint, 1) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Rsh", intType, false,
                new[] { P("x", ptrInt, 0), P("n", BuiltinTypes.Uint, 1) }, ptrInt));
            intType.AddMethod(new MethodSymbol("IsInt64", intType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            intType.AddMethod(new MethodSymbol("SetBytes", intType, false,
                new[] { P("buf", new SliceTypeSymbol(BuiltinTypes.Uint8), 0) }, ptrInt));
            intType.AddMethod(new MethodSymbol("SetUint64", intType, false,
                new[] { P("x", BuiltinTypes.Uint64, 0) }, ptrInt));
            intType.AddMethod(new MethodSymbol("Uint64", intType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint64));
            intType.AddMethod(new MethodSymbol("IsUint64", intType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            pkg.AddExport(intType);

            // Float type
            var floatType = new StructTypeSymbol("Float", Array.Empty<FieldSymbol>());
            var ptrFloat = new PointerTypeSymbol(floatType);
            floatType.AddMethod(new MethodSymbol("SetFloat64", floatType, false,
                new[] { P("x", BuiltinTypes.Float64, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Float64", floatType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Float64, iface }));
            floatType.AddMethod(new MethodSymbol("String", floatType, false,
                Array.Empty<ParameterSymbol>(), s));
            floatType.AddMethod(new MethodSymbol("Text", floatType, false,
                new[] { P("format", BuiltinTypes.Uint8, 0), P("prec", i, 1) }, s));
            floatType.AddMethod(new MethodSymbol("Cmp", floatType, false,
                new[] { P("y", ptrFloat, 0) }, i));
            floatType.AddMethod(new MethodSymbol("SetPrec", floatType, false,
                new[] { P("prec", BuiltinTypes.Uint, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Add", floatType, false,
                new[] { P("x", ptrFloat, 0), P("y", ptrFloat, 1) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Sub", floatType, false,
                new[] { P("x", ptrFloat, 0), P("y", ptrFloat, 1) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Mul", floatType, false,
                new[] { P("x", ptrFloat, 0), P("y", ptrFloat, 1) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Quo", floatType, false,
                new[] { P("x", ptrFloat, 0), P("y", ptrFloat, 1) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Sign", floatType, false,
                Array.Empty<ParameterSymbol>(), i));
            floatType.AddMethod(new MethodSymbol("Abs", floatType, false,
                new[] { P("x", ptrFloat, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Neg", floatType, false,
                new[] { P("x", ptrFloat, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("SetInt", floatType, false,
                new[] { P("x", ptrInt, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Int", floatType, false,
                new[] { P("z", ptrInt, 0) },
                new TypeSymbol[] { ptrInt, iface }));
            floatType.AddMethod(new MethodSymbol("Float32", floatType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Float32, iface }));
            floatType.AddMethod(new MethodSymbol("IsInf", floatType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            floatType.AddMethod(new MethodSymbol("IsInt", floatType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            floatType.AddMethod(new MethodSymbol("Prec", floatType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint));
            floatType.AddMethod(new MethodSymbol("MinPrec", floatType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Uint));
            floatType.AddMethod(new MethodSymbol("Copy", floatType, false,
                new[] { P("x", ptrFloat, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("SetInf", floatType, false,
                new[] { P("signbit", BuiltinTypes.Bool, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Set", floatType, false,
                new[] { P("x", ptrFloat, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("SetInt64", floatType, false,
                new[] { P("x", BuiltinTypes.Int64, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("SetUint64", floatType, false,
                new[] { P("x", BuiltinTypes.Uint64, 0) }, ptrFloat));
            floatType.AddMethod(new MethodSymbol("Int64", floatType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Int64, iface }));
            floatType.AddMethod(new MethodSymbol("Parse", floatType, false,
                new[] { P("s", s, 0), P("base", i, 1) },
                new TypeSymbol[] { ptrFloat, i, iface }));
            pkg.AddExport(floatType);

            // Accuracy type (for big.Exact, big.Above, big.Below)
            pkg.AddExport(new ConstantSymbol("Exact", iface, (long)0));
            pkg.AddExport(new ConstantSymbol("Above", iface, (long)1));
            pkg.AddExport(new ConstantSymbol("Below", iface, (long)-1));

            // Rat type
            var ratType = new StructTypeSymbol("Rat", Array.Empty<FieldSymbol>());
            var ptrRat = new PointerTypeSymbol(ratType);
            ratType.AddMethod(new MethodSymbol("SetString", ratType, false,
                new[] { P("s", s, 0) },
                new TypeSymbol[] { ptrRat, BuiltinTypes.Bool }));
            ratType.AddMethod(new MethodSymbol("FloatString", ratType, false,
                new[] { P("prec", i, 0) }, s));
            ratType.AddMethod(new MethodSymbol("String", ratType, false,
                Array.Empty<ParameterSymbol>(), s));
            ratType.AddMethod(new MethodSymbol("RatString", ratType, false,
                Array.Empty<ParameterSymbol>(), s));
            ratType.AddMethod(new MethodSymbol("Num", ratType, false,
                Array.Empty<ParameterSymbol>(), ptrInt));
            ratType.AddMethod(new MethodSymbol("Denom", ratType, false,
                Array.Empty<ParameterSymbol>(), ptrInt));
            ratType.AddMethod(new MethodSymbol("Float64", ratType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { BuiltinTypes.Float64, BuiltinTypes.Bool }));
            ratType.AddMethod(new MethodSymbol("SetInt", ratType, false,
                new[] { P("x", ptrInt, 0) }, ptrRat));
            ratType.AddMethod(new MethodSymbol("Mul", ratType, false,
                new[] { P("x", ptrRat, 0), P("y", ptrRat, 1) }, ptrRat));
            ratType.AddMethod(new MethodSymbol("Add", ratType, false,
                new[] { P("x", ptrRat, 0), P("y", ptrRat, 1) }, ptrRat));
            ratType.AddMethod(new MethodSymbol("Sub", ratType, false,
                new[] { P("x", ptrRat, 0), P("y", ptrRat, 1) }, ptrRat));
            ratType.AddMethod(new MethodSymbol("Quo", ratType, false,
                new[] { P("x", ptrRat, 0), P("y", ptrRat, 1) }, ptrRat));
            ratType.AddMethod(new MethodSymbol("SetFloat64", ratType, false,
                new[] { P("f", BuiltinTypes.Float64, 0) }, ptrRat));
            ratType.AddMethod(new MethodSymbol("Cmp", ratType, false,
                new[] { P("y", ptrRat, 0) }, i));
            ratType.AddMethod(new MethodSymbol("Sign", ratType, false,
                Array.Empty<ParameterSymbol>(), i));
            pkg.AddExport(ratType);

            // NewInt(x int64) *Int
            pkg.AddExport(new FunctionSymbol("NewInt",
                new[] { P("x", BuiltinTypes.Int64, 0) },
                new TypeSymbol[] { ptrInt }, packageName: "big"));

            // NewFloat(x float64) *Float
            pkg.AddExport(new FunctionSymbol("NewFloat",
                new[] { P("x", BuiltinTypes.Float64, 0) },
                new TypeSymbol[] { ptrFloat }, packageName: "big"));

            // NewRat(a, b int64) *Rat
            pkg.AddExport(new FunctionSymbol("NewRat",
                new[] { P("a", BuiltinTypes.Int64, 0), P("b", BuiltinTypes.Int64, 1) },
                new TypeSymbol[] { ptrRat }, packageName: "big"));

            // ParseFloat(s string, base int, prec uint, mode RoundingMode) (f *Float, b int, err error)
            pkg.AddExport(new FunctionSymbol("ParseFloat",
                new[] { P("s", s, 0), P("base", i, 1), P("prec", BuiltinTypes.Uint, 2), P("mode", iface, 3) },
                new TypeSymbol[] { ptrFloat, i, BuiltinTypes.Error }, packageName: "big"));

            return pkg;
        }

        private static PackageSymbol CreateImageColorPackage()
        {
            var pkg = new PackageSymbol("color", "image/color");

            var u8 = BuiltinTypes.Uint8;
            var u16 = BuiltinTypes.Uint16;
            var u32 = BuiltinTypes.Uint32;

            // color.Color interface { RGBA() (r, g, b, a uint32) }
            var colorIface = new InterfaceTypeSymbol("Color", new[]
            {
                new MethodSymbol("RGBA", null!, false,
                    Array.Empty<ParameterSymbol>(),
                    new TypeSymbol[] { u32, u32, u32, u32 }),
            });
            pkg.AddExport(colorIface);

            // color.Model interface { Convert(c Color) Color }
            var modelIface = new InterfaceTypeSymbol("Model", new[]
            {
                new MethodSymbol("Convert", null!, false,
                    new[] { new ParameterSymbol("c", colorIface, 0) },
                    new TypeSymbol[] { colorIface }),
            });
            pkg.AddExport(modelIface);

            // RGBA struct { R, G, B, A uint8 }
            var rgbaType = new StructTypeSymbol("RGBA", new[]
            {
                new FieldSymbol("R", u8, 0),
                new FieldSymbol("G", u8, 1),
                new FieldSymbol("B", u8, 2),
                new FieldSymbol("A", u8, 3),
            });
            rgbaType.AddMethod(new MethodSymbol("RGBA", rgbaType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { u32, u32, u32, u32 }));
            pkg.AddExport(rgbaType);

            // NRGBA struct { R, G, B, A uint8 }
            var nrgbaType = new StructTypeSymbol("NRGBA", new[]
            {
                new FieldSymbol("R", u8, 0),
                new FieldSymbol("G", u8, 1),
                new FieldSymbol("B", u8, 2),
                new FieldSymbol("A", u8, 3),
            });
            nrgbaType.AddMethod(new MethodSymbol("RGBA", nrgbaType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { u32, u32, u32, u32 }));
            pkg.AddExport(nrgbaType);

            // RGBA64 struct { R, G, B, A uint16 }
            var rgba64Type = new StructTypeSymbol("RGBA64", new[]
            {
                new FieldSymbol("R", u16, 0),
                new FieldSymbol("G", u16, 1),
                new FieldSymbol("B", u16, 2),
                new FieldSymbol("A", u16, 3),
            });
            rgba64Type.AddMethod(new MethodSymbol("RGBA", rgba64Type, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { u32, u32, u32, u32 }));
            pkg.AddExport(rgba64Type);

            // NRGBA64 struct { R, G, B, A uint16 }
            var nrgba64Type = new StructTypeSymbol("NRGBA64", new[]
            {
                new FieldSymbol("R", u16, 0),
                new FieldSymbol("G", u16, 1),
                new FieldSymbol("B", u16, 2),
                new FieldSymbol("A", u16, 3),
            });
            nrgba64Type.AddMethod(new MethodSymbol("RGBA", nrgba64Type, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { u32, u32, u32, u32 }));
            pkg.AddExport(nrgba64Type);

            // Gray struct { Y uint8 }
            var grayType = new StructTypeSymbol("Gray", new[]
            {
                new FieldSymbol("Y", u8, 0),
            });
            grayType.AddMethod(new MethodSymbol("RGBA", grayType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { u32, u32, u32, u32 }));
            pkg.AddExport(grayType);

            // Gray16 struct { Y uint16 }
            var gray16Type = new StructTypeSymbol("Gray16", new[]
            {
                new FieldSymbol("Y", u16, 0),
            });
            gray16Type.AddMethod(new MethodSymbol("RGBA", gray16Type, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { u32, u32, u32, u32 }));
            pkg.AddExport(gray16Type);

            // Alpha struct { A uint8 }
            var alphaType = new StructTypeSymbol("Alpha", new[]
            {
                new FieldSymbol("A", u8, 0),
            });
            alphaType.AddMethod(new MethodSymbol("RGBA", alphaType, false,
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { u32, u32, u32, u32 }));
            pkg.AddExport(alphaType);

            // Standard color models
            pkg.AddExport(new PackageVarSymbol("RGBAModel", modelIface, typeof(object), "RGBAModel"));
            pkg.AddExport(new PackageVarSymbol("NRGBA64Model", modelIface, typeof(object), "NRGBA64Model"));
            pkg.AddExport(new PackageVarSymbol("GrayModel", modelIface, typeof(object), "GrayModel"));
            pkg.AddExport(new PackageVarSymbol("Gray16Model", modelIface, typeof(object), "Gray16Model"));

            // Standard colors
            pkg.AddExport(new PackageVarSymbol("Black", colorIface, typeof(object), "Black"));
            pkg.AddExport(new PackageVarSymbol("White", colorIface, typeof(object), "White"));
            pkg.AddExport(new PackageVarSymbol("Transparent", colorIface, typeof(object), "Transparent"));
            pkg.AddExport(new PackageVarSymbol("Opaque", colorIface, typeof(object), "Opaque"));

            return pkg;
        }

        private static PackageSymbol CreateOsUserPackage()
        {
            var pkg = new PackageSymbol("user", "os/user");

            var s = BuiltinTypes.String;
            var err = BuiltinTypes.Error;

            // User struct
            var userType = new StructTypeSymbol("User", new[]
            {
                new FieldSymbol("Uid", s, 0),
                new FieldSymbol("Gid", s, 1),
                new FieldSymbol("Username", s, 2),
                new FieldSymbol("Name", s, 3),
                new FieldSymbol("HomeDir", s, 4),
            });
            pkg.AddExport(userType);

            // Current() (*User, error)
            pkg.AddExport(new FunctionSymbol("Current",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { new PointerTypeSymbol(userType), err }, packageName: "user"));

            // Lookup(username string) (*User, error)
            pkg.AddExport(new FunctionSymbol("Lookup",
                new[] { P("username", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(userType), err }, packageName: "user"));

            // LookupId(uid string) (*User, error)
            pkg.AddExport(new FunctionSymbol("LookupId",
                new[] { P("uid", s, 0) },
                new TypeSymbol[] { new PointerTypeSymbol(userType), err }, packageName: "user"));

            return pkg;
        }

        private static PackageSymbol CreateIoFsPackage()
        {
            var pkg = new PackageSymbol("fs", "io/fs");

            var s = BuiltinTypes.String;
            var err = BuiltinTypes.Error;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // File interface
            var fileIface = new InterfaceTypeSymbol("File", Array.Empty<MethodSymbol>());
            pkg.AddExport(fileIface);

            // FS interface — Open(name string) (File, error)
            var fsIface = new InterfaceTypeSymbol("FS", Array.Empty<MethodSymbol>());
            fsIface.SetMethods(new[]
            {
                new MethodSymbol("Open", fsIface, false,
                    new[] { P("name", s, 0) }, new TypeSymbol[] { fileIface, err }),
            });
            pkg.AddExport(fsIface);

            // FileMode type (uint32)
            var fileMode = new TypeSymbol("FileMode", TypeKind.Uint32, BuiltinTypes.Uint32);
            fileMode.AddMethod(new MethodSymbol("IsDir", fileMode, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            fileMode.AddMethod(new MethodSymbol("IsRegular", fileMode, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            fileMode.AddMethod(new MethodSymbol("Perm", fileMode, false,
                Array.Empty<ParameterSymbol>(), fileMode));
            fileMode.AddMethod(new MethodSymbol("String", fileMode, false,
                Array.Empty<ParameterSymbol>(), s));
            fileMode.AddMethod(new MethodSymbol("Type", fileMode, false,
                Array.Empty<ParameterSymbol>(), fileMode));
            pkg.AddExport(fileMode);

            // FileInfo interface
            var fileInfoIface = new InterfaceTypeSymbol("FileInfo", Array.Empty<MethodSymbol>());
            fileInfoIface.SetMethods(new[]
            {
                new MethodSymbol("Name", fileInfoIface, false,
                    Array.Empty<ParameterSymbol>(), s),
                new MethodSymbol("Size", fileInfoIface, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Int64),
                new MethodSymbol("Mode", fileInfoIface, false,
                    Array.Empty<ParameterSymbol>(), fileMode),
                new MethodSymbol("ModTime", fileInfoIface, false,
                    Array.Empty<ParameterSymbol>(), emptyIface),
                new MethodSymbol("IsDir", fileInfoIface, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool),
                new MethodSymbol("Sys", fileInfoIface, false,
                    Array.Empty<ParameterSymbol>(), emptyIface),
            });
            pkg.AddExport(fileInfoIface);

            // DirEntry interface
            var dirEntryIface = new InterfaceTypeSymbol("DirEntry", Array.Empty<MethodSymbol>());
            dirEntryIface.SetMethods(new[]
            {
                new MethodSymbol("Name", dirEntryIface, false,
                    Array.Empty<ParameterSymbol>(), s),
                new MethodSymbol("IsDir", dirEntryIface, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool),
                new MethodSymbol("Type", dirEntryIface, false,
                    Array.Empty<ParameterSymbol>(), fileMode),
                new MethodSymbol("Info", dirEntryIface, false,
                    Array.Empty<ParameterSymbol>(), new TypeSymbol[] { fileInfoIface, err }),
            });
            pkg.AddExport(dirEntryIface);

            // ReadDirFile interface — extends File with ReadDir method
            var readDirFileIface = new InterfaceTypeSymbol("ReadDirFile", Array.Empty<MethodSymbol>());
            readDirFileIface.AddMethod(new MethodSymbol("ReadDir", readDirFileIface, false,
                new[] { P("n", BuiltinTypes.Int, 0) },
                new TypeSymbol[] { new SliceTypeSymbol(dirEntryIface), err }));
            pkg.AddExport(readDirFileIface);

            // PathError struct
            var pathErrStruct = new StructTypeSymbol("PathError", new[]
            {
                new FieldSymbol("Op", s, 0),
                new FieldSymbol("Path", s, 1),
                new FieldSymbol("Err", err, 2),
            });
            pkg.AddExport(pathErrStruct);

            // WalkDirFunc type
            var walkDirFunc = new FunctionTypeSymbol(
                new TypeSymbol[] { s, dirEntryIface, err },
                new TypeSymbol[] { err });
            var walkDirFuncType = new TypeSymbol("WalkDirFunc", TypeKind.Function, walkDirFunc);
            pkg.AddExport(walkDirFuncType);

            // WalkDir(fsys FS, root string, fn WalkDirFunc) error
            pkg.AddExport(new FunctionSymbol("WalkDir",
                new[] { P("fsys", fsIface, 0), P("root", s, 1), P("fn", walkDirFunc, 2) },
                new[] { err }, packageName: "fs"));

            // Sub(fsys FS, dir string) (FS, error)
            pkg.AddExport(new FunctionSymbol("Sub",
                new[] { P("fsys", fsIface, 0), P("dir", s, 1) },
                new TypeSymbol[] { fsIface, err }, packageName: "fs"));

            // ReadFile(fsys FS, name string) ([]byte, error)
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            pkg.AddExport(new FunctionSymbol("ReadFile",
                new[] { P("fsys", fsIface, 0), P("name", s, 1) },
                new TypeSymbol[] { byteSlice, err }, packageName: "fs"));

            // ValidPath(name string) bool
            pkg.AddExport(new FunctionSymbol("ValidPath",
                new[] { P("name", s, 0) },
                new[] { BuiltinTypes.Bool }, packageName: "fs"));

            // fs.SkipDir — sentinel error for WalkDir
            pkg.AddExport(new PackageVarSymbol("SkipDir", err));

            // fs.SkipAll (Go 1.20)
            pkg.AddExport(new PackageVarSymbol("SkipAll", err));

            // fs.ErrNotExist, fs.ErrExist, fs.ErrPermission, fs.ErrClosed, fs.ErrInvalid
            pkg.AddExport(new PackageVarSymbol("ErrNotExist", err));
            pkg.AddExport(new PackageVarSymbol("ErrExist", err));
            pkg.AddExport(new PackageVarSymbol("ErrPermission", err));
            pkg.AddExport(new PackageVarSymbol("ErrClosed", err));
            pkg.AddExport(new PackageVarSymbol("ErrInvalid", err));

            // FileMode constants
            pkg.AddExport(new ConstantSymbol("ModeDir", fileMode, (long)0x80000000));
            pkg.AddExport(new ConstantSymbol("ModeAppend", fileMode, (long)0x40000000));
            pkg.AddExport(new ConstantSymbol("ModeExclusive", fileMode, (long)0x20000000));
            pkg.AddExport(new ConstantSymbol("ModeTemporary", fileMode, (long)0x10000000));
            pkg.AddExport(new ConstantSymbol("ModeSymlink", fileMode, (long)0x08000000));
            pkg.AddExport(new ConstantSymbol("ModeDevice", fileMode, (long)0x04000000));
            pkg.AddExport(new ConstantSymbol("ModeNamedPipe", fileMode, (long)0x02000000));
            pkg.AddExport(new ConstantSymbol("ModeSocket", fileMode, (long)0x01000000));
            pkg.AddExport(new ConstantSymbol("ModeSetuid", fileMode, (long)0x00800000));
            pkg.AddExport(new ConstantSymbol("ModeSetgid", fileMode, (long)0x00400000));
            pkg.AddExport(new ConstantSymbol("ModeCharDevice", fileMode, (long)0x00200000));
            pkg.AddExport(new ConstantSymbol("ModeSticky", fileMode, (long)0x00100000));
            pkg.AddExport(new ConstantSymbol("ModeIrregular", fileMode, (long)0x00080000));
            pkg.AddExport(new ConstantSymbol("ModeType", fileMode, unchecked((long)0xFF000000)));
            pkg.AddExport(new ConstantSymbol("ModePerm", fileMode, (long)0x1FF));

            // fs.FileInfoToDirEntry(info FileInfo) DirEntry
            pkg.AddExport(new FunctionSymbol("FileInfoToDirEntry",
                new[] { P("info", fileInfoIface, 0) },
                new[] { (TypeSymbol)dirEntryIface }, packageName: "fs"));

            // fs.Stat(fsys FS, name string) (FileInfo, error)
            pkg.AddExport(new FunctionSymbol("Stat",
                new[] { P("fsys", fsIface, 0), P("name", s, 1) },
                new TypeSymbol[] { fileInfoIface, err }, packageName: "fs"));

            // fs.ReadDir(fsys FS, name string) ([]DirEntry, error)
            var dirEntrySlice = new SliceTypeSymbol(dirEntryIface);
            pkg.AddExport(new FunctionSymbol("ReadDir",
                new[] { P("fsys", fsIface, 0), P("name", s, 1) },
                new TypeSymbol[] { dirEntrySlice, err }, packageName: "fs"));

            // fs.Glob(fsys FS, pattern string) ([]string, error)
            var sliceString = new SliceTypeSymbol(s);
            pkg.AddExport(new FunctionSymbol("Glob",
                new[] { P("fsys", fsIface, 0), P("pattern", s, 1) },
                new TypeSymbol[] { sliceString, err }, packageName: "fs"));

            return pkg;
        }

        private static PackageSymbol CreateCmpPackage()
        {
            var pkg = new PackageSymbol("cmp", "cmp");

            var i = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;
            var iface = BuiltinTypes.EmptyInterface;

            // cmp.Ordered is a constraint (interface), represent as interface
            var orderedType = new InterfaceTypeSymbol("Ordered", new List<MethodSymbol>());
            pkg.AddExport(orderedType);

            // cmp.Compare[T Ordered](x, y T) int — use interface{} for T
            pkg.AddExport(new FunctionSymbol("Compare",
                new[] { P("x", iface, 0), P("y", iface, 1) },
                new[] { i }, packageName: "cmp"));

            // cmp.Less[T Ordered](x, y T) bool
            pkg.AddExport(new FunctionSymbol("Less",
                new[] { P("x", iface, 0), P("y", iface, 1) },
                new[] { b }, packageName: "cmp"));

            // cmp.Or[T comparable](vals ...T) T
            pkg.AddExport(new FunctionSymbol("Or",
                new[] { P("vals", iface, 0) },
                new[] { iface }, packageName: "cmp", isVariadic: true));

            return pkg;
        }

        private static PackageSymbol CreateSlicesPackage()
        {
            var pkg = new PackageSymbol("slices", "slices");

            var i = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;
            var iface = BuiltinTypes.EmptyInterface;
            var sliceIface = new SliceTypeSymbol(iface);

            // slices.Sort[S ~[]E, E cmp.Ordered](x S)
            pkg.AddExport(new FunctionSymbol("Sort",
                new[] { P("x", sliceIface, 0) },
                Array.Empty<TypeSymbol>(), packageName: "slices"));

            // slices.SortFunc[S ~[]E, E any](x S, cmp func(a, b E) int)
            pkg.AddExport(new FunctionSymbol("SortFunc",
                new[] { P("x", sliceIface, 0), P("cmp", new FunctionTypeSymbol(
                    new[] { iface, iface }, new[] { i }), 1) },
                Array.Empty<TypeSymbol>(), packageName: "slices"));

            // slices.SortStableFunc
            pkg.AddExport(new FunctionSymbol("SortStableFunc",
                new[] { P("x", sliceIface, 0), P("cmp", new FunctionTypeSymbol(
                    new[] { iface, iface }, new[] { i }), 1) },
                Array.Empty<TypeSymbol>(), packageName: "slices"));

            // slices.Contains[S ~[]E, E comparable](s S, v E) bool
            pkg.AddExport(new FunctionSymbol("Contains",
                new[] { P("s", sliceIface, 0), P("v", iface, 1) },
                new[] { b }, packageName: "slices"));

            // slices.ContainsFunc
            pkg.AddExport(new FunctionSymbol("ContainsFunc",
                new[] { P("s", sliceIface, 0), P("f", new FunctionTypeSymbol(
                    new[] { iface }, new[] { b }), 1) },
                new[] { b }, packageName: "slices"));

            // slices.Index
            pkg.AddExport(new FunctionSymbol("Index",
                new[] { P("s", sliceIface, 0), P("v", iface, 1) },
                new[] { i }, packageName: "slices"));

            // slices.IndexFunc
            pkg.AddExport(new FunctionSymbol("IndexFunc",
                new[] { P("s", sliceIface, 0), P("f", new FunctionTypeSymbol(
                    new[] { iface }, new[] { b }), 1) },
                new[] { i }, packageName: "slices"));

            // slices.Compact[S ~[]E, E comparable](s S) S
            pkg.AddExport(new FunctionSymbol("Compact",
                new[] { P("s", sliceIface, 0) },
                new[] { sliceIface }, packageName: "slices"));

            // slices.CompactFunc
            pkg.AddExport(new FunctionSymbol("CompactFunc",
                new[] { P("s", sliceIface, 0), P("eq", new FunctionTypeSymbol(
                    new[] { iface, iface }, new[] { b }), 1) },
                new[] { sliceIface }, packageName: "slices"));

            // slices.Clone[S ~[]E, E any](s S) S
            pkg.AddExport(new FunctionSymbol("Clone",
                new[] { P("s", sliceIface, 0) },
                new[] { sliceIface }, packageName: "slices"));

            // slices.Reverse[S ~[]E, E any](s S)
            pkg.AddExport(new FunctionSymbol("Reverse",
                new[] { P("s", sliceIface, 0) },
                Array.Empty<TypeSymbol>(), packageName: "slices"));

            // slices.Equal[S ~[]E, E comparable](s1, s2 S) bool
            pkg.AddExport(new FunctionSymbol("Equal",
                new[] { P("s1", sliceIface, 0), P("s2", sliceIface, 1) },
                new[] { b }, packageName: "slices"));

            // slices.Delete[S ~[]E, E any](s S, i, j int) S
            pkg.AddExport(new FunctionSymbol("Delete",
                new[] { P("s", sliceIface, 0), P("i", i, 1), P("j", i, 2) },
                new[] { sliceIface }, packageName: "slices"));

            // slices.Insert[S ~[]E, E any](s S, i int, v ...E) S
            pkg.AddExport(new FunctionSymbol("Insert",
                new[] { P("s", sliceIface, 0), P("i", i, 1), P("v", iface, 2) },
                new[] { sliceIface }, packageName: "slices", isVariadic: true));

            // slices.Replace[S ~[]E, E any](s S, i, j int, v ...E) S
            pkg.AddExport(new FunctionSymbol("Replace",
                new[] { P("s", sliceIface, 0), P("i", i, 1), P("j", i, 2), P("v", iface, 3) },
                new[] { sliceIface }, packageName: "slices", isVariadic: true));

            // slices.Grow[S ~[]E, E any](s S, n int) S
            pkg.AddExport(new FunctionSymbol("Grow",
                new[] { P("s", sliceIface, 0), P("n", i, 1) },
                new[] { sliceIface }, packageName: "slices"));

            // slices.Clip[S ~[]E, E any](s S) S
            pkg.AddExport(new FunctionSymbol("Clip",
                new[] { P("s", sliceIface, 0) },
                new[] { sliceIface }, packageName: "slices"));

            // slices.BinarySearch[S ~[]E, E cmp.Ordered](x S, target E) (int, bool)
            pkg.AddExport(new FunctionSymbol("BinarySearch",
                new[] { P("x", sliceIface, 0), P("target", iface, 1) },
                new TypeSymbol[] { i, b }, packageName: "slices"));

            // slices.BinarySearchFunc
            pkg.AddExport(new FunctionSymbol("BinarySearchFunc",
                new[] { P("x", sliceIface, 0), P("target", iface, 1),
                    P("cmp", new FunctionTypeSymbol(new[] { iface, iface }, new[] { i }), 2) },
                new TypeSymbol[] { i, b }, packageName: "slices"));

            // slices.IsSorted[S ~[]E, E cmp.Ordered](x S) bool
            pkg.AddExport(new FunctionSymbol("IsSorted",
                new[] { P("x", sliceIface, 0) },
                new[] { b }, packageName: "slices"));

            // slices.IsSortedFunc
            pkg.AddExport(new FunctionSymbol("IsSortedFunc",
                new[] { P("x", sliceIface, 0), P("cmp", new FunctionTypeSymbol(
                    new[] { iface, iface }, new[] { i }), 1) },
                new[] { b }, packageName: "slices"));

            // slices.Min, Max[S ~[]E, E cmp.Ordered](x S) E
            pkg.AddExport(new FunctionSymbol("Min",
                new[] { P("x", sliceIface, 0) },
                new[] { iface }, packageName: "slices"));
            pkg.AddExport(new FunctionSymbol("Max",
                new[] { P("x", sliceIface, 0) },
                new[] { iface }, packageName: "slices"));
            pkg.AddExport(new FunctionSymbol("MinFunc",
                new[] { P("x", sliceIface, 0), P("cmp", new FunctionTypeSymbol(
                    new[] { iface, iface }, new[] { i }), 1) },
                new[] { iface }, packageName: "slices"));
            pkg.AddExport(new FunctionSymbol("MaxFunc",
                new[] { P("x", sliceIface, 0), P("cmp", new FunctionTypeSymbol(
                    new[] { iface, iface }, new[] { i }), 1) },
                new[] { iface }, packageName: "slices"));

            return pkg;
        }
    }
}
