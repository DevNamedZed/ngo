// -----------------------------------------------------------------------
// <copyright file="EmitTests.cs" company="Ziad">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Emit;

[TestClass]
public class EmitTests
{
    private static readonly string TestProjectRoot = Path.Combine(Path.GetTempPath(), "ngo-test-project");

    static EmitTests()
    {
        Directory.CreateDirectory(TestProjectRoot);
    }

    [TestInitialize]
    public void CleanCache()
    {
        var cacheDir = NgoArchive.GetCacheDir(TestProjectRoot);
        if (Directory.Exists(cacheDir))
        {
            Directory.Delete(cacheDir, recursive: true);
        }
    }

    private static string Run(string goSource)
    {
        var tree = SyntaxTree.Parse(goSource);
        var ctx = new CompilationContext(TestProjectRoot);
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

    private static (string stdout, string stderr) RunWithStderr(string goSource)
    {
        var tree = SyntaxTree.Parse(goSource);
        var ctx = new CompilationContext(TestProjectRoot);
        var result = SemanticAnalyzer.Analyze(tree, ctx);

        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

        var assembly = AssemblyEmitter.Emit(result, ctx);
        var entryPoint = AssemblyEmitter.FindEntryPoint(assembly);
        Assert.IsNotNull(entryPoint);

        var oldOut = Console.Out;
        var oldErr = Console.Error;
        var swOut = new StringWriter();
        var swErr = new StringWriter();
        Console.SetOut(swOut);
        Console.SetError(swErr);
        try
        {
            entryPoint.Invoke(null, null);
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }

        return (swOut.ToString().Replace("\r\n", "\n"), swErr.ToString().Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Hello_world()
    {
        var output = Run(@"
package main

func main() {
    println(""hello, world"")
}
");
        Assert.AreEqual("hello, world\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Empty_main()
    {
        var output = Run(@"
package main

func main() {
}
");
        Assert.AreEqual("", output);
    }

    [TestMethod]
    public void Print_multiple_args()
    {
        var output = Run(@"
package main

func main() {
    println(""a"", ""b"", ""c"")
}
");
        Assert.AreEqual("a b c\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Print_integer()
    {
        var output = Run(@"
package main

func main() {
    println(42)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Print_bool()
    {
        var output = Run(@"
package main

func main() {
    println(true)
    println(false)
}
");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Var_and_print()
    {
        var output = Run(@"
package main

func main() {
    var x int = 42
    println(x)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Short_var_and_print()
    {
        var output = Run(@"
package main

func main() {
    x := 10
    println(x)
}
");
        Assert.AreEqual("10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Arithmetic()
    {
        var output = Run(@"
package main

func main() {
    x := 10
    y := 3
    println(x + y)
    println(x - y)
    println(x * y)
}
");
        Assert.AreEqual("13\n7\n30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void String_concatenation()
    {
        var output = Run(@"
package main

func main() {
    s := ""hello"" + "" "" + ""world""
    println(s)
}
");
        Assert.AreEqual("hello world\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Assignment()
    {
        var output = Run(@"
package main

func main() {
    x := 1
    x = 2
    println(x)
}
");
        Assert.AreEqual("2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Inc_dec()
    {
        var output = Run(@"
package main

func main() {
    x := 5
    x++
    println(x)
    x--
    x--
    println(x)
}
");
        Assert.AreEqual("6\n4\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void If_else()
    {
        var output = Run(@"
package main

func main() {
    x := 10
    if x > 5 {
        println(""big"")
    } else {
        println(""small"")
    }
}
");
        Assert.AreEqual("big\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_loop()
    {
        var output = Run(@"
package main

func main() {
    for i := 0; i < 3; i++ {
        println(i)
    }
}
");
        Assert.AreEqual("0\n1\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Function_call()
    {
        var output = Run(@"
package main

func add(a int, b int) int {
    return a + b
}

func main() {
    println(add(3, 4))
}
");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_calls()
    {
        var output = Run(@"
package main

func double(x int) int {
    return x * 2
}

func main() {
    println(double(double(3)))
}
");
        Assert.AreEqual("12\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Comparison_operators()
    {
        var output = Run(@"
package main

func main() {
    println(1 == 1)
    println(1 != 2)
    println(3 < 5)
    println(5 > 3)
    println(3 <= 3)
    println(3 >= 3)
}
");
        Assert.AreEqual("true\ntrue\ntrue\ntrue\ntrue\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Logical_operators()
    {
        var output = Run(@"
package main

func main() {
    println(true && true)
    println(true && false)
    println(false || true)
    println(false || false)
    println(!true)
}
");
        Assert.AreEqual("true\nfalse\ntrue\nfalse\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Break_continue()
    {
        var output = Run(@"
package main

func main() {
    for i := 0; i < 10; i++ {
        if i == 3 {
            continue
        }
        if i == 5 {
            break
        }
        println(i)
    }
}
");
        Assert.AreEqual("0\n1\n2\n4\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Switch_statement()
    {
        var output = Run(@"
package main

func main() {
    x := 2
    switch x {
    case 1:
        println(""one"")
    case 2:
        println(""two"")
    case 3:
        println(""three"")
    default:
        println(""other"")
    }
}
");
        Assert.AreEqual("two\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fizzbuzz()
    {
        var output = Run(@"
package main

func main() {
    for i := 1; i <= 15; i++ {
        if i % 15 == 0 {
            println(""FizzBuzz"")
        } else if i % 3 == 0 {
            println(""Fizz"")
        } else if i % 5 == 0 {
            println(""Buzz"")
        } else {
            println(i)
        }
    }
}
");
        var expected = "1\n2\nFizz\n4\nBuzz\nFizz\n7\n8\nFizz\nBuzz\n11\nFizz\n13\n14\nFizzBuzz\n";
        Assert.AreEqual(expected, output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Struct_literal_and_field_access()
    {
        var output = Run(@"
package main

type Point struct {
    X int
    Y int
}

func main() {
    p := Point{X: 10, Y: 20}
    println(p.X)
    println(p.Y)
}
");
        Assert.AreEqual("10\n20\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Struct_field_assignment()
    {
        var output = Run(@"
package main

type Point struct {
    X int
    Y int
}

func main() {
    var p Point
    p.X = 5
    p.Y = 10
    println(p.X + p.Y)
}
");
        Assert.AreEqual("15\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Method_declaration_and_call()
    {
        var output = Run(@"
package main

type Point struct {
    X int
    Y int
}

func (p Point) Sum() int {
    return p.X + p.Y
}

func main() {
    pt := Point{X: 3, Y: 4}
    println(pt.Sum())
}
");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Slice_literal_and_len()
    {
        var output = Run(@"
package main

func main() {
    s := []int{10, 20, 30}
    println(len(s))
    println(s[0])
    println(s[1])
    println(s[2])
}
");
        Assert.AreEqual("3\n10\n20\n30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Slice_append()
    {
        var output = Run(@"
package main

func main() {
    s := []int{1, 2}
    s = append(s, 3)
    println(len(s))
    println(s[2])
}
");
        Assert.AreEqual("3\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Map_literal_and_access()
    {
        var output = Run(@"
package main

func main() {
    m := map[string]int{""a"": 1, ""b"": 2}
    println(m[""a""])
    println(m[""b""])
}
");
        Assert.AreEqual("1\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Constants()
    {
        var output = Run(@"
package main

const x = 42
const greeting = ""hello""

func main() {
    println(x)
    println(greeting)
}
");
        Assert.AreEqual("42\nhello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Multiple_functions()
    {
        var output = Run(@"
package main

func greet(name string) string {
    return ""Hello, "" + name + ""!""
}

func main() {
    println(greet(""World""))
}
");
        Assert.AreEqual("Hello, World!\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_range_slice()
    {
        var output = Run(@"
package main

func main() {
    s := []int{10, 20, 30}
    for _, v := range s {
        println(v)
    }
}
");
        Assert.AreEqual("10\n20\n30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Conversion_int_to_float()
    {
        var output = Run(@"
package main

func main() {
    x := 42
    f := float64(x)
    println(f)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Multiple_return_values()
    {
        var output = Run(@"
package main

func divmod(a int, b int) (int, int) {
    return a / b, a % b
}

func main() {
    q, r := divmod(17, 5)
    println(q)
    println(r)
}
");
        Assert.AreEqual("3\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Multiple_return_assignment()
    {
        var output = Run(@"
package main

func swap(a int, b int) (int, int) {
    return b, a
}

func main() {
    x := 10
    y := 20
    x, y = swap(x, y)
    println(x)
    println(y)
}
");
        Assert.AreEqual("20\n10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Multiple_return_blank_identifier()
    {
        var output = Run(@"
package main

func divmod(a int, b int) (int, int) {
    return a / b, a % b
}

func main() {
    _, r := divmod(17, 5)
    println(r)
}
");
        Assert.AreEqual("2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Recursive_fibonacci()
    {
        var output = Run(@"
package main

func fib(n int) int {
    if n <= 1 {
        return n
    }
    return fib(n - 1) + fib(n - 2)
}

func main() {
    for i := 0; i < 10; i++ {
        println(fib(i))
    }
}
");
        Assert.AreEqual("0\n1\n1\n2\n3\n5\n8\n13\n21\n34\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Package_level_variable()
    {
        var output = Run(@"
package main

var x int = 42

func main() {
    println(x)
    x = 100
    println(x)
}
");
        Assert.AreEqual("42\n100\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Complex_struct_with_methods_and_slices()
    {
        var output = Run(@"
package main

type Point struct {
    X int
    Y int
}

func (p Point) Distance() int {
    return p.X * p.X + p.Y * p.Y
}

func maxPoint(points []Point) Point {
    best := points[0]
    bestDist := best.Distance()
    for i := 1; i < len(points); i++ {
        d := points[i].Distance()
        if d > bestDist {
            best = points[i]
            bestDist = d
        }
    }
    return best
}

func main() {
    points := []Point{Point{X: 1, Y: 2}, Point{X: 3, Y: 4}, Point{X: 0, Y: 1}}
    best := maxPoint(points)
    println(best.X, best.Y)
}
");
        Assert.AreEqual("3 4\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Slice_index_assignment()
    {
        var output = Run(@"
package main

func main() {
    s := []int{1, 2, 3}
    s[1] = 42
    println(s[0])
    println(s[1])
    println(s[2])
}
");
        Assert.AreEqual("1\n42\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Map_assignment_and_delete()
    {
        var output = Run(@"
package main

func main() {
    m := map[string]int{""a"": 1, ""b"": 2}
    m[""c""] = 3
    println(m[""a""])
    println(m[""c""])
    println(len(m))
}
");
        Assert.AreEqual("1\n3\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void While_loop()
    {
        var output = Run(@"
package main

func main() {
    x := 1
    for x < 100 {
        x = x * 2
    }
    println(x)
}
");
        Assert.AreEqual("128\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_for_loops()
    {
        var output = Run(@"
package main

func main() {
    for i := 0; i < 3; i++ {
        for j := 0; j < 3; j++ {
            if i == j {
                println(i)
            }
        }
    }
}
");
        Assert.AreEqual("0\n1\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Make_slice()
    {
        var output = Run(@"
package main

func main() {
    s := make([]int, 3)
    s[0] = 10
    s[1] = 20
    s[2] = 30
    println(len(s))
    println(s[1])
}
");
        Assert.AreEqual("3\n20\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Three_return_values()
    {
        var output = Run(@"
package main

func minMaxSum(a int, b int, c int) (int, int, int) {
    min := a
    max := a
    if b < min { min = b }
    if c < min { min = c }
    if b > max { max = b }
    if c > max { max = c }
    return min, max, a + b + c
}

func main() {
    mn, mx, s := minMaxSum(3, 1, 2)
    println(mn)
    println(mx)
    println(s)
}
");
        Assert.AreEqual("1\n3\n6\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Switch_default()
    {
        var output = Run(@"
package main

func main() {
    x := 99
    switch x {
    case 1:
        println(""one"")
    case 2:
        println(""two"")
    default:
        println(""other"")
    }
}
");
        Assert.AreEqual("other\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_basic_ordering()
    {
        var output = Run(@"
package main

func main() {
    defer println(""third"")
    defer println(""second"")
    defer println(""first"")
    println(""start"")
}
");
        Assert.AreEqual("start\nfirst\nsecond\nthird\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_with_user_function()
    {
        var output = Run(@"
package main

func greet(name string) {
    println(""hello"", name)
}

func main() {
    defer greet(""world"")
    println(""before"")
}
");
        Assert.AreEqual("before\nhello world\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_no_args()
    {
        var output = Run(@"
package main

func done() {
    println(""done"")
}

func main() {
    defer done()
    println(""working"")
}
");
        Assert.AreEqual("working\ndone\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Channel_send_receive()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int, 1)
    ch <- 42
    v := <-ch
    println(v)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Channel_buffered_multiple()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int, 3)
    ch <- 10
    ch <- 20
    ch <- 30
    a := <-ch
    b := <-ch
    c := <-ch
    println(a, b, c)
}
");
        Assert.AreEqual("10 20 30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Channel_string()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan string, 1)
    ch <- ""hello""
    msg := <-ch
    println(msg)
}
");
        Assert.AreEqual("hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Go_basic_goroutine()
    {
        var output = Run(@"
package main

func worker(ch chan int) {
    ch <- 99
}

func main() {
    ch := make(chan int)
    go worker(ch)
    v := <-ch
    println(v)
}
");
        Assert.AreEqual("99\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_range_map()
    {
        var output = Run(@"
package main

func main() {
    m := map[string]int{""x"": 10}
    for k, v := range m {
        println(k, v)
    }
}
");
        Assert.AreEqual("x 10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_range_string()
    {
        var output = Run(@"
package main

func main() {
    for _, r := range ""AB"" {
        println(r)
    }
}
");
        Assert.AreEqual("65\n66\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Pointer_deref_read()
    {
        var output = Run(@"
package main

func main() {
    x := 42
    p := &x
    println(*p)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Pointer_deref_write()
    {
        var output = Run(@"
package main

func main() {
    x := 10
    p := &x
    *p = 20
    println(*p)
}
");
        Assert.AreEqual("20\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Const_declaration()
    {
        var output = Run(@"
package main

const pi = 3

func main() {
    println(pi)
}
");
        Assert.AreEqual("3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Const_iota()
    {
        var output = Run(@"
package main

const (
    a = iota
    b
    c
)

func main() {
    println(a, b, c)
}
");
        Assert.AreEqual("0 1 2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Type_switch_basic()
    {
        var output = Run(@"
package main

func main() {
    var x interface{} = 42
    switch v := x.(type) {
    case int:
        println(""int"", v)
    case string:
        println(""string"", v)
    default:
        println(""other"")
    }
}
");
        Assert.AreEqual("int 42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Type_switch_string_case()
    {
        var output = Run(@"
package main

func main() {
    var x interface{} = ""hello""
    switch v := x.(type) {
    case int:
        println(""int"", v)
    case string:
        println(""string"", v)
    default:
        println(""other"")
    }
}
");
        Assert.AreEqual("string hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Type_switch_default()
    {
        var output = Run(@"
package main

func main() {
    var x interface{} = true
    switch x.(type) {
    case int:
        println(""int"")
    case string:
        println(""string"")
    default:
        println(""other"")
    }
}
");
        Assert.AreEqual("other\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Interface_empty_int()
    {
        var output = Run(@"
package main

func main() {
    var x interface{} = 42
    println(x)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Interface_empty_string()
    {
        var output = Run(@"
package main

func main() {
    var x interface{} = ""hello""
    println(x)
}
");
        Assert.AreEqual("hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Interface_type_assert()
    {
        var output = Run(@"
package main

func main() {
    var x interface{} = 42
    v := x.(int)
    println(v + 1)
}
");
        Assert.AreEqual("43\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Interface_method_call()
    {
        var output = Run(@"
package main

type Stringer interface {
    String() string
}

type Greeter struct {
    Name string
}

func (g Greeter) String() string {
    return g.Name
}

func main() {
    var s Stringer = Greeter{Name: ""world""}
    println(s.String())
}
");
        Assert.AreEqual("world\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_capture_local()
    {
        var output = Run(@"
package main

func main() {
    x := 10
    f := func() int { return x + 1 }
    println(f())
}
");
        Assert.AreEqual("11\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_capture_multiple()
    {
        var output = Run(@"
package main

func main() {
    a := 3
    b := 4
    f := func() int { return a + b }
    println(f())
}
");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_with_return()
    {
        var output = Run(@"
package main

func makeAdder(x int) func(int) int {
    return func(y int) int { return x + y }
}

func main() {
    add5 := makeAdder(5)
    println(add5(3))
}
");
        Assert.AreEqual("8\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Variadic_sum()
    {
        var output = Run(@"
package main

func sum(nums ...int) int {
    total := 0
    for _, n := range nums {
        total = total + n
    }
    return total
}

func main() {
    println(sum(1, 2, 3))
    println(sum(10, 20))
    println(sum())
}
");
        Assert.AreEqual("6\n30\n0\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Variadic_with_required_params()
    {
        var output = Run(@"
package main

func add(base int, nums ...int) int {
    total := base
    for _, n := range nums {
        total = total + n
    }
    return total
}

func main() {
    println(add(100, 1, 2, 3))
    println(add(100))
}
");
        Assert.AreEqual("106\n100\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Variadic_no_args()
    {
        var output = Run(@"
package main

func count(items ...int) int {
    return len(items)
}

func main() {
    println(count())
    println(count(1))
    println(count(1, 2, 3, 4))
}
");
        Assert.AreEqual("0\n1\n4\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Named_return_single()
    {
        var output = Run(@"
package main

func double(x int) (result int) {
    result = x * 2
    return
}

func main() {
    println(double(5))
    println(double(21))
}
");
        Assert.AreEqual("10\n42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Named_return_multiple()
    {
        var output = Run(@"
package main

func swap(a int, b int) (x int, y int) {
    x = b
    y = a
    return
}

func main() {
    a, b := swap(1, 2)
    println(a, b)
}
");
        Assert.AreEqual("2 1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Named_return_zero_value()
    {
        var output = Run(@"
package main

func maybeDouble(x int, doIt bool) (result int) {
    if doIt {
        result = x * 2
    }
    return
}

func main() {
    println(maybeDouble(5, true))
    println(maybeDouble(5, false))
}
");
        Assert.AreEqual("10\n0\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void String_indexing_and_slicing()
    {
        var output = Run(@"
package main

func main() {
    s := ""Hello""
    println(s[0])
    println(s[1])
    sub := s[1:4]
    println(sub)
}
");
        Assert.AreEqual("72\n101\nell\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_range_channel()
    {
        var output = Run(@"
package main

func producer(ch chan int) {
    ch <- 10
    ch <- 20
    ch <- 30
    close(ch)
}

func main() {
    ch := make(chan int)
    go producer(ch)
    for v := range ch {
        println(v)
    }
}
");
        Assert.AreEqual("10\n20\n30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Method_with_return_value()
    {
        var output = Run(@"
package main

type Rect struct {
    W int
    H int
}

func (r Rect) Area() int {
    return r.W * r.H
}

func main() {
    r := Rect{W: 3, H: 4}
    a := r.Area()
    println(a)
}
");
        Assert.AreEqual("12\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Tagless_switch()
    {
        var output = Run(@"
package main

func classify(x int) int {
    switch {
    case x < 0:
        return -1
    case x == 0:
        return 0
    default:
        return 1
    }
}

func main() {
    println(classify(-5))
    println(classify(0))
    println(classify(7))
}
");
        Assert.AreEqual("-1\n0\n1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_println()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Println(""hello"", ""world"")
}
");
        StringAssert.Contains(output, "hello");
        StringAssert.Contains(output, "world");
    }

    [TestMethod]
    public void Defer_func_literal()
    {
        var output = Run(@"
package main

func main() {
    println(""start"")
    defer func() {
        println(""deferred"")
    }()
    println(""end"")
}
");
        Assert.AreEqual("start\nend\ndeferred\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Go_func_literal()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int)
    x := 42
    go func() {
        ch <- x
    }()
    v := <-ch
    println(v)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Select_with_default()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int)
    select {
    case <-ch:
        println(""received"")
    default:
        println(""default"")
    }
}
");
        Assert.AreEqual("default\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Select_send_and_receive()
    {
        var output = Run(@"
package main

func sender(ch chan int) {
    ch <- 42
}

func main() {
    ch := make(chan int)
    go sender(ch)
    select {
    case v := <-ch:
        println(v)
    }
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Pointer_receiver_method()
    {
        var output = Run(@"
package main

type Counter struct {
    N int
}

func (c *Counter) Inc() {
    c.N = c.N + 1
}

func main() {
    c := Counter{N: 0}
    c.Inc()
    c.Inc()
    c.Inc()
    println(c.N)
}
");
        Assert.AreEqual("3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Pointer_receiver_slice_field_nil_check()
    {
        var output = Run(@"
package main

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

func main() {
    b := Buf{S: ""hello""}
    b.AppendByte(72)
    b.AppendByte(105)
    println(string(b.Data[:b.W]))
}
");
        Assert.AreEqual("Hi\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Pointer_receiver_on_pointer()
    {
        var output = Run(@"
package main

type Point struct {
    X int
    Y int
}

func (p *Point) Translate(dx int, dy int) {
    p.X = p.X + dx
    p.Y = p.Y + dy
}

func main() {
    p := &Point{X: 1, Y: 2}
    p.Translate(10, 20)
    println(p.X)
    println(p.Y)
}
");
        Assert.AreEqual("11\n22\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Select_buffered_send()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int, 1)
    select {
    case ch <- 99:
        println(""sent"")
    default:
        println(""blocked"")
    }
    v := <-ch
    println(v)
}
");
        Assert.AreEqual("sent\n99\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Short_var_redeclaration_multi_return()
    {
        var output = Run(@"
package main

func pair() (int, string) {
    return 42, ""hello""
}

func main() {
    x := 10
    x, y := pair()
    println(x)
    println(y)
}
");
        Assert.AreEqual("42\nhello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Short_var_redeclaration_pair()
    {
        var output = Run(@"
package main

func main() {
    x := 1
    x, y := 2, 3
    println(x)
    println(y)
}
");
        Assert.AreEqual("2\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Multiple_package_level_vars()
    {
        var output = Run(@"
package main

var (
    a = 10
    b = 20
    c = a + b
)

func main() {
    println(a)
    println(b)
    println(c)
}
");
        Assert.AreEqual("10\n20\n30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_println_multiple_args()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Println(""hello"", ""world"")
    fmt.Println(1, 2, 3)
}
");
        Assert.AreEqual("hello world\n1 2 3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_printf_basic()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Printf(""%s is %d years old\n"", ""Alice"", 30)
}
");
        Assert.AreEqual("Alice is 30 years old\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_sprintf()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    s := fmt.Sprintf(""x=%d y=%d"", 10, 20)
    println(s)
}
");
        Assert.AreEqual("x=10 y=20\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_printf_verbs()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Printf(""%v %v %v\n"", 42, ""hi"", true)
}
");
        Assert.AreEqual("42 hi true\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Go_func_literal_with_args()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int)
    go func(x int) {
        ch <- x * 2
    }(21)
    v := <-ch
    println(v)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_func_literal_with_args()
    {
        var output = Run(@"
package main

func main() {
    x := 10
    defer func(v int) {
        println(v)
    }(x)
    x = 20
    println(x)
}
");
        // defer evaluates args eagerly, so v=10, then x=20 prints first, then deferred v=10
        Assert.AreEqual("20\n10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Go_func_literal_with_args_and_capture()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int)
    y := 100
    go func(x int) {
        ch <- x + y
    }(42)
    v := <-ch
    println(v)
}
");
        Assert.AreEqual("142\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strconv_itoa()
    {
        var output = Run(@"
package main

import ""strconv""

func main() {
    s := strconv.Itoa(42)
    println(s)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strconv_atoi()
    {
        var output = Run(@"
package main

import ""strconv""

func main() {
    n, err := strconv.Atoi(""123"")
    println(n)
    if err != nil {
        println(err)
    } else {
        println(""ok"")
    }
}
");
        Assert.AreEqual("123\nok\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_contains()
    {
        var output = Run(@"
package main

import ""strings""

func main() {
    println(strings.Contains(""hello world"", ""world""))
    println(strings.Contains(""hello"", ""xyz""))
}
");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_has_prefix_suffix()
    {
        var output = Run(@"
package main

import ""strings""

func main() {
    println(strings.HasPrefix(""hello"", ""hel""))
    println(strings.HasSuffix(""hello"", ""llo""))
}
");
        Assert.AreEqual("true\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_to_upper_lower()
    {
        var output = Run(@"
package main

import ""strings""

func main() {
    println(strings.ToUpper(""hello""))
    println(strings.ToLower(""WORLD""))
}
");
        Assert.AreEqual("HELLO\nworld\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_replace_all()
    {
        var output = Run(@"
package main

import ""strings""

func main() {
    s := strings.ReplaceAll(""aabbcc"", ""bb"", ""XX"")
    println(s)
}
");
        Assert.AreEqual("aaXXcc\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_sqrt()
    {
        var output = Run(@"
package main

import (
    ""math""
    ""fmt""
)

func main() {
    fmt.Printf(""%v\n"", math.Sqrt(16.0))
}
");
        Assert.AreEqual("4\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fizzbuzz_with_fmt()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    for i := 1; i <= 15; i++ {
        if i % 15 == 0 {
            fmt.Println(""FizzBuzz"")
        } else if i % 3 == 0 {
            fmt.Println(""Fizz"")
        } else if i % 5 == 0 {
            fmt.Println(""Buzz"")
        } else {
            fmt.Println(i)
        }
    }
}
");
        var expected = "1\n2\nFizz\n4\nBuzz\nFizz\n7\n8\nFizz\nBuzz\n11\nFizz\n13\n14\nFizzBuzz\n";
        Assert.AreEqual(expected, output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fibonacci_recursive()
    {
        var output = Run(@"
package main

func fib(n int) int {
    if n <= 1 {
        return n
    }
    return fib(n-1) + fib(n-2)
}

func main() {
    for i := 0; i < 10; i++ {
        println(fib(i))
    }
}
");
        Assert.AreEqual("0\n1\n1\n2\n3\n5\n8\n13\n21\n34\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Concurrent_counter()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int)
    for i := 0; i < 5; i++ {
        go func(n int) {
            ch <- n * n
        }(i)
    }
    sum := 0
    for i := 0; i < 5; i++ {
        sum = sum + <-ch
    }
    println(sum)
}
");
        // 0 + 1 + 4 + 9 + 16 = 30
        Assert.AreEqual("30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Interface_function_param()
    {
        var output = Run(@"
package main

type Stringer interface {
    String() string
}

type Greeter struct {
    Name string
}

func (g Greeter) String() string {
    return g.Name
}

func printString(s Stringer) {
    println(s.String())
}

func main() {
    g := Greeter{Name: ""hello""}
    printString(g)
}
");
        Assert.AreEqual("hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void String_escape_sequences()
    {
        var output = Run(@"
package main

func main() {
    println(""hello\tworld"")
    println(""line1\nline2"")
}
");
        Assert.AreEqual("hello\tworld\nline1\nline2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sync_waitgroup_no_goroutine()
    {
        var output = Run(@"
package main

import ""sync""

func main() {
    var wg sync.WaitGroup
    wg.Add(1)
    println(""added"")
    wg.Done()
    println(""done"")
    wg.Wait()
    println(""waited"")
}
");
        Assert.AreEqual("added\ndone\nwaited\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sync_mutex_basic()
    {
        var output = Run(@"
package main

import ""sync""

func main() {
    var mu sync.Mutex
    mu.Lock()
    println(""locked"")
    mu.Unlock()
    println(""unlocked"")
}
");
        Assert.AreEqual("locked\nunlocked\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sync_waitgroup_with_goroutine()
    {
        var output = Run(@"
package main

import ""sync""

func main() {
    var wg sync.WaitGroup
    wg.Add(1)
    go func() {
        println(""worker"")
        wg.Done()
    }()
    wg.Wait()
    println(""done"")
}
");
        var lines = output.Replace("\r\n", "\n").Trim().Split('\n');
        Assert.AreEqual("worker", lines[0]);
        Assert.AreEqual("done", lines[1]);
    }

    [TestMethod]
    public void Sync_defer_unlock()
    {
        var output = Run(@"
package main

import ""sync""

func main() {
    var mu sync.Mutex
    mu.Lock()
    defer mu.Unlock()
    println(""locked"")
}
");
        Assert.AreEqual("locked\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sync_defer_in_closure()
    {
        var output = Run(@"
package main

import ""sync""

func main() {
    var wg sync.WaitGroup
    wg.Add(1)
    f := func() {
        defer wg.Done()
        println(""in closure"")
    }
    f()
    wg.Wait()
    println(""done"")
}
");
        Assert.AreEqual("in closure\ndone\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sync_waitgroup_defer_in_goroutine()
    {
        var output = Run(@"
package main

import ""sync""

func main() {
    var wg sync.WaitGroup
    wg.Add(1)
    go func() {
        defer wg.Done()
        println(""worker"")
    }()
    wg.Wait()
    println(""done"")
}
");
        var lines = output.Replace("\r\n", "\n").Trim().Split('\n');
        Assert.AreEqual("worker", lines[0]);
        Assert.AreEqual("done", lines[1]);
    }

    [TestMethod]
    public void Sync_waitgroup_multiple_goroutines()
    {
        var output = Run(@"
package main

import ""sync""

func main() {
    var wg sync.WaitGroup
    wg.Add(3)
    go func() { defer wg.Done(); println(""worker1"") }()
    go func() { defer wg.Done(); println(""worker2"") }()
    go func() { defer wg.Done(); println(""worker3"") }()
    wg.Wait()
    println(""all done"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        // All workers must complete before "all done"
        StringAssert.Contains(normalized, "worker1");
        StringAssert.Contains(normalized, "worker2");
        StringAssert.Contains(normalized, "worker3");
        StringAssert.EndsWith(normalized, "all done\n");
    }

    [TestMethod]
    public void Os_getenv()
    {
        var output = Run(@"
package main

import ""os""

func main() {
    val := os.Getenv(""PATH"")
    if val != """" {
        println(""got path"")
    } else {
        println(""no path"")
    }
}
");
        Assert.AreEqual("got path\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Time_sleep()
    {
        var output = Run(@"
package main

import ""time""

func main() {
    println(""before"")
    time.Sleep(1 * time.Millisecond)
    println(""after"")
}
");
        Assert.AreEqual("before\nafter\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Time_constants()
    {
        var output = Run(@"
package main

import ""time""

func main() {
    ms := time.Millisecond
    println(ms)
    sec := time.Second
    println(sec)
}
");
        Assert.AreEqual("1000000\n1000000000\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sort_ints()
    {
        var output = Run(@"
package main

import ""sort""

func main() {
    a := []int{5, 3, 1, 4, 2}
    sort.Ints(a)
    for _, v := range a {
        println(v)
    }
}
");
        Assert.AreEqual("1\n2\n3\n4\n5\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sort_strings()
    {
        var output = Run(@"
package main

import ""sort""

func main() {
    a := []string{""banana"", ""apple"", ""cherry""}
    sort.Strings(a)
    for _, v := range a {
        println(v)
    }
}
");
        Assert.AreEqual("apple\nbanana\ncherry\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sort_ints_are_sorted()
    {
        var output = Run(@"
package main

import ""sort""

func main() {
    a := []int{1, 2, 3}
    println(sort.IntsAreSorted(a))
    b := []int{3, 1, 2}
    println(sort.IntsAreSorted(b))
}
");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Sort_search_ints()
    {
        var output = Run(@"
package main

import ""sort""

func main() {
    a := []int{1, 3, 5, 7, 9}
    println(sort.SearchInts(a, 5))
    println(sort.SearchInts(a, 4))
}
");
        Assert.AreEqual("2\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Os_args_len()
    {
        var output = Run(@"
package main

import ""os""

func main() {
    args := os.Args
    println(len(args) > 0)
}
");
        Assert.AreEqual("true\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Os_args_range()
    {
        var output = Run(@"
package main

import ""os""

func main() {
    for i, _ := range os.Args {
        if i == 0 {
            println(""has arg 0"")
        }
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        StringAssert.Contains(normalized, "has arg 0");
    }

    // --- Integration / edge case tests ---

    [TestMethod]
    public void Closure_captures_multiple_vars()
    {
        var output = Run(@"
package main

func main() {
    x := 10
    y := 20
    f := func() int {
        return x + y
    }
    println(f())
}
");
        Assert.AreEqual("30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Interface_with_multiple_methods()
    {
        var output = Run(@"
package main

type Shape interface {
    Area() int
    Name() string
}

type Square struct {
    Side int
}

func (s Square) Area() int {
    return s.Side * s.Side
}

func (s Square) Name() string {
    return ""square""
}

func describe(s Shape) {
    println(s.Name())
    println(s.Area())
}

func main() {
    sq := Square{Side: 5}
    describe(sq)
}
");
        Assert.AreEqual("square\n25\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Multi_return_with_blank_identifier()
    {
        var output = Run(@"
package main

func divide(a, b int) (int, string) {
    if b == 0 {
        return 0, ""division by zero""
    }
    return a / b, """"
}

func main() {
    result, _ := divide(10, 3)
    println(result)
    _, err := divide(10, 0)
    println(err)
}
");
        Assert.AreEqual("3\ndivision by zero\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Map_with_struct_values()
    {
        var output = Run(@"
package main

type Point struct {
    X int
    Y int
}

func main() {
    m := map[string]Point{
        ""origin"": Point{X: 0, Y: 0},
        ""one"":    Point{X: 1, Y: 1},
    }
    p := m[""one""]
    println(p.X)
    println(p.Y)
}
");
        Assert.AreEqual("1\n1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Slice_of_structs()
    {
        var output = Run(@"
package main

type Point struct {
    X int
    Y int
}

func main() {
    points := []Point{Point{X: 1, Y: 2}, Point{X: 3, Y: 4}}
    for _, p := range points {
        println(p.X + p.Y)
    }
}
");
        Assert.AreEqual("3\n7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_reads_captured_var()
    {
        var output = Run(@"
package main

func main() {
    msg := ""hello""
    f := func() {
        println(msg)
    }
    f()
}
");
        Assert.AreEqual("hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_with_literal_args()
    {
        var output = Run(@"
package main

func main() {
    defer println(""first"")
    defer println(""second"")
    defer println(""third"")
    println(""main"")
}
");
        Assert.AreEqual("main\nthird\nsecond\nfirst\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Type_switch_with_struct_case()
    {
        var output = Run(@"
package main

type MyVal struct {
    N int
}

func check(x interface{}) {
    switch v := x.(type) {
    case int:
        println(""int"")
        println(v)
    case string:
        println(""string"")
        println(v)
    default:
        println(""other"")
    }
}

func main() {
    check(42)
    check(""hello"")
    check(true)
}
");
        Assert.AreEqual("int\n42\nstring\nhello\nother\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_for_range()
    {
        var output = Run(@"
package main

func main() {
    matrix := [][]int{
        []int{1, 2},
        []int{3, 4},
    }
    sum := 0
    for _, row := range matrix {
        for _, val := range row {
            sum = sum + val
        }
    }
    println(sum)
}
");
        Assert.AreEqual("10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Method_returns_struct()
    {
        var output = Run(@"
package main

type Point struct {
    X int
    Y int
}

func (p Point) Translate(dx, dy int) Point {
    return Point{X: p.X + dx, Y: p.Y + dy}
}

func main() {
    p := Point{X: 1, Y: 2}
    q := p.Translate(10, 20)
    println(q.X)
    println(q.Y)
}
");
        Assert.AreEqual("11\n22\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Slice_append_in_loop()
    {
        var output = Run(@"
package main

func main() {
    s := []int{}
    for i := 0; i < 5; i++ {
        s = append(s, i*i)
    }
    for _, v := range s {
        println(v)
    }
}
");
        Assert.AreEqual("0\n1\n4\n9\n16\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Switch_with_multiple_case_values()
    {
        var output = Run(@"
package main

func classify(n int) string {
    switch n {
    case 1, 2, 3:
        return ""small""
    case 4, 5, 6:
        return ""medium""
    default:
        return ""large""
    }
}

func main() {
    println(classify(2))
    println(classify(5))
    println(classify(10))
}
");
        Assert.AreEqual("small\nmedium\nlarge\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_range_map_delete()
    {
        var output = Run(@"
package main

func main() {
    m := map[string]int{""a"": 1, ""b"": 2, ""c"": 3}
    delete(m, ""b"")
    println(len(m))
}
");
        Assert.AreEqual("2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void String_builder_pattern()
    {
        var output = Run(@"
package main

import ""strings""

func main() {
    parts := []string{""hello"", ""world"", ""go""}
    result := strings.Join(parts, "" "")
    println(result)
    println(strings.ToUpper(result))
}
");
        Assert.AreEqual("hello world go\nHELLO WORLD GO\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Grouped_params_two()
    {
        var output = Run(@"
package main

func add(a, b int) int {
    return a + b
}

func main() {
    println(add(3, 4))
}
");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Grouped_params_three()
    {
        var output = Run(@"
package main

func sum3(a, b, c int) int {
    return a + b + c
}

func main() {
    println(sum3(1, 2, 3))
}
");
        Assert.AreEqual("6\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Grouped_params_mixed()
    {
        var output = Run(@"
package main

func greet(first, last string, age int) {
    println(first)
    println(last)
    println(age)
}

func main() {
    greet(""John"", ""Doe"", 30)
}
");
        Assert.AreEqual("John\nDoe\n30\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Rand_intn()
    {
        var output = Run(@"
package main

import ""math/rand""

func main() {
    rand.Seed(42)
    n := rand.Intn(100)
    println(n >= 0)
    println(n < 100)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\ntrue\n", normalized);
    }

    [TestMethod]
    public void Rand_float64()
    {
        var output = Run(@"
package main

import ""math/rand""

func main() {
    rand.Seed(42)
    f := rand.Float64()
    println(f >= 0.0)
    println(f < 1.0)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\ntrue\n", normalized);
    }

    [TestMethod]
    public void Log_println()
    {
        var (stdout, stderr) = RunWithStderr(@"
package main

import ""log""

func main() {
    log.Println(""hello from log"")
    println(""stdout"")
}
");
        Assert.AreEqual("stdout\n", stdout.Replace("\r\n", "\n"));
        StringAssert.Contains(stderr, "hello from log");
    }

    [TestMethod]
    public void Log_printf()
    {
        var (stdout, stderr) = RunWithStderr(@"
package main

import ""log""

func main() {
    log.Printf(""value: %d"", 42)
    println(""done"")
}
");
        Assert.AreEqual("done\n", stdout.Replace("\r\n", "\n"));
        StringAssert.Contains(stderr, "value: 42");
    }

    [TestMethod]
    public void Cap_slice()
    {
        var output = Run(@"
package main

func main() {
    s := make([]int, 3, 10)
    println(cap(s))
    s = append(s, 1, 2, 3)
    println(len(s))
    println(cap(s))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("10\n6\n10\n", normalized);
    }

    [TestMethod]
    public void Cap_channel()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int, 5)
    println(cap(ch))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("5\n", normalized);
    }

    [TestMethod]
    public void Copy_slices()
    {
        var output = Run(@"
package main

func main() {
    src := []int{1, 2, 3, 4, 5}
    dst := make([]int, 3)
    n := copy(dst, src)
    println(n)
    println(dst[0])
    println(dst[1])
    println(dst[2])
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("3\n1\n2\n3\n", normalized);
    }

    [TestMethod]
    public void Copy_byte_slice_from_string()
    {
        var output = Run(@"
package main

func main() {
    b := make([]byte, 5)
    n := copy(b, ""hello"")
    println(n)
    println(b[0])
    println(b[4])
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("5\n104\n111\n", normalized);
    }

    [TestMethod]
    public void Array_literal_and_index()
    {
        var output = Run(@"
package main

func main() {
    arr := [3]int{10, 20, 30}
    println(arr[0])
    println(arr[1])
    println(arr[2])
    println(len(arr))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("10\n20\n30\n3\n", normalized);
    }

    [TestMethod]
    public void Bitwise_operators()
    {
        var output = Run(@"
package main

func main() {
    a := 0xFF
    b := 0x0F
    println(a & b)
    println(a | b)
    println(a ^ b)
    println(a &^ b)
    println(1 << 4)
    println(256 >> 4)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("15\n255\n240\n240\n16\n16\n", normalized);
    }

    [TestMethod]
    public void Rune_type()
    {
        var output = Run(@"
package main

func main() {
    var r rune = 'A'
    println(r)
    println(r + 1)
    println(string(r))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("65\n66\nA\n", normalized);
    }

    [TestMethod]
    public void Byte_type()
    {
        var output = Run(@"
package main

func main() {
    var b byte = 72
    println(b)
    println(string(b))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("72\nH\n", normalized);
    }

    [TestMethod]
    public void String_to_byte_slice()
    {
        var output = Run(@"
package main

func main() {
    s := ""Hi""
    bs := []byte(s)
    println(len(bs))
    println(bs[0])
    println(bs[1])
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("2\n72\n105\n", normalized);
    }

    [TestMethod]
    public void Modulo_operator()
    {
        var output = Run(@"
package main

func main() {
    println(10 % 3)
    println(17 % 5)
    println(100 % 10)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("1\n2\n0\n", normalized);
    }

    [TestMethod]
    public void Unary_operators()
    {
        var output = Run(@"
package main

func main() {
    x := 42
    println(-x)
    println(+x)
    b := true
    println(!b)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("-42\n42\nfalse\n", normalized);
    }

    [TestMethod]
    public void For_range_array()
    {
        var output = Run(@"
package main

func main() {
    arr := [3]string{""a"", ""b"", ""c""}
    for i, v := range arr {
        println(i, v)
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("0 a\n1 b\n2 c\n", normalized);
    }

    [TestMethod]
    public void Multiple_return_discard_first()
    {
        var output = Run(@"
package main

func divide(a, b int) (int, int) {
    return a / b, a % b
}

func main() {
    _, rem := divide(17, 5)
    println(rem)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("2\n", normalized);
    }

    [TestMethod]
    public void Const_multiple_types()
    {
        var output = Run(@"
package main

func main() {
    const pi = 3.14
    const name = ""Go""
    const flag = true
    println(pi)
    println(name)
    println(flag)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("3.14\nGo\ntrue\n", normalized);
    }

    [TestMethod]
    public void Nested_if_else()
    {
        var output = Run(@"
package main

func classify(x int) string {
    if x < 0 {
        return ""negative""
    } else if x == 0 {
        return ""zero""
    } else if x < 10 {
        return ""small""
    } else {
        return ""large""
    }
}

func main() {
    println(classify(-5))
    println(classify(0))
    println(classify(7))
    println(classify(42))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("negative\nzero\nsmall\nlarge\n", normalized);
    }

    [TestMethod]
    public void String_comparison()
    {
        var output = Run(@"
package main

func main() {
    a := ""apple""
    b := ""banana""
    println(a == b)
    println(a != b)
    println(a < b)
    println(a > b)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("false\ntrue\ntrue\nfalse\n", normalized);
    }

    [TestMethod]
    public void For_with_break_and_continue()
    {
        var output = Run(@"
package main

func main() {
    for i := 0; i < 10; i++ {
        if i == 3 {
            continue
        }
        if i == 7 {
            break
        }
        println(i)
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("0\n1\n2\n4\n5\n6\n", normalized);
    }

    [TestMethod]
    public void Io_readall_from_strings_reader()
    {
        var output = Run(@"
package main

import ""io""
import ""strings""

func main() {
    r := strings.NewReader(""hello world"")
    data, err := io.ReadAll(r)
    println(len(data))
    println(err)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("11\n\n", normalized);
    }

    [TestMethod]
    public void Io_copy_strings_reader_to_discard()
    {
        var output = Run(@"
package main

import ""io""
import ""strings""

func main() {
    r := strings.NewReader(""test data"")
    n, err := io.Copy(io.Discard, r)
    println(n)
    println(err)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("9\n\n", normalized);
    }

    [TestMethod]
    public void Io_writestring_to_discard()
    {
        var output = Run(@"
package main

import ""io""

func main() {
    n, err := io.WriteString(io.Discard, ""hello"")
    println(n)
    println(err)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("5\n\n", normalized);
    }

    [TestMethod]
    public void String_to_byte_slice_and_back()
    {
        var output = Run(@"
package main

func main() {
    s := ""Hello""
    bs := []byte(s)
    s2 := string(bs)
    println(s2)
    println(len(bs))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("Hello\n5\n", normalized);
    }

    [TestMethod]
    public void Hex_and_octal_literals()
    {
        var output = Run(@"
package main

func main() {
    println(0xFF)
    println(0o77)
    println(0b1010)
    println(077)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("255\n63\n10\n63\n", normalized);
    }

    [TestMethod]
    public void Bufio_scanner_lines()
    {
        var output = Run(@"
package main

import ""bufio""
import ""strings""

func main() {
    r := strings.NewReader(""line1\nline2\nline3"")
    scanner := bufio.NewScanner(r)
    for scanner.Scan() {
        println(scanner.Text())
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("line1\nline2\nline3\n", normalized);
    }

    [TestMethod]
    public void Bufio_scanner_count()
    {
        var output = Run(@"
package main

import ""bufio""
import ""strings""

func main() {
    r := strings.NewReader(""a\nb\nc\nd"")
    scanner := bufio.NewScanner(r)
    count := 0
    for scanner.Scan() {
        count++
    }
    println(count)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("4\n", normalized);
    }

    [TestMethod]
    public void Filepath_base()
    {
        var output = Run(@"
package main

import ""path/filepath""

func main() {
    println(filepath.Base(""/foo/bar/baz.txt""))
    println(filepath.Base(""hello.go""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("baz.txt\nhello.go\n", normalized);
    }

    [TestMethod]
    public void Filepath_ext()
    {
        var output = Run(@"
package main

import ""path/filepath""

func main() {
    println(filepath.Ext(""/foo/bar/baz.txt""))
    println(filepath.Ext(""/foo/bar/baz""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual(".txt\n\n", normalized);
    }

    [TestMethod]
    public void Filepath_join()
    {
        var output = Run(@"
package main

import ""path/filepath""

func main() {
    p := filepath.Join(""foo"", ""bar"", ""baz.txt"")
    println(filepath.Base(p))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("baz.txt\n", normalized);
    }

    [TestMethod]
    public void Filepath_isabs()
    {
        var output = Run(@"
package main

import ""path/filepath""

func main() {
    println(filepath.IsAbs(""/foo/bar""))
    println(filepath.IsAbs(""foo/bar""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\nfalse\n", normalized);
    }

    // --- strconv.ParseFloat / FormatFloat / ParseBool ---

    [TestMethod]
    public void Strconv_parse_float()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""strconv""
)

func main() {
    f, err := strconv.ParseFloat(""3.14"", 64)
    if err != nil {
        fmt.Println(""error"", err)
    } else {
        fmt.Println(f)
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("3.14\n", normalized);
    }

    [TestMethod]
    public void Strconv_format_float()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""strconv""
)

func main() {
    s := strconv.FormatFloat(3.14159, 'f', 2, 64)
    fmt.Println(s)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("3.14\n", normalized);
    }

    [TestMethod]
    public void Strconv_parse_bool()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""strconv""
)

func main() {
    v1, _ := strconv.ParseBool(""true"")
    v2, _ := strconv.ParseBool(""false"")
    v3, _ := strconv.ParseBool(""1"")
    fmt.Println(v1)
    fmt.Println(v2)
    fmt.Println(v3)
}
");
        Assert.AreEqual("true\nfalse\ntrue\n", output);
    }

    // --- fmt.Fprintf / Fprintln / Fprint ---

    [TestMethod]
    public void Fmt_fprintf_to_discard()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""io""
)

func main() {
    n, _ := fmt.Fprintf(io.Discard, ""hello %s %d"", ""world"", 42)
    fmt.Println(n)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("14\n", normalized);
    }

    [TestMethod]
    public void Fmt_fprintln_to_discard()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""io""
)

func main() {
    n, _ := fmt.Fprintln(io.Discard, ""hello"", ""world"")
    fmt.Println(n)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("12\n", normalized);
    }

    [TestMethod]
    public void Fmt_fprint_to_discard()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""io""
)

func main() {
    n, _ := fmt.Fprint(io.Discard, ""hello"", ""world"")
    fmt.Println(n)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("10\n", normalized);
    }

    // --- labeled break/continue ---

    [TestMethod]
    public void Labeled_break_outer_loop()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    outer:
    for i := 0; i < 5; i++ {
        for j := 0; j < 5; j++ {
            if j == 2 {
                break outer
            }
            fmt.Println(i, j)
        }
    }
    fmt.Println(""done"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("0 0\n0 1\ndone\n", normalized);
    }

    [TestMethod]
    public void Labeled_continue_outer_loop()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    outer:
    for i := 0; i < 3; i++ {
        for j := 0; j < 3; j++ {
            if j == 1 {
                continue outer
            }
            fmt.Println(i, j)
        }
    }
    fmt.Println(""done"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("0 0\n1 0\n2 0\ndone\n", normalized);
    }

    [TestMethod]
    public void Labeled_break_with_for_range()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    s := []int{1, 2, 3}
    outer:
    for _, v := range s {
        for i := 0; i < 3; i++ {
            if v == 2 && i == 0 {
                break outer
            }
            fmt.Println(v, i)
        }
    }
    fmt.Println(""done"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("1 0\n1 1\n1 2\ndone\n", normalized);
    }

    // --- os package ---

    [TestMethod]
    public void Os_readfile_writefile()
    {
        var tmpFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ngo_emit_{System.Guid.NewGuid()}.txt");
        try
        {
            var output = Run($@"
package main

import (
    ""fmt""
    ""os""
)

func main() {{
    err := os.WriteFile(""{tmpFile.Replace("\\", "\\\\")}"", []byte(""hello ngo""), 0644)
    if err != nil {{
        fmt.Println(""write error"", err)
    }}
    data, err2 := os.ReadFile(""{tmpFile.Replace("\\", "\\\\")}"")
    if err2 != nil {{
        fmt.Println(""read error"", err2)
    }}
    fmt.Println(string(data))
}}
");
            var normalized = output.Replace("\r\n", "\n");
            Assert.AreEqual("hello ngo\n", normalized);
        }
        finally
        {
            System.IO.File.Delete(tmpFile);
        }
    }

    [TestMethod]
    public void Os_getenv_setenv()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""os""
)

func main() {
    os.Setenv(""NGO_TEST_VAR"", ""hello"")
    fmt.Println(os.Getenv(""NGO_TEST_VAR""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("hello\n", normalized);
    }

    [TestMethod]
    public void Os_getwd()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""os""
)

func main() {
    dir, err := os.Getwd()
    if err != nil {
        fmt.Println(""error"", err)
    }
    fmt.Println(len(dir) > 0)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\n", normalized);
    }

    [TestMethod]
    public void Os_create_write_close()
    {
        var tmpFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ngo_emit_{System.Guid.NewGuid()}.txt");
        try
        {
            var output = Run($@"
package main

import (
    ""fmt""
    ""os""
)

func main() {{
    f, err := os.Create(""{tmpFile.Replace("\\", "\\\\")}"")
    if err != nil {{
        fmt.Println(""create error"", err)
        return
    }}
    n, err2 := f.WriteString(""world"")
    fmt.Println(n, err2)
    err3 := f.Close()
    fmt.Println(err3)
}}
");
            var normalized = output.Replace("\r\n", "\n");
            Assert.AreEqual("5 \n\n", normalized);
            Assert.AreEqual("world", System.IO.File.ReadAllText(tmpFile));
        }
        finally
        {
            System.IO.File.Delete(tmpFile);
        }
    }

    [TestMethod]
    public void Os_stdout_fprintf()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""os""
)

func main() {
    fmt.Fprintf(os.Stderr, ""err: %s\n"", ""test"")
    fmt.Println(""ok"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        // Stderr output goes to stderr, not captured by our test harness
        // Only stdout is captured
        StringAssert.Contains(normalized, "ok");
    }

    // --- regexp package ---

    [TestMethod]
    public void Regexp_must_compile_and_match()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""regexp""
)

func main() {
    re := regexp.MustCompile(""[0-9]+"")
    fmt.Println(re.MatchString(""abc123""))
    fmt.Println(re.MatchString(""abc""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\nfalse\n", normalized);
    }

    [TestMethod]
    public void Regexp_syntax_parse_only()
    {
        var output = Run(@"
package main
import ""regexp/syntax""
func main() {
    re, err := syntax.Parse(""[0-9]+"", syntax.Perl)
    if err != nil {
        println(""error"")
        return
    }
    println(""ok"", re.Op)
}
");
        Assert.IsTrue(output.Contains("ok"), $"Got: {output}");
    }

    [TestMethod]
    public void Regexp_compile_struct_literal()
    {
        var output = Run(@"
package main
import (
    ""regexp/syntax""
)
func main() {
    re, _ := syntax.Parse(""abc"", syntax.Perl)
    prog, _ := syntax.Compile(re)
    println(prog.NumCap)
    // Try creating a struct that mimics regexp.Regexp
    type SimpleRegexp struct {
        expr string
        prog *syntax.Prog
    }
    r := &SimpleRegexp{expr: ""abc"", prog: prog}
    println(r.expr)
}
");
        Assert.IsTrue(output.Contains("abc"), $"Got: {output}");
    }

    [TestMethod]
    public void Regexp_prog_methods()
    {
        var output = Run(@"
package main
import ""regexp/syntax""
func main() {
    re, _ := syntax.Parse(""abc"", syntax.Perl)
    prog, _ := syntax.Compile(re)
    prefix, complete := prog.Prefix()
    println(prefix, complete)
    cond := prog.StartCond()
    println(cond)
}
");
        Assert.IsTrue(output.Length > 0, $"Got empty output");
    }

    [TestMethod]
    public void Regexp_compile_substeps()
    {
        // Test the individual steps of regexp.compile to find which one fails
        var output = Run(@"
package main
import (
    ""regexp/syntax""
)
func main() {
    re, _ := syntax.Parse(""[0-9]+"", syntax.Perl)
    maxCap := re.MaxCap()
    println(""maxCap:"", maxCap)
    capNames := re.CapNames()
    println(""capNames:"", len(capNames))
    re = re.Simplify()
    println(""simplified"")
    prog, _ := syntax.Compile(re)
    println(""compiled, NumCap:"", prog.NumCap)
    matchcap := prog.NumCap
    if matchcap < 2 { matchcap = 2 }
    println(""matchcap:"", matchcap)
    cond := prog.StartCond()
    println(""cond:"", cond)
    prefix, complete := prog.Prefix()
    println(""prefix:"", prefix, complete)
}
");
        Assert.IsTrue(output.Contains("compiled"), $"Got: {output}");
    }

    [TestMethod]
    public void Regexp_compile_with_struct()
    {
        // Replicate what regexp.compile does
        var output = Run(@"
package main
import (
    ""regexp/syntax""
    ""regexp""
)
func main() {
    // Just try to call Compile — the real compile function
    re, err := regexp.Compile(""abc"")
    if err != nil {
        println(""err:"", err.Error())
    } else {
        println(""ok:"", re.String())
    }
}
");
        Assert.IsTrue(output.Contains("ok"), $"Got: {output}");
    }

    [TestMethod]
    public void Multi_return_to_struct_fields()
    {
        var output = Run(@"
package main

type Pair struct {
    A string
    B bool
}

func getPair() (string, bool) {
    return ""hello"", true
}

func main() {
    p := &Pair{}
    p.A, p.B = getPair()
    println(p.A, p.B)
}
");
        Assert.IsTrue(output.Contains("hello true"), $"Got: {output}");
    }

    [TestMethod]
    public void Regexp_compile_minimal_repro()
    {
        // Minimal reproduction: struct with many fields + method calls from dependency
        var output = Run(@"
package main
import (
    ""regexp/syntax""
    ""unicode/utf8""
)
type TestRegexp struct {
    expr        string
    prog        *syntax.Prog
    numSubexp   int
    subexpNames []string
    prefix      string
    prefixBytes []byte
    prefixRune  rune
    prefixEnd   uint32
    matchcap    int
    cond        syntax.EmptyOp
    minInputLen int
    longest     bool
}
func main() {
    re, _ := syntax.Parse(""abc"", syntax.Perl)
    prog, _ := syntax.Compile(re)
    matchcap := prog.NumCap
    if matchcap < 2 { matchcap = 2 }
    r := &TestRegexp{
        expr:      ""abc"",
        prog:      prog,
        numSubexp: re.MaxCap(),
        cond:      prog.StartCond(),
        longest:   false,
        matchcap:  matchcap,
    }
    r.prefix, _ = prog.Prefix()
    if r.prefix != """" {
        r.prefixBytes = []byte(r.prefix)
        r.prefixRune, _ = utf8.DecodeRuneInString(r.prefix)
    }
    println(""ok"", r.expr)
}
");
        Assert.IsTrue(output.Contains("ok"), $"Got: {output}");
    }

    [TestMethod]
    public void Package_level_array_index()
    {
        var output = Run(@"
package main
var sizes = [5]int{128, 512, 2048, 16384, 0}
func main() {
    i := 0
    for sizes[i] != 0 {
        println(sizes[i])
        i++
    }
    println(""done"")
}
");
        Assert.IsTrue(output.Contains("128"), $"Got: {output}");
    }

    [TestMethod]
    public void Regexp_compileOnePass_call()
    {
        // Test calling an internal regexp function that uses named returns
        var output = Run(@"
package main
import (
    ""regexp/syntax""
    ""regexp""
)
func main() {
    re, _ := syntax.Parse(""abc"", syntax.Perl)
    prog, _ := syntax.Compile(re)
    _ = prog
    // regexp.Compile internally calls compileOnePass — test that path
    r, _ := regexp.Compile(""abc"")
    println(r != nil)
}
");
        Assert.IsTrue(output.Length > 0, $"Got empty output");
    }

    [TestMethod]
    public void Regexp_compile_call()
    {
        GoRunner.Validate(@"
package main
import ""regexp""
func main() {
    re, err := regexp.Compile(""[0-9]+"")
    if err != nil {
        println(""error:"", err.Error())
    } else {
        println(""compiled ok"")
    }
    println(""calling MatchString"")
    println(re.MatchString(""abc123""))
}
", TestProjectRoot);
        var output = Run(@"
package main
import ""regexp""
func main() {
    re, err := regexp.Compile(""[0-9]+"")
    if err != nil {
        println(""error:"", err.Error())
    } else {
        println(""compiled ok"")
    }
    println(""calling MatchString"")
    println(re.MatchString(""abc123""))
}
");
        Assert.IsTrue(output.Contains("true"), $"Got: {output}");
    }

    [TestMethod]
    public void Regexp_syntax_compile_only()
    {
        var output = Run(@"
package main
import ""regexp/syntax""
func main() {
    re, err := syntax.Parse(""abc"", syntax.Perl)
    if err != nil {
        println(""parse error"")
        return
    }
    prog, err := syntax.Compile(re)
    if err != nil {
        println(""compile error"")
        return
    }
    println(""ok"", prog.NumCap)
}
");
        Assert.IsTrue(output.Contains("ok"), $"Got: {output}");
    }

    [TestMethod]
    public void Regexp_find_string()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""regexp""
)

func main() {
    re := regexp.MustCompile(""[0-9]+"")
    fmt.Println(re.FindString(""abc123def456""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("123\n", normalized);
    }

    [TestMethod]
    public void Regexp_replace_all_string()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""regexp""
)

func main() {
    re := regexp.MustCompile(""[0-9]+"")
    fmt.Println(re.ReplaceAllString(""a1b22c333"", ""X""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("aXbXcX\n", normalized);
    }

    [TestMethod]
    public void Regexp_match_string_static()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""regexp""
)

func main() {
    matched, _ := regexp.MatchString(""^[a-z]+$"", ""hello"")
    fmt.Println(matched)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\n", normalized);
    }

    [TestMethod]
    public void Regexp_compile_and_find_all()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""regexp""
)

func main() {
    re, _ := regexp.Compile(""[a-z]+"")
    results := re.FindAllString(""a1bb2ccc3"", -1)
    for _, s := range results {
        fmt.Println(s)
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("a\nbb\nccc\n", normalized);
    }

    // --- unicode / unicode/utf8 ---

    [TestMethod]
    public void Unicode_is_letter_digit()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""unicode""
)

func main() {
    fmt.Println(unicode.IsLetter('A'))
    fmt.Println(unicode.IsDigit('5'))
    fmt.Println(unicode.IsSpace(' '))
    fmt.Println(unicode.IsLetter('3'))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\ntrue\ntrue\nfalse\n", normalized);
    }

    [TestMethod]
    public void Unicode_to_upper_lower()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""unicode""
)

func main() {
    fmt.Println(string(unicode.ToUpper('a')))
    fmt.Println(string(unicode.ToLower('Z')))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("A\nz\n", normalized);
    }

    [TestMethod]
    public void Utf8_rune_count_in_string()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""unicode/utf8""
)

func main() {
    fmt.Println(utf8.RuneCountInString(""hello""))
    fmt.Println(utf8.RuneCountInString(""""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("5\n0\n", normalized);
    }

    [TestMethod]
    public void Utf8_valid_string()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""unicode/utf8""
)

func main() {
    fmt.Println(utf8.ValidString(""hello""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\n", normalized);
    }

    // --- embedded structs ---

    [TestMethod]
    public void Embedded_struct_field_promotion()
    {
        var output = Run(@"
package main

import ""fmt""

type Point struct {
    X int
    Y int
}

type Circle struct {
    Point
    Radius int
}

func main() {
    c := Circle{Point{3, 4}, 5}
    fmt.Println(c.X, c.Y, c.Radius)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("3 4 5\n", normalized);
    }

    [TestMethod]
    public void Embedded_struct_direct_access()
    {
        var output = Run(@"
package main

import ""fmt""

type Inner struct {
    Value int
}

type Outer struct {
    Inner
    Name string
}

func main() {
    o := Outer{Inner{42}, ""hello""}
    fmt.Println(o.Value)
    fmt.Println(o.Inner.Value)
    fmt.Println(o.Name)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("42\n42\nhello\n", normalized);
    }

    [TestMethod]
    public void Embedded_struct_method_promotion()
    {
        var output = Run(@"
package main

import ""fmt""

type Animal struct {
    Name string
}

func (a Animal) Speak() string {
    return a.Name + "" speaks""
}

type Dog struct {
    Animal
    Breed string
}

func main() {
    d := Dog{Animal{""Rex""}, ""Lab""}
    fmt.Println(d.Speak())
    fmt.Println(d.Name)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("Rex speaks\nRex\n", normalized);
    }

    [TestMethod]
    public void Init_function_runs_before_main()
    {
        var output = Run(@"
package main

import ""fmt""

var x int

func init() {
    x = 42
}

func main() {
    fmt.Println(x)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("42\n", normalized);
    }

    [TestMethod]
    public void Multiple_init_functions()
    {
        var output = Run(@"
package main

import ""fmt""

var x int

func init() {
    x = 10
}

func init() {
    x = x + 5
}

func main() {
    fmt.Println(x)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("15\n", normalized);
    }

    [TestMethod]
    public void Init_function_with_no_package_vars()
    {
        var output = Run(@"
package main

import ""fmt""

func init() {
    fmt.Println(""init ran"")
}

func main() {
    fmt.Println(""main ran"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("init ran\nmain ran\n", normalized);
    }

    [TestMethod]
    public void Map_comma_ok_present()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    m := map[string]int{""a"": 1, ""b"": 2}
    v, ok := m[""a""]
    fmt.Println(v, ok)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("1 true\n", normalized);
    }

    [TestMethod]
    public void Map_comma_ok_missing()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    m := map[string]int{""a"": 1}
    v, ok := m[""z""]
    fmt.Println(v, ok)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("0 false\n", normalized);
    }

    [TestMethod]
    public void Type_assert_comma_ok_success()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    var x interface{} = 42
    v, ok := x.(int)
    fmt.Println(v, ok)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("42 true\n", normalized);
    }

    [TestMethod]
    public void Type_assert_comma_ok_failure()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    var x interface{} = ""hello""
    v, ok := x.(int)
    fmt.Println(v, ok)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("0 false\n", normalized);
    }

    [TestMethod]
    public void Channel_receive_comma_ok()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    ch := make(chan int, 1)
    ch <- 42
    close(ch)
    v, ok := <-ch
    fmt.Println(v, ok)
    v2, ok2 := <-ch
    fmt.Println(v2, ok2)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("42 true\n0 false\n", normalized);
    }

    [TestMethod]
    public void If_init_with_map_comma_ok()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    m := map[string]int{""x"": 10}
    if v, ok := m[""x""]; ok {
        fmt.Println(""found"", v)
    }
    if _, ok := m[""y""]; !ok {
        fmt.Println(""not found"")
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("found 10\nnot found\n", normalized);
    }

    [TestMethod]
    public void If_init_with_type_assert_comma_ok()
    {
        var output = Run(@"
package main

import ""fmt""

func check(x interface{}) {
    if v, ok := x.(string); ok {
        fmt.Println(""string:"", v)
    } else {
        fmt.Println(""not string"")
    }
}

func main() {
    check(""hello"")
    check(42)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("string: hello\nnot string\n", normalized);
    }

    [TestMethod]
    public void Fmt_errorf()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    err := fmt.Errorf(""cannot divide %d by zero"", 10)
    fmt.Println(err)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("cannot divide 10 by zero\n", normalized);
    }

    [TestMethod]
    public void Map_comma_ok_in_assignment()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    m := map[string]int{""a"": 1}
    var v int
    var ok bool
    v, ok = m[""a""]
    fmt.Println(v, ok)
    v, ok = m[""z""]
    fmt.Println(v, ok)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("1 true\n0 false\n", normalized);
    }

    [TestMethod]
    public void Switch_multiple_case_values()
    {
        var output = Run(@"
package main

import ""fmt""

func classify(x int) string {
    switch x {
    case 1, 2, 3:
        return ""small""
    case 4, 5:
        return ""medium""
    default:
        return ""large""
    }
}

func main() {
    fmt.Println(classify(2))
    fmt.Println(classify(5))
    fmt.Println(classify(9))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("small\nmedium\nlarge\n", normalized);
    }

    [TestMethod]
    public void Compound_assignment_operators()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    x := 10
    x += 5
    fmt.Println(x)
    x -= 3
    fmt.Println(x)
    x *= 2
    fmt.Println(x)
    x /= 4
    fmt.Println(x)
    x %= 5
    fmt.Println(x)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("15\n12\n24\n6\n1\n", normalized);
    }

    [TestMethod]
    public void String_concatenation_compound()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    s := ""hello""
    s += "" ""
    s += ""world""
    fmt.Println(s)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("hello world\n", normalized);
    }

    [TestMethod]
    public void For_range_index_only()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    s := []int{10, 20, 30}
    sum := 0
    for i := range s {
        sum += s[i]
    }
    fmt.Println(sum)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("60\n", normalized);
    }

    [TestMethod]
    public void For_range_underscore_key()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    s := []string{""a"", ""b"", ""c""}
    for _, v := range s {
        fmt.Println(v)
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("a\nb\nc\n", normalized);
    }

    [TestMethod]
    public void Nil_map_comparison()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    var m map[string]int
    if m == nil {
        fmt.Println(""nil"")
    }
    m = map[string]int{""a"": 1}
    if m != nil {
        fmt.Println(""not nil"")
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("nil\nnot nil\n", normalized);
    }

    [TestMethod]
    public void Error_handling_pattern()
    {
        var output = Run(@"
package main

import ""fmt""

func divide(a, b int) (int, string) {
    if b == 0 {
        return 0, ""division by zero""
    }
    return a / b, """"
}

func main() {
    v, err := divide(10, 2)
    if err != """" {
        fmt.Println(""error:"", err)
    } else {
        fmt.Println(""result:"", v)
    }

    v2, err2 := divide(10, 0)
    if err2 != """" {
        fmt.Println(""error:"", err2)
    } else {
        fmt.Println(""result:"", v2)
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("result: 5\nerror: division by zero\n", normalized);
    }

    [TestMethod]
    public void Integration_struct_methods_interfaces()
    {
        var output = Run(@"
package main

import ""fmt""

type Shape interface {
    Area() float64
    Name() string
}

type Circle struct {
    Radius float64
}

func (c Circle) Area() float64 {
    return 3.14159 * c.Radius * c.Radius
}

func (c Circle) Name() string {
    return ""circle""
}

type Rectangle struct {
    Width  float64
    Height float64
}

func (r Rectangle) Area() float64 {
    return r.Width * r.Height
}

func (r Rectangle) Name() string {
    return ""rectangle""
}

func printShape(s Shape) {
    fmt.Println(s.Name(), s.Area())
}

func main() {
    c := Circle{5.0}
    r := Rectangle{3.0, 4.0}
    printShape(c)
    printShape(r)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("circle 78.53975\nrectangle 12\n", normalized);
    }

    [TestMethod]
    public void Integration_goroutine_channel()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    ch := make(chan int, 3)

    for i := 1; i <= 3; i++ {
        go func(n int) {
            ch <- n * 10
        }(i)
    }

    sum := 0
    for i := 0; i < 3; i++ {
        sum += <-ch
    }
    fmt.Println(sum)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("60\n", normalized);
    }

    [TestMethod]
    public void Integration_map_operations()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    m := map[string]int{}
    words := []string{""hello"", ""world"", ""hello"", ""go"", ""world"", ""hello""}
    for _, w := range words {
        m[w] += 1
    }

    if v, ok := m[""hello""]; ok {
        fmt.Println(""hello:"", v)
    }
    if v, ok := m[""go""]; ok {
        fmt.Println(""go:"", v)
    }
    if _, ok := m[""missing""]; !ok {
        fmt.Println(""missing: not found"")
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("hello: 3\ngo: 1\nmissing: not found\n", normalized);
    }

    [TestMethod]
    public void Integration_defer_ordering()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Println(""start"")
    defer fmt.Println(""first defer"")
    defer fmt.Println(""second defer"")
    fmt.Println(""end"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("start\nend\nsecond defer\nfirst defer\n", normalized);
    }

    [TestMethod]
    public void Integration_type_switch_and_assertions()
    {
        var output = Run(@"
package main

import ""fmt""

func describe(i interface{}) string {
    switch v := i.(type) {
    case int:
        return fmt.Sprintf(""int: %d"", v)
    case string:
        return fmt.Sprintf(""string: %s"", v)
    case bool:
        if v {
            return ""bool: true""
        }
        return ""bool: false""
    default:
        return ""unknown""
    }
}

func main() {
    fmt.Println(describe(42))
    fmt.Println(describe(""hello""))
    fmt.Println(describe(true))
    fmt.Println(describe(3.14))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("int: 42\nstring: hello\nbool: true\nunknown\n", normalized);
    }

    [TestMethod]
    public void Switch_fallthrough_basic()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    x := 1
    switch x {
    case 1:
        fmt.Println(""one"")
        fallthrough
    case 2:
        fmt.Println(""two"")
        fallthrough
    case 3:
        fmt.Println(""three"")
    case 4:
        fmt.Println(""four"")
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("one\ntwo\nthree\n", normalized);
    }

    [TestMethod]
    public void Switch_fallthrough_to_default()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    x := 5
    switch x {
    case 5:
        fmt.Println(""five"")
        fallthrough
    default:
        fmt.Println(""default"")
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("five\ndefault\n", normalized);
    }

    [TestMethod]
    public void Switch_fallthrough_skips_condition()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    x := 1
    switch x {
    case 1:
        fmt.Println(""matched 1"")
        fallthrough
    case 99:
        fmt.Println(""fell into 99"")
    case 3:
        fmt.Println(""matched 3"")
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("matched 1\nfell into 99\n", normalized);
    }

    [TestMethod]
    public void Switch_no_fallthrough()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    x := 2
    switch x {
    case 1:
        fmt.Println(""one"")
        fallthrough
    case 2:
        fmt.Println(""two"")
    case 3:
        fmt.Println(""three"")
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("two\n", normalized);
    }

    [TestMethod]
    public void Switch_on_string()
    {
        var output = Run(@"
package main

import ""fmt""

func greet(name string) string {
    switch name {
    case ""Alice"":
        return ""Hi Alice""
    case ""Bob"":
        return ""Hey Bob""
    default:
        return ""Hello "" + name
    }
}

func main() {
    fmt.Println(greet(""Alice""))
    fmt.Println(greet(""Bob""))
    fmt.Println(greet(""Charlie""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("Hi Alice\nHey Bob\nHello Charlie\n", normalized);
    }

    [TestMethod]
    public void Parallel_assignment_swap()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    a := 1
    b := 2
    a, b = b, a
    fmt.Println(a, b)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("2 1\n", normalized);
    }

    [TestMethod]
    public void Parallel_assignment_three_values()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    x := 10
    y := 20
    z := 30
    x, y, z = z, x, y
    fmt.Println(x, y, z)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("30 10 20\n", normalized);
    }

    [TestMethod]
    public void Goto_forward()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Println(""before"")
    goto skip
    fmt.Println(""skipped"")
skip:
    fmt.Println(""after"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("before\nafter\n", normalized);
    }

    [TestMethod]
    public void Goto_backward_loop()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    i := 0
again:
    if i >= 3 {
        goto done
    }
    fmt.Println(i)
    i++
    goto again
done:
    fmt.Println(""done"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("0\n1\n2\ndone\n", normalized);
    }

    [TestMethod]
    public void Struct_embedding_promoted_field()
    {
        var output = Run(@"
package main

import ""fmt""

type Base struct {
    Name string
}

type Derived struct {
    Base
    Age int
}

func main() {
    d := Derived{Base: Base{Name: ""Alice""}, Age: 30}
    fmt.Println(d.Name, d.Age)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("Alice 30\n", normalized);
    }

    [TestMethod]
    public void Struct_embedding_promoted_method()
    {
        var output = Run(@"
package main

import ""fmt""

type Animal struct {
    Sound string
}

func (a Animal) Speak() string {
    return a.Sound
}

type Dog struct {
    Animal
    Name string
}

func main() {
    d := Dog{Animal: Animal{Sound: ""Woof""}, Name: ""Rex""}
    fmt.Println(d.Name, d.Speak())
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("Rex Woof\n", normalized);
    }

    [TestMethod]
    public void Bytes_contains_and_equal()
    {
        var output = Run(@"
package main

import ""fmt""
import ""bytes""

func main() {
    a := []byte(""hello world"")
    b := []byte(""world"")
    fmt.Println(bytes.Contains(a, b))
    fmt.Println(bytes.Equal([]byte(""abc""), []byte(""abc"")))
    fmt.Println(bytes.Equal([]byte(""abc""), []byte(""xyz"")))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\ntrue\nfalse\n", normalized);
    }

    [TestMethod]
    public void Path_base_and_dir()
    {
        var output = Run(@"
package main

import ""fmt""
import ""path""

func main() {
    fmt.Println(path.Base(""/a/b/c.txt""))
    fmt.Println(path.Dir(""/a/b/c.txt""))
    fmt.Println(path.Ext(""/a/b/c.txt""))
    fmt.Println(path.IsAbs(""/absolute""))
    fmt.Println(path.IsAbs(""relative""))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("c.txt\n/a/b\n.txt\ntrue\nfalse\n", normalized);
    }

    [TestMethod]
    public void Unicode_character_classification()
    {
        var output = Run(@"
package main

import ""fmt""
import ""unicode""

func main() {
    fmt.Println(unicode.IsLetter('A'))
    fmt.Println(unicode.IsDigit('5'))
    fmt.Println(unicode.IsSpace(' '))
    fmt.Println(unicode.IsLetter('9'))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("true\ntrue\ntrue\nfalse\n", normalized);
    }

    [TestMethod]
    public void Interface_embedding()
    {
        var output = Run(@"
package main

import ""fmt""

type Reader interface {
    Read() string
}

type Writer interface {
    Write(s string)
}

type ReadWriter interface {
    Reader
    Writer
}

type MyFile struct {
    data string
}

func (f MyFile) Read() string {
    return f.data
}

func (f MyFile) Write(s string) {
    fmt.Println(""writing: "" + s)
}

func useReadWriter(rw ReadWriter) {
    fmt.Println(rw.Read())
    rw.Write(""hello"")
}

func main() {
    f := MyFile{data: ""file contents""}
    useReadWriter(f)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("file contents\nwriting: hello\n", normalized);
    }

    [TestMethod]
    public void Method_value_bound()
    {
        var output = Run(@"
package main

import ""fmt""

type Point struct {
    X int
    Y int
}

func (p Point) String() string {
    return fmt.Sprintf(""(%d, %d)"", p.X, p.Y)
}

func apply(f func() string) string {
    return f()
}

func main() {
    p := Point{X: 3, Y: 4}
    f := p.String
    fmt.Println(f())
    fmt.Println(apply(p.String))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("(3, 4)\n(3, 4)\n", normalized);
    }

    [TestMethod]
    public void Raw_string_literal()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    s := `hello\nworld`
    fmt.Println(s)
    fmt.Println(len(s))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("hello\\nworld\n12\n", normalized);
    }

    [TestMethod]
    public void Unary_bitwise_not()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    x := 0
    fmt.Println(^x)
    y := 7
    fmt.Println(^y)
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("-1\n-8\n", normalized);
    }

    [TestMethod]
    public void Map_of_slices()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    m := map[string][]int{
        ""a"": []int{1, 2, 3},
    }
    m[""b""] = []int{4, 5}
    fmt.Println(len(m[""a""]))
    fmt.Println(m[""b""][1])
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("3\n5\n", normalized);
    }

    [TestMethod]
    public void Custom_error_type()
    {
        var output = Run(@"
package main

import ""fmt""

type MyError struct {
    Code    int
    Message string
}

func (e MyError) Error() string {
    return fmt.Sprintf(""error %d: %s"", e.Code, e.Message)
}

func doWork() error {
    return MyError{Code: 404, Message: ""not found""}
}

func main() {
    err := doWork()
    fmt.Println(err.Error())
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("error 404: not found\n", normalized);
    }

    [TestMethod]
    public void Strings_repeat()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""strings""
)

func main() {
    fmt.Println(strings.Repeat(""ab"", 3))
    fmt.Println(strings.Repeat(""-"", 5))
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("ababab\n-----\n", normalized);
    }

    [TestMethod]
    public void Raw_string_multiline()
    {
        var output = Run("package main\n\nimport \"fmt\"\n\nfunc main() {\n\ts := `line1\nline2\nline3`\n\tfmt.Println(s)\n}\n");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("line1\nline2\nline3\n", normalized);
    }

    [TestMethod]
    public void Error_interface_method_call()
    {
        var output = Run(@"
package main

import ""fmt""

type MyError struct {
    Msg string
}

func (e MyError) Error() string {
    return e.Msg
}

func fail() error {
    return MyError{Msg: ""something broke""}
}

func main() {
    err := fail()
    fmt.Println(err.Error())
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("something broke\n", normalized);
    }

    [TestMethod]
    public void Error_nil_check()
    {
        var output = Run(@"
package main

import ""fmt""

type MyError struct {
    Msg string
}

func (e MyError) Error() string {
    return e.Msg
}

func mayFail(fail bool) error {
    if fail {
        return MyError{Msg: ""oops""}
    }
    return nil
}

func main() {
    err := mayFail(false)
    if err == nil {
        fmt.Println(""ok"")
    }
    err = mayFail(true)
    if err != nil {
        fmt.Println(err.Error())
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("ok\noops\n", normalized);
    }

    [TestMethod]
    public void Multi_return_with_error()
    {
        var output = Run(@"
package main

import ""fmt""

type ParseError struct {
    Msg string
}

func (e ParseError) Error() string {
    return e.Msg
}

func divide(a, b int) (int, error) {
    if b == 0 {
        return 0, ParseError{Msg: ""division by zero""}
    }
    return a / b, nil
}

func main() {
    result, err := divide(10, 2)
    if err == nil {
        fmt.Println(result)
    }
    result, err = divide(10, 0)
    if err != nil {
        fmt.Println(err.Error())
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("5\ndivision by zero\n", normalized);
    }

    [TestMethod]
    public void For_with_assignment_post()
    {
        var output = Run(@"
package main

func main() {
    for i := 0; i < 3; i = i + 1 {
        println(i)
    }
}
");
        Assert.AreEqual("0\n1\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_linked_list_traversal()
    {
        var output = Run(@"
package main

type Node struct {
    Value int
    Next  *Node
}

func main() {
    head := &Node{Value: 1, Next: &Node{Value: 2, Next: &Node{Value: 3, Next: nil}}}
    for n := head; n != nil; n = n.Next {
        println(n.Value)
    }
}
");
        Assert.AreEqual("1\n2\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_no_init_with_assignment_post()
    {
        var output = Run(@"
package main

func main() {
    i := 0
    for ; i < 3; i = i + 1 {
        println(i)
    }
}
");
        Assert.AreEqual("0\n1\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Panic_basic()
    {
        var output = Run(@"
package main

func main() {
    defer func() {
        r := recover()
        if r != nil {
            println(""caught"")
        }
    }()
    panic(""oops"")
}
");
        Assert.AreEqual("caught\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Panic_recover_value()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    defer func() {
        r := recover()
        if r != nil {
            fmt.Println(r)
        }
    }()
    panic(""hello panic"")
}
");
        Assert.AreEqual("hello panic\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Panic_recover_with_cleanup()
    {
        var output = Run(@"
package main

func safeCall(shouldPanic bool) int {
    defer func() {
        recover()
    }()
    if shouldPanic {
        panic(""boom"")
    }
    return 42
}

func main() {
    println(safeCall(false))
    println(safeCall(true))
    println(""done"")
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("42\n0\ndone\n", normalized);
    }

    [TestMethod]
    public void Self_referential_struct()
    {
        var output = Run(@"
package main

type Node struct {
    Value int
    Next  *Node
}

func main() {
    a := Node{Value: 10, Next: nil}
    b := Node{Value: 20, Next: &a}
    println(b.Value)
    println(b.Next.Value)
}
");
        Assert.AreEqual("20\n10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strconv_atoi_error_handling()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""strconv""
)

func main() {
    n, err := strconv.Atoi(""123"")
    if err != nil {
        fmt.Println(""error:"", err)
    } else {
        fmt.Println(""parsed:"", n)
    }

    n2, err2 := strconv.Atoi(""abc"")
    if err2 != nil {
        fmt.Println(""error:"", err2)
    } else {
        fmt.Println(""parsed:"", n2)
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("parsed: 123\nerror: strconv.Atoi: parsing \"abc\": invalid syntax\n", normalized);
    }

    [TestMethod]
    public void Errors_new_nil_check()
    {
        var output = Run(@"
package main

import (
    ""errors""
    ""fmt""
)

func validate(s string) error {
    if len(s) == 0 {
        return errors.New(""empty string"")
    }
    return nil
}

func main() {
    err := validate(""hello"")
    if err == nil {
        fmt.Println(""ok"")
    }
    err2 := validate("""")
    if err2 != nil {
        fmt.Println(err2)
    }
}
");
        var normalized = output.Replace("\r\n", "\n");
        Assert.AreEqual("ok\nempty string\n", normalized);
    }

    [TestMethod]
    public void Math_abs()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""math""
)

func main() {
    fmt.Println(math.Abs(-3.14))
}
");
        Assert.AreEqual("3.14\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_max_min()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""math""
)

func main() {
    fmt.Println(math.Max(1.0, 2.0))
    fmt.Println(math.Min(1.0, 2.0))
}
");
        Assert.AreEqual("2\n1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_floor_ceil()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""math""
)

func main() {
    fmt.Println(math.Floor(2.7))
    fmt.Println(math.Ceil(2.3))
}
");
        Assert.AreEqual("2\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_pow()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""math""
)

func main() {
    fmt.Println(math.Pow(2.0, 10.0))
}
");
        Assert.AreEqual("1024\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_split()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""strings""
)

func main() {
    parts := strings.Split(""a,b,c"", "","")
    for _, p := range parts {
        fmt.Println(p)
    }
}
");
        Assert.AreEqual("a\nb\nc\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_index()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""strings""
)

func main() {
    fmt.Println(strings.Index(""hello"", ""ll""))
}
");
        Assert.AreEqual("2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_trim_space()
    {
        var output = Run(@"
package main

import (
    ""fmt""
    ""strings""
)

func main() {
    fmt.Println(strings.TrimSpace(""  hi  ""))
}
");
        Assert.AreEqual("hi\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_printf_float()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Printf(""%f\n"", 3.14159)
}
");
        Assert.AreEqual("3.141590\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_printf_hex()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Printf(""%x\n"", 255)
}
");
        Assert.AreEqual("ff\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_printf_bool()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Printf(""%t\n"", true)
}
");
        Assert.AreEqual("true\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Fmt_printf_string_format()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    fmt.Printf(""%s=%d\n"", ""count"", 42)
}
");
        Assert.AreEqual("count=42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Bytes_has_prefix_suffix()
    {
        var output = Run(@"
package main

import (
    ""bytes""
    ""fmt""
)

func main() {
    fmt.Println(bytes.HasPrefix([]byte(""hello""), []byte(""he"")))
    fmt.Println(bytes.HasSuffix([]byte(""hello""), []byte(""lo"")))
}
");
        Assert.AreEqual("true\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Bytes_to_upper_lower()
    {
        var output = Run(@"
package main

import (
    ""bytes""
    ""fmt""
)

func main() {
    upper := bytes.ToUpper([]byte(""hi""))
    lower := bytes.ToLower([]byte(""HI""))
    fmt.Println(string(upper))
    fmt.Println(string(lower))
}
");
        Assert.AreEqual("HI\nhi\n", output.Replace("\r\n", "\n"));
    }

    // ----------------------------------------------------------------
    // Multi-char escape sequences (lexer + semantic)
    // ----------------------------------------------------------------

    [TestMethod]
    public void String_hex_escape()
    {
        var output = Run(@"
package main

func main() {
    println(""\x48\x69"")
}
");
        Assert.AreEqual("Hi\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void String_unicode_escape_u()
    {
        var output = Run(@"
package main

func main() {
    println(""\u0048\u0065\u006C\u006C\u006F"")
}
");
        Assert.AreEqual("Hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void String_octal_escape()
    {
        var output = Run(@"
package main

func main() {
    println(""\110\145\154\154\157"")
}
");
        Assert.AreEqual("Hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Rune_hex_escape_value()
    {
        var output = Run(@"
package main

func main() {
    var c rune = '\x41'
    println(c)
}
");
        Assert.AreEqual("65\n", output.Replace("\r\n", "\n"));
    }

    // ----------------------------------------------------------------
    // Multi-value C-style for init
    // ----------------------------------------------------------------

    [TestMethod]
    public void For_multi_value_init()
    {
        var output = Run(@"
package main

func main() {
    for i, j := 0, 5; i < j; i++ {
        println(i)
    }
}
");
        Assert.AreEqual("0\n1\n2\n3\n4\n", output.Replace("\r\n", "\n"));
    }

    // ----------------------------------------------------------------
    // Constant folding
    // ----------------------------------------------------------------

    [TestMethod]
    public void Const_shift_expression()
    {
        var output = Run(@"
package main

func main() {
    const x = 1 << 4
    println(x)
}
");
        Assert.AreEqual("16\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Const_iota_bitmask()
    {
        var output = Run(@"
package main

const (
    Read = 1 << iota
    Write
    Execute
)

func main() {
    println(Read, Write, Execute)
}
");
        Assert.AreEqual("1 2 4\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Const_string_concatenation()
    {
        var output = Run(@"
package main

func main() {
    const s = ""Go"" + "" "" + ""lang""
    println(s)
}
");
        Assert.AreEqual("Go lang\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Divide_by_zero_recovered()
    {
        var output = Run(@"
package main

func main() {
    defer func() {
        r := recover()
        if r != nil {
            println(r)
        }
    }()
    x := 1 / 0
    _ = x
}
");
        Assert.AreEqual("runtime error: integer divide by zero\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_capture_by_reference_increment()
    {
        var output = Run(@"
package main

func main() {
    x := 0
    inc := func() { x = x + 1 }
    inc()
    println(x)
}
");
        Assert.AreEqual("1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_capture_by_reference_outer_modifies_after_creation()
    {
        var output = Run(@"
package main

func main() {
    x := 1
    f := func() { println(x) }
    x = 2
    f()
}
");
        Assert.AreEqual("2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_capture_by_reference_multiple_closures_share_var()
    {
        var output = Run(@"
package main

func main() {
    x := 0
    a := func() { x = x + 1 }
    b := func() { x = x + 1 }
    a()
    b()
    println(x)
}
");
        Assert.AreEqual("2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_capture_by_reference_loop_var()
    {
        var output = Run(@"
package main

func main() {
    x := 0
    for i := 0; i < 5; i++ {
        func() { x = x + 1 }()
    }
    println(x)
}
");
        Assert.AreEqual("5\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Closure_capture_by_reference_parameter()
    {
        var output = Run(@"
package main

func modify(x int) func() int {
    f := func() int {
        x = x + 10
        return x
    }
    return f
}

func main() {
    f := modify(5)
    println(f())
}
");
        Assert.AreEqual("15\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_closure_reads_outer_variable()
    {
        var output = Run(@"
package main

func main() {
    x := 10
    f := func() {
        g := func() int { return x }
        println(g())
    }
    f()
}
");
        Assert.AreEqual("10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_closure_mutates_outer_variable()
    {
        var output = Run(@"
package main

func main() {
    x := 0
    f := func() {
        g := func() { x++ }
        g()
    }
    f()
    println(x)
}
");
        Assert.AreEqual("1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_closure_three_levels_deep()
    {
        var output = Run(@"
package main

func main() {
    x := 1
    f := func() {
        g := func() {
            h := func() { x = x * 10 }
            h()
        }
        g()
    }
    f()
    println(x)
}
");
        Assert.AreEqual("10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_closure_captures_parent_parameter()
    {
        var output = Run(@"
package main

func main() {
    f := func(x int) func() int {
        g := func() int { return x + 1 }
        return g
    }
    println(f(41)())
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_closure_factory_pattern()
    {
        var output = Run(@"
package main

func main() {
    makeCounter := func() func() int {
        count := 0
        return func() int {
            count++
            return count
        }
    }
    c := makeCounter()
    println(c())
    println(c())
    println(c())
}
");
        Assert.AreEqual("1\n2\n3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_modifies_named_return()
    {
        var output = Run(@"
package main

func add10() (result int) {
    defer func() { result += 10 }()
    return 5
}

func main() {
    println(add10())
}
");
        Assert.AreEqual("15\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_modifies_named_return_bare_return()
    {
        var output = Run(@"
package main

func greeting() (msg string) {
    msg = ""hello""
    defer func() { msg = ""goodbye"" }()
    return
}

func main() {
    println(greeting())
}
");
        Assert.AreEqual("goodbye\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_modifies_multiple_named_returns()
    {
        var output = Run(@"
package main

func swap() (a int, b int) {
    defer func() { a, b = b, a }()
    return 1, 2
}

func main() {
    x, y := swap()
    println(x)
    println(y)
}
");
        Assert.AreEqual("2\n1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_reads_named_return_no_modify()
    {
        var output = Run(@"
package main

func foo() (result int) {
    defer func() { println(result) }()
    return 42
}

func main() {
    x := foo()
    println(x)
}
");
        Assert.AreEqual("42\n42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_recover_sets_named_return()
    {
        var output = Run(@"
package main

func safe() (result int) {
    defer func() {
        if r := recover(); r != nil {
            result = -1
        }
    }()
    panic(""oops"")
    return 0
}

func main() {
    println(safe())
}
");
        Assert.AreEqual("-1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Struct_embedding_satisfies_interface()
    {
        var output = Run(@"
package main

import ""fmt""

type Speaker interface {
    Speak() string
}

type Animal struct {
    Name string
}

func (a Animal) Speak() string {
    return a.Name + "" speaks""
}

type Dog struct {
    Animal
}

func main() {
    var s Speaker = Dog{Animal: Animal{Name: ""Rex""}}
    fmt.Println(s.Speak())
}
");
        Assert.AreEqual("Rex speaks\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Struct_embedding_multiple_interface_methods()
    {
        var output = Run(@"
package main

import ""fmt""

type Describer interface {
    Name() string
    Describe() string
}

type Base struct {
    Val string
}

func (b Base) Name() string {
    return b.Val
}

func (b Base) Describe() string {
    return ""I am "" + b.Val
}

type Widget struct {
    Base
}

func main() {
    var d Describer = Widget{Base: Base{Val: ""button""}}
    fmt.Println(d.Name())
    fmt.Println(d.Describe())
}
");
        Assert.AreEqual("button\nI am button\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Struct_embedding_direct_method_shadows_promoted()
    {
        var output = Run(@"
package main

import ""fmt""

type Speaker interface {
    Speak() string
}

type Animal struct{}

func (a Animal) Speak() string {
    return ""animal""
}

type Dog struct {
    Animal
}

func (d Dog) Speak() string {
    return ""dog""
}

func main() {
    var s Speaker = Dog{}
    fmt.Println(s.Speak())
}
");
        Assert.AreEqual("dog\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Struct_embedding_interface_via_function_param()
    {
        var output = Run(@"
package main

import ""fmt""

type Greeter interface {
    Greet() string
}

type Person struct {
    Name string
}

func (p Person) Greet() string {
    return ""Hello, "" + p.Name
}

type Employee struct {
    Person
    Title string
}

func greet(g Greeter) {
    fmt.Println(g.Greet())
}

func main() {
    e := Employee{Person: Person{Name: ""Alice""}, Title: ""Engineer""}
    greet(e)
}
");
        Assert.AreEqual("Hello, Alice\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Build_and_verify_hello_world_IL()
    {
        var source = @"
package main

import ""fmt""

func main() {
    fmt.Println(""Hello, World!"")
}
";
        var tree = SyntaxTree.Parse(source);
        var ctx = new CompilationContext(TestProjectRoot);
        var result = SemanticAnalyzer.Analyze(tree, ctx);
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

        var tempDir = Path.Combine(Path.GetTempPath(), "ngo_ilverify_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var outputPath = Path.Combine(tempDir, "test.dll");
            AssemblyEmitter.EmitToFile(result, ctx, outputPath);

            // Copy Ngo.Runtime.dll alongside the output
            var runtimePath = typeof(Ngo.Runtime.BuiltIn).Assembly.Location;
            File.Copy(runtimePath, Path.Combine(tempDir, "Ngo.Runtime.dll"), overwrite: true);

            var errors = ILVerifier.Verify(outputPath);
            Assert.AreEqual(0, errors.Count, "IL verification errors:\n" + string.Join("\n", errors));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch (IOException) { /* file may be locked by other tests */ }
        }
    }

    [TestMethod]
    public void Regexp_ilverify()
    {
        var source = @"
package main

import (
    ""fmt""
    ""regexp""
)

func main() {
    re := regexp.MustCompile(""[0-9]+"")
    fmt.Println(re.MatchString(""abc123""))
}
";
        var tree = SyntaxTree.Parse(source);
        var ctx = new CompilationContext(TestProjectRoot);
        var result = SemanticAnalyzer.Analyze(tree, ctx);
        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));

        var tempDir = "/tmp/ngo_ilverify_regexp";
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
        Directory.CreateDirectory(tempDir);

        var outputPath = Path.Combine(tempDir, "test.dll");
        AssemblyEmitter.EmitToFile(result, ctx, outputPath);

        var runtimePath = typeof(Ngo.Runtime.BuiltIn).Assembly.Location;
        File.Copy(runtimePath, Path.Combine(tempDir, "Ngo.Runtime.dll"), overwrite: true);

        var errors = ILVerifier.Verify(outputPath);
        File.WriteAllLines("/tmp/ilverify_all.txt", errors);
        var nonStackTypeErrors = errors
            .Where(e => !e.Contains("StackUnexpected"))
            .ToList();
        File.WriteAllLines("/tmp/ilverify_nonstack.txt", nonStackTypeErrors);

        Assert.AreEqual(0, nonStackTypeErrors.Count,
            $"IL errors (excluding StackUnexpected): {nonStackTypeErrors.Count}\n" +
            string.Join("\n", nonStackTypeErrors.Take(200)));
    }

    // ── Keyed slice/array literals ──

    [TestMethod]
    public void Keyed_slice_literal()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    s := []int{0: 10, 2: 30}
    fmt.Println(s[0])
    fmt.Println(s[1])
    fmt.Println(s[2])
    fmt.Println(len(s))
}");
        Assert.AreEqual("10\n0\n30\n3\n", output);
    }

    [TestMethod]
    public void Keyed_slice_literal_mixed()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    s := []int{1, 2, 5: 10}
    fmt.Println(s[0])
    fmt.Println(s[1])
    fmt.Println(s[2])
    fmt.Println(s[5])
    fmt.Println(len(s))
}");
        Assert.AreEqual("1\n2\n0\n10\n6\n", output);
    }

    [TestMethod]
    public void Keyed_array_literal()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    a := [5]int{1: 10, 3: 30}
    fmt.Println(a[0])
    fmt.Println(a[1])
    fmt.Println(a[3])
    fmt.Println(len(a))
}");
        Assert.AreEqual("0\n10\n30\n5\n", output);
    }

    [TestMethod]
    public void Uintptr_variable()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    var x uintptr = 42
    fmt.Println(x)
}");
        Assert.AreEqual("42\n", output);
    }

    [TestMethod]
    public void Keyed_array_inferred_length()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    a := [...]int{4: 100}
    fmt.Println(a[0])
    fmt.Println(a[4])
    fmt.Println(len(a))
}");
        Assert.AreEqual("0\n100\n5\n", output);
    }

    [TestMethod]
    public void Complex_imaginary_literal()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    c := 3i
    fmt.Println(c)
}");
        Assert.AreEqual("<0; 3>\n", output);
    }

    [TestMethod]
    public void Complex_arithmetic()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    a := complex(1.0, 2.0)
    b := complex(3.0, 4.0)
    fmt.Println(a + b)
    fmt.Println(a - b)
    fmt.Println(a * b)
}");
        Assert.AreEqual("<4; 6>\n<-2; -2>\n<-5; 10>\n", output);
    }

    [TestMethod]
    public void Complex_real_imag()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    c := complex(3.0, 4.0)
    fmt.Println(real(c))
    fmt.Println(imag(c))
}");
        Assert.AreEqual("3\n4\n", output);
    }

    [TestMethod]
    public void Complex_equality()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    a := complex(1.0, 2.0)
    b := complex(1.0, 2.0)
    c := complex(3.0, 4.0)
    fmt.Println(a == b)
    fmt.Println(a == c)
    fmt.Println(a != c)
}");
        Assert.AreEqual("true\nfalse\ntrue\n", output);
    }

    [TestMethod]
    public void Complex_negation()
    {
        var output = Run(@"package main
import ""fmt""
func main() {
    c := complex(1.0, 2.0)
    fmt.Println(-c)
}");
        Assert.AreEqual("<-1; -2>\n", output);
    }

    // --- Dot imports ---

    [TestMethod]
    public void Dot_import_fmt()
    {
        var output = Run(@"
package main

import . ""fmt""

func main() {
    Println(""hello from dot import"")
}
");
        Assert.AreEqual("hello from dot import\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Dot_import_strconv()
    {
        var output = Run(@"
package main

import . ""strconv""
import ""fmt""

func main() {
    s := Itoa(42)
    fmt.Println(s)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Dot_import_multiple_functions()
    {
        var output = Run(@"
package main

import . ""fmt""

func main() {
    s := Sprintf(""value=%d"", 10)
    Println(s)
}
");
        Assert.AreEqual("value=10\n", output.Replace("\r\n", "\n"));
    }

    // --- Defer/go builtins ---

    [TestMethod]
    public void Defer_fmt_Printf()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    defer fmt.Printf(""x=%d\n"", 42)
    fmt.Println(""before"")
}
");
        Assert.AreEqual("before\nx=42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_builtin_println()
    {
        var output = Run(@"
package main

func main() {
    defer println(""deferred"")
    println(""main"")
}
");
        Assert.AreEqual("main\ndeferred\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_close_channel()
    {
        var output = Run(@"
package main

func main() {
    ch := make(chan int, 1)
    defer close(ch)
    ch <- 42
    v := <-ch
    println(v)
}
");
        Assert.AreEqual("42\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_os_Exit_replaced_by_println()
    {
        // Test defer with a stdlib function (using a simple one)
        var output = Run(@"
package main

import ""fmt""

func main() {
    defer fmt.Println(""deferred print"")
    fmt.Println(""main"")
}
");
        Assert.AreEqual("main\ndeferred print\n", output.Replace("\r\n", "\n"));
    }

    // --- Tuple arity (8+ return values) ---

    [TestMethod]
    public void Eight_return_values()
    {
        var output = Run(@"
package main

func eight() (int, int, int, int, int, int, int, int) {
    return 1, 2, 3, 4, 5, 6, 7, 8
}

func main() {
    a, b, c, d, e, f, g, h := eight()
    println(a, b, c, d, e, f, g, h)
}
");
        Assert.AreEqual("1 2 3 4 5 6 7 8\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nine_return_values()
    {
        var output = Run(@"
package main

func nine() (int, int, int, int, int, int, int, int, int) {
    return 1, 2, 3, 4, 5, 6, 7, 8, 9
}

func main() {
    a, b, c, d, e, f, g, h, i := nine()
    println(a, b, c, d, e, f, g, h, i)
}
");
        Assert.AreEqual("1 2 3 4 5 6 7 8 9\n", output.Replace("\r\n", "\n"));
    }

    // --- Delegate arity (6+ params) ---

    [TestMethod]
    public void Function_literal_six_params()
    {
        var output = Run(@"
package main

func apply(fn func(int, int, int, int, int, int) int) int {
    return fn(1, 2, 3, 4, 5, 6)
}

func main() {
    result := apply(func(a, b, c, d, e, f int) int {
        return a + b + c + d + e + f
    })
    println(result)
}
");
        Assert.AreEqual("21\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Action_literal_six_params()
    {
        var output = Run(@"
package main

func run(fn func(int, int, int, int, int, int)) {
    fn(1, 2, 3, 4, 5, 6)
}

func main() {
    run(func(a, b, c, d, e, f int) {
        println(a + b + c + d + e + f)
    })
}
");
        Assert.AreEqual("21\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_in_loop_captures_correct_values()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    for i := 0; i < 3; i++ {
        defer fmt.Println(i)
    }
}
");
        Assert.AreEqual("2\n1\n0\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Defer_in_loop_multiple_args()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    for i := 0; i < 3; i++ {
        defer fmt.Println(""i="", i)
    }
}
");
        Assert.AreEqual("i= 2\ni= 1\ni= 0\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Type_switch_unwraps_interface_to_struct()
    {
        var output = Run(@"
package main

import ""fmt""

type MyError struct {
    msg string
}

func (e MyError) Error() string {
    return e.msg
}

func check(e error) string {
    switch v := e.(type) {
    case MyError:
        return ""MyError: "" + v.msg
    default:
        return ""other""
    }
}

func main() {
    e := MyError{msg: ""boom""}
    fmt.Println(check(e))
}
");
        Assert.AreEqual("MyError: boom\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Recover_catches_divide_by_zero()
    {
        var output = Run(@"
package main

import ""fmt""

func safeDivide(a, b int) (result int, err string) {
    defer func() {
        r := recover()
        if r != nil {
            err = ""caught panic""
        }
    }()
    return a / b, """"
}

func main() {
    result, err := safeDivide(10, 0)
    fmt.Println(result, err)
}
");
        Assert.AreEqual("0 caught panic\n", output.Replace("\r\n", "\n"));
    }

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
}
");
        Assert.AreEqual("0\n1\n2\n3\n4\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void For_range_integer_blank_key()
    {
        var output = Run(@"
package main
import ""fmt""

func main() {
    sum := 0
    for _ = range 10 {
        sum = sum + 1
    }
    fmt.Println(sum)
}
");
        Assert.AreEqual("10\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Builtin_min_int()
    {
        var output = Run(@"
package main
import ""fmt""

func main() {
    a := min(3, 1, 4, 1, 5)
    fmt.Println(a)
}
");
        Assert.AreEqual("1\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Builtin_max_int()
    {
        var output = Run(@"
package main
import ""fmt""

func main() {
    a := max(3, 1, 4, 1, 5)
    fmt.Println(a)
}
");
        Assert.AreEqual("5\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Builtin_min_string()
    {
        var output = Run(@"
package main
import ""fmt""

func main() {
    a := min(""banana"", ""apple"", ""cherry"")
    fmt.Println(a)
}
");
        Assert.AreEqual("apple\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Builtin_max_string()
    {
        var output = Run(@"
package main
import ""fmt""

func main() {
    a := max(""banana"", ""apple"", ""cherry"")
    fmt.Println(a)
}
");
        Assert.AreEqual("cherry\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_cut()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""

func main() {
    before, after, found := strings.Cut(""hello=world"", ""="")
    fmt.Println(before, after, found)
}
");
        Assert.AreEqual("hello world true\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strconv_quote_unquote()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strconv""

func main() {
    q := strconv.Quote(""hello\nworld"")
    fmt.Println(q)
}
");
        Assert.AreEqual("\"hello\\nworld\"\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Stringer_dispatch_explicit()
    {
        // User-defined Stringer via explicit .String() call (auto-dispatch not yet supported)
        var output = Run(@"
package main
import ""fmt""

type Point struct {
    x int
    y int
}

func (p Point) String() string {
    return fmt.Sprintf(""(%d, %d)"", p.x, p.y)
}

func main() {
    p := Point{3, 4}
    fmt.Println(p.String())
}
");
        Assert.AreEqual("(3, 4)\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Builtin_clear_map()
    {
        var output = Run(@"
package main
import ""fmt""

func main() {
    m := make(map[string]int)
    m[""a""] = 1
    m[""b""] = 2
    clear(m)
    fmt.Println(len(m))
}
");
        Assert.AreEqual("0\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Builtin_min_two_args()
    {
        var output = Run(@"
package main
import ""fmt""

func main() {
    fmt.Println(min(10, 20))
    fmt.Println(max(10, 20))
}
");
        Assert.AreEqual("10\n20\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Time_now_compiles()
    {
        // Just verify time.Now() compiles and returns something
        var output = Run(@"
package main
import ""fmt""
import ""time""

func main() {
    t := time.Now()
    _ = t
    fmt.Println(""ok"")
}
");
        Assert.AreEqual("ok\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_builder()
    {
        var output = Run(@"
package main
import ""fmt""
import ""strings""

func main() {
    var b strings.Builder
    b.WriteString(""hello"")
    b.WriteString("" "")
    b.WriteString(""world"")
    fmt.Println(b.String())
}
");
        Assert.AreEqual("hello world\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Struct_equality()
    {
        var output = Run(@"
package main

import ""fmt""

type Point struct {
    X int
    Y int
}

func main() {
    a := Point{X: 1, Y: 2}
    b := Point{X: 1, Y: 2}
    c := Point{X: 3, Y: 4}
    fmt.Println(a == b)
    fmt.Println(a == c)
    fmt.Println(a != c)
}
");
        Assert.AreEqual("true\nfalse\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Struct_equality_with_string_fields()
    {
        var output = Run(@"
package main

import ""fmt""

type Person struct {
    Name string
    Age  int
}

func main() {
    a := Person{Name: ""Alice"", Age: 30}
    b := Person{Name: ""Alice"", Age: 30}
    c := Person{Name: ""Bob"", Age: 30}
    fmt.Println(a == b)
    fmt.Println(a == c)
}
");
        Assert.AreEqual("true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Array_equality()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    a := [3]int{1, 2, 3}
    b := [3]int{1, 2, 3}
    c := [3]int{1, 2, 4}
    fmt.Println(a == b)
    fmt.Println(a == c)
    fmt.Println(a != c)
}
");
        Assert.AreEqual("true\nfalse\ntrue\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Method_expression()
    {
        var output = Run(@"
package main

import ""fmt""

type Point struct {
    X int
    Y int
}

func (p Point) Sum() int {
    return p.X + p.Y
}

func main() {
    fn := Point.Sum
    p := Point{X: 3, Y: 4}
    fmt.Println(fn(p))
}
");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Method_expression_with_args()
    {
        var output = Run(@"
package main

import ""fmt""

type Calc struct {
    Base int
}

func (c Calc) Add(x int) int {
    return c.Base + x
}

func main() {
    add := Calc.Add
    c := Calc{Base: 10}
    fmt.Println(add(c, 5))
}
");
        Assert.AreEqual("15\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Multi_return_spread_to_function()
    {
        var output = Run(@"
package main

import ""fmt""

func pair() (int, string) {
    return 42, ""hello""
}

func show(n int, s string) {
    fmt.Println(n, s)
}

func main() {
    show(pair())
}
");
        Assert.AreEqual("42 hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Multi_return_spread_to_println()
    {
        var output = Run(@"
package main

import ""fmt""

func pair() (int, string) {
    return 42, ""hello""
}

func main() {
    fmt.Println(pair())
}
");
        Assert.AreEqual("42 hello\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Nested_type_declaration()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    type Point struct {
        X int
        Y int
    }
    p := Point{X: 3, Y: 4}
    fmt.Println(p.X + p.Y)
}
");
        Assert.AreEqual("7\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Slice_to_array_conversion()
    {
        var output = Run(@"
package main

import ""fmt""

func main() {
    s := []int{1, 2, 3, 4, 5}
    a := [3]int(s[:3])
    fmt.Println(a[0], a[1], a[2])
}
");
        Assert.AreEqual("1 2 3\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_trig_functions()
    {
        var output = Run(@"
package main

import ""fmt""
import ""math""

func main() {
    fmt.Println(math.Pow10(3))
    fmt.Println(math.Hypot(3, 4))
    fmt.Println(math.Dim(5, 3))
}
");
        Assert.AreEqual("1000\n5\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Math_constants()
    {
        var output = Run(@"
package main

import ""fmt""
import ""math""

func main() {
    fmt.Println(math.MaxInt32)
    fmt.Println(math.MinInt8)
}
");
        Assert.AreEqual("2147483647\n-128\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Os_lookup_env()
    {
        var output = Run(@"
package main

import ""fmt""
import ""os""

func main() {
    os.Setenv(""NGO_TEST_VAR"", ""hello"")
    val, ok := os.LookupEnv(""NGO_TEST_VAR"")
    fmt.Println(val, ok)
    _, ok2 := os.LookupEnv(""NGO_NONEXISTENT_VAR_12345"")
    fmt.Println(ok2)
}
");
        Assert.AreEqual("hello true\nfalse\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_split_n()
    {
        var output = Run(@"
package main

import ""fmt""
import ""strings""

func main() {
    parts := strings.SplitN(""a:b:c:d"", "":"", 3)
    fmt.Println(parts[0], parts[1], parts[2])
}
");
        Assert.AreEqual("a b c:d\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Strings_index_any()
    {
        var output = Run(@"
package main

import ""fmt""
import ""strings""

func main() {
    fmt.Println(strings.IndexAny(""hello"", ""aeiou""))
    fmt.Println(strings.IndexByte(""hello"", 'l'))
}
");
        Assert.AreEqual("1\n2\n", output.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Inline_array_reslice_and_append()
    {
        var output = Run(@"
package main

import ""fmt""

type Item struct {
    Value int
}

func main() {
    a := []*Item{&Item{1}, &Item{2}}
    b := []*Item{&Item{3}}
    c := append(a, b...)
    fmt.Println(len(c))
}
");
        Assert.IsTrue(output.Contains("3"), $"Got: {output}");
    }
}
