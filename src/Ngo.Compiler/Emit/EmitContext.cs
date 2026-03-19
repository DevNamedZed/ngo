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
        public EmitContext(IModuleBuilder module, TypeMapper mapper, EmitOptions? options = null)
        {
            Module = module;
            Mapper = mapper;
            Options = options ?? EmitOptions.Default;
        }

        public IModuleBuilder Module { get; }
        public TypeMapper Mapper { get; }
        public EmitOptions Options { get; }
        public ITypeBuilder PackageType { get; set; } = null!;

        // Track types that have been finalized (CreateType called) across packages
        public HashSet<TypeSymbol> FinalizedTypes { get; } = new();
        public DeclarationEmitter? DeclEmitter { get; set; }

        // Per-method state (reset for each method body)
        public CilWriter IL { get; set; } = null!;
        public Dictionary<Symbol, LocalBuilder> Locals { get; } = new();
        public Dictionary<Symbol, int> Parameters { get; } = new();

        // Symbols captured by closures in the current function body (stored in Box<T>)
        public HashSet<Symbol> CapturedSymbols { get; } = new();

        // All emitted methods (for resolving calls)
        public Dictionary<Symbol, IMethodBuilder> Methods { get; } = new();

        // Methods from cached/precompiled assemblies (for resolving calls to cached packages)
        public Dictionary<Symbol, MethodInfo> CachedMethods { get; } = new();

        // CGo native library resolver initializer (called from .cctor)
        public IMethodBuilder? CgoResolverInitMethod { get; set; }

        // Loop label stack for break/continue
        public Stack<LoopLabel> LoopLabels { get; } = new();

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
            Options.Namespace != null ? $"{Options.Namespace}.{name}" : name;

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
                        return TypeBuilder.GetConstructor(type, baseCtor);
                }
                // Param types contain TypeBuilders or exact match failed — match by count
                foreach (var ctor in genericDef.GetConstructors())
                {
                    if (ctor.GetParameters().Length == paramTypes.Length)
                        return TypeBuilder.GetConstructor(type, ctor);
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
                    return TypeBuilder.GetField(type, baseField);
            }
            return type.GetField(name)!;
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
                    return TypeBuilder.GetMethod(type, baseMethod);
            }

            if (paramTypes != null)
            {
                if (paramsHaveTB)
                {
                    foreach (var m in type.GetMethods())
                    {
                        if (m.Name == name && m.GetParameters().Length == paramTypes.Length)
                            return m;
                    }
                }
                return type.GetMethod(name, paramTypes)!;
            }
            return type.GetMethod(name)!;
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
                    return TypeBuilder.GetMethod(type, baseGetter);
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

        private static bool HasAnyTypeBuilder(Type[] types)
        {
            foreach (var t in types)
            {
                if (t is TypeBuilder || t is GenericTypeParameterBuilder)
                    return true;
                if (t.IsGenericType && HasTypeBuilderArgs(t))
                    return true;
            }
            return false;
        }

        private static bool HasTypeBuilderArgs(Type type)
        {
            if (!type.IsGenericType || type.IsGenericTypeDefinition)
                return false;
            foreach (var arg in type.GetGenericArguments())
            {
                if (arg is TypeBuilder || arg is GenericTypeParameterBuilder)
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
            CapturedSymbols.Clear();
            DeferStack = null;
            DeferReturnLocal = null;
        }
    }
}
