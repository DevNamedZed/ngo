// -----------------------------------------------------------------------
// <copyright file="ArchiveRoundTripTests.cs" company="Ziad">
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
using System.IO;
using System.Linq;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Archive;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Emit;

/// <summary>
/// Tests that the .ngo archive round-trip (write → read → link) produces
/// identical results to direct compilation. Each test compiles Go source
/// both ways and verifies the output matches.
/// </summary>
[TestClass]
public class ArchiveRoundTripTests
{
    /// <summary>
    /// Compiles Go source through the archive path (write → read → link),
    /// then persists to a PE file and runs ILVerify to check IL validity.
    /// Fails if any verification errors are found.
    /// </summary>
    private static void VerifyArchiveIL(string goSource)
    {
        var archivePath = Path.Combine(Path.GetTempPath(), "ngo-verify-" + Guid.NewGuid() + ".ngo");
        // Save PE next to the test assembly so ILVerify can find Ngo.Runtime.dll
        var testDir = Path.GetDirectoryName(typeof(ArchiveRoundTripTests).Assembly.Location)!;
        var dllPath = Path.Combine(testDir, "ngo-verify-" + Guid.NewGuid() + ".dll");

        try
        {
            // Step 1: Compile and write .ngo archive
            var tree = SyntaxTree.Parse(goSource);
            var compilationContext = new CompilationContext(null);
            var result = SemanticAnalyzer.Analyze(tree, compilationContext);
            Assert.IsFalse(result.HasErrors, "Analysis errors: " + string.Join("\n", result.Errors));

            var pkgSymbol = result.Root.Package.Symbol;

            // Populate exports from AST
            foreach (var func in result.Root.Functions)
            {
                if (func.Symbol.Name.Length > 0 && char.IsUpper(func.Symbol.Name[0]))
                {
                    pkgSymbol.AddExport(func.Symbol);
                }
            }
            foreach (var method in result.Root.Methods)
            {
                if (method.Symbol.Name.Length > 0 && char.IsUpper(method.Symbol.Name[0]))
                {
                    pkgSymbol.AddExport(method.Symbol);
                }
            }
            foreach (var typeDecl in result.Root.Types)
            {
                if (typeDecl.Symbol.Name.Length > 0 && char.IsUpper(typeDecl.Symbol.Name[0]))
                {
                    pkgSymbol.AddExport(typeDecl.Symbol);
                }
            }
            foreach (var varDecl in result.Root.Variables)
            {
                if (varDecl.Symbol.Name.Length > 0 && char.IsUpper(varDecl.Symbol.Name[0]))
                {
                    pkgSymbol.AddExport(varDecl.Symbol);
                }
            }

            ILSerializer.WriteArchive(archivePath, pkgSymbol, "testpkg", result, compilationContext);

            // Step 2: Read Go metadata back
            var readPkg = NgoArchive.ReadGoMetadata(archivePath);
            Assert.IsNotNull(readPkg, "Failed to read Go metadata from archive");

            // Step 3: Link archived IL into a real persisted assembly
            var asmName = new System.Reflection.AssemblyName("verify_" + Guid.NewGuid().ToString("N"));
            var persistedAsm = new System.Reflection.Emit.PersistedAssemblyBuilder(asmName, typeof(object).Assembly);
            var moduleBuilder = persistedAsm.DefineDynamicModule(asmName.Name!);

            var mapper = new TypeMapper(compilationContext);
            var emitCtx = new EmitContext(new Ngo.Compiler.Emit.Builder.LiveModuleBuilder(moduleBuilder), mapper, null, NullLog.Instance);
            mapper.SetEmitContext(emitCtx);

            var linked = ILSerializer.LinkFromArchive(archivePath, readPkg, emitCtx);
            Assert.IsTrue(linked, "LinkFromArchive returned false");

            // Step 4: Save to PE file
            var metadataBuilder = persistedAsm.GenerateMetadata(
                out System.Reflection.Metadata.BlobBuilder ilStream,
                out System.Reflection.Metadata.BlobBuilder fieldData);

            var peBuilder = new System.Reflection.PortableExecutable.ManagedPEBuilder(
                System.Reflection.PortableExecutable.PEHeaderBuilder.CreateLibraryHeader(),
                new System.Reflection.Metadata.Ecma335.MetadataRootBuilder(metadataBuilder),
                ilStream,
                mappedFieldData: fieldData);

            var peBlob = new System.Reflection.Metadata.BlobBuilder();
            peBuilder.Serialize(peBlob);

            using (var fs = new FileStream(dllPath, FileMode.Create))
            {
                peBlob.WriteContentTo(fs);
            }

            // Step 5: Run ILVerify on the persisted assembly
            var errors = ILVerifier.Verify(dllPath);
            if (errors.Count > 0)
            {
                var errorMessages = string.Join("\n", errors.Take(10));
                Assert.Fail($"IL verification failed with {errors.Count} error(s):\n{errorMessages}");
            }
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
            if (File.Exists(dllPath))
            {
                File.Delete(dllPath);
            }
        }
    }

    // ================================================================
    // ZIP Format Tests
    // ================================================================

    [TestMethod]
    public void Archive_is_valid_zip()
    {
        VerifyArchiveIL(@"
package testpkg

func Add(a, b int) int {
    return a + b
}");
    }

    [TestMethod]
    public void Archive_go_metadata_roundtrip()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), "ngo-meta-" + Guid.NewGuid() + ".ngo");
        try
        {
            var tree = SyntaxTree.Parse(@"
package testpkg

func Add(a, b int) int { return a + b }
func Greet(name string) string { return ""hello "" + name }
");
            var compilationContext = new CompilationContext(null);
            var result = SemanticAnalyzer.Analyze(tree, compilationContext);
            var pkgSymbol = result.Root.Package.Symbol;

            // Populate exports from AST (normally done by GoPackageResolver)
            foreach (var func in result.Root.Functions)
            {
                if (func.Symbol.Name.Length > 0 && char.IsUpper(func.Symbol.Name[0]))
                {
                    pkgSymbol.AddExport(func.Symbol);
                }
            }

            ILSerializer.WriteArchive(archivePath, pkgSymbol, "testpkg", result, compilationContext);

            var readPkg = NgoArchive.ReadGoMetadata(archivePath);
            Assert.IsNotNull(readPkg);
            Assert.AreEqual("testpkg", readPkg.Name);

            // Verify exported functions are readable from the archive
            bool foundAdd = false;
            bool foundGreet = false;
            foreach (var export in readPkg.Exports)
            {
                if (export.Key == "Add")
                {
                    foundAdd = true;
                    Assert.IsInstanceOfType(export.Value, typeof(FunctionSymbol));
                }
                if (export.Key == "Greet")
                {
                    foundGreet = true;
                    Assert.IsInstanceOfType(export.Value, typeof(FunctionSymbol));
                }
            }
            Assert.IsTrue(foundAdd, "Add function not found. Exports: " + string.Join(", ", readPkg.Exports.Select(e => e.Key)));
            Assert.IsTrue(foundGreet, "Greet function not found. Exports: " + string.Join(", ", readPkg.Exports.Select(e => e.Key)));
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
    }

    // ================================================================
    // Integer Type Tests — verify all int sizes survive the round-trip
    // ================================================================

    [TestMethod]
    public void Archive_int_arithmetic()
    {
        VerifyArchiveIL(@"
package testpkg

func Compute(x int) int {
    return x * 10 + 5
}");
    }

    [TestMethod]
    public void Archive_uint_arithmetic()
    {
        VerifyArchiveIL(@"
package testpkg

func Compute(x uint) uint {
    return x * 10 + 5
}");
    }

    [TestMethod]
    public void Archive_byte_operations()
    {
        VerifyArchiveIL(@"
package testpkg

func ToByte(x int) byte {
    return byte(x)
}

func ByteAdd(a, b byte) byte {
    return a + b
}");
    }

    [TestMethod]
    public void Archive_uint16_operations()
    {
        VerifyArchiveIL(@"
package testpkg

func ToUint16(x int) uint16 {
    return uint16(x)
}");
    }

    [TestMethod]
    public void Archive_uint32_operations()
    {
        VerifyArchiveIL(@"
package testpkg

func ToUint32(x int) uint32 {
    return uint32(x)
}");
    }

    [TestMethod]
    public void Archive_shift_operations()
    {
        VerifyArchiveIL(@"
package testpkg

func ShiftLeft(x uint64, n uint) uint64 {
    return x << n
}

func ShiftRight(x uint64, n uint) uint64 {
    return x >> n
}");
    }

    // ================================================================
    // Comparison Tests — byte/uint vs untyped constant comparisons
    // ================================================================

    [TestMethod]
    public void Archive_byte_comparison_with_constant()
    {
        VerifyArchiveIL(@"
package testpkg

func IsDigit(ch byte) bool {
    return ch >= '0' && ch <= '9'
}

func IsLetter(ch byte) bool {
    return (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z')
}");
    }

    [TestMethod]
    public void Archive_uint_comparison_with_constant()
    {
        VerifyArchiveIL(@"
package testpkg

func IsSmall(x uint) bool {
    return x < 100
}");
    }

    [TestMethod]
    public void Archive_uint_increment()
    {
        VerifyArchiveIL(@"
package testpkg

func Count(n uint) uint {
    var result uint = 0
    for i := uint(0); i < n; i++ {
        result++
    }
    return result
}");
    }

    // ================================================================
    // Slice and Array Tests
    // ================================================================

    [TestMethod]
    public void Archive_byte_array_init()
    {
        VerifyArchiveIL(@"
package testpkg

func MakeBuffer() [10]byte {
    var buf [10]byte
    buf[0] = 'A'
    buf[1] = 'B'
    return buf
}");
    }

    [TestMethod]
    public void Archive_slice_indexing()
    {
        VerifyArchiveIL(@"
package testpkg

func First(s []int) int {
    return s[0]
}

func Set(s []int, i int, v int) {
    s[i] = v
}");
    }

    [TestMethod]
    public void Archive_slice_reslice()
    {
        VerifyArchiveIL(@"
package testpkg

func Tail(s []int) []int {
    return s[1:]
}

func Head(s []int, n int) []int {
    return s[:n]
}");
    }

    [TestMethod]
    public void Archive_append_elements()
    {
        VerifyArchiveIL(@"
package testpkg

func AppendOne(s []int, v int) []int {
    return append(s, v)
}");
    }

    [TestMethod]
    public void Archive_append_spread()
    {
        VerifyArchiveIL(@"
package testpkg

func Concat(a, b []byte) []byte {
    return append(a, b...)
}");
    }

    [TestMethod]
    public void Archive_string_indexing()
    {
        VerifyArchiveIL(@"
package testpkg

func CharAt(s string, i int) byte {
    return s[i]
}");
    }

    [TestMethod]
    public void Archive_string_slicing()
    {
        VerifyArchiveIL(@"
package testpkg

func Substr(s string, lo, hi int) string {
    return s[lo:hi]
}");
    }

    // ================================================================
    // Slice/Array Literal Tests — the .cctor pattern
    // ================================================================

    [TestMethod]
    public void Archive_int_slice_literal()
    {
        VerifyArchiveIL(@"
package testpkg

var Nums = []int{1, 3, 6, 9, 13, 16, 19}");
    }

    [TestMethod]
    public void Archive_uint16_slice_literal()
    {
        VerifyArchiveIL(@"
package testpkg

var Data = []uint16{0x0020, 0x007e, 0x00a0, 0x00ac, 0x00ae}");
    }

    [TestMethod]
    public void Archive_uint32_slice_literal()
    {
        VerifyArchiveIL(@"
package testpkg

var Data = []uint32{0x10000, 0x1000f, 0x10020}");
    }

    [TestMethod]
    public void Archive_float64_slice_literal()
    {
        VerifyArchiveIL(@"
package testpkg

var Powers = []float64{1e0, 1e1, 1e2, 1e3, 1e4}");
    }

    [TestMethod]
    public void Archive_uint64_array_literal()
    {
        VerifyArchiveIL(@"
package testpkg

var Table = [...]uint64{100, 200, 300, 400, 500}");
    }

    [TestMethod]
    public void Archive_nested_array_literal()
    {
        VerifyArchiveIL(@"
package testpkg

var Pairs = [...][2]uint64{
    {0x1234, 0x5678},
    {0xABCD, 0xEF01},
}");
    }

    [TestMethod]
    public void Archive_string_slice_literal()
    {
        VerifyArchiveIL(@"
package testpkg

var Names = []string{""alpha"", ""beta"", ""gamma""}");
    }

    // ================================================================
    // Struct Tests
    // ================================================================

    [TestMethod]
    public void Archive_struct_definition()
    {
        VerifyArchiveIL(@"
package testpkg

type Point struct {
    X int
    Y int
}

func NewPoint(x, y int) Point {
    return Point{X: x, Y: y}
}");
    }

    [TestMethod]
    public void Archive_struct_with_string_method()
    {
        VerifyArchiveIL(@"
package testpkg

type Greeting struct {
    Name string
}

func (g Greeting) String() string {
    return ""Hello, "" + g.Name
}");
    }

    [TestMethod]
    public void Archive_struct_pointer_receiver()
    {
        VerifyArchiveIL(@"
package testpkg

type Counter struct {
    Value int
}

func (c *Counter) Increment() {
    c.Value++
}

func (c *Counter) Get() int {
    return c.Value
}");
    }

    [TestMethod]
    public void Archive_struct_ptr_receiver_slice_nil_check()
    {
        VerifyArchiveIL(@"
package testpkg

type Buf struct {
    Data []byte
    W    int
    S    string
}

func (b *Buf) AppendByte(c byte) {
    if b.Data == nil {
        if b.W < len(b.S) && b.S[b.W] == c {
            b.W++
            return
        }
        b.Data = make([]byte, len(b.S))
        copy(b.Data, b.S[:b.W])
    }
    b.Data[b.W] = c
    b.W++
}

func (b *Buf) Str() string {
    if b.Data == nil {
        return b.S[:b.W]
    }
    return string(b.Data[:b.W])
}");
    }

    [TestMethod]
    public void Archive_struct_literal_slice()
    {
        VerifyArchiveIL(@"
package testpkg

type Pair struct {
    Key   int
    Value string
}

var Pairs = []Pair{
    {1, ""one""},
    {2, ""two""},
    {3, ""three""},
}");
    }

    // ================================================================
    // Multi-Return and Nil Tests
    // ================================================================

    [TestMethod]
    public void Archive_multi_return_int_error()
    {
        VerifyArchiveIL(@"
package testpkg

func Parse(s string) (int, error) {
    if len(s) == 0 {
        return 0, nil
    }
    return len(s), nil
}");
    }

    [TestMethod]
    public void Archive_multi_return_slice_string()
    {
        VerifyArchiveIL(@"
package testpkg

func Split(s string) ([]byte, string) {
    return nil, s
}");
    }

    [TestMethod]
    public void Archive_named_returns_with_bare_return()
    {
        VerifyArchiveIL(@"
package testpkg

func Format(x int) (result string, ok bool) {
    if x > 0 {
        result = ""positive""
        ok = true
        return
    }
    result = ""non-positive""
    return
}");
    }

    [TestMethod]
    public void Archive_named_returns_with_nil_slice()
    {
        VerifyArchiveIL(@"
package testpkg

func Process(data []byte, appendMode bool) (output []byte, text string) {
    if appendMode {
        output = append(data, 'x')
        return
    }
    text = ""done""
    return
}");
    }

    // ================================================================
    // Control Flow Tests
    // ================================================================

    [TestMethod]
    public void Archive_for_loop_with_break()
    {
        VerifyArchiveIL(@"
package testpkg

func Find(s string, ch byte) int {
    for i := 0; i < len(s); i++ {
        if s[i] == ch {
            return i
        }
    }
    return -1
}");
    }

    [TestMethod]
    public void Archive_range_over_byte_slice()
    {
        VerifyArchiveIL(@"
package testpkg

func Sum(data []byte) int {
    total := 0
    for _, b := range data {
        total += int(b)
    }
    return total
}");
    }

    [TestMethod]
    public void Archive_switch_statement()
    {
        VerifyArchiveIL(@"
package testpkg

func Classify(x int) string {
    switch {
    case x < 0:
        return ""negative""
    case x == 0:
        return ""zero""
    default:
        return ""positive""
    }
}");
    }

    // ================================================================
    // Itoa Pattern — the real-world test case that motivated this work
    // ================================================================

    [TestMethod]
    public void Archive_itoa_pattern()
    {
        VerifyArchiveIL(@"
package testpkg

const digits = ""0123456789""

func Uitoa(val uint) string {
    if val == 0 {
        return ""0""
    }
    var buf [20]byte
    i := len(buf) - 1
    for val >= 10 {
        q := val / 10
        buf[i] = byte('0' + val - q*10)
        i--
        val = q
    }
    buf[i] = byte('0' + val)
    return string(buf[i:])
}

func Itoa(val int) string {
    if val < 0 {
        return ""-"" + Uitoa(uint(-val))
    }
    return Uitoa(uint(val))
}");
    }

    [TestMethod]
    public void Archive_hex_format_pattern()
    {
        VerifyArchiveIL(@"
package testpkg

const hexDigits = ""0123456789abcdef""

func FormatHex(val uint64) string {
    if val == 0 {
        return ""0""
    }
    var buf [16]byte
    i := len(buf) - 1
    for val > 0 {
        buf[i] = hexDigits[val&0xf]
        val >>= 4
        i--
    }
    return string(buf[i+1:])
}");
    }

    // ================================================================
    // Type Conversion Tests
    // ================================================================

    [TestMethod]
    public void Archive_int_to_byte_conversion()
    {
        VerifyArchiveIL(@"
package testpkg

func IntToByte(x int) byte {
    return byte(x)
}

func ByteToInt(b byte) int {
    return int(b)
}");
    }

    [TestMethod]
    public void Archive_uint_to_int_conversion()
    {
        VerifyArchiveIL(@"
package testpkg

func UintToInt(x uint) int {
    return int(x)
}

func IntToUint(x int) uint {
    return uint(x)
}");
    }

    [TestMethod]
    public void Archive_string_to_bytes()
    {
        VerifyArchiveIL(@"
package testpkg

func ToBytes(s string) []byte {
    return []byte(s)
}

func FromBytes(b []byte) string {
    return string(b)
}");
    }

    // ================================================================
    // Map Tests
    // ================================================================

    [TestMethod]
    public void Archive_map_operations()
    {
        VerifyArchiveIL(@"
package testpkg

func MapLen(m map[string]int) int {
    return len(m)
}

func MapGet(m map[string]int, key string) int {
    return m[key]
}");
    }

    // ================================================================
    // Error/Interface Tests
    // ================================================================

    [TestMethod]
    public void Archive_error_return()
    {
        VerifyArchiveIL(@"
package testpkg

import ""errors""

var ErrBad = errors.New(""bad"")

func Validate(x int) error {
    if x < 0 {
        return ErrBad
    }
    return nil
}");
    }

    // ================================================================
    // Package Variable Initialization Tests
    // ================================================================

    [TestMethod]
    public void Archive_package_var_bool()
    {
        VerifyArchiveIL(@"
package testpkg

var Enabled = true
var Debug = false");
    }

    [TestMethod]
    public void Archive_package_var_string()
    {
        VerifyArchiveIL(@"
package testpkg

var Name = ""hello""
var Empty = """"");
    }

    [TestMethod]
    public void Archive_package_var_computed()
    {
        VerifyArchiveIL(@"
package testpkg

var IntSize = 32 << (^uint(0) >> 63)");
    }

    [TestMethod]
    public void Archive_generic_field_type_preserved()
    {
        // Tests that struct field types including slices and maps survive
        // archive round-trip serialization with correct type arguments.
        VerifyArchiveIL(@"package testpkg

type Registry struct {
    Names  []string
    Counts map[string]int
}

func NewRegistry() Registry {
    return Registry{
        Names:  []string{""a"", ""b""},
        Counts: map[string]int{""x"": 1},
    }
}

func (r Registry) Len() int {
    return len(r.Names)
}

func (r Registry) Lookup(key string) int {
    return r.Counts[key]
}
");
    }
}
