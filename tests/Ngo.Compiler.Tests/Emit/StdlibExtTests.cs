// -----------------------------------------------------------------------
// <copyright file="StdlibExtTests.cs" company="Ziad">
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
using Ngo.Compiler.Emit;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Emit;

[TestClass]
public class StdlibExtTests
{
    private static string Run(string goSource)
    {
        var tree = SyntaxTree.Parse(goSource);
        var ctx = new CompilationContext(null);
        var result = SemanticAnalyzer.Analyze(tree, ctx);

        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

        var assembly = AssemblyEmitter.Emit(result, ctx);
        var entryPoint = AssemblyEmitter.FindEntryPoint(assembly);
        Assert.IsNotNull(entryPoint);

        var oldOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            entryPoint.Invoke(null, null);
        }
        finally
        {
            Console.SetOut(oldOut);
        }

        return sw.ToString().Replace("\r\n", "\n");
    }

    // ================================================================
    // strings extensions
    // ================================================================

    [TestMethod]
    public void Strings_TrimPrefix()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    fmt.Println(strings.TrimPrefix(""Hello, World"", ""Hello, ""))
    fmt.Println(strings.TrimPrefix(""Hello, World"", ""Foo""))
}");
        Assert.AreEqual("World\nHello, World\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_TrimSuffix()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    fmt.Println(strings.TrimSuffix(""file.go"", "".go""))
    fmt.Println(strings.TrimSuffix(""file.go"", "".txt""))
}");
        Assert.AreEqual("file\nfile.go\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_Count()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    fmt.Println(strings.Count(""hello"", ""l""))
    fmt.Println(strings.Count(""cheese"", ""e""))
}");
        Assert.AreEqual("2\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_EqualFold()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    fmt.Println(strings.EqualFold(""Go"", ""go""))
    fmt.Println(strings.EqualFold(""Go"", ""java""))
}");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_Fields()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    words := strings.Fields(""  hello   world  "")
    fmt.Println(len(words))
    fmt.Println(words[0])
    fmt.Println(words[1])
}");
        Assert.AreEqual("2\nhello\nworld\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_LastIndex()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    fmt.Println(strings.LastIndex(""go gopher"", ""go""))
    fmt.Println(strings.LastIndex(""hello"", ""x""))
}");
        Assert.AreEqual("3\n-1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_ContainsAny()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    fmt.Println(strings.ContainsAny(""hello"", ""aeiou""))
    fmt.Println(strings.ContainsAny(""xyz"", ""aeiou""))
}");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // math extensions
    // ================================================================

    [TestMethod]
    public void Math_trig_functions()
    {
        var output = Run(@"
package main
import ""fmt""
import ""math""
func main() {
    fmt.Println(math.Sin(0))
    fmt.Println(math.Cos(0))
}");
        Assert.AreEqual("0\n1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_log_functions()
    {
        var output = Run(@"
package main
import ""fmt""
import ""math""
func main() {
    fmt.Println(math.Log2(8))
    fmt.Println(math.Log10(1000))
}");
        Assert.AreEqual("3\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_Exp()
    {
        var output = Run(@"
package main
import ""fmt""
import ""math""
func main() {
    fmt.Println(math.Exp(0))
}");
        Assert.AreEqual("1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_Inf_and_NaN()
    {
        var output = Run(@"
package main
import ""fmt""
import ""math""
func main() {
    fmt.Println(math.IsNaN(math.NaN()))
    fmt.Println(math.IsInf(math.Inf(1), 1))
}");
        Assert.AreEqual("true\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_Trunc()
    {
        var output = Run(@"
package main
import ""fmt""
import ""math""
func main() {
    fmt.Println(math.Trunc(3.7))
    fmt.Println(math.Trunc(-3.7))
}");
        Assert.AreEqual("3\n-3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_Pi_constant()
    {
        var output = Run(@"
package main
import ""fmt""
import ""math""
func main() {
    fmt.Println(math.Pi > 3.14)
    fmt.Println(math.Pi < 3.15)
}");
        Assert.AreEqual("true\ntrue\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // errors extensions
    // ================================================================

    [TestMethod]
    public void Errors_Unwrap()
    {
        var output = Run(@"
package main
import ""fmt""
import ""errors""
func main() {
    inner := errors.New(""inner error"")
    wrapped := fmt.Errorf(""outer: %w"", inner)
    unwrapped := errors.Unwrap(wrapped)
    fmt.Println(unwrapped)
}");
        Assert.AreEqual("inner error\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Errors_Is()
    {
        var output = Run(@"
package main
import ""fmt""
import ""errors""
func main() {
    target := errors.New(""not found"")
    wrapped := fmt.Errorf(""failed: %w"", target)
    fmt.Println(errors.Is(wrapped, target))
    other := errors.New(""other"")
    fmt.Println(errors.Is(wrapped, other))
}");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_errorf_with_wrapping()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    err := fmt.Errorf(""failed to open file: %s"", ""data.txt"")
    fmt.Println(err)
}");
        Assert.AreEqual("failed to open file: data.txt\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Go 1.21 builtins: min, max, clear
    // ================================================================

    [TestMethod]
    public void Min_builtin()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    fmt.Println(min(3, 1, 2))
    fmt.Println(min(5, 10))
}");
        Assert.AreEqual("1\n5\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Max_builtin()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    fmt.Println(max(3, 1, 2))
    fmt.Println(max(5, 10))
}");
        Assert.AreEqual("3\n10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Clear_builtin_slice()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    s := []int{1, 2, 3}
    clear(s)
    fmt.Println(s[0], s[1], s[2])
    fmt.Println(len(s))
}");
        Assert.AreEqual("0 0 0\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Clear_builtin_map()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    m := map[string]int{""a"": 1, ""b"": 2}
    clear(m)
    fmt.Println(len(m))
}");
        Assert.AreEqual("0\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Go 1.22: for range N
    // ================================================================

    [TestMethod]
    public void For_range_integer()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    for i := range 5 {
        fmt.Println(i)
    }
}");
        Assert.AreEqual("0\n1\n2\n3\n4\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // strings.NewReplacer
    // ================================================================

    [TestMethod]
    public void Strings_NewReplacer()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    r := strings.NewReplacer(""a"", ""1"", ""b"", ""2"")
    fmt.Println(r.Replace(""abc""))
}");
        Assert.AreEqual("12c\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // strings.Builder
    // ================================================================

    [TestMethod]
    public void Strings_Builder()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    var b strings.Builder
    b.WriteString(""hello"")
    b.WriteString("" world"")
    fmt.Println(b.String())
    fmt.Println(b.Len())
}");
        Assert.AreEqual("hello world\n11\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // strings.Cut
    // ================================================================

    [TestMethod]
    public void Strings_Cut()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""
func main() {
    before, after, found := strings.Cut(""hello=world"", ""="")
    fmt.Println(before)
    fmt.Println(after)
    fmt.Println(found)
}");
        Assert.AreEqual("hello\nworld\ntrue\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // time package
    // ================================================================

    [TestMethod]
    public void Time_now_and_year()
    {
        var output = Run(@"
package main
import ""fmt""
import ""time""
func main() {
    t := time.Now()
    fmt.Println(t.Year() >= 2024)
}");
        Assert.AreEqual("true\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Time_date_constructor()
    {
        var output = Run(@"
package main
import ""fmt""
import ""time""
func main() {
    t := time.Date(2023, 6, 15, 12, 30, 0, 0, time.UTC)
    fmt.Println(t.Year())
    fmt.Println(t.Month())
    fmt.Println(t.Day())
}");
        Assert.AreEqual("2023\n6\n15\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Time_format()
    {
        var output = Run(@"
package main
import ""fmt""
import ""time""
func main() {
    t := time.Date(2023, 6, 15, 12, 30, 45, 0, time.UTC)
    fmt.Println(t.Format(""2006-01-02""))
}");
        Assert.AreEqual("2023-06-15\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Time_sub_and_before()
    {
        var output = Run(@"
package main
import ""fmt""
import ""time""
func main() {
    t1 := time.Date(2023, 1, 1, 0, 0, 0, 0, time.UTC)
    t2 := time.Date(2023, 1, 2, 0, 0, 0, 0, time.UTC)
    fmt.Println(t1.Before(t2))
    fmt.Println(t2.After(t1))
}");
        Assert.AreEqual("true\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Time_unix()
    {
        var output = Run(@"
package main
import ""fmt""
import ""time""
func main() {
    t := time.Unix(0, 0)
    fmt.Println(t.Year())
    fmt.Println(t.Unix())
}");
        Assert.AreEqual("1970\n0\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // os extensions
    // ================================================================

    [TestMethod]
    public void Os_stat()
    {
        var output = Run(@"
package main
import ""fmt""
import ""os""
func main() {
    _, err := os.Stat(""nonexistent_file_xyz"")
    fmt.Println(err != nil)
}");
        Assert.AreEqual("true\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Os_temp_dir()
    {
        var output = Run(@"
package main
import ""fmt""
import ""os""
func main() {
    d := os.TempDir()
    fmt.Println(len(d) > 0)
}");
        Assert.AreEqual("true\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // context package
    // ================================================================

    [TestMethod]
    public void Context_background_and_with_value()
    {
        var output = Run(@"
package main
import ""fmt""
import ""context""
func main() {
    ctx := context.Background()
    ctx = context.WithValue(ctx, ""key"", ""hello"")
    v := ctx.Value(""key"")
    fmt.Println(v)
}");
        Assert.AreEqual("hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Context_with_cancel()
    {
        var output = Run(@"
package main
import ""fmt""
import ""context""
func main() {
    ctx, cancel := context.WithCancel(context.Background())
    fmt.Println(ctx.Err())
    cancel()
    fmt.Println(ctx.Err())
}");
        Assert.AreEqual("<nil>\ncontext canceled\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // fmt.Stringer dispatch
    // ================================================================

    [TestMethod]
    public void Stringer_dispatch()
    {
        var output = Run(@"
package main
import ""fmt""

type Color struct {
    R int
    G int
    B int
}

func (c Color) String() string {
    return fmt.Sprintf(""rgb(%d,%d,%d)"", c.R, c.G, c.B)
}

func main() {
    c := Color{255, 128, 0}
    fmt.Println(c)
    fmt.Printf(""%v\n"", c)
}");
        Assert.AreEqual("rgb(255,128,0)\nrgb(255,128,0)\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // bytes.Buffer
    // ================================================================

    [TestMethod]
    public void Bytes_Buffer_write_and_string()
    {
        var output = Run(@"
package main
import ""fmt""
import ""bytes""
func main() {
    var buf bytes.Buffer
    buf.WriteString(""hello"")
    buf.WriteString("" world"")
    fmt.Println(buf.String())
    fmt.Println(buf.Len())
}");
        Assert.AreEqual("hello world\n11\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Bytes_NewBuffer()
    {
        var output = Run(@"
package main
import ""fmt""
import ""bytes""
func main() {
    buf := bytes.NewBuffer([]byte(""initial""))
    buf.WriteString("" data"")
    fmt.Println(buf.String())
}");
        Assert.AreEqual("initial data\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Bytes_Buffer_bytes_and_reset()
    {
        var output = Run(@"
package main
import ""fmt""
import ""bytes""
func main() {
    var buf bytes.Buffer
    buf.WriteString(""abc"")
    b := buf.Bytes()
    fmt.Println(len(b))
    buf.Reset()
    fmt.Println(buf.Len())
}");
        Assert.AreEqual("3\n0\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // sort.Slice
    // ================================================================

    [TestMethod]
    public void Sort_Slice_ints()
    {
        var output = Run(@"
package main
import ""fmt""
import ""sort""
func main() {
    s := []int{3, 1, 4, 1, 5, 9}
    sort.Slice(s, func(i, j int) bool {
        return s[i] < s[j]
    })
    fmt.Println(s[0], s[1], s[2], s[3], s[4], s[5])
}");
        Assert.AreEqual("1 1 3 4 5 9\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sort_SliceIsSorted()
    {
        var output = Run(@"
package main
import ""fmt""
import ""sort""
func main() {
    s := []int{1, 2, 3, 4, 5}
    fmt.Println(sort.SliceIsSorted(s, func(i, j int) bool {
        return s[i] < s[j]
    }))
    s2 := []int{5, 3, 1}
    fmt.Println(sort.SliceIsSorted(s2, func(i, j int) bool {
        return s2[i] < s2[j]
    }))
}");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // encoding/json
    // ================================================================

    [TestMethod]
    public void Json_Marshal_map()
    {
        var output = Run(@"
package main
import ""fmt""
import ""encoding/json""
func main() {
    m := map[string]int{""a"": 1, ""b"": 2}
    data, err := json.Marshal(m)
    fmt.Println(err)
    fmt.Println(string(data))
}");
        var lines = output.Replace("\r\n", "\n").Split('\n');
        Assert.AreEqual("<nil>", lines[0]);
        // Map order is non-deterministic, just check it's valid JSON with both keys
        Assert.IsTrue(lines[1].Contains("\"a\":1") || lines[1].Contains("\"a\": 1"));
        Assert.IsTrue(lines[1].Contains("\"b\":2") || lines[1].Contains("\"b\": 2"));
    }

    [TestMethod]
    public void Json_Valid()
    {
        var output = Run(@"
package main
import ""fmt""
import ""encoding/json""
func main() {
    fmt.Println(json.Valid([]byte(`{""key"":1}`)))
    fmt.Println(json.Valid([]byte(""not json"")))
}");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Go 1.22 loop variable scoping
    // ================================================================

    [TestMethod]
    public void Loop_var_scoping_for()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    funcs := make([]func() int, 3)
    for i := 0; i < 3; i++ {
        funcs[i] = func() int { return i }
    }
    fmt.Println(funcs[0]())
    fmt.Println(funcs[1]())
    fmt.Println(funcs[2]())
}");
        // Go 1.22: each iteration gets its own i, so closures capture 0, 1, 2
        Assert.AreEqual("0\n1\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Loop_var_scoping_for_range()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    values := []string{""a"", ""b"", ""c""}
    funcs := make([]func() string, 3)
    for i, v := range values {
        funcs[i] = func() string { return v }
    }
    fmt.Println(funcs[0]())
    fmt.Println(funcs[1]())
    fmt.Println(funcs[2]())
}");
        // Go 1.22: each iteration gets its own v
        Assert.AreEqual("a\nb\nc\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // Struct field tags
    // ================================================================

    [TestMethod]
    public void Json_marshal_with_struct_tags()
    {
        var bt = "\u0060";
        var output = Run(
"package main\n" +
"import \"fmt\"\n" +
"import \"encoding/json\"\n" +
"type Person struct {\n" +
"    Name string " + bt + "json:\"name\"" + bt + "\n" +
"    Age  int    " + bt + "json:\"age\"" + bt + "\n" +
"}\n" +
"func main() {\n" +
"    p := Person{Name: \"Alice\", Age: 30}\n" +
"    data, _ := json.Marshal(&p)\n" +
"    fmt.Println(string(data))\n" +
"}");
        Assert.AreEqual("{\"name\":\"Alice\",\"age\":30}\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Json_marshal_omitempty_tag()
    {
        var bt = "\u0060";
        var output = Run(
"package main\n" +
"import \"fmt\"\n" +
"import \"encoding/json\"\n" +
"type Item struct {\n" +
"    Name  string " + bt + "json:\"name\"" + bt + "\n" +
"    Count int    " + bt + "json:\"count,omitempty\"" + bt + "\n" +
"}\n" +
"func main() {\n" +
"    item := Item{Name: \"widget\"}\n" +
"    data, _ := json.Marshal(&item)\n" +
"    fmt.Println(string(data))\n" +
"}");
        Assert.AreEqual("{\"name\":\"widget\"}\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Json_marshal_skip_tag()
    {
        var bt = "\u0060";
        var output = Run(
"package main\n" +
"import \"fmt\"\n" +
"import \"encoding/json\"\n" +
"type Secret struct {\n" +
"    Name     string " + bt + "json:\"name\"" + bt + "\n" +
"    Password string " + bt + "json:\"-\"" + bt + "\n" +
"}\n" +
"func main() {\n" +
"    s := Secret{Name: \"Bob\", Password: \"secret123\"}\n" +
"    data, _ := json.Marshal(&s)\n" +
"    fmt.Println(string(data))\n" +
"}");
        Assert.AreEqual("{\"name\":\"Bob\"}\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // io extensions
    // ================================================================

    [TestMethod]
    public void Io_LimitReader()
    {
        var output = Run(@"
package main
import ""fmt""
import ""io""
import ""strings""
func main() {
    r := strings.NewReader(""Hello, World!"")
    lr := io.LimitReader(r, 5)
    data, _ := io.ReadAll(lr)
    fmt.Println(string(data))
}");
        Assert.AreEqual("Hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Io_NopCloser()
    {
        var output = Run(@"
package main
import ""fmt""
import ""io""
import ""strings""
func main() {
    r := strings.NewReader(""test"")
    rc := io.NopCloser(r)
    data, _ := io.ReadAll(rc)
    fmt.Println(string(data))
}");
        Assert.AreEqual("test\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // io/ioutil
    // ================================================================

    [TestMethod]
    public void Ioutil_ReadAll()
    {
        var output = Run(@"
package main
import ""fmt""
import ""io/ioutil""
import ""strings""
func main() {
    r := strings.NewReader(""ioutil works"")
    data, _ := ioutil.ReadAll(r)
    fmt.Println(string(data))
}");
        Assert.AreEqual("ioutil works\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // filepath extensions
    // ================================================================

    [TestMethod]
    public void Filepath_Match()
    {
        var output = Run(@"
package main
import ""fmt""
import ""path/filepath""
func main() {
    m1, _ := filepath.Match(""*.go"", ""main.go"")
    m2, _ := filepath.Match(""*.go"", ""main.txt"")
    fmt.Println(m1)
    fmt.Println(m2)
}");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // fmt.Sscan
    // ================================================================

    [TestMethod]
    public void Fmt_Sscan_count()
    {
        var output = Run(@"
package main
import ""fmt""
func main() {
    n, err := fmt.Sscan(""42 99"")
    fmt.Println(n)
    fmt.Println(err == nil)
}");
        Assert.AreEqual("0\ntrue\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // fmt.Scan / Scanln (stdin)
    // ================================================================

    [TestMethod]
    public void Fmt_Scan_from_stdin()
    {
        var output = RunWithStdin("hello world\n", @"
package main
import ""fmt""
func main() {
    n, _ := fmt.Scan()
    fmt.Println(n)
}");
        Assert.AreEqual("0\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_Scanln_from_stdin()
    {
        var output = RunWithStdin("hello world\n", @"
package main
import ""fmt""
func main() {
    n, _ := fmt.Scanln()
    fmt.Println(n)
}");
        Assert.AreEqual("0\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // testing.T
    // ================================================================

    [TestMethod]
    public void Testing_T_compiles()
    {
        // Verify that importing testing and using T methods compiles and runs
        var tree = SyntaxTree.Parse(@"
package main
import ""testing""
func runTest(t *testing.T) {
    t.Log(""hello"")
    t.Error(""something failed"")
}
func main() {
}");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
        var assembly = AssemblyEmitter.Emit(result, new CompilationContext(null));
        Assert.IsNotNull(assembly);
    }

    [TestMethod]
    public void Testing_T_methods_execute()
    {
        // Test that T methods actually work at runtime
        var t = new Ngo.Runtime.Testing.T("TestExample");
        Assert.AreEqual("TestExample", t.Name());
        Assert.IsFalse(t.Failed());

        t.Error("oops");
        Assert.IsTrue(t.Failed());

        var logs = t.GetLogs();
        Assert.AreEqual(1, logs.Count);
        Assert.AreEqual("oops", logs[0]);
    }

    [TestMethod]
    public void Testing_T_Fatal_throws()
    {
        var t = new Ngo.Runtime.Testing.T("TestFatal");
        Assert.ThrowsException<Ngo.Runtime.Testing.TestFailException>(() => t.Fatal("stop"));
        Assert.IsTrue(t.Failed());
    }

    [TestMethod]
    public void Testing_T_Skip_throws()
    {
        var t = new Ngo.Runtime.Testing.T("TestSkip");
        Assert.ThrowsException<Ngo.Runtime.Testing.TestSkipException>(() => t.Skip("not ready"));
        Assert.IsTrue(t.Skipped());
    }

    [TestMethod]
    public void Testing_T_Run_subtest()
    {
        var t = new Ngo.Runtime.Testing.T("Parent");
        var result = t.Run("child", sub =>
        {
            sub.Log("child ran");
        });
        Assert.IsTrue(result);
        Assert.IsFalse(t.Failed());
    }

    [TestMethod]
    public void Testing_T_Run_failing_subtest()
    {
        var t = new Ngo.Runtime.Testing.T("Parent");
        var result = t.Run("failing", sub =>
        {
            sub.Fatal("boom");
        });
        Assert.IsFalse(result);
        Assert.IsTrue(t.Failed());
    }

    [TestMethod]
    public void Testing_T_TempDir()
    {
        var t = new Ngo.Runtime.Testing.T("TestTemp");
        var dir = t.TempDir();
        Assert.IsTrue(System.IO.Directory.Exists(dir));
        t.RunCleanups();
        Assert.IsFalse(System.IO.Directory.Exists(dir));
    }

    // ================================================================
    // sync.Map.Range
    // ================================================================

    [TestMethod]
    public void Sync_Map_Range_compiles()
    {
        var tree = SyntaxTree.Parse(@"
package main
import ""sync""
import ""fmt""
func main() {
    var m sync.Map
    m.Store(""a"", 1)
    m.Store(""b"", 2)
    count := 0
    m.Range(func(key, value interface{}) bool {
        count = count + 1
        return true
    })
    fmt.Println(count)
}");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    // ================================================================
    // encoding/base64
    // ================================================================

    [TestMethod]
    public void Base64_StdEncoding_encode_decode()
    {
        var output = Run(@"
package main
import ""fmt""
import ""encoding/base64""
func main() {
    data := []byte(""Hello, World!"")
    encoded := base64.StdEncoding.EncodeToString(data)
    fmt.Println(encoded)
    decoded, err := base64.StdEncoding.DecodeString(encoded)
    fmt.Println(string(decoded))
    fmt.Println(err == nil)
}");
        Assert.AreEqual("SGVsbG8sIFdvcmxkIQ==\nHello, World!\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Base64_URLEncoding()
    {
        var output = Run(@"
package main
import ""fmt""
import ""encoding/base64""
func main() {
    data := []byte(""Hello+World/"")
    encoded := base64.URLEncoding.EncodeToString(data)
    fmt.Println(encoded)
}");
        var result = output.Replace("\r\n", "\n").Trim();
        // URL encoding replaces + with - and / with _
        Assert.IsFalse(result.Contains("+"));
        Assert.IsFalse(result.Contains("/"));
    }

    // ================================================================
    // encoding/hex
    // ================================================================

    [TestMethod]
    public void Hex_EncodeToString()
    {
        var output = Run(@"
package main
import ""fmt""
import ""encoding/hex""
func main() {
    data := []byte(""Hello"")
    fmt.Println(hex.EncodeToString(data))
}");
        Assert.AreEqual("48656c6c6f\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Hex_DecodeString()
    {
        var output = Run(@"
package main
import ""fmt""
import ""encoding/hex""
func main() {
    decoded, err := hex.DecodeString(""48656c6c6f"")
    fmt.Println(string(decoded))
    fmt.Println(err == nil)
}");
        Assert.AreEqual("Hello\ntrue\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // crypto/sha256
    // ================================================================

    [TestMethod]
    public void Sha256_Sum256()
    {
        var output = Run(@"
package main
import ""fmt""
import ""crypto/sha256""
import ""encoding/hex""
func main() {
    data := []byte(""hello"")
    hash := sha256.Sum256(data)
    fmt.Println(hex.EncodeToString(hash[:]))
}");
        Assert.AreEqual("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824\n",
            output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // flag
    // ================================================================

    [TestMethod]
    public void Flag_package_compiles()
    {
        var tree = SyntaxTree.Parse(@"
package main
import ""flag""
import ""fmt""
func main() {
    name := flag.String(""name"", ""world"", ""a name"")
    flag.Parse()
    _ = name
    fmt.Println(flag.Parsed())
}");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    // ================================================================
    // encoding/csv
    // ================================================================

    [TestMethod]
    public void Csv_package_compiles()
    {
        var tree = SyntaxTree.Parse(@"
package main
import ""encoding/csv""
import ""strings""
func main() {
    r := csv.NewReader(strings.NewReader(""a,b,c\n1,2,3""))
    _, _ = r.Read()
    _ = r
}");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    // ================================================================
    // net/http
    // ================================================================

    [TestMethod]
    public void Http_package_compiles()
    {
        var tree = SyntaxTree.Parse(@"
package main
import ""net/http""
import ""fmt""
func main() {
    _ = http.StatusOK
    _ = http.StatusNotFound
    fmt.Println(""http ready"")
}");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    // ================================================================
    // crypto/rand
    // ================================================================

    [TestMethod]
    public void CryptoRand_compiles()
    {
        var tree = SyntaxTree.Parse(@"
package main
import ""crypto/rand""
func main() {
    b := make([]byte, 16)
    _, _ = rand.Read(b)
}");
        var result = SemanticAnalyzer.Analyze(tree, new CompilationContext(null));
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
    }

    // ================================================================
    // reflect
    // ================================================================

    [TestMethod]
    public void Reflect_TypeOf_basic_types()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    fmt.Println(reflect.TypeOf(42).Name())
    fmt.Println(reflect.TypeOf(""hello"").Name())
    fmt.Println(reflect.TypeOf(true).Name())
    fmt.Println(reflect.TypeOf(3.14).Name())
}");
        Assert.AreEqual("int\nstring\nbool\nfloat64\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_TypeOf_Kind()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    fmt.Println(reflect.TypeOf(42).Kind() == reflect.Int)
    fmt.Println(reflect.TypeOf(""hi"").Kind() == reflect.String)
    fmt.Println(reflect.TypeOf(true).Kind() == reflect.Bool)
    fmt.Println(reflect.TypeOf(3.14).Kind() == reflect.Float64)
}");
        Assert.AreEqual("true\ntrue\ntrue\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_ValueOf_Int()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    v := reflect.ValueOf(42)
    fmt.Println(v.Int())
    fmt.Println(v.Kind() == reflect.Int)
}");
        Assert.AreEqual("42\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_ValueOf_String()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    v := reflect.ValueOf(""hello"")
    fmt.Println(v.String())
}");
        Assert.AreEqual("hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_ValueOf_Bool()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    v := reflect.ValueOf(true)
    fmt.Println(v.Bool())
}");
        Assert.AreEqual("true\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_ValueOf_Float()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    v := reflect.ValueOf(3.14)
    fmt.Println(v.Float())
}");
        Assert.AreEqual("3.14\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_DeepEqual()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    fmt.Println(reflect.DeepEqual(1, 1))
    fmt.Println(reflect.DeepEqual(1, 2))
    fmt.Println(reflect.DeepEqual(""abc"", ""abc""))
    fmt.Println(reflect.DeepEqual(""abc"", ""def""))
}");
        Assert.AreEqual("true\nfalse\ntrue\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_Kind_constants()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    fmt.Println(reflect.Invalid)
    fmt.Println(reflect.Bool)
    fmt.Println(reflect.Int)
    fmt.Println(reflect.String)
    fmt.Println(reflect.Slice)
    fmt.Println(reflect.Map)
    fmt.Println(reflect.Struct)
    fmt.Println(reflect.Ptr)
}");
        Assert.AreEqual("0\n1\n2\n24\n23\n21\n25\n22\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_ValueOf_IsValid()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    v := reflect.ValueOf(42)
    fmt.Println(v.IsValid())
    fmt.Println(v.IsNil())
}");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_ValueOf_IsZero()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    fmt.Println(reflect.ValueOf(0).IsZero())
    fmt.Println(reflect.ValueOf(42).IsZero())
    fmt.Println(reflect.ValueOf("""").IsZero())
    fmt.Println(reflect.ValueOf(""hello"").IsZero())
    fmt.Println(reflect.ValueOf(false).IsZero())
    fmt.Println(reflect.ValueOf(true).IsZero())
}");
        Assert.AreEqual("true\nfalse\ntrue\nfalse\ntrue\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_Type_String()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    fmt.Println(reflect.TypeOf(42).String())
    fmt.Println(reflect.TypeOf(""hi"").String())
    fmt.Println(reflect.TypeOf(3.14).String())
    fmt.Println(reflect.TypeOf(true).String())
}");
        Assert.AreEqual("int\nstring\nfloat64\nbool\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_Type_Comparable()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    fmt.Println(reflect.TypeOf(42).Comparable())
    fmt.Println(reflect.TypeOf(""hi"").Comparable())
    fmt.Println(reflect.TypeOf(true).Comparable())
}");
        Assert.AreEqual("true\ntrue\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Reflect_ValueOf_Interface()
    {
        var output = Run(@"
package main
import ""fmt""
import ""reflect""
func main() {
    v := reflect.ValueOf(42)
    x := v.Interface()
    fmt.Println(x)
}");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    // ================================================================
    // GoModuleResolver
    // ================================================================

    [TestMethod]
    public void GoModuleResolver_parses_require()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngo_test_gomod_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "go.mod"),
                "module example.com/myapp\n\nrequire github.com/pkg/errors v0.9.1\n");

            var resolver = new GoModuleResolver();
            resolver.LoadGoMod(dir);

            Assert.AreEqual("example.com/myapp", resolver.ModuleName);
            Assert.AreEqual(dir, resolver.ModuleRoot);
            Assert.AreEqual(1, resolver.Requirements.Count);
            Assert.IsTrue(resolver.Requirements.ContainsKey("github.com/pkg/errors"));
            Assert.AreEqual("v0.9.1", resolver.Requirements["github.com/pkg/errors"]);

            var match = resolver.FindModule("github.com/pkg/errors");
            Assert.IsNotNull(match);
            Assert.AreEqual("github.com/pkg/errors", match!.Module);

            var match2 = resolver.FindModule("github.com/pkg/errors/sub");
            Assert.IsNotNull(match2);
            Assert.AreEqual("github.com/pkg/errors", match2!.Module);

            var noMatch = resolver.FindModule("github.com/other/pkg");
            Assert.IsNull(noMatch);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void GoModuleResolver_parses_require_block()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngo_test_gomod_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "go.mod"),
                "module example.com/myapp\n\nrequire (\n\tgithub.com/pkg/errors v0.9.1\n\tgolang.org/x/text v0.3.7 // indirect\n)\n");

            var resolver = new GoModuleResolver();
            resolver.LoadGoMod(dir);

            Assert.AreEqual(2, resolver.Requirements.Count);
            Assert.AreEqual("v0.9.1", resolver.Requirements["github.com/pkg/errors"]);
            Assert.AreEqual("v0.3.7", resolver.Requirements["golang.org/x/text"]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void GoModuleResolver_parses_replace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngo_test_gomod_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "go.mod"),
                "module example.com/myapp\n\nrequire github.com/foo/bar v1.0.0\n\nreplace github.com/foo/bar => ../localbar\n");

            var resolver = new GoModuleResolver();
            resolver.LoadGoMod(dir);

            Assert.AreEqual(1, resolver.Requirements.Count);
            Assert.AreEqual(1, resolver.Replaces.Count);
            Assert.AreEqual("../localbar", resolver.Replaces["github.com/foo/bar"]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void GoModuleResolver_parses_replace_block()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngo_test_gomod_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "go.mod"),
                "module example.com/myapp\n\nreplace (\n\tgithub.com/foo/bar => ../localbar\n\tgithub.com/baz/qux v1.0.0 => ./vendor/qux\n)\n");

            var resolver = new GoModuleResolver();
            resolver.LoadGoMod(dir);

            Assert.AreEqual(2, resolver.Replaces.Count);
            Assert.AreEqual("../localbar", resolver.Replaces["github.com/foo/bar"]);
            Assert.AreEqual("./vendor/qux", resolver.Replaces["github.com/baz/qux"]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void GoModuleResolver_replace_local_path_resolves()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngo_test_gomod_" + Guid.NewGuid().ToString("N"));
        var libDir = Path.Combine(dir, "mylib");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(libDir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "go.mod"),
                "module example.com/myapp\n\nrequire github.com/foo/mylib v1.0.0\n\nreplace github.com/foo/mylib => ./mylib\n");
            File.WriteAllText(Path.Combine(libDir, "lib.go"),
                "package mylib\n\nfunc Hello() string { return \"hi\" }\n");

            var resolver = new GoModuleResolver();
            resolver.LoadGoMod(dir);

            var pkgDir = resolver.ResolvePackageDir("github.com/foo/mylib", "github.com/foo/mylib", "v1.0.0");
            Assert.IsNotNull(pkgDir);
            Assert.IsTrue(File.Exists(Path.Combine(pkgDir!, "lib.go")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void External_module_replace_local_compile_and_run()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "ngo_test_replace_" + Guid.NewGuid().ToString("N"));
        var libDir = Path.Combine(projectDir, "localutil");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(libDir);

        try
        {
            File.WriteAllText(Path.Combine(libDir, "util.go"),
                "package localutil\n\nfunc Double(x int) int {\n\treturn x * 2\n}\n");

            File.WriteAllText(Path.Combine(projectDir, "go.mod"),
                "module example.com/repltest\n\nrequire github.com/fake/util v0.0.0\n\nreplace github.com/fake/util => ./localutil\n");
            File.WriteAllText(Path.Combine(projectDir, "main.go"),
                "package main\n\nimport (\n\t\"fmt\"\n\t\"github.com/fake/util\"\n)\n\nfunc main() {\n\tfmt.Println(localutil.Double(5))\n}\n");

            var compilation = new CompilationContext(projectDir);

            var tree = SyntaxTree.Parse(File.ReadAllText(Path.Combine(projectDir, "main.go")));
            var result = SemanticAnalyzer.Analyze(tree, compilation);
            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

            var assembly = AssemblyEmitter.Emit(result, compilation);
            var entryPoint = AssemblyEmitter.FindEntryPoint(assembly);
            Assert.IsNotNull(entryPoint);

            var oldOut = Console.Out;
            var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                entryPoint.Invoke(null, null);
            }
            finally
            {
                Console.SetOut(oldOut);
            }

            Assert.AreEqual("10\n", sw.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            try { Directory.Delete(projectDir, true); } catch { }
        }
    }

    [TestMethod]
    public void GoModuleResolver_escape_module_path()
    {
        Assert.AreEqual("github.com/!azure/azure-sdk", GoModuleResolver.EscapeModulePath("github.com/Azure/azure-sdk"));
        Assert.AreEqual("github.com/pkg/errors", GoModuleResolver.EscapeModulePath("github.com/pkg/errors"));
    }

    [TestMethod]
    public void GoModuleResolver_download_and_resolve()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngo_test_gomod_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "go.mod"),
                "module example.com/myapp\n\nrequire github.com/pkg/errors v0.9.1\n");

            var resolver = new GoModuleResolver();
            resolver.LoadGoMod(dir);

            var pkgDir = resolver.ResolvePackageDir("github.com/pkg/errors", "github.com/pkg/errors", "v0.9.1");
            Assert.IsNotNull(pkgDir, "ResolvePackageDir returned null");
            Assert.IsTrue(Directory.Exists(pkgDir), $"Directory does not exist: {pkgDir}");

            // Should contain .go files
            var goFiles = Directory.GetFiles(pkgDir!, "*.go");
            Assert.IsTrue(goFiles.Length > 0, "No .go files found in resolved directory");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void External_module_resolution_via_PackageRegistry()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngo_test_gomod_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "go.mod"),
                "module example.com/myapp\n\nrequire github.com/pkg/errors v0.9.1\n");

            var moduleResolver = new GoModuleResolver();
            moduleResolver.LoadGoMod(dir);

            // Verify the module resolver found the right module
            var match = moduleResolver.FindModule("github.com/pkg/errors");
            Assert.IsNotNull(match);
            Assert.AreEqual("github.com/pkg/errors", match!.Module);
            Assert.AreEqual("v0.9.1", match.Version);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [TestMethod]
    public void External_module_compile_and_run()
    {
        // Create a fake external module in the cache
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ngo", "mod", "cache", "github.com", "test", "mathutil@v1.0.0");
        Directory.CreateDirectory(cacheDir);

        var projectDir = Path.Combine(Path.GetTempPath(), "ngo_test_ext_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDir);

        try
        {
            // Write the external library
            File.WriteAllText(Path.Combine(cacheDir, "math.go"),
                "package mathutil\n\nfunc Triple(x int) int {\n\treturn x * 3\n}\n\nfunc Sum(a, b int) int {\n\treturn a + b\n}\n");

            // Write the project
            File.WriteAllText(Path.Combine(projectDir, "go.mod"),
                "module example.com/exttest\n\nrequire github.com/test/mathutil v1.0.0\n");
            File.WriteAllText(Path.Combine(projectDir, "main.go"),
                "package main\n\nimport (\n\t\"fmt\"\n\t\"github.com/test/mathutil\"\n)\n\nfunc main() {\n\tfmt.Println(mathutil.Triple(7))\n\tfmt.Println(mathutil.Sum(10, 20))\n}\n");

            var compilation = new CompilationContext(projectDir);

            var tree = SyntaxTree.Parse(File.ReadAllText(Path.Combine(projectDir, "main.go")));
            var result = SemanticAnalyzer.Analyze(tree, compilation);
            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

            var assembly = AssemblyEmitter.Emit(result, compilation);
            var entryPoint = AssemblyEmitter.FindEntryPoint(assembly);
            Assert.IsNotNull(entryPoint);

            var oldOut = Console.Out;
            var sw = new StringWriter();
            Console.SetOut(sw);
            try
            {
                entryPoint.Invoke(null, null);
            }
            finally
            {
                Console.SetOut(oldOut);
            }

            Assert.AreEqual("21\n30\n", sw.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            try { Directory.Delete(projectDir, true); } catch { }
            try { Directory.Delete(cacheDir, true); } catch { }
        }
    }

    // ================================================================

    private static string RunWithStdin(string stdinContent, string goSource)
    {
        var tree = SyntaxTree.Parse(goSource);
        var ctx = new CompilationContext(null);
        var result = SemanticAnalyzer.Analyze(tree, ctx);

        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

        var assembly = AssemblyEmitter.Emit(result, ctx);
        var entryPoint = AssemblyEmitter.FindEntryPoint(assembly);
        Assert.IsNotNull(entryPoint);

        var oldOut = Console.Out;
        var oldIn = Console.In;
        var sw = new StringWriter();
        var sr = new StringReader(stdinContent);
        Console.SetOut(sw);
        Console.SetIn(sr);
        try
        {
            entryPoint.Invoke(null, null);
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetIn(oldIn);
        }

        return sw.ToString().Replace("\r\n", "\n");
    }
}
