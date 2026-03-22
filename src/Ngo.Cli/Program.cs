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
using System.IO;
using System.Reflection;
using Ngo.Compiler;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace Ngo.Cli;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        switch (args[0])
        {
            case "version":
            case "--version":
            case "-v":
                Console.WriteLine("ngo v0.3.0 (.NET 9.0, MSIL backend)");
                return 0;

            case "help":
            case "--help":
            case "-h":
                PrintUsage();
                return 0;

            case "run":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("ngo run: missing file argument");
                    Console.Error.WriteLine("Usage: ngo run <file.go>");
                    return 1;
                }
                return RunFile(args[1]);

            case "build":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("ngo build: missing file argument");
                    Console.Error.WriteLine("Usage: ngo build [-o output] <file.go>");
                    return 1;
                }
                return BuildFile(args[1..]);

            case "verify":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("ngo verify: missing file argument");
                    Console.Error.WriteLine("Usage: ngo verify <assembly.dll>");
                    return 1;
                }
                return VerifyFile(args[1]);

            case "test":
                return RunTests(args.Length >= 2 ? args[1] : ".");

            case "get":
                return GetModules(args.Length >= 2 ? args[1] : ".");

            case "precompile-stdlib":
                return PrecompileStdlib(args[1..]);

            case "check":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("ngo check: missing file argument");
                    Console.Error.WriteLine("Usage: ngo check <file.go>");
                    return 1;
                }
                return CheckFile(args[1]);

            default:
                // If it ends with .go, treat it as `ngo run <file>`
                if (args[0].EndsWith(".go", StringComparison.OrdinalIgnoreCase))
                {
                    return RunFile(args[0]);
                }
                Console.Error.WriteLine($"ngo: unknown command '{args[0]}'");
                Console.Error.WriteLine("Run 'ngo help' for usage.");
                return 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("ngo - Go compiler for .NET");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ngo run <file.go>          Compile and run a Go source file");
        Console.WriteLine("  ngo build [options] <file> Compile to a .NET assembly");
        Console.WriteLine("    -o <output>              Output file path");
        Console.WriteLine("    --library                Emit as .NET library (visibility from Go exports)");
        Console.WriteLine("    --namespace <ns>         Set .NET namespace (implies --library)");
        Console.WriteLine("  ngo verify <assembly.dll>  Verify IL of a compiled assembly");
        Console.WriteLine("  ngo get [dir]              Download module dependencies from go.mod");
        Console.WriteLine("  ngo test [dir]             Run tests in *_test.go files");
        Console.WriteLine("  ngo check <file.go>        Check a Go source file for errors");
        Console.WriteLine("  ngo precompile-stdlib      Precompile Go stdlib to .NET assemblies");
        Console.WriteLine("    --go-version <ver>       Go version (default: go1.22.6)");
        Console.WriteLine("    --go-src <dir>           Go source directory");
        Console.WriteLine("    --output <dir>           Output directory for .dll files");
        Console.WriteLine("  ngo version                Print version information");
        Console.WriteLine("  ngo help                   Print this help message");
    }

    static IReadOnlyList<SourceFile> ParseGoSources(string path)
    {
        var results = new List<SourceFile>();

        if (Directory.Exists(path))
        {
            // Directory mode: compile all .go files in directory
            var goFiles = Directory.GetFiles(path, "*.go");
            foreach (var file in goFiles)
            {
                if (file.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (GoPackageResolver.ShouldSkipGoFile(file))
                {
                    continue;
                }
                var source = File.ReadAllText(file);
                results.Add(new SourceFile(SyntaxTree.Parse(source), file));
            }
        }
        else if (File.Exists(path))
        {
            var source = File.ReadAllText(path);
            results.Add(new SourceFile(SyntaxTree.Parse(source), path));
        }

        return results;
    }

    static int RunFile(string filePath)
    {
        var sources = ParseGoSources(filePath);
        if (sources.Count == 0)
        {
            Console.Error.WriteLine(Directory.Exists(filePath)
                ? $"ngo: no .go files found in {filePath}"
                : $"ngo: file not found: {filePath}");
            return 1;
        }

        var projectRoot = Directory.Exists(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var compilation = new CompilationContext(projectRoot, new ConsoleLog(verbose: false));

        var trees = new List<SyntaxTree>();
        foreach (var src in sources)
        {
            trees.Add(src.Tree);
        }

        // Multi-file packages: don't check unused (a var in file A may be used in file B)
        var result = SemanticAnalyzer.Analyze(trees, compilation, checkUnused: trees.Count == 1);

        if (result.HasErrors)
        {
            foreach (var src in sources)
            {
                var fileErrors = new List<CompileError>();
                foreach (var err in result.Errors)
                {
                    fileErrors.Add(err);
                }
                if (fileErrors.Count > 0)
                {
                    PrintErrors(src.Tree.SourceText, src.FilePath, fileErrors);
                    break;
                }
            }
            return 1;
        }

        Assembly assembly;
        try
        {
            assembly = AssemblyEmitter.Emit(result, compilation);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ngo: internal compiler error during code generation");
            Console.Error.WriteLine($"  {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }

        // Register native library resolver for CGo shared libraries
        if (compilation.CgoResult?.NativeLibraryPath != null)
        {
            var nativeLibDir = Path.GetDirectoryName(compilation.CgoResult.NativeLibraryPath) ?? "";
            System.Runtime.Loader.AssemblyLoadContext.Default.ResolvingUnmanagedDll += (asm, libraryName) =>
            {
                string[] candidates = new[]
                {
                    Path.Combine(nativeLibDir, $"lib{libraryName}.so"),
                    Path.Combine(nativeLibDir, $"{libraryName}.so"),
                    Path.Combine(nativeLibDir, $"lib{libraryName}.dylib"),
                    Path.Combine(nativeLibDir, $"{libraryName}.dll"),
                };
                foreach (var candidate in candidates)
                {
                    if (System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out var handle))
                    {
                        return handle;
                    }
                }
                return IntPtr.Zero;
            };
        }

        var entryPoint = AssemblyEmitter.FindEntryPoint(assembly);
        if (entryPoint == null)
        {
            Console.Error.WriteLine($"{filePath}: error: no main function found in package main");
            return 1;
        }

        // Set os.Args for the Go program — filter out ngo CLI args
        // Find "--" separator and pass everything after it, or just the program name
        var cliArgs = Environment.GetCommandLineArgs();
        var dashDashIdx = Array.IndexOf(cliArgs, "--");
        if (dashDashIdx >= 0)
        {
            var goArgs = new string[cliArgs.Length - dashDashIdx - 1 + 1];
            goArgs[0] = filePath; // program name
            Array.Copy(cliArgs, dashDashIdx + 1, goArgs, 1, cliArgs.Length - dashDashIdx - 1);
            Ngo.Runtime.Os.GoOs.OverrideArgs = goArgs;
        }
        else
        {
            Ngo.Runtime.Os.GoOs.OverrideArgs = new[] { filePath };
        }

        try
        {
            entryPoint.Invoke(null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            var inner = ex.InnerException;
            if (inner is Ngo.Runtime.GoPanicException panic)
            {
                Console.Error.WriteLine($"goroutine 1 [running]:");
                Console.Error.WriteLine($"panic: {panic.Value}");
                Console.Error.WriteLine();
                Console.Error.WriteLine($"exit status 2");
                return 2;
            }

            Console.Error.WriteLine($"ngo: runtime error: {inner.Message}");
            return 2;
        }

        return 0;
    }

    static int BuildFile(string[] args)
    {
        string? outputPath = null;
        string? filePath = null;
        bool isLibrary = false;
        string? ns = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-o" && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
            else if (args[i] == "--library")
            {
                isLibrary = true;
            }
            else if (args[i] == "--namespace" && i + 1 < args.Length)
            {
                ns = args[++i];
                isLibrary = true;
            }
            else if (filePath == null)
            {
                filePath = args[i];
            }
            else
            {
                Console.Error.WriteLine($"ngo build: unexpected argument '{args[i]}'");
                return 1;
            }
        }

        if (filePath == null)
        {
            Console.Error.WriteLine("ngo build: missing file argument");
            Console.Error.WriteLine("Usage: ngo build [--library] [--namespace <ns>] [-o output] <file.go>");
            return 1;
        }

        var sources = ParseGoSources(filePath);
        if (sources.Count == 0)
        {
            Console.Error.WriteLine(Directory.Exists(filePath)
                ? $"ngo: no .go files found in {filePath}"
                : $"ngo: file not found: {filePath}");
            return 1;
        }

        // Default output: same name as input but with .dll extension
        if (outputPath == null)
        {
            var baseName = Directory.Exists(filePath)
                ? Path.GetFileName(Path.GetFullPath(filePath))
                : Path.GetFileNameWithoutExtension(filePath);
            outputPath = baseName + ".dll";
        }

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var projectRoot = Directory.Exists(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var compilation = new CompilationContext(projectRoot, new ConsoleLog(verbose: false));

        var trees = new List<SyntaxTree>();
        foreach (var src in sources)
        {
            trees.Add(src.Tree);
        }

        var result = SemanticAnalyzer.Analyze(trees, compilation, checkUnused: true);

        if (result.HasErrors)
        {
            foreach (var src in sources)
            {
                PrintErrors(src.Tree.SourceText, src.FilePath, result.Errors);
                break;
            }
            return 1;
        }

        var assemblyName = Path.GetFileNameWithoutExtension(outputPath);
        var emitOptions = (isLibrary || ns != null)
            ? new EmitOptions { IsLibrary = true, Namespace = ns }
            : null;

        try
        {
            AssemblyEmitter.EmitToFile(result, compilation, outputPath, assemblyName, emitOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ngo: internal compiler error during code generation");
            Console.Error.WriteLine($"  {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }

        // Copy Ngo.Runtime.dll alongside the output
        var runtimeAssemblyPath = typeof(Ngo.Runtime.BuiltIn).Assembly.Location;
        if (!string.IsNullOrEmpty(runtimeAssemblyPath))
        {
            var runtimeDest = Path.Combine(
                outputDir ?? ".",
                Path.GetFileName(runtimeAssemblyPath));
            if (!string.Equals(Path.GetFullPath(runtimeAssemblyPath), Path.GetFullPath(runtimeDest), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(runtimeAssemblyPath, runtimeDest, overwrite: true);
            }
        }

        // Generate .runtimeconfig.json
        var runtimeConfigPath = Path.ChangeExtension(outputPath, ".runtimeconfig.json");
        var runtimeConfig = @"{
  ""runtimeOptions"": {
    ""tfm"": ""net9.0"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""9.0.0""
    }
  }
}";
        File.WriteAllText(runtimeConfigPath, runtimeConfig);

        Console.WriteLine($"ngo: wrote {outputPath}");
        return 0;
    }

    static int VerifyFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"ngo: file not found: {filePath}");
            return 1;
        }

        try
        {
            var errors = ILVerifier.Verify(filePath);
            if (errors.Count == 0)
            {
                Console.WriteLine($"{filePath}: IL verified ok");
                return 0;
            }

            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            var plural = errors.Count == 1 ? "error" : "errors";
            Console.Error.WriteLine($"{errors.Count} verification {plural}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ngo verify: {ex.Message}");
            return 2;
        }
    }

    static int CheckFile(string filePath)
    {
        var sources = ParseGoSources(filePath);
        if (sources.Count == 0)
        {
            Console.Error.WriteLine(Directory.Exists(filePath)
                ? $"ngo: no .go files found in {filePath}"
                : $"ngo: file not found: {filePath}");
            return 1;
        }

        var projectRoot = Directory.Exists(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var compilation = new CompilationContext(projectRoot, new ConsoleLog(verbose: false));

        var trees = new List<SyntaxTree>();
        foreach (var src in sources)
        {
            trees.Add(src.Tree);
        }

        var result = SemanticAnalyzer.Analyze(trees, compilation, checkUnused: true);

        if (result.HasErrors)
        {
            foreach (var src in sources)
            {
                PrintErrors(src.Tree.SourceText, src.FilePath, result.Errors);
                break;
            }
            return 1;
        }

        Console.WriteLine($"{filePath}: ok");
        return 0;
    }

    static int RunTests(string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            Console.Error.WriteLine($"ngo test: directory not found: {dirPath}");
            return 1;
        }

        var fullDir = Path.GetFullPath(dirPath);

        // Collect all .go files (including *_test.go)
        var allGoFiles = Directory.GetFiles(fullDir, "*.go");
        var testFiles = new List<string>();
        var sourceFiles = new List<string>();

        foreach (var f in allGoFiles)
        {
            if (Path.GetFileName(f).EndsWith("_test.go", StringComparison.OrdinalIgnoreCase))
                testFiles.Add(f);
            else
                sourceFiles.Add(f);
        }

        if (testFiles.Count == 0)
        {
            Console.WriteLine("?   no test files");
            return 0;
        }

        var compilation = new CompilationContext(fullDir, new ConsoleLog(verbose: false));

        {
            // Parse all files
            var trees = new List<SyntaxTree>();
            foreach (var f in sourceFiles.Concat(testFiles))
            {
                var source = File.ReadAllText(f);
                trees.Add(SyntaxTree.Parse(source));
            }

            // Analyze (don't check unused — test files may import testing but not use all)
            var result = SemanticAnalyzer.Analyze(trees, compilation, checkUnused: false);

            if (result.HasErrors)
            {
                foreach (var err in result.Errors)
                    Console.Error.WriteLine($"  {err.Message}");
                Console.Error.WriteLine("FAIL");
                return 1;
            }

            // Find test functions: func Test*(t *testing.T)
            var testFuncs = new List<Ngo.Compiler.Ast.FunctionDeclaration>();
            foreach (var func in result.Root.Functions)
            {
                if (func.Symbol.Name.StartsWith("Test")
                    && func.Symbol.Name.Length > 4
                    && func.Symbol.Parameters.Count == 1
                    && func.Symbol.Parameters[0].Type is Ngo.Compiler.Symbols.PointerTypeSymbol pt
                    && pt.ElementType.Name == "T")
                {
                    testFuncs.Add(func);
                }
            }

            if (testFuncs.Count == 0)
            {
                Console.WriteLine("?   no test functions");
                return 0;
            }

            // Emit the assembly
            Assembly assembly;
            try
            {
                assembly = AssemblyEmitter.Emit(result, compilation);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ngo test: compilation error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 2;
            }

            // Find the package type in the emitted assembly
            var packageType = assembly.GetTypes().FirstOrDefault(t => t.IsAbstract && t.IsSealed);
            if (packageType == null)
            {
                Console.Error.WriteLine("ngo test: cannot find package type in assembly");
                return 2;
            }

            // Run each test
            int passed = 0;
            int failed = 0;
            int skipped = 0;
            var startTime = DateTime.UtcNow;

            foreach (var testFunc in testFuncs)
            {
                var method = packageType.GetMethod(testFunc.Symbol.Name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null)
                {
                    Console.Error.WriteLine($"--- FAIL: {testFunc.Symbol.Name} (method not found)");
                    failed++;
                    continue;
                }

                var t = new Ngo.Runtime.Testing.T(testFunc.Symbol.Name);
                var testStart = DateTime.UtcNow;

                try
                {
                    method.Invoke(null, new object[] { t });
                }
                catch (TargetInvocationException ex) when (ex.InnerException is Ngo.Runtime.Testing.TestFailException)
                {
                    // Already handled — t.Failed() is true
                }
                catch (TargetInvocationException ex) when (ex.InnerException is Ngo.Runtime.Testing.TestSkipException)
                {
                    // Already handled — t.Skipped() is true
                }
                catch (TargetInvocationException ex) when (ex.InnerException is Ngo.Runtime.GoPanicException panic)
                {
                    Console.Error.WriteLine($"--- FAIL: {testFunc.Symbol.Name} (panic: {panic.Value})");
                    failed++;
                    continue;
                }
                catch (TargetInvocationException ex)
                {
                    Console.Error.WriteLine($"--- FAIL: {testFunc.Symbol.Name} ({ex.InnerException?.Message})");
                    failed++;
                    continue;
                }
                finally
                {
                    t.RunCleanups();
                }

                var elapsed = DateTime.UtcNow - testStart;
                var elapsedStr = $"({elapsed.TotalSeconds:F2}s)";

                if (t.Skipped())
                {
                    Console.WriteLine($"--- SKIP: {testFunc.Symbol.Name} {elapsedStr}");
                    foreach (var log in t.GetLogs())
                        Console.WriteLine($"        {log}");
                    skipped++;
                }
                else if (t.Failed())
                {
                    Console.WriteLine($"--- FAIL: {testFunc.Symbol.Name} {elapsedStr}");
                    foreach (var log in t.GetLogs())
                        Console.WriteLine($"        {log}");
                    failed++;
                }
                else
                {
                    Console.WriteLine($"--- PASS: {testFunc.Symbol.Name} {elapsedStr}");
                    passed++;
                }
            }

            var totalElapsed = DateTime.UtcNow - startTime;

            Console.WriteLine();
            if (failed > 0)
            {
                Console.WriteLine($"FAIL");
                Console.WriteLine($"{passed} passed, {failed} failed, {skipped} skipped ({totalElapsed.TotalSeconds:F3}s)");
                return 1;
            }

            Console.WriteLine($"ok");
            Console.WriteLine($"{passed} passed, {skipped} skipped ({totalElapsed.TotalSeconds:F3}s)");
            return 0;
        }
    }

    static int GetModules(string dir)
    {
        var fullDir = Path.GetFullPath(dir);
        if (!Directory.Exists(fullDir))
        {
            Console.Error.WriteLine($"ngo get: directory not found: {dir}");
            return 1;
        }

        var resolver = new Ngo.Compiler.Semantics.GoModuleResolver(new ConsoleLog(verbose: false));
        resolver.LoadGoMod(fullDir);

        if (resolver.ModuleName == null)
        {
            Console.Error.WriteLine("ngo get: no go.mod found");
            return 1;
        }

        if (resolver.Requirements.Count == 0)
        {
            Console.WriteLine("ngo get: no dependencies in go.mod");
            return 0;
        }

        Console.WriteLine($"module {resolver.ModuleName}");
        Console.WriteLine($"{resolver.Requirements.Count} dependencies");
        Console.WriteLine();

        int downloaded = 0;
        int cached = 0;
        int failed = 0;

        foreach (var (module, version) in resolver.Requirements)
        {
            var cachedDir = resolver.GetCachedModuleDir(module, version);
            if (cachedDir != null)
            {
                Console.WriteLine($"  cached  {module}@{version}");
                cached++;
                continue;
            }

            Console.Write($"  get     {module}@{version} ...");
            var result = resolver.DownloadModule(module, version);
            if (result != null)
            {
                Console.WriteLine(" ok");
                downloaded++;
            }
            else
            {
                Console.WriteLine(" FAILED");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{downloaded} downloaded, {cached} cached, {failed} failed");
        return failed > 0 ? 1 : 0;
    }

    static int PrecompileStdlib(string[] args)
    {
        Console.Error.WriteLine("ngo: precompile-stdlib is not yet implemented with the new package resolver architecture.");
        return 1;
    }

    static void PrintErrors(string source, string fileName,
        System.Collections.Generic.IReadOnlyList<CompileError> errors)
    {
        var lines = source.Split('\n');
        int errorCount = 0;

        foreach (var error in errors)
        {
            if (error.Severity != ErrorSeverity.Error)
                continue;

            errorCount++;

            // Convert character offset to line/column
            var pos = GetLineAndColumn(source, error.Location.Start);
            var line = pos.Line;
            var col = pos.Column;

            Console.Error.WriteLine($"{fileName}:{line}:{col}: {error.Message}");

            // Show source context if we have a valid line
            if (line >= 1 && line <= lines.Length)
            {
                var sourceLine = lines[line - 1].TrimEnd('\r');
                Console.Error.WriteLine($"    {sourceLine}");

                // Show caret pointing to error column
                if (col >= 1 && col <= sourceLine.Length + 1)
                {
                    var padding = new string(' ', col - 1 + 4); // +4 for the indent
                    Console.Error.WriteLine($"{padding}^");
                }
            }
        }

        if (errorCount > 0)
        {
            var plural = errorCount == 1 ? "error" : "errors";
            Console.Error.WriteLine($"{errorCount} {plural}");
        }
    }

    static LinePosition GetLineAndColumn(string source, int offset)
    {
        if (offset < 0 || offset > source.Length)
        {
            return new LinePosition(1, 1);
        }

        int line = 1;
        int col = 1;
        for (int i = 0; i < offset && i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }
        return new LinePosition(line, col);
    }
}
