// -----------------------------------------------------------------------
// <copyright file="AssemblyEmitter.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Entry point for code generation. Transforms AnalysisResult into a .NET Assembly.
    /// </summary>
    public static class AssemblyEmitter
    {
        /// <summary>
        /// Emits an in-memory assembly for immediate execution (used by ngo run).
        /// </summary>
        public static Assembly Emit(AnalysisResult result, Semantics.CompilationContext compilationContext, string assemblyName = "NgoProgram", EmitOptions? options = null)
        {
            if (result.HasErrors)
                throw new InvalidOperationException("Cannot emit assembly from source with errors.");

            var asmName = new AssemblyName(assemblyName);
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
            var moduleBuilder = asmBuilder.DefineDynamicModule(assemblyName);

            EmitCore(result, moduleBuilder, options, compilationContext);

            return asmBuilder;
        }

        /// <summary>
        /// Emits a persisted assembly to disk (used by ngo build).
        /// </summary>
        public static void EmitToFile(AnalysisResult result, Semantics.CompilationContext compilationContext, string outputPath, string assemblyName = "NgoProgram", EmitOptions? options = null)
        {
            if (result.HasErrors)
                throw new InvalidOperationException("Cannot emit assembly from source with errors.");

            var asmName = new AssemblyName(assemblyName);
            var ab = new PersistedAssemblyBuilder(asmName, typeof(object).Assembly);
            var moduleBuilder = ab.DefineDynamicModule(assemblyName);

            var ctx = EmitCore(result, moduleBuilder, options, compilationContext);

            // Find main() entry point
            MethodBuilder? entryPointMethod = null;
            foreach (var func in result.Root.Functions)
            {
                if (func.Symbol.Name == "main" && ctx.Methods.TryGetValue(func.Symbol, out var mb))
                {
                    entryPointMethod = ((LiveMethodBuilder)mb).Inner;
                    break;
                }
            }

            MetadataBuilder metadataBuilder = ab.GenerateMetadata(out BlobBuilder ilStream, out BlobBuilder fieldData);

            PEHeaderBuilder header;
            MethodDefinitionHandle entryPointHandle = default;

            if (entryPointMethod != null)
            {
                header = PEHeaderBuilder.CreateExecutableHeader();
                entryPointHandle = MetadataTokens.MethodDefinitionHandle(entryPointMethod.MetadataToken);
            }
            else
            {
                header = PEHeaderBuilder.CreateLibraryHeader();
            }

            var peBuilder = new ManagedPEBuilder(
                header: header,
                metadataRootBuilder: new MetadataRootBuilder(metadataBuilder),
                ilStream: ilStream,
                mappedFieldData: fieldData,
                entryPoint: entryPointHandle);

            var peBlob = new BlobBuilder();
            peBuilder.Serialize(peBlob);

            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            peBlob.WriteContentTo(fileStream);
        }

        /// <summary>
        /// Shared 3-pass emit logic used by both Emit and EmitToFile.
        /// </summary>
        private static EmitContext EmitCore(AnalysisResult result, ModuleBuilder moduleBuilder, EmitOptions? options, Semantics.CompilationContext compilationContext)
        {
            var mapper = new TypeMapper(compilationContext);
            var ctx = new EmitContext(new LiveModuleBuilder(moduleBuilder), mapper, options);

            // Link dependency IL from .ngo archives on disk
            if (compilationContext.ProjectRoot != null)
            {
                var linked = new HashSet<string>();
                LinkDependencies(result.Root, ctx, compilationContext, linked);
            }

            // Emit the main package
            EmitPackage(result.Root, ctx);

            return ctx;
        }

        /// <summary>
        /// Links pre-compiled dependency packages from .ngo archives into the target module.
        /// Reads IL metadata (Section 2) and IL bytecode (Section 3), creates TypeBuilders
        /// and MethodBuilders, remaps tokens, and sets method bodies.
        /// </summary>
        private static void LinkDependencies(Ast.SourceFile root, EmitContext ctx, Semantics.CompilationContext compilationContext, HashSet<string> linked)
        {
            foreach (var import in root.Imports)
            {
                var importPath = import.Path;
                if (string.IsNullOrEmpty(importPath) || linked.Contains(importPath))
                {
                    continue;
                }
                linked.Add(importPath);

                // Skip runtime packages — they're in Ngo.Runtime.dll
                if (RuntimePackageResolver.Instance.Resolve(importPath) != null)
                {
                    continue;
                }

                // Try to link from .ngo archive (Sections 2+3)
                bool linkedFromArchive = false;
                if (compilationContext.ProjectRoot != null)
                {
                    var cacheDir = NgoArchive.GetCacheDir(compilationContext.ProjectRoot);
                    var archivePath = NgoArchive.GetArchivePath(cacheDir, importPath);
                    linkedFromArchive = ILSerializer.LinkFromArchive(archivePath, import.Package, ctx);
                }

                if (!linkedFromArchive)
                {
                    compilationContext.Log.Warn($"archive link failed for '{importPath}', falling back to source compilation");
                    EmitDependencyFromSource(importPath, import.Package, ctx, compilationContext);
                }
            }
        }

        /// <summary>
        /// Compiles a dependency package from Go source directly into the host module.
        /// Used when the .ngo archive has no IL (Section 1 only).
        /// After emitting, registers the emitted methods/types against the original
        /// PackageSymbol so the main package emit can find them by symbol identity.
        /// </summary>
        private static void EmitDependencyFromSource(string importPath, PackageSymbol originalPkg, EmitContext ctx, Semantics.CompilationContext compilationContext)
        {
            var dir = compilationContext.GetSourceDir(importPath);
            if (dir == null)
            {
                return;
            }

            try
            {
                var trees = new List<Language.SyntaxTree>();
                foreach (var file in System.IO.Directory.GetFiles(dir, "*.go"))
                {
                    if (GoPackageResolver.ShouldSkipGoFile(file))
                    {
                        continue;
                    }
                    var source = System.IO.File.ReadAllText(file);
                    trees.Add(Language.SyntaxTree.Parse(source));
                }

                if (trees.Count == 0)
                {
                    return;
                }

                var result = SemanticAnalyzer.Analyze(trees, compilationContext);
                EmitPackage(result.Root, ctx);

                // Bridge: map original PackageSymbol's exports to the freshly emitted methods.
                // The main package's AST references the ORIGINAL FunctionSymbol instances,
                // but EmitPackage registered the NEW ones from re-analysis.
                // Match by name and register in CachedMethods so EmitCall can find them.
                foreach (var export in originalPkg.Exports)
                {
                    if (export.Value is FunctionSymbol origFunc)
                    {
                        // Find the corresponding emitted method by name
                        foreach (var kvp in ctx.Methods)
                        {
                            if (kvp.Key.Name == origFunc.Name)
                            {
                                ctx.CachedMethods[origFunc] = kvp.Value.AsMethodInfo();
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                compilationContext.Log.Warn($"dependency emit failed for '{importPath}': {ex.Message}");
            }
        }

        private static void EmitPackage(Ast.SourceFile root, EmitContext ctx)
        {
            var packageName = root.Package.Symbol.Name;

            // Create the package static class
            var previousPackageType = ctx.PackageType;
            ctx.PackageType = ctx.Module.DefineType(
                ctx.QualifyName(packageName),
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            var declEmitter = new DeclarationEmitter(ctx);
            ctx.DeclEmitter = declEmitter;
            var bodyEmitter = new MethodBodyEmitter(ctx);

            // Emit builtin error interface before user types (only for the first package)
            if (previousPackageType == null)
            {
                declEmitter.EmitBuiltinErrorInterface();
            }

            // Pass 1a: Forward-declare all struct and interface types (TypeBuilders only)
            foreach (var typeDecl in root.Types)
            {
                if (typeDecl.Symbol is StructTypeSymbol structType)
                    declEmitter.DefineStructType(structType);
                else if (typeDecl.Symbol is InterfaceTypeSymbol)
                    declEmitter.EmitTypeDeclaration(typeDecl);
            }

            // Pass 1b: Populate struct fields now that all types are forward-declared
            foreach (var typeDecl in root.Types)
            {
                if (typeDecl.Symbol is StructTypeSymbol structType)
                    declEmitter.PopulateStructFields(structType);
            }

            // Finalize struct types and register the runtime types in the mapper
            foreach (var kvp in ctx.StructTypes)
            {
                if (!ctx.FinalizedTypes.Contains(kvp.Key))
                {
                    var runtimeType = kvp.Value.CreateType()!;
                    ctx.Mapper.Register(kvp.Key, runtimeType);
                    ctx.FinalizedTypes.Add(kvp.Key);
                }
            }

            // Finalize interface types and register the runtime types
            foreach (var kvp in ctx.InterfaceTypes)
            {
                if (!ctx.FinalizedTypes.Contains(kvp.Key))
                {
                    var runtimeType = kvp.Value.CreateType()!;
                    ctx.Mapper.Register(kvp.Key, runtimeType);
                    ctx.FinalizedTypes.Add(kvp.Key);
                }
            }

            // Pass 2: Define all function and method signatures
            foreach (var func in root.Functions)
            {
                declEmitter.EmitFunction(func);
            }

            foreach (var method in root.Methods)
            {
                declEmitter.EmitMethod(method);
            }

            // Define package-level variables
            foreach (var varDecl in root.Variables)
            {
                declEmitter.EmitPackageVar(varDecl);
            }

            // Pass 3: Emit all function and method bodies
            foreach (var func in root.Functions)
            {
                bodyEmitter.EmitFunctionBody(func);
            }

            foreach (var method in root.Methods)
            {
                bodyEmitter.EmitMethodBody(method);
            }

            // Collect init() functions
            var initFuncs = new List<FunctionDeclaration>();
            foreach (var func in root.Functions)
            {
                if (func.Symbol.Name == "init")
                    initFuncs.Add(func);
            }

            // Emit package-level variable initializers and init() calls in a static constructor
            if (root.Variables.Count > 0 || initFuncs.Count > 0)
            {
                bodyEmitter.EmitPackageInit(root.Variables, initFuncs);
            }

            // In library mode, emit a public Initialize() method that triggers the .cctor
            if (ctx.Options.IsLibrary)
            {
                var initMethod = ctx.PackageType.DefineMethod(
                    "Initialize",
                    MethodAttributes.Public | MethodAttributes.Static,
                    typeof(void),
                    Type.EmptyTypes);
                var initIL = initMethod.GetILWriter();
                initIL.Emit(OpCodes.Ret);
            }

            ctx.PackageType.CreateType();
        }

        /// <summary>
        /// Find and invoke the main() function in the emitted assembly.
        /// Returns the MethodInfo for main, or null if not found.
        /// </summary>
        public static MethodInfo? FindEntryPoint(Assembly assembly, string packageName = "main")
        {
            var type = assembly.GetType(packageName);

            // Search with namespace prefix if flat name not found
            if (type == null)
            {
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == packageName || t.FullName?.EndsWith("." + packageName) == true)
                    {
                        type = t;
                        break;
                    }
                }
            }

            if (type == null) return null;
            return type.GetMethod("main", BindingFlags.Public | BindingFlags.Static);
        }
    }
}
