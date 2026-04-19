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
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Ngo.Compiler.Archive;
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
        public static Assembly Emit(
            AnalysisResult result,
            Semantics.CompilationContext compilationContext,
            string assemblyName = "NgoProgram",
            EmitOptions? options = null)
        {
            return EmitWithContext(result, compilationContext, assemblyName, options).Assembly;
        }

        public static EmitResult EmitWithContext(
            AnalysisResult result,
            Semantics.CompilationContext compilationContext,
            string assemblyName = "NgoProgram",
            EmitOptions? options = null)
        {
            if (result.HasErrors)
            {
                throw new InvalidOperationException("Cannot emit assembly from source with errors.");
            }

            var asmName = new AssemblyName(assemblyName);
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
            var moduleBuilder = asmBuilder.DefineDynamicModule(assemblyName);

            var ctx = EmitCore(result, moduleBuilder, options, compilationContext);

            // For ngo run: compile CGo static libs and link to temp directory
            if (compilationContext.CgoPreamble != null && compilationContext.CgoPreamble.HasCSource)
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "ngo", "run");
                Directory.CreateDirectory(tempDir);
                CompileCgoNativeLibrary(compilationContext, Path.Combine(tempDir, "dummy.dll"));

                // Add temp dir to native library search path
                var nativeLibDir = compilationContext.CgoResult?.NativeLibraryPath != null
                    ? Path.GetDirectoryName(compilationContext.CgoResult.NativeLibraryPath) ?? tempDir
                    : tempDir;
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH",
                    nativeLibDir + ":" + (Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? ""));
                Environment.SetEnvironmentVariable("PATH",
                    nativeLibDir + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? ""));
            }

            return new EmitResult(asmBuilder, ctx.ILTraces);
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

            // If CGo is in use, compile C code and copy native library to output directory
            if (compilationContext.CgoPreamble != null && compilationContext.CgoPreamble.HasCSource)
            {
                CompileCgoNativeLibrary(compilationContext, outputPath);
            }
        }

        /// <summary>
        /// Compile CGo C preamble to a static library (.a), then link all static libs
        /// into a single shared library placed next to the output assembly.
        /// </summary>
        private static void CompileCgoNativeLibrary(Semantics.CompilationContext compilation, string outputPath)
        {
            var preamble = compilation.CgoPreamble;
            if (preamble == null || !preamble.HasCSource)
            {
                return;
            }

            var driver = new Cgo.CCompilerDriver();
            var resolution = driver.Resolve(compilation.CgoOptions);

            var cacheDir = Path.Combine(Path.GetTempPath(), "ngo", "cache");
            var cgoCompiler = new Cgo.CgoCompiler(cacheDir, driver, resolution);

            var probeRequest = new Cgo.CgoProbeRequest();
            probeRequest.TypeSizes.Add("int");
            probeRequest.TypeSizes.Add("long");
            probeRequest.TypeSizes.Add("unsigned long");

            var result = cgoCompiler.Compile(preamble, probeRequest, "main");

            if (result.NativeLibraryPath == null)
            {
                return;
            }

            string outputDir = Path.GetDirectoryName(outputPath) ?? ".";
            var staticLibs = new List<string> { result.NativeLibraryPath };
            string ldflags = result.LDFlags ?? string.Empty;

            if (preamble.CSource.Contains("#include <math.h>") && !ldflags.Contains("-lm"))
            {
                ldflags = string.IsNullOrEmpty(ldflags) ? "-lm" : ldflags + " -lm";
            }

            result.NativeLibraryPath = driver.LinkStaticLibraries(
                staticLibs, outputDir, "ngo_native", ldflags);
            compilation.CgoResult = result;

            if (compilation.ProjectRoot != null)
            {
                var pkgCacheDir = NgoArchive.GetCacheDir(compilation.ProjectRoot);
                Cgo.CgoArchiveManager.SaveCgoMetadata(
                    NgoArchive.GetArchivePath(pkgCacheDir, "main"), result);
            }
        }

        /// <summary>
        /// Shared 3-pass emit logic used by both Emit and EmitToFile.
        /// </summary>
        private static EmitContext EmitCore(AnalysisResult result, ModuleBuilder moduleBuilder, EmitOptions? options, Semantics.CompilationContext compilationContext)
        {
            var mapper = new TypeMapper(compilationContext);
            var ctx = new EmitContext(new LiveModuleBuilder(moduleBuilder), mapper, options, compilationContext.Log);
            mapper.SetEmitContext(ctx);

            if (options?.TracedMethodNames != null)
            {
                ctx.TracedMethodNames = options.TracedMethodNames;
            }

            // Link dependency IL from .ngo archives on disk
            if (compilationContext.ProjectRoot != null)
            {
                LinkDependencies(result.Root, ctx, compilationContext, ctx.LinkedPackages);
            }

            // Emit CGo P/Invoke stubs BEFORE package emission
            // so method bodies can resolve C.funcname() calls
            if (compilationContext.CgoPreamble != null)
            {
                Cgo.CgoPInvokeEmitter.Emit(ctx, compilationContext);
            }

            // Emit the main package
            EmitPackage(result.Root, ctx);


            // Apply [UnmanagedCallersOnly] to //export functions
            if (compilationContext.CgoExports != null && compilationContext.CgoExports.Count > 0)
            {
                ApplyCgoExports(result.Root, ctx, compilationContext);
            }

            return ctx;
        }

        /// <summary>
        /// Apply [UnmanagedCallersOnly] attribute to Go functions marked with //export.
        /// This makes them callable from C via reverse P/Invoke.
        /// </summary>
        private static void ApplyCgoExports(Ast.SourceFile root, EmitContext ctx, Semantics.CompilationContext compilation)
        {
            var exports = compilation.CgoExports;
            if (exports == null) return;

            foreach (var func in root.Functions)
            {
                if (exports.TryGetValue(func.Symbol.Name, out var exportName))
                {
                    if (ctx.Methods.TryGetValue(func.Symbol, out var methodBuilder))
                    {
                        try
                        {
                            // Apply [UnmanagedCallersOnly(EntryPoint = "exportName")]
                            var attr = new System.Reflection.Emit.CustomAttributeBuilder(
                                typeof(System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute)
                                    .GetConstructor(Type.EmptyTypes)!,
                                Array.Empty<object>(),
                                new[] {
                                    typeof(System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute)
                                        .GetField("EntryPoint")!
                                },
                                new object[] { exportName });
                            methodBuilder.SetCustomAttribute(attr);
                        }
                        catch (Exception ex)
                        {
                            compilation.Diagnostics.ReportError(new Language.TextSpan(0, 0), ErrorCode.InternalError,
                                $"cgo: failed to apply //export to '{func.Symbol.Name}': {ex.GetType().Name}: {ex.Message}");
                            compilation.Log.Error($"cgo: failed to apply //export to '{func.Symbol.Name}':\n{ex}");
                        }
                    }
                }
            }
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

                // Skip the C pseudo-package — handled by CGo P/Invoke emission
                if (importPath == "C")
                {
                    continue;
                }

                // Try to link from .ngo archive first, then fall back to source compilation
                bool linked2 = false;
                if (compilationContext.ProjectRoot != null)
                {
                    linked2 = LinkPackageWithDeps(importPath, import.Package, ctx, compilationContext, linked);
                }

                if (!linked2)
                {
                    EmitDependencyFromSource(importPath, import.Package, ctx, compilationContext);
                }
            }
        }

        private static bool LinkPackageWithDeps(string importPath, PackageSymbol pkg,
            EmitContext ctx, Semantics.CompilationContext compilationContext, HashSet<string> linked)
        {
            // Recursively link dependencies first
            foreach (var depImport in pkg.Imports)
            {
                if (linked.Contains(depImport)
                    || RuntimePackageResolver.Instance.Resolve(depImport) != null
                    || depImport == "C")
                {
                    continue;
                }

                linked.Add(depImport);
                var depPkg = compilationContext.ResolvePackage(depImport);
                if (depPkg != null)
                {
                    bool depLinked = LinkPackageWithDeps(depImport, depPkg, ctx, compilationContext, linked);
                    if (!depLinked)
                    {
                        EmitDependencyFromSource(depImport, depPkg, ctx, compilationContext);
                    }
                }
            }

            // Link this package from archive
            var cacheDir = NgoArchive.GetCacheDir(compilationContext.ProjectRoot!);
            var sourceDir = compilationContext.GetSourceDir(importPath);
            var archivePath = NgoArchive.GetArchivePath(cacheDir, importPath, sourceDir);
            if (ILSerializer.LinkFromArchive(archivePath, pkg, ctx))
            {
                return true;
            }
            archivePath = NgoArchive.GetArchivePath(cacheDir, importPath);
            return ILSerializer.LinkFromArchive(archivePath, pkg, ctx);
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
                    trees.Add(Language.SyntaxTree.Parse(source, file));
                }

                if (trees.Count == 0)
                {
                    return;
                }

                var result = SemanticAnalyzer.Analyze(trees, compilationContext);

                // If analysis has errors, don't try to emit broken code
                if (result.HasErrors)
                {
                    var errorCount = result.Errors.Count(e => e.Severity == ErrorSeverity.Error);
                    var firstErr = result.Errors.FirstOrDefault(e => e.Severity == ErrorSeverity.Error);
                    compilationContext.Log.Warn($"dependency '{importPath}' has {errorCount} errors, skipping emission. First: {firstErr?.Message}");
                    return;
                }

                // Recursively link this dependency's own dependencies first
                LinkDependencies(result.Root, ctx, compilationContext, ctx.LinkedPackages);

                ctx.IsDependencyEmit = true;
                ctx.CurrentPackagePath = importPath;

                EmitPackage(result.Root, ctx);
                ctx.IsDependencyEmit = false;
                ctx.CurrentPackagePath = null;

                // Register the package type in LinkedTypes so archive linking can find it
                if (ctx.PackageType is Builder.LiveTypeBuilder pkgLiveTb)
                {
                    ctx.LinkedTypes[pkgLiveTb.Inner.Name] = pkgLiveTb.Inner;
                }

                // Bridge: map original PackageSymbol's exports to the freshly emitted methods.
                // The main package's AST references the ORIGINAL FunctionSymbol instances,
                // but EmitPackage registered the NEW ones from re-analysis.
                // Match by name and register in CachedMethods so EmitCall can find them.
                foreach (var export in originalPkg.Exports)
                {
                    if (export.Value is FunctionSymbol origFunc)
                    {
                        var packageTypeName = ctx.PackageType?.AsType().Name;
                        foreach (var kvp in ctx.Methods)
                        {
                            if (kvp.Key.Name == origFunc.Name
                                && kvp.Value is Builder.LiveMethodBuilder liveBuilder)
                            {
                                var methodBuilder = liveBuilder.Inner;
                                if (methodBuilder.DeclaringType?.Name == packageTypeName)
                                {
                                    ctx.CachedMethods[origFunc] = methodBuilder;
                                    // Also register in LinkedMethods so archive linking
                                    // can resolve cross-package method tokens
                                    var linkedKey = packageTypeName + "." + origFunc.Name;
                                    ctx.LinkedMethods[linkedKey] = methodBuilder;
                                    ctx.LinkedMethods[origFunc.Name] = methodBuilder;
                                    break;
                                }
                            }
                        }
                    }
                    else if (export.Value is Symbols.StructTypeSymbol origStruct)
                    {
                        // Bridge struct methods
                        foreach (var origMethod in origStruct.Methods)
                        {
                            foreach (var kvp in ctx.Methods)
                            {
                                if (kvp.Key.Name == origMethod.Name && kvp.Key is MethodSymbol
                                    && kvp.Value is Builder.LiveMethodBuilder liveBuilder)
                                {
                                    ctx.CachedMethods[origMethod] = liveBuilder.Inner;
                                    break;
                                }
                            }
                        }

                        // Bridge struct fields
                        foreach (var origField in origStruct.Fields)
                        {
                            if (!ctx.StructFields.ContainsKey(origField))
                            {
                                foreach (var kvp in ctx.StructFields)
                                {
                                    if (kvp.Key.Name == origField.Name)
                                    {
                                        ctx.StructFields[origField] = kvp.Value;
                                        break;
                                    }
                                }
                            }
                        }

                        // Bridge struct types: map the original symbol to the same TypeBuilder
                        foreach (var kvp in ctx.StructTypes)
                        {
                            if (kvp.Key.Name == origStruct.Name)
                            {
                                var structClrType = kvp.Value.AsType();
                                ctx.Mapper.Register(origStruct, structClrType);
                                compilationContext.RegisterSourceCompiledType(
                                    importPath, origStruct.Name, structClrType);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to emit dependency '{importPath}' from source: {ex.Message}", ex);
            }
        }

        private static void EmitPackage(Ast.SourceFile root, EmitContext ctx)
        {
            var packageName = root.Package.Symbol.Name;

            // Create the package static class (skip if already defined from another dependency)
            var previousPackageType = ctx.PackageType;
            // For dependencies, use bare package name to avoid cross-package qualification issues.
            // For the main package, use QualifyName to support library namespace.
            var pkgTypeName = ctx.IsDependencyEmit ? packageName : ctx.QualifyName(packageName);
            Builder.ITypeBuilder? existingPkgType = null;
            // Check if this package type was already created
            if (ctx.PackageTypes.TryGetValue(pkgTypeName, out existingPkgType))
            {
                ctx.PackageType = existingPkgType;
            }
            else
            {
                ctx.PackageType = ctx.Module.DefineType(
                    pkgTypeName,
                    TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                ctx.PackageTypes[pkgTypeName] = ctx.PackageType;
                ctx.Definitions.RegisterType(pkgTypeName, ctx.PackageType);
            }

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

            // Finalize interface types first — they don't depend on struct layouts.
            foreach (var kvp in ctx.InterfaceTypes)
            {
                if (!ctx.FinalizedTypes.Contains(kvp.Key))
                {
                    kvp.Value.CreateType();
                    ctx.FinalizedTypes.Add(kvp.Key);
                }
            }

            // Finalize struct types in dependency order. For each struct:
            // define fields, CreateType, register. One pass, no retries.
            {
                var sortedStructs = TopologicalSortStructs(ctx);
                foreach (var structType in sortedStructs)
                {
                    if (ctx.FinalizedTypes.Contains(structType))
                    {
                        continue;
                    }
                    if (!ctx.StructTypes.TryGetValue(structType, out var typeBuilder))
                    {
                        continue;
                    }

                    declEmitter.PopulateStructFields(structType);

                    // CreateType any InlineArray types BEFORE the struct that uses them as fields.
                    // InlineArray types are created on-demand by TypeMapper when field types are mapped.
                    CreatePendingInlineArrays(ctx);

                    typeBuilder.CreateType();
                    ctx.FinalizedTypes.Add(structType);

                    // Register for cross-package resolution by path+name.
                    // The TypeBuilder is already in _typeCache from DefineStructType.
                    if (ctx.CurrentPackagePath != null && !string.IsNullOrEmpty(structType.Name))
                    {
                        ctx.Mapper.RegisterSourceCompiledType(
                            ctx.CurrentPackagePath, structType.Name, typeBuilder.AsType());
                    }
                    if (ctx.IsDependencyEmit && typeBuilder is Builder.LiveTypeBuilder liveTb)
                    {
                        ctx.LinkedTypes[liveTb.Inner.Name] = liveTb.Inner;
                        if (liveTb.Inner.FullName != null && liveTb.Inner.FullName != liveTb.Inner.Name)
                        {
                            ctx.LinkedTypes[liveTb.Inner.FullName] = liveTb.Inner;
                        }
                    }
                }
            }

            ctx.Mapper.PromoteTypeBuilders();

            // Pass 2: Define all function and method signatures
            foreach (var func in root.Functions)
            {
                declEmitter.EmitFunction(func);
            }

            foreach (var method in root.Methods)
            {
                declEmitter.EmitMethod(method);
            }

            // Register all declared methods in LinkedMethods for cross-archive resolution
            if (ctx.IsDependencyEmit)
            {
                var packageTypeName = ctx.PackageType?.AsType().Name;
                foreach (var kvp in ctx.Methods)
                {
                    if (kvp.Value is Builder.LiveMethodBuilder liveBuilder
                        && liveBuilder.Inner.DeclaringType?.Name == packageTypeName)
                    {
                        var methodBuilder = liveBuilder.Inner;
                        var fullKey = packageTypeName + "." + methodBuilder.Name;
                        if (!ctx.LinkedMethods.ContainsKey(fullKey))
                        {
                            ctx.LinkedMethods[fullKey] = methodBuilder;
                        }
                        if (!ctx.LinkedMethods.ContainsKey(methodBuilder.Name))
                        {
                            ctx.LinkedMethods[methodBuilder.Name] = methodBuilder;
                        }
                    }
                }
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
            // Also emit if CGo resolver needs initialization
            if (root.Variables.Count > 0 || initFuncs.Count > 0 || ctx.CgoResolverInitMethod != null)
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

        private static List<StructTypeSymbol> TopologicalSortStructs(EmitContext ctx)
        {
            var pending = new Dictionary<StructTypeSymbol, HashSet<StructTypeSymbol>>();
            foreach (var kvp in ctx.StructTypes)
            {
                if (ctx.FinalizedTypes.Contains(kvp.Key))
                {
                    continue;
                }
                if (kvp.Key is StructTypeSymbol structSym)
                {
                    var deps = new HashSet<StructTypeSymbol>();
                    foreach (var field in structSym.Fields)
                    {
                        CollectStructDependencies(field.Type, structSym, ctx, deps, new HashSet<TypeSymbol>());
                    }
                    pending[structSym] = deps;
                }
            }

            // Kahn's algorithm
            var inDegree = new Dictionary<StructTypeSymbol, int>();
            foreach (var kvp in pending)
            {
                if (!inDegree.ContainsKey(kvp.Key))
                {
                    inDegree[kvp.Key] = 0;
                }
                foreach (var dep in kvp.Value)
                {
                    if (pending.ContainsKey(dep))
                    {
                        inDegree.TryGetValue(kvp.Key, out var current);
                        inDegree[kvp.Key] = current + 1;
                    }
                }
            }

            var queue = new Queue<StructTypeSymbol>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                {
                    queue.Enqueue(kvp.Key);
                }
            }

            var sorted = new List<StructTypeSymbol>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                sorted.Add(current);
                foreach (var kvp in pending)
                {
                    if (kvp.Value.Contains(current))
                    {
                        inDegree[kvp.Key]--;
                        if (inDegree[kvp.Key] == 0)
                        {
                            queue.Enqueue(kvp.Key);
                        }
                    }
                }
            }

            // Remaining nodes (cycles through reference types — safe to create in any order)
            foreach (var kvp in pending)
            {
                if (!sorted.Contains(kvp.Key))
                {
                    sorted.Add(kvp.Key);
                }
            }

            return sorted;
        }

        private static void CollectStructDependencies(TypeSymbol type, StructTypeSymbol self,
            EmitContext ctx, HashSet<StructTypeSymbol> result, HashSet<TypeSymbol> visited)
        {
            if (type == null || !visited.Add(type))
            {
                return;
            }

            if (type is StructTypeSymbol structDep && structDep != self
                && ctx.StructTypes.ContainsKey(structDep)
                && !ctx.FinalizedTypes.Contains(structDep))
            {
                result.Add(structDep);
            }

            // Recurse into composite types to find struct dependencies
            if (type is SliceTypeSymbol slice)
            {
                CollectStructDependencies(slice.ElementType, self, ctx, result, visited);
            }
            else if (type is ArrayTypeSymbol arr)
            {
                CollectStructDependencies(arr.ElementType, self, ctx, result, visited);
            }
            else if (type is MapTypeSymbol map)
            {
                CollectStructDependencies(map.KeyType, self, ctx, result, visited);
                CollectStructDependencies(map.ValueType, self, ctx, result, visited);
            }
            else if (type is PointerTypeSymbol ptr && ptr.ElementType != null)
            {
                CollectStructDependencies(ptr.ElementType, self, ctx, result, visited);
            }
            else if (type is ChannelTypeSymbol chan)
            {
                CollectStructDependencies(chan.ElementType, self, ctx, result, visited);
            }
            else if (type.UnderlyingType != null && type.GetType() == typeof(TypeSymbol))
            {
                CollectStructDependencies(type.UnderlyingType, self, ctx, result, visited);
            }
        }


        private static void CreatePendingInlineArrays(EmitContext ctx)
        {
            foreach (var kvp in ctx.InlineArrayTypes)
            {
                if (kvp.Value is TypeBuilder inlineTb && !inlineTb.IsCreated())
                {
                    inlineTb.CreateType();
                }
            }
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
