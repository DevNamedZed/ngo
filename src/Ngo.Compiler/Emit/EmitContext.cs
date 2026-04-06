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
        }

        public IModuleBuilder Module { get; }
        public TypeMapper Mapper { get; }
        public EmitOptions Options { get; }
        public ICompilerLog Log { get; }
        public ITypeBuilder PackageType { get; set; } = null!;

        // Track types that have been finalized (CreateType called) across packages
        public HashSet<TypeSymbol> FinalizedTypes { get; } = new();

        // Track packages already compiled from source to avoid re-analysis
        public HashSet<string> LinkedPackages { get; } = new();

        // InlineArray types shared across all TypeMapper instances in this compilation
        public Dictionary<(Type elementType, int length), Type> InlineArrayTypes { get; } = new();

        // InlineArray TypeBuilders whose element type was a TypeBuilder at creation time.
        // These must be CreateType'd after their element types are finalized.
        public List<(TypeBuilder Builder, int Length)> PendingInlineArrayTypes { get; } = new();

        public DeclarationEmitter? DeclEmitter { get; set; }

        // Per-method state (reset for each method body)
        public CilWriter IL { get; set; } = null!;
        public Dictionary<Symbol, LocalBuilder> Locals { get; } = new();
        public Dictionary<Symbol, int> Parameters { get; } = new();

        // Symbols captured by closures in the current function body (stored in Box<T>)
        public HashSet<Symbol> CapturedSymbols { get; } = new();

        // Generic parameters of the current enclosing function (for closure/lambda propagation)
        public string[] EnclosingGenericParamNames { get; set; } = Array.Empty<string>();
        public Symbols.TypeParameterSymbol[] EnclosingGenericParamSymbols { get; set; } = Array.Empty<Symbols.TypeParameterSymbol>();

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

        // Generic method metadata for instantiation during IL replay
        public Dictionary<string, (string[] GenericParamNames, string[] ParamTypeNames)> MethodGenericInfo { get; } = new();

        private readonly Dictionary<string, MethodInfo> _crossPkgMethodCache = new();

        public MethodInfo GetCrossPackageMethod(Symbols.FunctionSymbol func, TypeMapper mapper)
        {
            var key = (func.PackageName ?? "") + "::" + func.Name;
            if (_crossPkgMethodCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var fullPackageName = func.PackageName ?? "unknown";
            var lastSlash = fullPackageName.LastIndexOf('/');
            var shortPackageName = lastSlash >= 0 ? fullPackageName.Substring(lastSlash + 1) : fullPackageName;
            var declaringType = new Builder.NgoProxyType(shortPackageName);

            var paramTypes = new Type[func.Parameters.Count];
            for (int i = 0; i < paramTypes.Length; i++)
            {
                paramTypes[i] = mapper.Map(func.Parameters[i].Type);
            }

            var returnType = mapper.MapReturnType(func.ReturnTypes);
            var proxy = new Builder.NgoProxyMethodInfo(declaringType, func.Name, paramTypes, returnType);
            _crossPkgMethodCache[key] = proxy;
            return proxy;
        }

        // CGo native library resolver initializer (called from .cctor)
        public IMethodBuilder? CgoResolverInitMethod { get; set; }

        // Loop label stack for break/continue
        public Stack<LoopLabel> LoopLabels { get; } = new();

        // Whether we're emitting a dependency package (errors are recoverable)
        public bool IsDependencyEmit { get; set; }

        // Track package types already defined to avoid duplicates across dependencies
        public Dictionary<string, ITypeBuilder> PackageTypes { get; } = new();

        // Fallthrough target label for switch cases
        public Label? FallthroughLabel { get; set; }

        // Goto target labels: "labelName" → IL label
        public Dictionary<string, Label> GotoLabels { get; } = new();

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

        // Defer stack local for the current method (null if no defer statements)
        public LocalBuilder? DeferStack { get; set; }

        // For non-void defer-wrapped functions: store return value here, then leave
        public LocalBuilder? DeferReturnLocal { get; set; }
        public Label DeferExitLabel { get; set; }

        public string QualifyName(string name) =>
            Options?.Namespace != null ? $"{Options.Namespace}.{name}" : name;

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

        /// <summary>
        /// Gets a constructor from a possibly TypeBuilder-instantiated generic type.
        /// When a generic type is instantiated with TypeBuilder args, normal GetConstructor fails.
        /// </summary>
        public static ConstructorInfo GetConstructorSafe(Type type, Type[] paramTypes)
        {
            bool typeHasTBArgs = HasTypeBuilderArgs(type);
            bool paramsHaveTB = HasAnyTypeBuilder(paramTypes);

            if (typeHasTBArgs)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (!paramsHaveTB)
                {
                    var baseCtor = genericDef.GetConstructor(paramTypes);
                    if (baseCtor != null)
                    {
                        try { return TypeBuilder.GetConstructor(type, baseCtor); }
                        catch (NotSupportedException) { return new Builder.NgoProxyConstructorInfo(type, paramTypes); }
                    }
                }
                foreach (var ctor in genericDef.GetConstructors())
                {
                    if (ctor.GetParameters().Length == paramTypes.Length)
                    {
                        try { return TypeBuilder.GetConstructor(type, ctor); }
                        catch (NotSupportedException) { return new Builder.NgoProxyConstructorInfo(type, paramTypes); }
                    }
                }
            }

            if (paramsHaveTB)
            {
                // Type is normal but params contain TypeBuilders — match by count
                foreach (var ctor in type.GetConstructors())
                {
                    if (ctor.GetParameters().Length == paramTypes.Length)
                        return ctor;
                }
            }

            return type.GetConstructor(paramTypes)!;
        }

        /// <summary>
        /// Gets a field from a possibly TypeBuilder-instantiated generic type.
        /// </summary>
        public static FieldInfo GetFieldSafe(Type type, string name)
        {
            if (HasTypeBuilderArgs(type))
            {
                var genericDef = type.GetGenericTypeDefinition();
                var baseField = genericDef.GetField(name);
                if (baseField != null)
                {
                    try
                    {
                        return TypeBuilder.GetField(type, baseField);
                    }
                    catch (NotSupportedException)
                    {
                        return new Builder.NgoProxyFieldInfo(type, name, baseField.FieldType);
                    }
                }
            }
            var field = type.GetField(name);
            if (field == null)
            {
                throw new InvalidOperationException(
                    $"Field '{name}' not found on type '{type.FullName ?? type.Name}'");
            }
            return field;
        }

        /// <summary>
        /// Gets a method from a possibly TypeBuilder-instantiated generic type.
        /// </summary>
        public static MethodInfo GetMethodSafe(Type type, string name, Type[]? paramTypes = null)
        {
            bool typeHasTBArgs = HasTypeBuilderArgs(type);
            bool paramsHaveTB = paramTypes != null && HasAnyTypeBuilder(paramTypes);

            if (typeHasTBArgs)
            {
                var genericDef = type.GetGenericTypeDefinition();
                MethodInfo? baseMethod = null;

                if (paramTypes != null && !paramsHaveTB)
                {
                    baseMethod = genericDef.GetMethod(name, paramTypes);
                }

                // Param types contain TypeBuilders or exact match failed — find by name + count
                if (baseMethod == null)
                {
                    foreach (var m in genericDef.GetMethods())
                    {
                        if (m.Name == name && (paramTypes == null || m.GetParameters().Length == paramTypes.Length))
                        {
                            baseMethod = m;
                            break;
                        }
                    }
                }

                if (baseMethod == null && paramTypes == null)
                    baseMethod = genericDef.GetMethod(name);

                if (baseMethod != null)
                {
                    try
                    {
                        return TypeBuilder.GetMethod(type, baseMethod);
                    }
                    catch (NotSupportedException)
                    {
                        var baseParams = baseMethod.GetParameters();
                        var proxyParams = new Type[baseParams.Length];
                        for (int pi = 0; pi < baseParams.Length; pi++)
                        {
                            proxyParams[pi] = baseParams[pi].ParameterType;
                        }
                        return new Builder.NgoProxyMethodInfo(type, name, proxyParams, baseMethod.ReturnType);
                    }
                }
            }

            if (paramTypes != null)
            {
                if (paramsHaveTB || IsNonRuntimeType(type))
                {
                    try
                    {
                        foreach (var m in type.GetMethods())
                        {
                            if (m.Name == name && m.GetParameters().Length == paramTypes.Length)
                                return m;
                        }
                    }
                    catch (NotSupportedException)
                    {
                        // TypeBuilderInstantiation doesn't support GetMethods()
                        // Use TypeBuilder.GetMethod for generic type instantiations
                        if (type.IsGenericType && type.GetGenericTypeDefinition() is System.Reflection.Emit.TypeBuilder genericDef)
                        {
                            var baseMethods = genericDef.GetMethods();
                            foreach (var baseMethod in baseMethods)
                            {
                                if (baseMethod.Name == name && baseMethod.GetParameters().Length == paramTypes.Length)
                                {
                                    return System.Reflection.Emit.TypeBuilder.GetMethod(type, baseMethod);
                                }
                            }
                        }
                    }
                }
                    try
                {
                    return type.GetMethod(name, paramTypes)!;
                }
                catch (NotSupportedException)
                {
                    // Verify the method exists on the generic definition before creating a proxy.
                    // This prevents fake proxies for methods that don't exist on the type.
                    if (type.IsGenericType)
                    {
                        var genDef = type.GetGenericTypeDefinition();
                        bool found = false;
                        foreach (var candidate in genDef.GetMethods())
                        {
                            if (candidate.Name == name && candidate.GetParameters().Length == paramTypes.Length)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            return null;
                        }
                    }
                    return new Builder.NgoProxyMethodInfo(type, name, paramTypes, typeof(object));
                }
            }
            try
            {
                var result = type.GetMethod(name);
                return result;
            }
            catch (NotSupportedException)
            {
                // Verify method exists on generic definition before creating proxy
                if (type.IsGenericType)
                {
                    var genDef = type.GetGenericTypeDefinition();
                    if (genDef.GetMethod(name) == null)
                    {
                        return null;
                    }
                }
                return new Builder.NgoProxyMethodInfo(type, name);
            }
        }

        /// <summary>
        /// Gets a property getter from a possibly TypeBuilder-instantiated generic type.
        /// </summary>
        public static MethodInfo GetPropertyGetterSafe(Type type, string name)
        {
            if (HasTypeBuilderArgs(type))
            {
                var genericDef = type.GetGenericTypeDefinition();
                var baseProp = genericDef.GetProperty(name);
                if (baseProp != null)
                {
                    var baseGetter = baseProp.GetGetMethod()!;
                    try
                    {
                        return TypeBuilder.GetMethod(type, baseGetter);
                    }
                    catch (NotSupportedException)
                    {
                        return new Builder.NgoProxyMethodInfo(type, baseGetter.Name,
                            Type.EmptyTypes, baseProp.PropertyType);
                    }
                }
            }
            return type.GetProperty(name)!.GetGetMethod()!;
        }

        /// <summary>
        /// Gets a property setter from a possibly TypeBuilder-instantiated generic type.
        /// </summary>
        public static MethodInfo GetPropertySetterSafe(Type type, string name)
        {
            if (HasTypeBuilderArgs(type))
            {
                var genericDef = type.GetGenericTypeDefinition();
                var baseProp = genericDef.GetProperty(name);
                if (baseProp != null)
                {
                    var baseSetter = baseProp.GetSetMethod()!;
                    return TypeBuilder.GetMethod(type, baseSetter);
                }
            }
            return type.GetProperty(name)!.GetSetMethod()!;
        }

        public static bool HasAnyTypeBuilderPublic(Type[] types) => HasAnyTypeBuilder(types);

        public static bool IsNonRuntimeType(Type type)
        {
            return type is TypeBuilder
                || type is GenericTypeParameterBuilder
                || type is Builder.NgoProxyType;
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
            DeferStack = null;
            DeferReturnLocal = null;
        }
    }
}
