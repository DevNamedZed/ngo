// -----------------------------------------------------------------------
// <copyright file="EmitContext.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Shared mutable context passed to all emitter components.
    /// Uses IModuleBuilder/ITypeBuilder/IMethodBuilder/IFieldBuilder abstractions.
    /// </summary>
    internal sealed class EmitContext
    {
        public EmitContext(IModuleBuilder module, TypeMapper mapper, EmitOptions? options = null,
            ICompilerLog? log = null)
        {
            Module = module;
            Mapper = mapper;
            Options = options ?? EmitOptions.Default;
            Log = log ?? NullLog.Instance;
            Definitions = new DefinitionTable();
        }

        public IModuleBuilder Module { get; }
        public TypeMapper Mapper { get; }
        public EmitOptions Options { get; }
        public ICompilerLog Log { get; }
        public DefinitionTable Definitions { get; }
        public ITypeBuilder PackageType { get; set; } = null!;

        // Track types that have been finalized (CreateType called) across packages
        public HashSet<TypeSymbol> FinalizedTypes { get; } = new();

        // Track packages already compiled from source to avoid re-analysis
        public HashSet<string> LinkedPackages { get; } = new();

        // InlineArray types shared across all TypeMapper instances in this compilation
        public Dictionary<(Type elementType, int length), Type> InlineArrayTypes { get; } = new();

        public DeclarationEmitter? DeclEmitter { get; set; }

        // Per-method state (reset for each method body)
        public CilWriter IL { get; set; } = null!;
        public Dictionary<Symbol, LocalSlot> Locals { get; } = new();
        public Dictionary<Symbol, int> Parameters { get; } = new();

        // Symbols captured by closures in the current function body (stored in Box<T>)
        public HashSet<Symbol> CapturedSymbols { get; } = new();

        // Generic parameters of the current enclosing function (for closure/lambda propagation)
        public string[] EnclosingGenericParamNames { get; set; } = Array.Empty<string>();
        public Symbols.TypeParameterSymbol[] EnclosingGenericParamSymbols { get; set; } = Array.Empty<Symbols.TypeParameterSymbol>();
        public Type[] EnclosingGenericParamTypes { get; set; } = Array.Empty<Type>();

        // Current package import path (set during dependency emit for external type detection)
        public string? CurrentPackagePath { get; set; }

        // All emitted methods (for resolving calls)
        public Dictionary<Symbol, IMethodBuilder> Methods { get; } = new();

        // Methods from cached/precompiled assemblies (for resolving calls to cached packages)
        public Dictionary<Symbol, MethodInfo> CachedMethods { get; } = new();

        // All linked methods by their IL-level name (e.g., "Scanner_Scan")
        public Dictionary<string, MethodBuilder> LinkedMethods { get; } = new();

        // All linked type builders across all archives (for cross-archive type resolution)
        public Dictionary<string, TypeBuilder> LinkedTypes { get; } = new();

        // All linked field builders across all archives (for cross-package variable access)
        public Dictionary<string, FieldBuilder> LinkedFields { get; } = new();

        private readonly Dictionary<string, Refs.MethodRef> _crossPkgMethodCache = new();

        public Refs.MethodRef GetCrossPackageMethod(Symbols.FunctionSymbol func, TypeMapper mapper)
        {
            var key = (func.PackageName ?? "") + "::" + func.Name;
            if (_crossPkgMethodCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var fullPackageName = func.PackageName ?? "unknown";
            var lastSlash = fullPackageName.LastIndexOf('/');
            var shortPackageName = lastSlash >= 0 ? fullPackageName.Substring(lastSlash + 1) : fullPackageName;
            var declaringType = Refs.TypeRef.ExternalPackage(fullPackageName, shortPackageName);

            var parameterTypeRefs = new Refs.TypeRef[func.Parameters.Count];
            for (int i = 0; i < parameterTypeRefs.Length; i++)
            {
                parameterTypeRefs[i] = Refs.TypeRef.FromRuntime(mapper.Map(func.Parameters[i].Type));
            }

            var returnTypeRef = Refs.TypeRef.FromRuntime(mapper.MapReturnType(func.ReturnTypes));
            var methodRef = Refs.MethodRef.MemberRef(
                declaringType, func.Name, parameterTypeRefs, returnTypeRef, isStatic: true);
            _crossPkgMethodCache[key] = methodRef;
            return methodRef;
        }

        // CGo native library resolver initializer (called from .cctor)
        public IMethodBuilder? CgoResolverInitMethod { get; set; }

        // Loop label stack for break/continue
        public Stack<LoopLabel> LoopLabels { get; } = new();

        // Whether we're emitting a dependency package (errors are recoverable)
        public bool IsDependencyEmit { get; set; }

        // IL tracing: set of method names to trace (e.g. "parse"). When non-null,
        // GetILWriter results for matching methods are wrapped in TracingCilWriter.
        public HashSet<string>? TracedMethodNames { get; set; }

        // Collected IL traces keyed by method name
        public Dictionary<string, IReadOnlyList<string>> ILTraces { get; } = new();

        // Track package types already defined to avoid duplicates across dependencies
        public Dictionary<string, ITypeBuilder> PackageTypes { get; } = new();

        // Fallthrough target label for switch cases
        public LabelSlot? FallthroughLabel { get; set; }

        // Goto target labels: "labelName" → IL label
        public Dictionary<string, LabelSlot> GotoLabels { get; } = new();

        // Named labels for labeled break/continue: "labelName" → (breakLabel, continueLabel)
        public Dictionary<string, LoopLabel> NamedLabels { get; } = new();

        // Package-level fields (var declarations)
        public Dictionary<Symbol, IFieldBuilder> PackageFields { get; } = new();

        // Struct type builders (for composite literals and field access)
        public Dictionary<TypeSymbol, ITypeBuilder> StructTypes { get; } = new();

        // Struct field builders (FieldSymbol → IFieldBuilder)
        public Dictionary<FieldSymbol, IFieldBuilder> StructFields { get; } = new();

        // Interface type builders (InterfaceTypeSymbol → ITypeBuilder)
        public Dictionary<InterfaceTypeSymbol, ITypeBuilder> InterfaceTypes { get; } = new();

        // Wrapper types for interface satisfaction: (concrete, interface) → WrapperTypeInfo
        public Dictionary<WrapperTypeKey, WrapperTypeInfo> WrapperTypes { get; } = new();

        // Slice-element pointer tracking: symbol → (slice local, index local)
        // for variables assigned &slice[i] on value-type elements.
        public Dictionary<Symbol, SliceElementPointer> SliceElementPointers { get; } = new();

        // Defer stack local for the current method (null if no defer statements)
        public LocalSlot? DeferStack { get; set; }

        // For non-void defer-wrapped functions: store return value here, then leave
        public LocalSlot? DeferReturnLocal { get; set; }
        public LabelSlot? DeferExitLabel { get; set; }

        public string QualifyName(string name) =>
            Options?.Namespace != null ? $"{Options.Namespace}.{name}" : name;

        public string QualifyCrossPackageType(string? packagePath, string typeName)
        {
            if (!IsDependencyEmit || string.IsNullOrEmpty(packagePath))
            {
                return QualifyName(typeName);
            }
            var sanitized = packagePath.Replace('/', '.');
            return QualifyName(sanitized + "." + typeName);
        }

        /// <summary>
        /// Qualifies a name with the current package prefix. Used for types/closures
        /// that need unique names across dependency packages.
        /// </summary>
        public string QualifyWithPackage(string name)
        {
            var baseName = QualifyName(name);
            if (baseName != name)
            {
                return baseName;
            }
            // No namespace — use package type name to avoid cross-dependency collisions
            var pkgName = PackageType?.AsType().Name;
            if (pkgName != null)
            {
                return $"{pkgName}.{name}";
            }
            return name;
        }

        public bool IsExported(string goName) =>
            goName.Length > 0 && char.IsUpper(goName[0]);

        public static bool HasAnyTypeBuilderPublic(Type[] types) => HasAnyTypeBuilder(types);

        public static bool IsNonRuntimeType(Type type)
        {
            return type is TypeBuilder
                || type is GenericTypeParameterBuilder
                || type is Builder.NgoBuilderType
                || type is Builder.NgoGenericParameterType;
        }

        private static bool HasAnyTypeBuilder(Type[] types)
        {
            foreach (var t in types)
            {
                if (IsNonRuntimeType(t))
                    return true;
                if (t.IsGenericType && HasTypeBuilderArgs(t))
                    return true;
                if (t.IsArray)
                {
                    var elemType = t.GetElementType();
                    if (elemType != null && IsNonRuntimeType(elemType))
                        return true;
                    if (elemType != null && elemType.IsGenericType && HasTypeBuilderArgs(elemType))
                        return true;
                }
            }
            return false;
        }

        public static bool HasTypeBuilderArgs(Type type)
        {
            if (!type.IsGenericType || type.IsGenericTypeDefinition)
                return false;
            foreach (var arg in type.GetGenericArguments())
            {
                if (IsNonRuntimeType(arg))
                    return true;
                if (arg.IsGenericType && HasTypeBuilderArgs(arg))
                    return true;
            }
            return false;
        }

        public void ResetMethodState()
        {
            Locals.Clear();
            Parameters.Clear();
            NamedLabels.Clear();
            GotoLabels.Clear();
            LoopLabels.Clear();
            CapturedSymbols.Clear();
            SliceElementPointers.Clear();
            DeferStack = null;
            DeferReturnLocal = null;
        }
    }
}
