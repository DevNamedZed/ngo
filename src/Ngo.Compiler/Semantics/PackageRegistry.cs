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
using Ngo.Compiler.Symbols;
using Ngo.Runtime;

namespace Ngo.Compiler.Semantics
{
    public static class PackageRegistry
    {
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
        };

        public static PackageSymbol? Resolve(string importPath)
        {
            if (_packages.TryGetValue(importPath, out var factory))
            {
                return factory();
            }

            return null;
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
            pkg.AddExport(CreateFormatFunc("Errorf", BuiltinTypes.String));
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

            // strings.NewReader(s string) *strings.Reader (returns io.Reader)
            var emptyIface = new InterfaceTypeSymbol("interface{}", Array.Empty<MethodSymbol>());
            pkg.AddExport(new FunctionSymbol("NewReader",
                new[] { P("s", s, 0) }, new TypeSymbol[] { emptyIface }));

            return pkg;
        }

        private static PackageSymbol CreateErrorsPackage()
        {
            var pkg = new PackageSymbol("errors", "errors");

            // errors.New(text string) error
            pkg.AddExport(new FunctionSymbol("New",
                new[] { P("text", BuiltinTypes.String, 0) },
                new[] { BuiltinTypes.EmptyInterface }));

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
            pkg.AddExport(new FunctionSymbol("Mod",
                new[] { P("x", f, 0), P("y", f, 1) }, new[] { f }));

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

            // os.Stdin, os.Stdout, os.Stderr
            pkg.AddExport(new PackageVarSymbol("Stdin", fileType, typeof(GoOs), "Stdin"));
            pkg.AddExport(new PackageVarSymbol("Stdout", fileType, typeof(GoOs), "Stdout"));
            pkg.AddExport(new PackageVarSymbol("Stderr", fileType, typeof(GoOs), "Stderr"));

            return pkg;
        }

        private static PackageSymbol CreateTimePackage()
        {
            var pkg = new PackageSymbol("time", "time");

            // time.Sleep(d Duration) — Duration is int64 nanoseconds
            pkg.AddExport(new FunctionSymbol("Sleep",
                new[] { new ParameterSymbol("d", BuiltinTypes.Int, 0) },
                Array.Empty<TypeSymbol>()));

            // Duration constants
            pkg.AddExport(new ConstantSymbol("Nanosecond", BuiltinTypes.Int, (long)1));
            pkg.AddExport(new ConstantSymbol("Microsecond", BuiltinTypes.Int, (long)1000));
            pkg.AddExport(new ConstantSymbol("Millisecond", BuiltinTypes.Int, (long)1_000_000));
            pkg.AddExport(new ConstantSymbol("Second", BuiltinTypes.Int, (long)1_000_000_000));

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
    }
}
