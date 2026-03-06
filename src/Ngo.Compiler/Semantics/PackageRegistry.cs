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
            ["sync"] = CreateSyncPackage,
            ["os"] = CreateOsPackage,
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
            ["bytes"] = CreateBytesPackage,
            ["path"] = CreatePathPackage,
            ["dotnet"] = CreateDotnetPackage,
            ["context"] = CreateContextPackage,
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

            // Find all .go files in the directory (excluding _test.go)
            var goFiles = Directory.GetFiles(pkgDir, "*.go");
            if (goFiles.Length == 0)
                return null;

            // Parse all files
            var trees = new List<SyntaxTree>();
            foreach (var file in goFiles)
            {
                if (file.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                    continue;
                var source = File.ReadAllText(file);
                trees.Add(SyntaxTree.Parse(source));
            }

            if (trees.Count == 0)
                return null;

            // Analyze the package
            var result = SemanticAnalyzer.Analyze(trees);
            if (result.HasErrors)
            {
                // For external modules, report errors but still try to extract exports
                // Many real Go packages use features we don't support yet
                // For now, return null — the caller will report a meaningful error
                return null;
            }

            // Cache the result for code generation later
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
            // For now, simplified: variadic args → accept any number of any type
            // We model these as functions with no required params; call validation is relaxed for variadic.
            pkg.AddExport(CreateVariadicPrintFunc("Println"));
            pkg.AddExport(CreateVariadicPrintFunc("Print"));
            pkg.AddExport(CreateFormatFunc("Printf"));
            pkg.AddExport(CreateFormatFunc("Sprintf", BuiltinTypes.String));
            pkg.AddExport(CreateFormatFunc("Errorf", BuiltinTypes.EmptyInterface));
            pkg.AddExport(CreateVariadicPrintFunc("Sprint", BuiltinTypes.String));
            pkg.AddExport(CreateVariadicPrintFunc("Sprintln", BuiltinTypes.String));

            // Fprintf(w io.Writer, format string, a ...interface{}) (n int, err error)
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            var i64 = BuiltinTypes.Int;
            var s = BuiltinTypes.String;
            pkg.AddExport(new FunctionSymbol("Fprintf",
                new[] { new ParameterSymbol("w", iface, 0),
                        new ParameterSymbol("format", s, 1) },
                new TypeSymbol[] { i64, s }, isVariadic: true, packageName: "fmt"));

            // Fprintln(w io.Writer, a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Fprintln",
                new[] { new ParameterSymbol("w", iface, 0) },
                new TypeSymbol[] { i64, s }, isVariadic: true, packageName: "fmt"));

            // Fprint(w io.Writer, a ...interface{}) (n int, err error)
            pkg.AddExport(new FunctionSymbol("Fprint",
                new[] { new ParameterSymbol("w", iface, 0) },
                new TypeSymbol[] { i64, s }, isVariadic: true, packageName: "fmt"));

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
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.EmptyInterface }));

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
                new TypeSymbol[] { BuiltinTypes.Int64, BuiltinTypes.EmptyInterface }));

            // ParseFloat(s string, bitSize int) (float64, error)
            pkg.AddExport(new FunctionSymbol("ParseFloat",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0),
                        new ParameterSymbol("bitSize", BuiltinTypes.Int, 1) },
                new TypeSymbol[] { BuiltinTypes.Float64, BuiltinTypes.EmptyInterface }));

            // FormatFloat(f float64, fmt byte, prec int, bitSize int) string
            pkg.AddExport(new FunctionSymbol("FormatFloat",
                new[] { new ParameterSymbol("f", BuiltinTypes.Float64, 0),
                        new ParameterSymbol("fmt", BuiltinTypes.Uint8, 1),
                        new ParameterSymbol("prec", BuiltinTypes.Int, 2),
                        new ParameterSymbol("bitSize", BuiltinTypes.Int, 3) },
                new[] { BuiltinTypes.String }));

            // ParseBool(str string) bool — simplified (no error return)
            pkg.AddExport(new FunctionSymbol("ParseBool",
                new[] { new ParameterSymbol("str", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.Bool }));

            // ParseUint(s string, base int, bitSize int) (uint64, error)
            pkg.AddExport(new FunctionSymbol("ParseUint",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0),
                        new ParameterSymbol("base", BuiltinTypes.Int, 1),
                        new ParameterSymbol("bitSize", BuiltinTypes.Int, 2) },
                new TypeSymbol[] { BuiltinTypes.Int, BuiltinTypes.EmptyInterface }));

            // FormatUint(i uint64, base int) string
            pkg.AddExport(new FunctionSymbol("FormatUint",
                new[] { new ParameterSymbol("i", BuiltinTypes.Int, 0),
                        new ParameterSymbol("base", BuiltinTypes.Int, 1) },
                new[] { BuiltinTypes.String }));

            // Quote(s string) string
            pkg.AddExport(new FunctionSymbol("Quote",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.String }));

            // Unquote(s string) (string, error)
            pkg.AddExport(new FunctionSymbol("Unquote",
                new[] { new ParameterSymbol("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { BuiltinTypes.String, BuiltinTypes.EmptyInterface }));

            return pkg;
        }

        private static PackageSymbol CreateStringsPackage()
        {
            var pkg = new PackageSymbol("strings", "strings");

            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;

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

            // strings.IndexByte(s string, c byte) int
            pkg.AddExport(new FunctionSymbol("IndexByte",
                new[] { P("s", s, 0), P("c", BuiltinTypes.Uint8, 1) }, new[] { i }));

            // strings.IndexRune(s string, r rune) int
            pkg.AddExport(new FunctionSymbol("IndexRune",
                new[] { P("s", s, 0), P("r", BuiltinTypes.Rune, 1) }, new[] { i }));

            // strings.IndexAny(s, chars string) int
            pkg.AddExport(new FunctionSymbol("IndexAny",
                new[] { P("s", s, 0), P("chars", s, 1) }, new[] { i }));

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
            pkg.AddExport(builderType);

            return pkg;
        }

        private static PackageSymbol CreateErrorsPackage()
        {
            var pkg = new PackageSymbol("errors", "errors");

            // errors.New(text string) error
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("text", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.EmptyInterface }));

            // errors.Unwrap(err error) error
            pkg.AddExport(new FunctionSymbol("Unwrap",
                new[] { P("err", BuiltinTypes.EmptyInterface, 0) },
                new[] { BuiltinTypes.EmptyInterface }));

            // errors.Is(err, target error) bool
            pkg.AddExport(new FunctionSymbol("Is",
                new[] { P("err", BuiltinTypes.EmptyInterface, 0), P("target", BuiltinTypes.EmptyInterface, 1) },
                new[] { BuiltinTypes.Bool }));

            // errors.As(err error, target interface{}) bool
            pkg.AddExport(new FunctionSymbol("As",
                new[] { P("err", BuiltinTypes.EmptyInterface, 0), P("target", BuiltinTypes.EmptyInterface, 1) },
                new[] { BuiltinTypes.Bool }));

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

            // math constants as package variables
            pkg.AddExport(new PackageVarSymbol("Pi", f, typeof(GoMath), "Pi"));
            pkg.AddExport(new PackageVarSymbol("E", f, typeof(GoMath), "E"));
            pkg.AddExport(new PackageVarSymbol("MaxFloat64", f, typeof(GoMath), "MaxFloat64"));
            pkg.AddExport(new PackageVarSymbol("SmallestNonzeroFloat64", f, typeof(GoMath), "SmallestNonzeroFloat64"));
            pkg.AddExport(new PackageVarSymbol("MaxInt", i, typeof(GoMath), "MaxInt"));
            pkg.AddExport(new PackageVarSymbol("MinInt", i, typeof(GoMath), "MinInt"));
            pkg.AddExport(new PackageVarSymbol("MaxInt8", i, typeof(GoMath), "MaxInt8"));
            pkg.AddExport(new PackageVarSymbol("MinInt8", i, typeof(GoMath), "MinInt8"));
            pkg.AddExport(new PackageVarSymbol("MaxInt16", i, typeof(GoMath), "MaxInt16"));
            pkg.AddExport(new PackageVarSymbol("MinInt16", i, typeof(GoMath), "MinInt16"));
            pkg.AddExport(new PackageVarSymbol("MaxInt32", i, typeof(GoMath), "MaxInt32"));
            pkg.AddExport(new PackageVarSymbol("MinInt32", i, typeof(GoMath), "MinInt32"));
            pkg.AddExport(new PackageVarSymbol("MaxInt64", i, typeof(GoMath), "MaxInt64"));
            pkg.AddExport(new PackageVarSymbol("MinInt64", i, typeof(GoMath), "MinInt64"));
            pkg.AddExport(new PackageVarSymbol("MaxFloat32", f, typeof(GoMath), "MaxFloat32"));
            pkg.AddExport(new PackageVarSymbol("Phi", f, typeof(GoMath), "Phi"));
            pkg.AddExport(new PackageVarSymbol("Sqrt2", f, typeof(GoMath), "Sqrt2"));
            pkg.AddExport(new PackageVarSymbol("SqrtE", f, typeof(GoMath), "SqrtE"));
            pkg.AddExport(new PackageVarSymbol("SqrtPi", f, typeof(GoMath), "SqrtPi"));
            pkg.AddExport(new PackageVarSymbol("SqrtPhi", f, typeof(GoMath), "SqrtPhi"));
            pkg.AddExport(new PackageVarSymbol("Ln2", f, typeof(GoMath), "Ln2"));
            pkg.AddExport(new PackageVarSymbol("Log2E", f, typeof(GoMath), "Log2E"));
            pkg.AddExport(new PackageVarSymbol("Ln10", f, typeof(GoMath), "Ln10"));
            pkg.AddExport(new PackageVarSymbol("Log10E", f, typeof(GoMath), "Log10E"));

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

            return pkg;
        }

        private static PackageSymbol CreateOsPackage()
        {
            var pkg = new PackageSymbol("os", "os");

            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            // File type with methods
            var fileType = new StructTypeSymbol("File", Array.Empty<FieldSymbol>());
            fileType.AddMethod(new MethodSymbol("Close", fileType, false,
                Array.Empty<ParameterSymbol>(), s));
            fileType.AddMethod(new MethodSymbol("Name", fileType, false,
                Array.Empty<ParameterSymbol>(), s));
            fileType.AddMethod(new MethodSymbol("WriteString", fileType, false,
                new[] { P("s", s, 0) },
                new TypeSymbol[] { i, s }));
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

            var err = BuiltinTypes.EmptyInterface;

            // os.Create(name string) (*File, error)
            pkg.AddExport(new FunctionSymbol("Create",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { fileType, err }, packageName: "os"));

            // os.Open(name string) (*File, error)
            pkg.AddExport(new FunctionSymbol("Open",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { fileType, err }, packageName: "os"));

            // os.ReadFile(name string) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("ReadFile",
                new[] { P("name", s, 0) },
                new TypeSymbol[] { byteSlice, err }, packageName: "os"));

            // os.WriteFile(name string, data []byte, perm FileMode) error
            pkg.AddExport(new FunctionSymbol("WriteFile",
                new[] { P("name", s, 0), new ParameterSymbol("data", byteSlice, 1),
                        P("perm", i, 2) },
                new[] { err }, packageName: "os"));

            // os.Remove(name string) error
            pkg.AddExport(new FunctionSymbol("Remove",
                new[] { P("name", s, 0) },
                new[] { err }, packageName: "os"));

            // os.MkdirAll(path string, perm FileMode) error
            pkg.AddExport(new FunctionSymbol("MkdirAll",
                new[] { P("path", s, 0), P("perm", i, 1) },
                new[] { err }, packageName: "os"));

            // os.Getwd() (string, error)
            pkg.AddExport(new FunctionSymbol("Getwd",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { s, err }, packageName: "os"));

            // os.Args []string
            pkg.AddExport(new PackageVarSymbol("Args",
                new SliceTypeSymbol(s),
                typeof(GoOs), "Args"));

            // os.Rename(oldpath, newpath string) error
            pkg.AddExport(new FunctionSymbol("Rename",
                new[] { P("oldpath", s, 0), P("newpath", s, 1) },
                new[] { err }, packageName: "os"));

            // os.Stat(name string) (FileInfo, error)
            var fileInfoType = new StructTypeSymbol("FileInfo", Array.Empty<FieldSymbol>());
            fileInfoType.AddMethod(new MethodSymbol("Name", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), s));
            fileInfoType.AddMethod(new MethodSymbol("Size", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), i));
            fileInfoType.AddMethod(new MethodSymbol("IsDir", fileInfoType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Bool));
            pkg.AddExport(fileInfoType);
            pkg.AddExport(new FunctionSymbol("Stat",
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
            pkg.AddExport(new PackageVarSymbol("Stdin", fileType, typeof(GoOs), "Stdin"));
            pkg.AddExport(new PackageVarSymbol("Stdout", fileType, typeof(GoOs), "Stdout"));
            pkg.AddExport(new PackageVarSymbol("Stderr", fileType, typeof(GoOs), "Stderr"));

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
                new[] { P("name", s, 0), P("mode", i, 1) },
                new[] { err }, packageName: "os"));

            return pkg;
        }

        private static PackageSymbol CreateTimePackage()
        {
            var pkg = new PackageSymbol("time", "time");

            var i = BuiltinTypes.Int;
            var s = BuiltinTypes.String;
            var b = BuiltinTypes.Bool;

            // time.Time type
            var timeType = new StructTypeSymbol("Time", Array.Empty<FieldSymbol>());
            timeType.AddMethod(new MethodSymbol("Unix", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("UnixMilli", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("UnixNano", timeType, false,
                Array.Empty<ParameterSymbol>(), i));
            timeType.AddMethod(new MethodSymbol("String", timeType, false,
                Array.Empty<ParameterSymbol>(), s));
            timeType.AddMethod(new MethodSymbol("Format", timeType, false,
                new[] { P("layout", s, 0) }, s));
            timeType.AddMethod(new MethodSymbol("Sub", timeType, false,
                new[] { P("u", timeType, 0) }, i));
            timeType.AddMethod(new MethodSymbol("Add", timeType, false,
                new[] { P("d", i, 0) }, new[] { timeType }));
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
                Array.Empty<ParameterSymbol>(), i));
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
                Array.Empty<ParameterSymbol>(), i));
            pkg.AddExport(timeType);

            // time.Sleep(d Duration) — Duration is int64 nanoseconds
            pkg.AddExport(new FunctionSymbol("Sleep",
                new[] { P("d", i, 0) },
                Array.Empty<TypeSymbol>(), packageName: "time"));

            // time.Now() Time
            pkg.AddExport(new FunctionSymbol("Now",
                Array.Empty<ParameterSymbol>(),
                new TypeSymbol[] { timeType }, packageName: "time"));

            // time.Since(t Time) Duration
            pkg.AddExport(new FunctionSymbol("Since",
                new[] { P("t", timeType, 0) },
                new[] { i }, packageName: "time"));

            // time.Parse(layout, value string) (Time, error)
            pkg.AddExport(new FunctionSymbol("Parse",
                new[] { P("layout", s, 0), P("value", s, 1) },
                new TypeSymbol[] { timeType, BuiltinTypes.EmptyInterface },
                packageName: "time"));

            // time.Unix(sec, nsec int64) Time
            pkg.AddExport(new FunctionSymbol("Unix",
                new[] { P("sec", i, 0), P("nsec", i, 1) },
                new TypeSymbol[] { timeType }, packageName: "time"));

            // time.Date(year, month, day, hour, min, sec, nsec int, loc *Location) Time
            pkg.AddExport(new FunctionSymbol("Date",
                new[] { P("year", i, 0), P("month", i, 1), P("day", i, 2),
                        P("hour", i, 3), P("min", i, 4), P("sec", i, 5),
                        P("nsec", i, 6), P("loc", BuiltinTypes.EmptyInterface, 7) },
                new TypeSymbol[] { timeType }, packageName: "time"));

            // Duration constants
            pkg.AddExport(new ConstantSymbol("Nanosecond", i, (long)1));
            pkg.AddExport(new ConstantSymbol("Microsecond", i, (long)1000));
            pkg.AddExport(new ConstantSymbol("Millisecond", i, (long)1_000_000));
            pkg.AddExport(new ConstantSymbol("Second", i, (long)1_000_000_000));
            pkg.AddExport(new ConstantSymbol("Minute", i, (long)60_000_000_000));
            pkg.AddExport(new ConstantSymbol("Hour", i, (long)3_600_000_000_000));

            // Layout constants
            pkg.AddExport(new ConstantSymbol("RFC3339", s, "2006-01-02T15:04:05Z07:00"));
            pkg.AddExport(new ConstantSymbol("RFC822", s, "02 Jan 06 15:04 MST"));
            pkg.AddExport(new ConstantSymbol("Kitchen", s, "3:04PM"));
            pkg.AddExport(new ConstantSymbol("DateTime", s, "2006-01-02 15:04:05"));
            pkg.AddExport(new ConstantSymbol("DateOnly", s, "2006-01-02"));
            pkg.AddExport(new ConstantSymbol("TimeOnly", s, "15:04:05"));

            // time.UTC (a nil placeholder for Location)
            pkg.AddExport(new PackageVarSymbol("UTC", BuiltinTypes.EmptyInterface,
                typeof(Ngo.Runtime.GoTime), "UTC"));

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

            return pkg;
        }

        private static PackageSymbol CreateIoPackage()
        {
            var pkg = new PackageSymbol("io", "io");

            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);

            // Use empty interface type for Reader/Writer params (mapped to object)
            var iface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // io.Copy(dst Writer, src Reader) (int64, error)
            pkg.AddExport(new FunctionSymbol("Copy",
                new[] { new ParameterSymbol("dst", iface, 0),
                        new ParameterSymbol("src", iface, 1) },
                new TypeSymbol[] { i64, s }, packageName: "io"));

            // io.ReadAll(r Reader) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("ReadAll",
                new[] { new ParameterSymbol("r", iface, 0) },
                new TypeSymbol[] { byteSlice, s }, packageName: "io"));

            // io.WriteString(w Writer, s string) (int, error)
            pkg.AddExport(new FunctionSymbol("WriteString",
                new[] { new ParameterSymbol("w", iface, 0),
                        new ParameterSymbol("s", s, 1) },
                new TypeSymbol[] { i64, s }, packageName: "io"));

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
            pkg.AddExport(new PackageVarSymbol("EOF", s, typeof(GoIo), "EOF"));

            // io.Discard — Writer that discards all data
            pkg.AddExport(new PackageVarSymbol("Discard", iface,
                typeof(DiscardWriter), "Instance"));

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

            // Reader type with ReadString() method
            var readerType = new StructTypeSymbol("Reader", Array.Empty<FieldSymbol>());
            readerType.AddMethod(new MethodSymbol("ReadString", readerType, false,
                new[] { new ParameterSymbol("delim", BuiltinTypes.Uint8, 0) },
                new TypeSymbol[] { s, s }));
            pkg.AddExport(readerType);

            // Writer type with Flush() method
            var writerType = new StructTypeSymbol("Writer", Array.Empty<FieldSymbol>());
            writerType.AddMethod(new MethodSymbol("Flush", writerType, false,
                Array.Empty<ParameterSymbol>(), s));
            pkg.AddExport(writerType);

            // bufio.NewScanner(r Reader) *Scanner
            pkg.AddExport(new FunctionSymbol("NewScanner",
                new[] { new ParameterSymbol("r", iface, 0) },
                new TypeSymbol[] { scannerType }, packageName: "bufio"));

            // bufio.NewReader(r Reader) *Reader
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { new ParameterSymbol("r", iface, 0) },
                new TypeSymbol[] { readerType }, packageName: "bufio"));

            // bufio.NewWriter(w Writer) *Writer
            pkg.AddExport(new FunctionSymbol("NewWriter",
                new[] { new ParameterSymbol("w", iface, 0) },
                new TypeSymbol[] { writerType }, packageName: "bufio"));

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
                new TypeSymbol[] { s, s }, packageName: "filepath"));

            // filepath.Rel(basepath, targpath string) (string, error)
            pkg.AddExport(new FunctionSymbol("Rel",
                new[] { P("basepath", s, 0), P("targpath", s, 1) },
                new TypeSymbol[] { s, s }, packageName: "filepath"));

            // filepath.Match(pattern, name string) (bool, error)
            pkg.AddExport(new FunctionSymbol("Match",
                new[] { P("pattern", s, 0), P("name", s, 1) },
                new TypeSymbol[] { b, s }, packageName: "filepath"));

            // filepath.Glob(pattern string) ([]string, error)
            var sliceString = new SliceTypeSymbol(s);
            pkg.AddExport(new FunctionSymbol("Glob",
                new[] { P("pattern", s, 0) },
                new TypeSymbol[] { sliceString, s }, packageName: "filepath"));

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
            regexpType.AddMethod(new MethodSymbol("Split", regexpType, false,
                new[] { P("s", s, 0), P("n", i, 1) },
                new TypeSymbol[] { sliceString }));
            regexpType.AddMethod(new MethodSymbol("FindStringSubmatch", regexpType, false,
                new[] { P("s", s, 0) },
                new TypeSymbol[] { sliceString }));
            pkg.AddExport(regexpType);

            // regexp.Compile(expr string) (*Regexp, error)
            pkg.AddExport(new FunctionSymbol("Compile",
                new[] { P("expr", s, 0) },
                new TypeSymbol[] { regexpType, s }, packageName: "regexp"));

            // regexp.MustCompile(expr string) *Regexp
            pkg.AddExport(new FunctionSymbol("MustCompile",
                new[] { P("expr", s, 0) },
                new TypeSymbol[] { regexpType }, packageName: "regexp"));

            // regexp.MatchString(pattern, s string) (bool, error)
            pkg.AddExport(new FunctionSymbol("MatchString",
                new[] { P("pattern", s, 0), P("s", s, 1) },
                new TypeSymbol[] { b, s }, packageName: "regexp"));

            return pkg;
        }

        private static PackageSymbol CreateUnicodePackage()
        {
            var pkg = new PackageSymbol("unicode", "unicode");

            var i = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;

            pkg.AddExport(new FunctionSymbol("IsLetter",
                new[] { P("r", i, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsDigit",
                new[] { P("r", i, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsSpace",
                new[] { P("r", i, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsUpper",
                new[] { P("r", i, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsLower",
                new[] { P("r", i, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsPunct",
                new[] { P("r", i, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("IsControl",
                new[] { P("r", i, 0) }, new[] { b }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("ToUpper",
                new[] { P("r", i, 0) }, new[] { i }, packageName: "unicode"));
            pkg.AddExport(new FunctionSymbol("ToLower",
                new[] { P("r", i, 0) }, new[] { i }, packageName: "unicode"));

            return pkg;
        }

        private static PackageSymbol CreateUtf8Package()
        {
            var pkg = new PackageSymbol("utf8", "unicode/utf8");

            var s = BuiltinTypes.String;
            var i = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;

            // utf8.RuneCountInString(s string) int
            pkg.AddExport(new FunctionSymbol("RuneCountInString",
                new[] { P("s", s, 0) }, new[] { i }, packageName: "utf8"));

            // utf8.ValidString(s string) bool
            pkg.AddExport(new FunctionSymbol("ValidString",
                new[] { P("s", s, 0) }, new[] { b }, packageName: "utf8"));

            // utf8.DecodeRuneInString(s string) (rune, int)
            pkg.AddExport(new FunctionSymbol("DecodeRuneInString",
                new[] { P("s", s, 0) },
                new TypeSymbol[] { i, i }, packageName: "utf8"));

            // utf8.RuneLen(r rune) int
            pkg.AddExport(new FunctionSymbol("RuneLen",
                new[] { P("r", i, 0) }, new[] { i }, packageName: "utf8"));

            // Constants
            pkg.AddExport(new ConstantSymbol("RuneError", i, (long)0xFFFD));
            pkg.AddExport(new ConstantSymbol("MaxRune", i, (long)0x10FFFF));
            pkg.AddExport(new ConstantSymbol("UTFMax", i, (long)4));

            return pkg;
        }

        private static ParameterSymbol P(string name, TypeSymbol type, int ordinal) =>
            new ParameterSymbol(name, type, ordinal);

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

            // bytes.Buffer type
            var bufferType = new StructTypeSymbol("Buffer", Array.Empty<FieldSymbol>());
            bufferType.AddMethod(new MethodSymbol("Write", bufferType, false,
                new[] { P("p", byteSlice, 0) },
                new TypeSymbol[] { i, BuiltinTypes.EmptyInterface }));
            bufferType.AddMethod(new MethodSymbol("WriteString", bufferType, false,
                new[] { P("s", BuiltinTypes.String, 0) },
                new TypeSymbol[] { i, BuiltinTypes.EmptyInterface }));
            bufferType.AddMethod(new MethodSymbol("WriteByte", bufferType, false,
                new[] { P("c", BuiltinTypes.Byte, 0) },
                new TypeSymbol[] { BuiltinTypes.EmptyInterface }));
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
                new TypeSymbol[] { i, BuiltinTypes.EmptyInterface }));
            pkg.AddExport(bufferType);

            // bytes.NewBuffer(buf []byte) *Buffer
            pkg.AddExport(new FunctionSymbol("NewBuffer",
                new[] { P("buf", byteSlice, 0) },
                new TypeSymbol[] { bufferType }, packageName: "bytes"));

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
                new[] { P("a", s, 0), P("b", s, 1) }, new[] { s }, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("Clean",
                new[] { P("path", s, 0) }, new[] { s }, packageName: "path"));
            pkg.AddExport(new FunctionSymbol("IsAbs",
                new[] { P("path", s, 0) }, new[] { b }, packageName: "path"));

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

            // context.Context type
            var ctxType = new StructTypeSymbol("Context", Array.Empty<FieldSymbol>());
            ctxType.AddMethod(new MethodSymbol("Value", ctxType, false,
                new[] { P("key", iface, 0) }, iface));
            ctxType.AddMethod(new MethodSymbol("Err", ctxType, false,
                Array.Empty<ParameterSymbol>(), iface));
            ctxType.AddMethod(new MethodSymbol("Done", ctxType, false,
                Array.Empty<ParameterSymbol>(),
                new[] { new ChannelTypeSymbol(iface) }));
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

            // context.WithValue(parent Context, key, val interface{}) Context
            pkg.AddExport(new FunctionSymbol("WithValue",
                new[] { P("parent", ctxType, 0), P("key", iface, 1), P("val", iface, 2) },
                new TypeSymbol[] { ctxType }, packageName: "context"));

            return pkg;
        }

        private static PackageSymbol CreateJsonPackage()
        {
            var pkg = new PackageSymbol("json", "encoding/json");

            var iface = BuiltinTypes.EmptyInterface;
            var byteSlice = new SliceTypeSymbol(BuiltinTypes.Byte);
            var s = BuiltinTypes.String;

            // json.Marshal(v interface{}) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("Marshal",
                new[] { P("v", iface, 0) },
                new TypeSymbol[] { byteSlice, iface }, packageName: "json"));

            // json.MarshalIndent(v interface{}, prefix, indent string) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("MarshalIndent",
                new[] { P("v", iface, 0), P("prefix", s, 1), P("indent", s, 2) },
                new TypeSymbol[] { byteSlice, iface }, packageName: "json"));

            // json.Unmarshal(data []byte, v interface{}) error
            pkg.AddExport(new FunctionSymbol("Unmarshal",
                new[] { P("data", byteSlice, 0), P("v", iface, 1) },
                new TypeSymbol[] { iface }, packageName: "json"));

            // json.Valid(data []byte) bool
            pkg.AddExport(new FunctionSymbol("Valid",
                new[] { P("data", byteSlice, 0) },
                new TypeSymbol[] { BuiltinTypes.Bool }, packageName: "json"));

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
                new TypeSymbol[] { byteSlice, s }, packageName: "ioutil"));

            // ioutil.ReadFile(filename string) ([]byte, error)
            pkg.AddExport(new FunctionSymbol("ReadFile",
                new[] { P("filename", s, 0) },
                new TypeSymbol[] { byteSlice, s }, packageName: "ioutil"));

            // ioutil.WriteFile(filename string, data []byte, perm os.FileMode) error
            pkg.AddExport(new FunctionSymbol("WriteFile",
                new[] { P("filename", s, 0), P("data", byteSlice, 1),
                        P("perm", BuiltinTypes.Int, 2) },
                new TypeSymbol[] { s }, packageName: "ioutil"));

            // ioutil.NopCloser(r Reader) ReadCloser
            pkg.AddExport(new FunctionSymbol("NopCloser",
                new[] { P("r", iface, 0) },
                new[] { iface }, packageName: "ioutil"));

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
                new[] { (TypeSymbol)readerType }, packageName: "csv"));

            // NewWriter(w io.Writer) *Writer
            pkg.AddExport(new FunctionSymbol("NewWriter",
                new[] { new ParameterSymbol("w", emptyIface, 0) },
                new[] { (TypeSymbol)writerType }, packageName: "csv"));

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
                new TypeSymbol[] { i64, emptyIface }, packageName: "crand"));

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
                        new FieldSymbol("Status", s, 1) });

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

            // Status code constants
            pkg.AddExport(new PackageVarSymbol("StatusOK", i64,
                typeof(Ngo.Runtime.GoHttp), "StatusOK"));
            pkg.AddExport(new PackageVarSymbol("StatusCreated", i64,
                typeof(Ngo.Runtime.GoHttp), "StatusCreated"));
            pkg.AddExport(new PackageVarSymbol("StatusBadRequest", i64,
                typeof(Ngo.Runtime.GoHttp), "StatusBadRequest"));
            pkg.AddExport(new PackageVarSymbol("StatusUnauthorized", i64,
                typeof(Ngo.Runtime.GoHttp), "StatusUnauthorized"));
            pkg.AddExport(new PackageVarSymbol("StatusForbidden", i64,
                typeof(Ngo.Runtime.GoHttp), "StatusForbidden"));
            pkg.AddExport(new PackageVarSymbol("StatusNotFound", i64,
                typeof(Ngo.Runtime.GoHttp), "StatusNotFound"));
            pkg.AddExport(new PackageVarSymbol("StatusInternalServerError", i64,
                typeof(Ngo.Runtime.GoHttp), "StatusInternalServerError"));

            return pkg;
        }

        private static PackageSymbol CreateReflectPackage()
        {
            var pkg = new PackageSymbol("reflect", "reflect");

            var s = BuiltinTypes.String;
            var i64 = BuiltinTypes.Int;
            var b = BuiltinTypes.Bool;
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());

            // reflect.Type type (struct with methods)
            var typeType = new StructTypeSymbol("Type", Array.Empty<FieldSymbol>());
            typeType.AddMethod(new MethodSymbol("Name", typeType, false,
                Array.Empty<ParameterSymbol>(), s));
            typeType.AddMethod(new MethodSymbol("Kind", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("String", typeType, false,
                Array.Empty<ParameterSymbol>(), s));
            typeType.AddMethod(new MethodSymbol("NumField", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("Field", typeType, false,
                new[] { P("i", i64, 0) },
                new TypeSymbol[] { new StructTypeSymbol("StructField", Array.Empty<FieldSymbol>()) }));
            typeType.AddMethod(new MethodSymbol("NumMethod", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("Elem", typeType, false,
                Array.Empty<ParameterSymbol>(), typeType));
            typeType.AddMethod(new MethodSymbol("Key", typeType, false,
                Array.Empty<ParameterSymbol>(), typeType));
            typeType.AddMethod(new MethodSymbol("Len", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("Comparable", typeType, false,
                Array.Empty<ParameterSymbol>(), b));
            typeType.AddMethod(new MethodSymbol("Size", typeType, false,
                Array.Empty<ParameterSymbol>(), i64));
            typeType.AddMethod(new MethodSymbol("AssignableTo", typeType, false,
                new[] { P("u", typeType, 0) }, b));
            typeType.AddMethod(new MethodSymbol("Implements", typeType, false,
                new[] { P("u", typeType, 0) }, b));
            pkg.AddExport(typeType);

            // reflect.Value type
            var valueType = new StructTypeSymbol("Value", Array.Empty<FieldSymbol>());
            valueType.AddMethod(new MethodSymbol("Kind", valueType, false,
                Array.Empty<ParameterSymbol>(), i64));
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
                Array.Empty<ParameterSymbol>(), i64));
            valueType.AddMethod(new MethodSymbol("Float", valueType, false,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Float64));
            valueType.AddMethod(new MethodSymbol("Bool", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("String", valueType, false,
                Array.Empty<ParameterSymbol>(), s));
            valueType.AddMethod(new MethodSymbol("Len", valueType, false,
                Array.Empty<ParameterSymbol>(), i64));
            valueType.AddMethod(new MethodSymbol("Index", valueType, false,
                new[] { P("i", i64, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("MapKeys", valueType, false,
                Array.Empty<ParameterSymbol>(),
                new SliceTypeSymbol(valueType)));
            valueType.AddMethod(new MethodSymbol("MapIndex", valueType, false,
                new[] { P("key", valueType, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("NumField", valueType, false,
                Array.Empty<ParameterSymbol>(), i64));
            valueType.AddMethod(new MethodSymbol("Field", valueType, false,
                new[] { P("i", i64, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("FieldByName", valueType, false,
                new[] { P("name", s, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("MethodByName", valueType, false,
                new[] { P("name", s, 0) }, valueType));
            valueType.AddMethod(new MethodSymbol("Elem", valueType, false,
                Array.Empty<ParameterSymbol>(), valueType));
            valueType.AddMethod(new MethodSymbol("CanSet", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("CanInterface", valueType, false,
                Array.Empty<ParameterSymbol>(), b));
            valueType.AddMethod(new MethodSymbol("Set", valueType, false,
                new[] { P("x", valueType, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetInt", valueType, false,
                new[] { P("x", i64, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetFloat", valueType, false,
                new[] { P("x", BuiltinTypes.Float64, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetString", valueType, false,
                new[] { P("x", s, 0) }, BuiltinTypes.Void));
            valueType.AddMethod(new MethodSymbol("SetBool", valueType, false,
                new[] { P("x", b, 0) }, BuiltinTypes.Void));
            pkg.AddExport(valueType);

            // reflect.StructField type
            var structFieldType = new StructTypeSymbol("StructField",
                new[]
                {
                    new FieldSymbol("Name", s, 0),
                    new FieldSymbol("Type", typeType, 1),
                    new FieldSymbol("Tag", s, 2),
                    new FieldSymbol("Index", i64, 3),
                    new FieldSymbol("Anonymous", b, 4),
                });
            pkg.AddExport(structFieldType);

            // Top-level functions
            // TypeOf(v interface{}) Type
            pkg.AddExport(new FunctionSymbol("TypeOf",
                new[] { P("v", emptyIface, 0) },
                new TypeSymbol[] { typeType }, packageName: "reflect"));

            // ValueOf(v interface{}) Value
            pkg.AddExport(new FunctionSymbol("ValueOf",
                new[] { P("v", emptyIface, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));

            // DeepEqual(x, y interface{}) bool
            pkg.AddExport(new FunctionSymbol("DeepEqual",
                new[] { P("x", emptyIface, 0), P("y", emptyIface, 1) },
                new TypeSymbol[] { b }, packageName: "reflect"));

            // Zero(typ Type) Value
            pkg.AddExport(new FunctionSymbol("Zero",
                new[] { P("typ", typeType, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));

            // New(typ Type) Value
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("typ", typeType, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));

            // MakeSlice(typ Type, len, cap int) Value
            pkg.AddExport(new FunctionSymbol("MakeSlice",
                new[] { P("typ", typeType, 0), P("len", i64, 1), P("cap", i64, 2) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));

            // MakeMap(typ Type) Value
            pkg.AddExport(new FunctionSymbol("MakeMap",
                new[] { P("typ", typeType, 0) },
                new TypeSymbol[] { valueType }, packageName: "reflect"));

            // Kind constants
            pkg.AddExport(new PackageVarSymbol("Invalid", i64, typeof(Ngo.Runtime.GoReflectKinds), "Invalid"));
            pkg.AddExport(new PackageVarSymbol("Bool", i64, typeof(Ngo.Runtime.GoReflectKinds), "Bool"));
            pkg.AddExport(new PackageVarSymbol("Int", i64, typeof(Ngo.Runtime.GoReflectKinds), "Int"));
            pkg.AddExport(new PackageVarSymbol("Int8", i64, typeof(Ngo.Runtime.GoReflectKinds), "Int8"));
            pkg.AddExport(new PackageVarSymbol("Int16", i64, typeof(Ngo.Runtime.GoReflectKinds), "Int16"));
            pkg.AddExport(new PackageVarSymbol("Int32", i64, typeof(Ngo.Runtime.GoReflectKinds), "Int32"));
            pkg.AddExport(new PackageVarSymbol("Int64", i64, typeof(Ngo.Runtime.GoReflectKinds), "Int64"));
            pkg.AddExport(new PackageVarSymbol("Uint", i64, typeof(Ngo.Runtime.GoReflectKinds), "Uint"));
            pkg.AddExport(new PackageVarSymbol("Uint8", i64, typeof(Ngo.Runtime.GoReflectKinds), "Uint8"));
            pkg.AddExport(new PackageVarSymbol("Uint16", i64, typeof(Ngo.Runtime.GoReflectKinds), "Uint16"));
            pkg.AddExport(new PackageVarSymbol("Uint32", i64, typeof(Ngo.Runtime.GoReflectKinds), "Uint32"));
            pkg.AddExport(new PackageVarSymbol("Uint64", i64, typeof(Ngo.Runtime.GoReflectKinds), "Uint64"));
            pkg.AddExport(new PackageVarSymbol("Uintptr", i64, typeof(Ngo.Runtime.GoReflectKinds), "Uintptr"));
            pkg.AddExport(new PackageVarSymbol("Float32", i64, typeof(Ngo.Runtime.GoReflectKinds), "Float32"));
            pkg.AddExport(new PackageVarSymbol("Float64", i64, typeof(Ngo.Runtime.GoReflectKinds), "Float64"));
            pkg.AddExport(new PackageVarSymbol("Complex64", i64, typeof(Ngo.Runtime.GoReflectKinds), "Complex64"));
            pkg.AddExport(new PackageVarSymbol("Complex128", i64, typeof(Ngo.Runtime.GoReflectKinds), "Complex128"));
            pkg.AddExport(new PackageVarSymbol("Array", i64, typeof(Ngo.Runtime.GoReflectKinds), "Array"));
            pkg.AddExport(new PackageVarSymbol("Chan", i64, typeof(Ngo.Runtime.GoReflectKinds), "Chan"));
            pkg.AddExport(new PackageVarSymbol("Func", i64, typeof(Ngo.Runtime.GoReflectKinds), "Func"));
            pkg.AddExport(new PackageVarSymbol("Interface", i64, typeof(Ngo.Runtime.GoReflectKinds), "Interface"));
            pkg.AddExport(new PackageVarSymbol("Map", i64, typeof(Ngo.Runtime.GoReflectKinds), "Map"));
            pkg.AddExport(new PackageVarSymbol("Pointer", i64, typeof(Ngo.Runtime.GoReflectKinds), "Pointer"));
            pkg.AddExport(new PackageVarSymbol("Ptr", i64, typeof(Ngo.Runtime.GoReflectKinds), "Ptr"));
            pkg.AddExport(new PackageVarSymbol("Slice", i64, typeof(Ngo.Runtime.GoReflectKinds), "Slice"));
            pkg.AddExport(new PackageVarSymbol("String", i64, typeof(Ngo.Runtime.GoReflectKinds), "String"));
            pkg.AddExport(new PackageVarSymbol("Struct", i64, typeof(Ngo.Runtime.GoReflectKinds), "Struct"));
            pkg.AddExport(new PackageVarSymbol("UnsafePointer", i64, typeof(Ngo.Runtime.GoReflectKinds), "UnsafePointer"));

            return pkg;
        }
    }
}
