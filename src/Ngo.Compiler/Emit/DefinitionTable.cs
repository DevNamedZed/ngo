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

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Tracks every type, method, field, and constructor defined during emission.
    /// Provides member resolution without reflecting on unfinished TypeBuilders.
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

        public ConstructorInfo? GetConstructor(Type type, Type[] parameterTypes)
        {
            var typeDef = FindTypeDefinition(type);
            if (typeDef != null)
            {
                return ResolveConstructor(typeDef, type, parameterTypes);
            }

            // Runtime type with TypeBuilder generic args (e.g., Ptr<MyStructBuilder>)
            if (type.IsGenericType && HasTypeBuilderArgs(type))
            {
                return ResolveConstructorOnRuntimeGenericWithBuilderArgs(type, parameterTypes);
            }

            // Pure runtime type — normal reflection works
            return type.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameterTypes, null);
        }

        public MethodInfo? GetMethod(Type type, string name, Type[]? parameterTypes)
        {
            var typeDef = FindTypeDefinition(type);
            if (typeDef != null)
            {
                return ResolveMethod(typeDef, type, name, parameterTypes);
            }

            // Runtime type — check if it has TypeBuilder generic args
            if (type.IsGenericType && HasTypeBuilderArgs(type))
            {
                return ResolveMethodOnRuntimeGenericWithBuilderArgs(type, name, parameterTypes);
            }

            // Pure runtime type — normal reflection works
            if (parameterTypes != null)
            {
                return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static, null, parameterTypes, null);
            }
            return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);
        }

        public MethodInfo? GetMethod(Type type, string name)
        {
            return GetMethod(type, name, null);
        }

        public FieldInfo? GetField(Type type, string name)
        {
            var typeDef = FindTypeDefinition(type);
            if (typeDef != null)
            {
                return ResolveField(typeDef, type, name);
            }

            // Runtime type — check if it has TypeBuilder generic args
            if (type.IsGenericType && HasTypeBuilderArgs(type))
            {
                return ResolveFieldOnRuntimeGenericWithBuilderArgs(type, name);
            }

            // Pure runtime type — normal reflection works
            return type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);
        }

        public MethodInfo? GetPropertyGetter(Type type, string propertyName)
        {
            return GetMethod(type, "get_" + propertyName);
        }

        public MethodInfo? GetPropertySetter(Type type, string propertyName)
        {
            return GetMethod(type, "set_" + propertyName);
        }

        // --- Private resolution methods ---

        private TypeDefinition? FindTypeDefinition(Type type)
        {
            var name = type.FullName ?? type.Name;

            // Direct match — type is a registered TypeBuilder
            if (_types.TryGetValue(name, out var typeDef))
            {
                return typeDef;
            }

            // Generic instantiation of a registered TypeBuilder
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var genericDef = GetGenericDefinitionSafe(type);
                if (genericDef != null)
                {
                    var defName = genericDef.FullName ?? genericDef.Name;
                    if (_types.TryGetValue(defName, out typeDef))
                    {
                        return typeDef;
                    }
                }
            }

            return null;
        }

        private ConstructorInfo? ResolveConstructor(TypeDefinition typeDef, Type concreteType,
            Type[] parameterTypes)
        {
            foreach (var registration in typeDef.Constructors)
            {
                if (registration.ParameterTypes.Length == parameterTypes.Length)
                {
                    var baseConstructor = GetConstructorBuilderInfo(registration);
                    if (IsGenericInstantiation(concreteType, typeDef))
                    {
                        return TypeBuilder.GetConstructor(concreteType, baseConstructor);
                    }
                    return baseConstructor;
                }
            }
            return null;
        }

        private MethodInfo? ResolveMethod(TypeDefinition typeDef, Type concreteType, string name,
            Type[]? parameterTypes)
        {
            foreach (var registration in typeDef.Methods)
            {
                if (registration.Name == name)
                {
                    if (parameterTypes != null && registration.ParameterTypes.Length != parameterTypes.Length)
                    {
                        continue;
                    }
                    var baseMethod = GetMethodBuilderInfo(registration);
                    if (IsGenericInstantiation(concreteType, typeDef))
                    {
                        return TypeBuilder.GetMethod(concreteType, baseMethod);
                    }
                    return baseMethod;
                }
            }
            return null;
        }

        private FieldInfo? ResolveField(TypeDefinition typeDef, Type concreteType, string name)
        {
            foreach (var registration in typeDef.Fields)
            {
                if (registration.Name == name)
                {
                    var baseField = GetFieldBuilderInfo(registration);
                    if (IsGenericInstantiation(concreteType, typeDef))
                    {
                        return TypeBuilder.GetField(concreteType, baseField);
                    }
                    return baseField;
                }
            }
            return null;
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

        private static ConstructorInfo GetConstructorBuilderInfo(ConstructorDefinition registration)
        {
            if (registration.Builder is LiveConstructorBuilder liveBuilder)
            {
                return liveBuilder.Inner;
            }
            // For archive path, the constructor builder must provide a ConstructorInfo.
            // This will be addressed when NgoConstructorBuilder implements the pattern.
            throw new InvalidOperationException(
                "DefinitionTable: constructor builder does not expose a ConstructorInfo");
        }

        private static MethodInfo GetMethodBuilderInfo(MethodDefinition registration)
        {
            if (registration.Builder is LiveMethodBuilder liveBuilder)
            {
                return liveBuilder.Inner;
            }
            throw new InvalidOperationException(
                "DefinitionTable: method builder does not expose a MethodInfo compatible with TypeBuilder.GetMethod");
        }

        private static FieldInfo GetFieldBuilderInfo(FieldDefinition registration)
        {
            if (registration.Builder is LiveFieldBuilder liveBuilder)
            {
                return liveBuilder.Inner;
            }
            throw new InvalidOperationException(
                "DefinitionTable: field builder does not expose a FieldInfo compatible with TypeBuilder.GetField");
        }

        private static ConstructorInfo? ResolveConstructorOnRuntimeGenericWithBuilderArgs(Type type,
            Type[] parameterTypes)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            foreach (var baseConstructor in genericDefinition.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (baseConstructor.GetParameters().Length == parameterTypes.Length)
                {
                    return TypeBuilder.GetConstructor(type, baseConstructor);
                }
            }
            return null;
        }

        private static MethodInfo? ResolveMethodOnRuntimeGenericWithBuilderArgs(Type type, string name,
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

            // First pass: match by name + substituted parameter types.
            // The generic definition's parameters use T, T[], Slice<T>, etc.
            // We substitute T → concrete type args and compare against the requested types.
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
                        return TypeBuilder.GetMethod(type, baseMethod);
                    }
                }
            }

            // Second pass: match by name + parameter count only (fallback).
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
                return TypeBuilder.GetMethod(type, baseMethod);
            }
            return null;
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
            return type.FullName ?? type.Name;
        }

        private static FieldInfo? ResolveFieldOnRuntimeGenericWithBuilderArgs(Type type, string name)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            foreach (var baseField in genericDefinition.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (baseField.Name == name)
                {
                    return TypeBuilder.GetField(type, baseField);
                }
            }
            return null;
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
                    || argument is Builder.NgoBuilderType || argument is Builder.NgoGenericParameterType)
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

        // --- Inner types ---

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
