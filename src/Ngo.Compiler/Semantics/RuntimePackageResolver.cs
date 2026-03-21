// -----------------------------------------------------------------------
// <copyright file="RuntimePackageResolver.cs" company="Ziad">
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
using Ngo.Compiler.Emit;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;
using Ngo.Runtime.Discovery;

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Resolves Go packages implemented in Ngo.Runtime.dll via [GoPackage] attributes.
    /// Lazy singleton — Ngo.Runtime.dll is immutable for the process lifetime,
    /// so caching reflection results is correct and safe.
    /// </summary>
    public sealed class RuntimePackageResolver : IPackageResolver
    {
        private static readonly Lazy<RuntimePackageResolver> _instance = new(Build);
        public static RuntimePackageResolver Instance => _instance.Value;

        private readonly Dictionary<string, PackageSymbol> _packages;
        private readonly Dictionary<ClrTypeKey, Type> _clrTypes;

        private RuntimePackageResolver(
            Dictionary<string, PackageSymbol> packages,
            Dictionary<ClrTypeKey, Type> clrTypes)
        {
            _packages = packages;
            _clrTypes = clrTypes;
        }

        public PackageSymbol? Resolve(string importPath)
        {
            _packages.TryGetValue(importPath, out var pkg);
            return pkg;
        }

        /// <summary>
        /// Resolves a package by its short name (last segment of import path).
        /// Used for cross-package type resolution during .ngo deserialization.
        /// </summary>
        public PackageSymbol? ResolveByName(string shortName)
        {
            // Direct match first (e.g., "io", "fmt", "os")
            if (_packages.TryGetValue(shortName, out var direct))
                return direct;

            // Search for sub-packages (e.g., "atomic" → "sync/atomic")
            foreach (var kvp in _packages)
            {
                var lastSlash = kvp.Key.LastIndexOf('/');
                if (lastSlash >= 0 && kvp.Key.Substring(lastSlash + 1) == shortName)
                    return kvp.Value;
            }

            return null;
        }

        public Type? ResolveClrType(string importPath, string typeName)
        {
            _clrTypes.TryGetValue(new ClrTypeKey(importPath, typeName), out var type);
            return type;
        }

        private static RuntimePackageResolver Build()
        {
            var packages = new Dictionary<string, PackageSymbol>();
            var clrTypes = new Dictionary<ClrTypeKey, Type>();

            var asm = typeof(GoPackageAttribute).Assembly;
            var packageTypes = new Dictionary<string, Type>();
            var typesByPkg = new Dictionary<string, List<Type>>();

            foreach (var type in asm.GetTypes())
            {
                var pkgAttr = type.GetCustomAttribute<GoPackageAttribute>();
                if (pkgAttr != null)
                {
                    packageTypes[pkgAttr.ImportPath] = type;
                    // Also register as a type if it has [GoType] (e.g. context.Context)
                    var dualTypeAttr = type.GetCustomAttribute<GoTypeAttribute>();
                    if (dualTypeAttr?.Package != null)
                    {
                        if (!typesByPkg.TryGetValue(dualTypeAttr.Package, out var dualList))
                        {
                            dualList = new List<Type>();
                            typesByPkg[dualTypeAttr.Package] = dualList;
                        }
                        dualList.Add(type);
                    }
                    continue;
                }

                var typeAttr = type.GetCustomAttribute<GoTypeAttribute>();
                if (typeAttr?.Package != null)
                {
                    if (!typesByPkg.TryGetValue(typeAttr.Package, out var list))
                    {
                        list = new List<Type>();
                        typesByPkg[typeAttr.Package] = list;
                    }
                    list.Add(type);
                }
            }

            var typesByPackage = new Dictionary<string, Type[]>();
            foreach (var kvp in typesByPkg)
                typesByPackage[kvp.Key] = kvp.Value.ToArray();

            // Synthetic types for Go-source-only packages referenced by runtime GoField/GoParam annotations.
            // These are packages compiled from Go source (not runtime), but runtime types reference their types
            // via GoField Type attributes (e.g., "*parse.Tree"). Without these, the type becomes a broken placeholder.
            var syntheticTypes = new Dictionary<ClrTypeKey, TypeSymbol>();
            {
                // text/template/parse.Tree — referenced by text/template and html/template Tree fields
                var treeFields = new[]
                {
                    new FieldSymbol("Name", BuiltinTypes.String, 0),
                    new FieldSymbol("ParseName", BuiltinTypes.String, 1),
                    new FieldSymbol("Root", BuiltinTypes.EmptyInterface, 2),
                    new FieldSymbol("Mode", BuiltinTypes.Uint, 3),
                };
                var treeStruct = new StructTypeSymbol("Tree", treeFields);
                treeStruct.PackagePath = "text/template/parse";
                var copyMethod = new MethodSymbol("Copy", treeStruct, true,
                    System.Array.Empty<ParameterSymbol>(),
                    new TypeSymbol[] { new PointerTypeSymbol(treeStruct) });
                treeStruct.AddMethod(copyMethod);
                syntheticTypes[new ClrTypeKey("parse", "Tree")] = treeStruct;
                syntheticTypes[new ClrTypeKey("text/template/parse", "Tree")] = treeStruct;
            }

            // Cross-package type resolver: looks up types from already-built packages
            System.Func<string, string, TypeSymbol?> crossPkgResolver = (crossPkgName, typeName) =>
            {
                foreach (var kvp2 in packages)
                {
                    // Match by short package name (e.g., "big") or full import path (e.g., "math/big")
                    if (GetPackageName(kvp2.Key) == crossPkgName || kvp2.Key == crossPkgName)
                    {
                        var export = kvp2.Value.LookupExport(typeName);
                        if (export is TypeSymbol ts) return ts;
                    }
                }
                // Fallback: check synthetic types for Go-source-only packages
                if (syntheticTypes.TryGetValue(new ClrTypeKey(crossPkgName, typeName), out var synthetic))
                    return synthetic;
                return null;
            };

            // Pass 1a: Forward-declare all type names for every package (no fields/methods yet)
            var packageBuildInfo = new Dictionary<string, PackageBuildInfo>();
            foreach (var kvp in packageTypes)
            {
                var importPath = kvp.Key;
                var clrType = kvp.Value;
                var pkgName = GetPackageName(importPath);
                var pkg = new PackageSymbol(pkgName, importPath);
                var typeMap = new Dictionary<string, TypeSymbol>();
                DeclareTypes(clrType, importPath, pkg, typeMap, typesByPackage, clrTypes, crossPkgResolver);
                packages[importPath] = pkg;
                packageBuildInfo[importPath] = new PackageBuildInfo(clrType, pkg, typeMap);
            }

            // Pass 1b: Populate struct fields and type methods (all type names now registered across packages)
            foreach (var kvp in packageBuildInfo)
            {
                var importPath = kvp.Key;
                var info = kvp.Value;
                PopulateTypeMembers(info.ClrType, importPath, info.Package, info.TypeMap, typesByPackage, clrTypes, crossPkgResolver);
            }

            // Pass 2: Build methods/functions/vars (all cross-package types now available)
            foreach (var kvp in packageBuildInfo)
            {
                var importPath = kvp.Key;
                var info = kvp.Value;
                BuildPackageMembers(info.ClrType, importPath, info.Package, info.TypeMap, packages);
            }

            return new RuntimePackageResolver(packages, clrTypes);
        }

        private static void BuildPackageMembers(Type clrType, string importPath,
            PackageSymbol pkg, Dictionary<string, TypeSymbol> typeMap,
            Dictionary<string, PackageSymbol> allPackages)
        {
            var pkgName = GetPackageName(importPath);

            // Cross-package type resolver: looks up types from already-built packages
            System.Func<string, string, TypeSymbol?> crossPkgResolver = (crossPkgName, typeName) =>
            {
                foreach (var kvp in allPackages)
                {
                    if (GetPackageName(kvp.Key) == crossPkgName || kvp.Key == crossPkgName)
                    {
                        var export = kvp.Value.LookupExport(typeName);
                        if (export is TypeSymbol ts) return ts;
                    }
                }
                return null;
            };

            foreach (var method in clrType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName) continue;

                var attr = method.GetCustomAttribute<GoFuncAttribute>();
                var goName = attr?.Name ?? method.Name;
                var isVariadic = attr?.IsVariadic ?? HasParamsArray(method);
                var parameters = BuildParameters(method, typeMap, isVariadic, crossPkgResolver);
                var returnTypes = BuildReturnTypes(method, typeMap, crossPkgResolver);
                pkg.AddExport(new FunctionSymbol(goName, parameters, returnTypes,
                    isVariadic, pkgName));
            }

            foreach (var field in clrType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var varAttr = field.GetCustomAttribute<GoVarAttribute>();
                var constAttr = field.GetCustomAttribute<GoConstAttribute>();

                if (constAttr != null)
                {
                    var goName = constAttr.Name ?? field.Name;
                    var goType = constAttr.Type != null
                        ? PackageMetadataSerializer.StringToType(constAttr.Type, typeMap, crossPkgResolver)
                        : MapClrTypeForConst(field.FieldType, typeMap);
                    object? value = field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
                    pkg.AddExport(new ConstantSymbol(goName, goType, value));
                }
                else
                {
                    var goName = varAttr?.Name ?? field.Name;
                    var goType = varAttr?.Type != null
                        ? PackageMetadataSerializer.StringToType(varAttr.Type, typeMap, crossPkgResolver)
                        : MapClrType(field.FieldType, typeMap);
                    pkg.AddExport(new PackageVarSymbol(goName, goType, field.DeclaringType!, field.Name));
                }
            }

            foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var varAttr = prop.GetCustomAttribute<GoVarAttribute>();

                var goName = varAttr?.Name ?? prop.Name;
                var goType = varAttr?.Type != null
                    ? PackageMetadataSerializer.StringToType(varAttr.Type, typeMap, crossPkgResolver)
                    : MapClrType(prop.PropertyType, typeMap);
                pkg.AddExport(new PackageVarSymbol(goName, goType, prop.DeclaringType!, prop.Name));
            }
        }

        private static List<GoTypeEntry> CollectGoTypes(
            Type pkgType, string importPath, Dictionary<string, Type[]> typesByPackage)
        {
            var goTypes = new List<GoTypeEntry>();
            var seen = new HashSet<Type>();
            foreach (var nested in pkgType.GetNestedTypes(BindingFlags.Public))
            {
                var attr = nested.GetCustomAttribute<GoTypeAttribute>();
                if (attr != null && seen.Add(nested))
                    goTypes.Add(new GoTypeEntry(nested, attr));
            }
            if (typesByPackage.TryGetValue(importPath, out var externalTypes))
            {
                foreach (var ext in externalTypes)
                {
                    var attr = ext.GetCustomAttribute<GoTypeAttribute>();
                    if (attr != null && seen.Add(ext))
                        goTypes.Add(new GoTypeEntry(ext, attr));
                }
            }
            return goTypes;
        }

        private static void DeclareTypes(Type pkgType, string importPath,
            PackageSymbol pkg, Dictionary<string, TypeSymbol> typeMap,
            Dictionary<string, Type[]> typesByPackage,
            Dictionary<ClrTypeKey, Type> clrTypes,
            System.Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var goTypes = CollectGoTypes(pkgType, importPath, typesByPackage);

            foreach (var entry in goTypes)
            {
                var goName = entry.Attribute.Name ?? entry.ClrType.Name;
                TypeSymbol goType;
                switch (entry.Attribute.Kind)
                {
                    case "struct":
                        goType = new StructTypeSymbol(goName, new List<FieldSymbol>());
                        break;
                    case "interface":
                        goType = new InterfaceTypeSymbol(goName, new List<MethodSymbol>());
                        break;
                    default:
                        // Defer underlying type parsing to Pass 1b when all type names are registered.
                        // Underlying types like "func(ResponseWriter, *Request)" reference other types
                        // from the same package that may not be declared yet.
                        goType = new TypeSymbol(goName, TypeKind.Struct, null);
                        goType._deferredUnderlying = entry.Attribute.Underlying;
                        break;
                }
                goType.PackagePath = importPath;
                typeMap[goName] = goType;
                pkg.AddExport(goType);

                // Register CLR type mapping: (importPath, goName) → System.Type
                clrTypes[new ClrTypeKey(importPath, goName)] = entry.ClrType;
            }
        }

        private static void PopulateTypeMembers(Type pkgType, string importPath,
            PackageSymbol pkg, Dictionary<string, TypeSymbol> typeMap,
            Dictionary<string, Type[]> typesByPackage,
            Dictionary<ClrTypeKey, Type> clrTypes,
            System.Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var goTypes = CollectGoTypes(pkgType, importPath, typesByPackage);

            foreach (var entry in goTypes)
            {
                var goName = entry.Attribute.Name ?? entry.ClrType.Name;
                if (!typeMap.TryGetValue(goName, out var goType))
                {
                    continue;
                }

                // Resolve deferred underlying types now that all type names are registered
                if (goType._deferredUnderlying != null)
                {
                    var underlying = PackageMetadataSerializer.StringToType(
                        goType._deferredUnderlying, typeMap, crossPkgResolver);
                    goType.UnderlyingType = underlying;
                    goType.TypeKind = underlying.TypeKind;
                    goType._deferredUnderlying = null;
                }

                if (goType is StructTypeSymbol structType)
                {
                    var fields = new List<FieldSymbol>();
                    // Include inherited fields to support Go struct embedding (base class = embedded struct)
                    foreach (var fi in entry.ClrType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var fieldAttr = fi.GetCustomAttribute<GoFieldAttribute>();
                        if (fieldAttr == null)
                        {
                            continue;
                        }
                        var fieldName = fieldAttr.Name ?? fi.Name;
                        var fieldType = fieldAttr.Type != null
                            ? PackageMetadataSerializer.StringToType(fieldAttr.Type, typeMap, crossPkgResolver)
                            : MapClrType(fi.FieldType, typeMap);
                        fields.Add(new FieldSymbol(fieldName, fieldType, fields.Count, isEmbedded: fieldAttr.Embedded));
                    }
                    foreach (var pi in entry.ClrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var fieldAttr = pi.GetCustomAttribute<GoFieldAttribute>();
                        if (fieldAttr == null)
                        {
                            continue;
                        }
                        var fieldName = fieldAttr.Name ?? pi.Name;
                        var fieldType = fieldAttr.Type != null
                            ? PackageMetadataSerializer.StringToType(fieldAttr.Type, typeMap, crossPkgResolver)
                            : MapClrType(pi.PropertyType, typeMap);
                        fields.Add(new FieldSymbol(fieldName, fieldType, fields.Count, isEmbedded: fieldAttr.Embedded));
                    }
                    if (fields.Count > 0)
                    {
                        structType.SetFields(fields);
                    }
                }

                // For type methods, only include instance methods (not static — those are package functions)
                // Exception: include static methods only if the class doesn't also have [GoPackage]
                var includeStatic = entry.ClrType.GetCustomAttribute<GoPackageAttribute>() == null;
                // Include inherited methods for Go struct embedding support
                var methodFlags = BindingFlags.Public | BindingFlags.Instance;
                if (includeStatic)
                {
                    methodFlags |= BindingFlags.Static;
                }
                // For interfaces, also include inherited methods from parent interfaces
                // (e.g., io.ReadSeeker inherits Read from Reader and Seek from Seeker)
                var methods = entry.ClrType.GetMethods(methodFlags);
                if (goType is InterfaceTypeSymbol && entry.ClrType.IsInterface)
                {
                    var allMethods = new List<System.Reflection.MethodInfo>(methods);
                    var seen = new HashSet<string>();
                    foreach (var m in methods) seen.Add(m.Name);
                    foreach (var parentIface in entry.ClrType.GetInterfaces())
                    {
                        foreach (var m in parentIface.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (!m.IsSpecialName && seen.Add(m.Name))
                                allMethods.Add(m);
                        }
                    }
                    methods = allMethods.ToArray();
                }
                foreach (var mi in methods)
                {
                    if (mi.IsSpecialName) continue; // skip property getters/setters

                    var methodAttr = mi.GetCustomAttribute<GoMethodAttribute>();
                    var methodName = methodAttr?.Name ?? mi.Name;
                    var isMethodVariadic = methodAttr?.IsVariadic ?? HasParamsArray(mi);
                    var parameters = BuildParameters(mi, typeMap, isMethodVariadic, crossPkgResolver);
                    var returnTypes = BuildReturnTypes(mi, typeMap, crossPkgResolver);

                    if (goType is InterfaceTypeSymbol ifaceType)
                    {
                        ifaceType.AddMethod(new MethodSymbol(methodName, null!, false,
                            Array.Empty<TypeParameterSymbol>(), parameters, returnTypes, isMethodVariadic));
                    }
                    else
                    {
                        goType.AddMethod(new MethodSymbol(methodName, goType, false,
                            Array.Empty<TypeParameterSymbol>(), parameters, returnTypes, isMethodVariadic));
                    }
                }
            }
        }

        private static List<ParameterSymbol> BuildParameters(MethodInfo method,
            Dictionary<string, TypeSymbol> typeMap, bool isVariadic = false,
            System.Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var result = new List<ParameterSymbol>();
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                var pi = parameters[i];
                var paramAttr = pi.GetCustomAttribute<GoParamAttribute>();
                TypeSymbol goType;
                if (paramAttr != null)
                {
                    goType = PackageMetadataSerializer.StringToType(paramAttr.Type, typeMap, crossPkgResolver);
                }
                else if (isVariadic && i == parameters.Length - 1 && pi.ParameterType.IsArray)
                {
                    // Variadic: params T[] → []T (SliceTypeSymbol) so CallResolver recognizes it
                    var elemType = MapClrType(pi.ParameterType.GetElementType()!, typeMap);
                    goType = new SliceTypeSymbol(elemType);
                }
                else
                {
                    goType = MapClrType(pi.ParameterType, typeMap);
                }
                result.Add(new ParameterSymbol(pi.Name ?? $"p{pi.Position}", goType, i));
            }
            return result;
        }

        private static List<TypeSymbol> BuildReturnTypes(MethodInfo method,
            Dictionary<string, TypeSymbol> typeMap,
            System.Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var retAttr = method.ReturnParameter.GetCustomAttribute<GoReturnAttribute>();
            if (retAttr != null)
            {
                var types = new List<TypeSymbol>();
                foreach (var t in retAttr.Types)
                    types.Add(PackageMetadataSerializer.StringToType(t, typeMap, crossPkgResolver));
                return types;
            }

            var returnType = method.ReturnType;
            if (returnType == typeof(void))
                return new List<TypeSymbol>();

            if (IsTupleType(returnType))
            {
                var types = new List<TypeSymbol>();
                foreach (var arg in returnType.GetGenericArguments())
                    types.Add(MapClrType(arg, typeMap));
                return types;
            }

            return new List<TypeSymbol> { MapClrType(returnType, typeMap) };
        }

        /// <summary>
        /// Maps CLR types to Go untyped constants (for [GoConst] without explicit Type).
        /// Go constants without type annotations are untyped.
        /// </summary>
        private static TypeSymbol MapClrTypeForConst(Type clrType, Dictionary<string, TypeSymbol> typeMap)
        {
            if (clrType == typeof(double) || clrType == typeof(float))
                return BuiltinTypes.UntypedFloat;
            if (clrType == typeof(long) || clrType == typeof(int) || clrType == typeof(short) || clrType == typeof(sbyte)
                || clrType == typeof(ulong) || clrType == typeof(uint) || clrType == typeof(ushort) || clrType == typeof(byte))
                return BuiltinTypes.UntypedInt;
            if (clrType == typeof(string))
                return BuiltinTypes.UntypedString;
            if (clrType == typeof(bool))
                return BuiltinTypes.UntypedBool;
            return MapClrType(clrType, typeMap);
        }

        private static TypeSymbol MapClrType(Type clrType, Dictionary<string, TypeSymbol> typeMap)
        {
            var underlying = Nullable.GetUnderlyingType(clrType);
            if (underlying != null)
                clrType = underlying;

            if (clrType == typeof(bool)) return BuiltinTypes.Bool;
            if (clrType == typeof(long)) return BuiltinTypes.Int;
            if (clrType == typeof(int)) return BuiltinTypes.Int32;
            if (clrType == typeof(short)) return BuiltinTypes.Int16;
            if (clrType == typeof(sbyte)) return BuiltinTypes.Int8;
            if (clrType == typeof(ulong)) return BuiltinTypes.Uint64;
            if (clrType == typeof(uint)) return BuiltinTypes.Uint32;
            if (clrType == typeof(ushort)) return BuiltinTypes.Uint16;
            if (clrType == typeof(byte)) return BuiltinTypes.Byte;
            if (clrType == typeof(double)) return BuiltinTypes.Float64;
            if (clrType == typeof(float)) return BuiltinTypes.Float32;
            if (clrType == typeof(nuint)) return BuiltinTypes.Uintptr;
            if (clrType == typeof(nint)) return BuiltinTypes.Int;
            if (clrType == typeof(string)) return BuiltinTypes.String;
            if (clrType == typeof(object)) return BuiltinTypes.EmptyInterface;

            if (clrType.IsGenericType)
            {
                var genDef = clrType.GetGenericTypeDefinition();
                var args = clrType.GetGenericArguments();

                if (genDef == typeof(Slice<>))
                    return new SliceTypeSymbol(MapClrType(args[0], typeMap));
                if (genDef == typeof(Map<,>))
                    return new MapTypeSymbol(MapClrType(args[0], typeMap), MapClrType(args[1], typeMap));
                if (genDef == typeof(Channel<>))
                    return new ChannelTypeSymbol(MapClrType(args[0], typeMap));
                if (genDef == typeof(Ptr<>))
                    return new PointerTypeSymbol(MapClrType(args[0], typeMap));

                if (genDef.FullName?.StartsWith("System.Func`") == true)
                {
                    var paramTypes = new List<TypeSymbol>();
                    for (int i = 0; i < args.Length - 1; i++)
                    {
                        paramTypes.Add(MapClrType(args[i], typeMap));
                    }
                    var retClrType = args[args.Length - 1];
                    var retTypes = new List<TypeSymbol>();
                    if (IsTupleType(retClrType))
                    {
                        foreach (var tupleArg in retClrType.GetGenericArguments())
                        {
                            retTypes.Add(MapClrType(tupleArg, typeMap));
                        }
                    }
                    else
                    {
                        retTypes.Add(MapClrType(retClrType, typeMap));
                    }
                    return new FunctionTypeSymbol(paramTypes, retTypes);
                }
                if (genDef.FullName?.StartsWith("System.Action`") == true)
                {
                    var paramTypes = new List<TypeSymbol>();
                    foreach (var arg in args)
                        paramTypes.Add(MapClrType(arg, typeMap));
                    return new FunctionTypeSymbol(paramTypes, new List<TypeSymbol>());
                }
            }

            if (clrType == typeof(Action))
                return new FunctionTypeSymbol(new List<TypeSymbol>(), new List<TypeSymbol>());

            var goTypeAttr = clrType.GetCustomAttribute<GoTypeAttribute>();
            if (goTypeAttr != null)
            {
                var name = goTypeAttr.Name ?? clrType.Name;
                if (typeMap.TryGetValue(name, out var mapped))
                {
                    if (goTypeAttr.Pointer)
                    {
                        return new PointerTypeSymbol(mapped);
                    }
                    return mapped;
                }
            }

            return BuiltinTypes.EmptyInterface;
        }

        private static bool IsTupleType(Type type)
        {
            if (!type.IsGenericType) return false;
            var name = type.FullName;
            return name != null && name.StartsWith("System.ValueTuple`");
        }

        private static bool HasParamsArray(MethodInfo method)
        {
            var parameters = method.GetParameters();
            return parameters.Length > 0 &&
                   parameters[parameters.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);
        }

        private static string GetPackageName(string importPath)
        {
            var lastSlash = importPath.LastIndexOf('/');
            return lastSlash >= 0 ? importPath.Substring(lastSlash + 1) : importPath;
        }
    }
}
