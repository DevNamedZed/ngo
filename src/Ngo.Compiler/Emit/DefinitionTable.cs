// -----------------------------------------------------------------------
// <copyright file="DefinitionTable.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Refs;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Tracks every type, method, field, and constructor defined during emission.
    /// Resolves member lookups to structured Ref objects that work for both the live
    /// (ILGenerator) and archive (NgoWriter) emit paths without reflecting on unfinished
    /// TypeBuilders.
    /// </summary>
    internal sealed class DefinitionTable
    {
        private readonly Dictionary<string, TypeDefinition> _types = new();

        public void RegisterType(string fullName, ITypeBuilder builder)
        {
            if (!_types.ContainsKey(fullName))
            {
                _types[fullName] = new TypeDefinition(fullName, builder);
            }
        }

        public void RegisterConstructor(string declaringTypeName, Type[] parameterTypes, IConstructorBuilder builder)
        {
            if (_types.TryGetValue(declaringTypeName, out var typeDef))
            {
                typeDef.Constructors.Add(new ConstructorDefinition(builder, parameterTypes));
            }
        }

        public void RegisterMethod(string declaringTypeName, string methodName, Type[] parameterTypes,
            IMethodBuilder builder)
        {
            if (_types.TryGetValue(declaringTypeName, out var typeDef))
            {
                typeDef.Methods.Add(new MethodDefinition(builder, methodName, parameterTypes));
            }
        }

        public void RegisterField(string declaringTypeName, string fieldName, IFieldBuilder builder)
        {
            if (_types.TryGetValue(declaringTypeName, out var typeDef))
            {
                typeDef.Fields.Add(new FieldDefinition(builder, fieldName));
            }
        }

        public Type? FindType(string fullName)
        {
            if (_types.TryGetValue(fullName, out var typeDef))
            {
                return typeDef.Builder.AsType();
            }
            return null;
        }

        public Type? FindType(string packagePath, string typeName)
        {
            var qualifiedName = packagePath.Replace("/", ".") + "." + typeName;
            return FindType(qualifiedName) ?? FindType(typeName);
        }

        public ITypeBuilder? FindTypeBuilder(string fullName)
        {
            if (_types.TryGetValue(fullName, out var typeDef))
            {
                return typeDef.Builder;
            }
            return null;
        }

        public CtorRef? GetConstructor(Type type, Type[] parameterTypes)
        {
            var typeDef = FindTypeDefinition(type);
            if (typeDef != null)
            {
                return ResolveConstructor(typeDef, type, parameterTypes);
            }

            if (type.IsGenericType && (HasTypeBuilderArgs(type) || IsTypeBuilderInstantiation(type)))
            {
                return ResolveConstructorOnRuntimeGenericWithBuilderArgs(type, parameterTypes);
            }

            if (ParameterTypesContainNonRuntimeType(parameterTypes))
            {
                return ResolveConstructorByArity(type, parameterTypes);
            }

            var runtimeConstructor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameterTypes, null);
            return runtimeConstructor != null ? CtorRef.FromRuntime(runtimeConstructor) : null;
        }

        public MethodRef? GetMethod(Type type, string name, Type[]? parameterTypes)
        {
            var typeDef = FindTypeDefinition(type);
            if (typeDef != null)
            {
                return ResolveMethod(typeDef, type, name, parameterTypes);
            }

            if (type.IsGenericType && (HasTypeBuilderArgs(type) || IsTypeBuilderInstantiation(type)))
            {
                return ResolveMethodOnRuntimeGenericWithBuilderArgs(type, name, parameterTypes);
            }

            var runtimeMethod = FindRuntimeMethod(type, name, parameterTypes);
            if (runtimeMethod == null && type.IsInterface)
            {
                foreach (var baseInterface in type.GetInterfaces())
                {
                    runtimeMethod = FindRuntimeMethod(baseInterface, name, parameterTypes);
                    if (runtimeMethod != null)
                    {
                        break;
                    }
                }
            }
            return runtimeMethod != null ? MethodRef.FromRuntime(runtimeMethod) : null;
        }

        private static MethodInfo? FindRuntimeMethod(Type type, string name, Type[]? parameterTypes)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;
            if (parameterTypes != null)
            {
                if (ParameterTypesContainNonRuntimeType(parameterTypes))
                {
                    return FindRuntimeMethodByNameAndArity(type, name, flags, parameterTypes);
                }
                return type.GetMethod(name, flags, null, parameterTypes, null);
            }
            return type.GetMethod(name, flags);
        }

        private static bool ParameterTypesContainNonRuntimeType(Type[] parameterTypes)
        {
            foreach (var parameterType in parameterTypes)
            {
                if (EmitContext.IsNonRuntimeType(parameterType))
                {
                    return true;
                }
                if (parameterType.IsGenericType && EmitContext.HasTypeBuilderArgs(parameterType))
                {
                    return true;
                }
                if (parameterType.HasElementType)
                {
                    var elementType = parameterType.GetElementType();
                    if (elementType != null && EmitContext.IsNonRuntimeType(elementType))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static MethodInfo? FindRuntimeMethodByNameAndArity(
            Type type, string name, BindingFlags flags, Type[] parameterTypes)
        {
            MethodInfo? match = null;
            foreach (var candidate in type.GetMethods(flags))
            {
                if (candidate.Name != name)
                {
                    continue;
                }
                var candidateParams = candidate.GetParameters();
                if (candidateParams.Length != parameterTypes.Length)
                {
                    continue;
                }
                if (match != null)
                {
                    return null;
                }
                match = candidate;
            }
            return match;
        }

        public MethodRef? GetMethod(Type type, string name)
        {
            return GetMethod(type, name, null);
        }

        public FieldRef? GetField(Type type, string name)
        {
            var typeDef = FindTypeDefinition(type);
            if (typeDef != null)
            {
                return ResolveField(typeDef, type, name);
            }

            if (type.IsGenericType && (HasTypeBuilderArgs(type) || IsTypeBuilderInstantiation(type)))
            {
                return ResolveFieldOnRuntimeGenericWithBuilderArgs(type, name);
            }

            var runtimeField = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);
            return runtimeField != null ? FieldRef.FromRuntime(runtimeField) : null;
        }

        public FieldRef RequireField(Type type, string name)
        {
            var fieldRef = GetField(type, name);
            if (fieldRef == null)
            {
                throw new InvalidOperationException(
                    $"DefinitionTable: required field '{name}' not found on type '{type}' " +
                    $"(clrName={type.GetType().Name}, isGeneric={type.IsGenericType})");
            }
            return fieldRef;
        }

        private static bool IsTypeBuilderInstantiation(Type type)
        {
            return type.GetType().Name == "TypeBuilderInstantiation";
        }

        public MethodRef? GetPropertyGetter(Type type, string propertyName)
        {
            return GetMethod(type, "get_" + propertyName);
        }

        public MethodRef? GetPropertySetter(Type type, string propertyName)
        {
            return GetMethod(type, "set_" + propertyName);
        }

        private TypeDefinition? FindTypeDefinition(Type type)
        {
            var name = GetLookupName(type);

            if (_types.TryGetValue(name, out var typeDef))
            {
                return typeDef;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var genericDef = GetGenericDefinitionSafe(type);
                if (genericDef != null)
                {
                    var definitionName = GetLookupName(genericDef);
                    if (_types.TryGetValue(definitionName, out typeDef))
                    {
                        return typeDef;
                    }
                }
            }

            return null;
        }

        private static string GetLookupName(Type type)
        {
            try
            {
                return type.FullName ?? BuildFallbackName(type);
            }
            catch (ArgumentException)
            {
                return BuildFallbackName(type);
            }
            catch (NotSupportedException)
            {
                return BuildFallbackName(type);
            }
        }

        private static string BuildFallbackName(Type type)
        {
            var ns = type.Namespace;
            return string.IsNullOrEmpty(ns) ? type.Name : ns + "." + type.Name;
        }

        private CtorRef? ResolveConstructor(TypeDefinition typeDef, Type concreteType,
            Type[] parameterTypes)
        {
            foreach (var registration in typeDef.Constructors)
            {
                if (registration.ParameterTypes.Length == parameterTypes.Length)
                {
                    var declaringTypeRef = BuildDeclaringTypeRef(typeDef, concreteType);
                    return CtorRef.FromBuilder(registration.Builder, declaringTypeRef);
                }
            }
            return null;
        }

        private MethodRef? ResolveMethod(TypeDefinition typeDef, Type concreteType, string name,
            Type[]? parameterTypes)
        {
            foreach (var registration in typeDef.Methods)
            {
                if (registration.Name != name)
                {
                    continue;
                }
                if (parameterTypes != null && registration.ParameterTypes.Length != parameterTypes.Length)
                {
                    continue;
                }
                var declaringTypeRef = BuildDeclaringTypeRef(typeDef, concreteType);
                return MethodRef.FromBuilder(registration.Builder, declaringTypeRef);
            }
            return null;
        }

        private FieldRef? ResolveField(TypeDefinition typeDef, Type concreteType, string name)
        {
            foreach (var registration in typeDef.Fields)
            {
                if (registration.Name == name)
                {
                    var declaringTypeRef = BuildDeclaringTypeRef(typeDef, concreteType);
                    return FieldRef.FromBuilder(registration.Builder, declaringTypeRef);
                }
            }
            return null;
        }

        private TypeRef BuildDeclaringTypeRef(TypeDefinition typeDef, Type concreteType)
        {
            var builderRef = TypeRef.FromBuilder(typeDef.Builder);
            if (!IsGenericInstantiation(concreteType, typeDef))
            {
                return builderRef;
            }
            var typeArguments = concreteType.GetGenericArguments();
            var argumentRefs = new TypeRef[typeArguments.Length];
            for (int index = 0; index < typeArguments.Length; index++)
            {
                argumentRefs[index] = ConvertTypeToTypeRef(typeArguments[index]);
            }
            return TypeRef.GenericInstantiation(builderRef, argumentRefs);
        }

        private TypeRef ConvertTypeToTypeRef(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var typeDef = FindTypeDefinition(type);
            if (typeDef != null)
            {
                return BuildDeclaringTypeRef(typeDef, type);
            }

            if (type is NgoBuilderType ngoBuilderType)
            {
                return TypeRef.FromDefined(ngoBuilderType.FullName ?? ngoBuilderType.Name);
            }

            if (type is NgoGenericParameterType ngoGenericParameter)
            {
                return TypeRef.GenericTypeParameter(ngoGenericParameter.GenericParameterPosition);
            }

            if (type is TypeBuilder typeBuilder)
            {
                return TypeRef.FromDefined(typeBuilder.FullName ?? typeBuilder.Name);
            }

            if (type is GenericTypeParameterBuilder)
            {
                return TypeRef.GenericTypeParameter(type.GenericParameterPosition);
            }

            if (type.IsGenericParameter)
            {
                if (type.DeclaringMethod != null)
                {
                    return TypeRef.GenericMethodParameter(type.GenericParameterPosition);
                }
                return TypeRef.GenericTypeParameter(type.GenericParameterPosition);
            }

            if (type.IsArray)
            {
                return TypeRef.Array(ConvertTypeToTypeRef(type.GetElementType()!));
            }

            if (type.IsByRef)
            {
                return TypeRef.ByRef(ConvertTypeToTypeRef(type.GetElementType()!));
            }

            if (type.IsPointer)
            {
                return TypeRef.Pointer(ConvertTypeToTypeRef(type.GetElementType()!));
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var genericDefinition = GetGenericDefinitionSafe(type);
                if (genericDefinition != null)
                {
                    var typeArguments = type.GetGenericArguments();
                    var argumentRefs = new TypeRef[typeArguments.Length];
                    for (int index = 0; index < typeArguments.Length; index++)
                    {
                        argumentRefs[index] = ConvertTypeToTypeRef(typeArguments[index]);
                    }
                    return TypeRef.GenericInstantiation(ConvertTypeToTypeRef(genericDefinition), argumentRefs);
                }
            }

            return TypeRef.FromRuntime(type);
        }

        private static bool IsGenericInstantiation(Type concreteType, TypeDefinition typeDef)
        {
            if (!concreteType.IsGenericType || concreteType.IsGenericTypeDefinition)
            {
                return false;
            }
            var builderType = typeDef.Builder.AsType();
            return concreteType != builderType;
        }

        private static CtorRef? ResolveConstructorByArity(Type type, Type[] parameterTypes)
        {
            foreach (var constructor in type.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (constructor.GetParameters().Length == parameterTypes.Length)
                {
                    return CtorRef.FromRuntime(constructor);
                }
            }
            return null;
        }

        private CtorRef? ResolveConstructorOnRuntimeGenericWithBuilderArgs(Type type,
            Type[] parameterTypes)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            var typeArguments = type.GetGenericArguments();
            Type[] genericParams;
            try
            {
                genericParams = genericDefinition.GetGenericArguments();
            }
            catch
            {
                genericParams = Type.EmptyTypes;
            }
            foreach (var baseConstructor in genericDefinition.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (baseConstructor.GetParameters().Length == parameterTypes.Length)
                {
                    if (CanUseTypeBuilderResolution(type))
                    {
                        var instantiatedConstructor = TypeBuilder.GetConstructor(type, baseConstructor);
                        return CtorRef.FromRuntime(instantiatedConstructor);
                    }
                    var declaringTypeRef = ConvertTypeToTypeRef(type);
                    var baseParams = baseConstructor.GetParameters();
                    var parameterRefs = new TypeRef[baseParams.Length];
                    for (int index = 0; index < baseParams.Length; index++)
                    {
                        var substituted = SubstituteGenericParameters(
                            baseParams[index].ParameterType, genericParams, typeArguments);
                        parameterRefs[index] = ConvertTypeToTypeRef(substituted);
                    }
                    return CtorRef.MemberRef(declaringTypeRef, parameterRefs);
                }
            }
            return null;
        }

        private MethodRef? ResolveMethodOnRuntimeGenericWithBuilderArgs(Type type, string name,
            Type[]? parameterTypes)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            var typeArguments = type.GetGenericArguments();
            Type[] genericParams;
            try
            {
                genericParams = genericDefinition.GetGenericArguments();
            }
            catch
            {
                genericParams = Type.EmptyTypes;
            }

            var allMethods = genericDefinition.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (parameterTypes != null && genericParams.Length > 0)
            {
                foreach (var baseMethod in allMethods)
                {
                    if (baseMethod.Name != name)
                    {
                        continue;
                    }
                    var methodParams = baseMethod.GetParameters();
                    if (methodParams.Length != parameterTypes.Length)
                    {
                        continue;
                    }
                    bool allMatch = true;
                    for (int index = 0; index < methodParams.Length; index++)
                    {
                        var substituted = SubstituteGenericParameters(
                            methodParams[index].ParameterType, genericParams, typeArguments);
                        if (substituted != parameterTypes[index]
                            && GetTypeName(substituted) != GetTypeName(parameterTypes[index]))
                        {
                            allMatch = false;
                            break;
                        }
                    }
                    if (allMatch)
                    {
                        return BuildMethodRefOnGenericInstantiation(type, baseMethod);
                    }
                }
            }

            foreach (var baseMethod in allMethods)
            {
                if (baseMethod.Name != name)
                {
                    continue;
                }
                if (parameterTypes != null && baseMethod.GetParameters().Length != parameterTypes.Length)
                {
                    continue;
                }
                return BuildMethodRefOnGenericInstantiation(type, baseMethod);
            }
            return null;
        }

        private MethodRef BuildMethodRefOnGenericInstantiation(Type type, MethodInfo baseMethod)
        {
            if (CanUseTypeBuilderResolution(type))
            {
                return MethodRef.FromRuntime(TypeBuilder.GetMethod(type, baseMethod));
            }
            var declaringTypeRef = ConvertTypeToTypeRef(type);
            var genericDefinition = type.GetGenericTypeDefinition();
            var typeArguments = type.GetGenericArguments();
            Type[] genericParams;
            try
            {
                genericParams = genericDefinition.GetGenericArguments();
            }
            catch
            {
                genericParams = Type.EmptyTypes;
            }
            var substitutedReturnType = SubstituteGenericParameters(
                baseMethod.ReturnType, genericParams, typeArguments);
            var returnTypeRef = ConvertTypeToTypeRef(substitutedReturnType);
            var baseParams = baseMethod.GetParameters();
            var paramRefs = new TypeRef[baseParams.Length];
            for (int index = 0; index < baseParams.Length; index++)
            {
                var substituted = SubstituteGenericParameters(
                    baseParams[index].ParameterType, genericParams, typeArguments);
                paramRefs[index] = ConvertTypeToTypeRef(substituted);
            }
            return MethodRef.MemberRef(declaringTypeRef, baseMethod.Name, paramRefs, returnTypeRef,
                isStatic: baseMethod.IsStatic);
        }

        private static bool CanUseTypeBuilderResolution(Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }
            if (IsTypeBuilderInstantiation(type))
            {
                return true;
            }
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef is TypeBuilder)
            {
                return true;
            }
            foreach (var argument in type.GetGenericArguments())
            {
                if (argument is TypeBuilder || argument is GenericTypeParameterBuilder)
                {
                    return true;
                }
            }
            return false;
        }

        private static Type SubstituteGenericParameters(Type type, Type[] genericParams, Type[] typeArguments)
        {
            if (type.IsGenericParameter)
            {
                for (int index = 0; index < genericParams.Length; index++)
                {
                    if (genericParams[index] == type || genericParams[index].Name == type.Name)
                    {
                        return typeArguments[index];
                    }
                }
                return type;
            }

            if (type.IsArray)
            {
                var elementType = SubstituteGenericParameters(type.GetElementType()!, genericParams, typeArguments);
                return elementType.MakeArrayType();
            }

            if (type.IsByRef)
            {
                var elementType = SubstituteGenericParameters(type.GetElementType()!, genericParams, typeArguments);
                return elementType.MakeByRefType();
            }

            if (type.IsGenericType)
            {
                var args = type.GetGenericArguments();
                var substitutedArgs = new Type[args.Length];
                bool anyChanged = false;
                for (int index = 0; index < args.Length; index++)
                {
                    substitutedArgs[index] = SubstituteGenericParameters(args[index], genericParams, typeArguments);
                    if (substitutedArgs[index] != args[index])
                    {
                        anyChanged = true;
                    }
                }
                if (anyChanged)
                {
                    return type.GetGenericTypeDefinition().MakeGenericType(substitutedArgs);
                }
            }

            return type;
        }

        private static string GetTypeName(Type type)
        {
            try
            {
                return type.FullName ?? type.Name;
            }
            catch (ArgumentException)
            {
                var ns = type.Namespace;
                return string.IsNullOrEmpty(ns) ? type.Name : ns + "." + type.Name;
            }
            catch (NotSupportedException)
            {
                var ns = type.Namespace;
                return string.IsNullOrEmpty(ns) ? type.Name : ns + "." + type.Name;
            }
        }

        private FieldRef? ResolveFieldOnRuntimeGenericWithBuilderArgs(Type type, string name)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            foreach (var baseField in genericDefinition.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (baseField.Name == name)
                {
                    if (CanUseTypeBuilderResolution(type))
                    {
                        return FieldRef.FromRuntime(TypeBuilder.GetField(type, baseField));
                    }
                    var declaringTypeRef = ConvertTypeToTypeRef(type);
                    var fieldTypeRef = ConvertTypeToTypeRef(
                        SubstituteFieldType(baseField.FieldType, type));
                    return FieldRef.MemberRef(declaringTypeRef, name, fieldTypeRef);
                }
            }
            return null;
        }

        private static Type SubstituteFieldType(Type fieldType, Type genericInstantiation)
        {
            var genericDefinition = genericInstantiation.GetGenericTypeDefinition();
            Type[] genericParams;
            try
            {
                genericParams = genericDefinition.GetGenericArguments();
            }
            catch
            {
                return fieldType;
            }
            var typeArguments = genericInstantiation.GetGenericArguments();
            return SubstituteGenericParameters(fieldType, genericParams, typeArguments);
        }

        private static Type? GetGenericDefinitionSafe(Type type)
        {
            try
            {
                return type.GetGenericTypeDefinition();
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private static bool HasTypeBuilderArgs(Type type)
        {
            if (!type.IsGenericType || type.IsGenericTypeDefinition)
            {
                return false;
            }
            foreach (var argument in type.GetGenericArguments())
            {
                if (argument is TypeBuilder || argument is GenericTypeParameterBuilder
                    || argument is NgoBuilderType || argument is NgoGenericParameterType)
                {
                    return true;
                }
                if (argument.IsGenericType && HasTypeBuilderArgs(argument))
                {
                    return true;
                }
            }
            return false;
        }

        internal sealed class TypeDefinition
        {
            public TypeDefinition(string fullName, ITypeBuilder builder)
            {
                FullName = fullName;
                Builder = builder;
            }

            public string FullName { get; }
            public ITypeBuilder Builder { get; }
            public List<ConstructorDefinition> Constructors { get; } = new();
            public List<MethodDefinition> Methods { get; } = new();
            public List<FieldDefinition> Fields { get; } = new();
        }

        internal sealed class ConstructorDefinition
        {
            public ConstructorDefinition(IConstructorBuilder builder, Type[] parameterTypes)
            {
                Builder = builder;
                ParameterTypes = parameterTypes;
            }

            public IConstructorBuilder Builder { get; }
            public Type[] ParameterTypes { get; }
        }

        internal sealed class MethodDefinition
        {
            public MethodDefinition(IMethodBuilder builder, string name, Type[] parameterTypes)
            {
                Builder = builder;
                Name = name;
                ParameterTypes = parameterTypes;
            }

            public IMethodBuilder Builder { get; }
            public string Name { get; }
            public Type[] ParameterTypes { get; }
        }

        internal sealed class FieldDefinition
        {
            public FieldDefinition(IFieldBuilder builder, string name)
            {
                Builder = builder;
                Name = name;
            }

            public IFieldBuilder Builder { get; }
            public string Name { get; }
        }
    }
}
