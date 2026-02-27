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
        Console.WriteLine("  ngo build [-o out] <file>  Compile to a .NET assembly");
        Console.WriteLine("  ngo verify <assembly.dll>  Verify IL of a compiled assembly");
        Console.WriteLine("  ngo check <file.go>        Check a Go source file for errors");
        Console.WriteLine("  ngo version                Print version information");
        Console.WriteLine("  ngo help                   Print this help message");
    }

    static int RunFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"ngo: file not found: {filePath}");
            return 1;
        }

        var source = File.ReadAllText(filePath);

        // Parse + analyze
        var tree = SyntaxTree.Parse(source);
        var result = SemanticAnalyzer.Analyze(tree, checkUnused: true);

        if (result.HasErrors)
        {
            PrintErrors(source, filePath, result.Errors);
            return 1;
        }

        // Emit
        Assembly assembly;
        try
        {
            assembly = AssemblyEmitter.Emit(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ngo: internal compiler error during code generation");
            Console.Error.WriteLine($"  {ex.Message}");
            return 2;
        }

        var entryPoint = AssemblyEmitter.FindEntryPoint(assembly);
        if (entryPoint == null)
        {
            Console.Error.WriteLine($"{filePath}: error: no main function found in package main");
            return 1;
        }

        // Run
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

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-o" && i + 1 < args.Length)
            {
                outputPath = args[++i];
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
            Console.Error.WriteLine("Usage: ngo build [-o output] <file.go>");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"ngo: file not found: {filePath}");
            return 1;
        }

        // Default output: same name as input but with .dll extension
        if (outputPath == null)
        {
            outputPath = Path.ChangeExtension(Path.GetFileName(filePath), ".dll");
        }

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var source = File.ReadAllText(filePath);

        // Parse + analyze
        var tree = SyntaxTree.Parse(source);
        var result = SemanticAnalyzer.Analyze(tree, checkUnused: true);

        if (result.HasErrors)
        {
            PrintErrors(source, filePath, result.Errors);
            return 1;
        }

        var assemblyName = Path.GetFileNameWithoutExtension(outputPath);

        // Emit to file
        try
        {
            AssemblyEmitter.EmitToFile(result, outputPath, assemblyName);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ngo: internal compiler error during code generation");
            Console.Error.WriteLine($"  {ex.Message}");
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
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"ngo: file not found: {filePath}");
            return 1;
        }

        var source = File.ReadAllText(filePath);
        var tree = SyntaxTree.Parse(source);
        var result = SemanticAnalyzer.Analyze(tree, checkUnused: true);

        if (result.HasErrors)
        {
            PrintErrors(source, filePath, result.Errors);
            return 1;
        }

        Console.WriteLine($"{filePath}: ok");
        return 0;
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
            var (line, col) = GetLineAndColumn(source, error.Location.Start);

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

    static (int line, int column) GetLineAndColumn(string source, int offset)
    {
        if (offset < 0 || offset > source.Length)
            return (1, 1);

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
        return (line, col);
    }
}
