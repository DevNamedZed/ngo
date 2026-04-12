using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Archive;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Language;
using Ngo.Compiler.Semantics;

namespace Ngo.Compiler.Tests;

public class GoRunner
{
    public static void Validate(
        string goSource, 
        string testProjectRoot)
    {
        var tree = SyntaxTree.Parse(goSource);
        var ctx = new CompilationContext(testProjectRoot);
        var result = SemanticAnalyzer.Analyze(tree, ctx);

        Assert.IsFalse(result.HasErrors, string.Join("\n", result.Errors));
        string dllPath = $"{Path.GetTempFileName()}.dll";
        
        AssemblyEmitter.EmitToFile(result, ctx, dllPath);
        var errors = ILVerifier.Verify(dllPath);
        if (errors.Count > 0)
        {
            var errorMessages = string.Join("\n", errors.Take(10));
            Assert.Fail($"IL verification failed with {errors.Count} error(s):\n{errorMessages}");
        }
    }
    
    private static string Run(
        string goSource, 
        string testProjectRoot)
    {
        var tree = SyntaxTree.Parse(goSource);
        var ctx = new CompilationContext(testProjectRoot);
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

}