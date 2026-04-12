// -----------------------------------------------------------------------
// <copyright file="ILLinker.cs" company="Ziad">
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
using Ngo.Compiler.Emit;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Symbols;
using Ngo.Runtime.Discovery;

namespace Ngo.Compiler.Archive
{
    internal sealed class ILLinker
    {
        private readonly PackageSymbol _package;
        private readonly EmitContext _emitContext;
        private readonly Dictionary<string, TypeBuilder> _typeBuilders;
        private readonly Dictionary<string, Dictionary<string, Type>> _typeGenericParams;
        private readonly Dictionary<string, FieldBuilder> _fieldBuilders;
        private readonly Dictionary<string, MethodBuilder> _methodBuilders;
        private readonly Dictionary<string, int> _methodILIndices;
        private readonly Dictionary<string, Dictionary<string, Type>> _methodGenericParams;
        private readonly Dictionary<string, ConstructorBuilder> _constructorBuilders;
        private readonly List<DeserializedTypeInfo> _typeInfos;
        private readonly HashSet<string> _currentArchiveTypes;
        private readonly List<string> _deferredClassTypes;
        private readonly HashSet<string> _sourceCompiledTypes;

        private readonly Assembly _runtimeAssembly;

        private Type[] _currentMethodGenericParameters;
        private Type[] _currentTypeGenericParameters;
        private string _currentReplayMethodKey = "";

        private const BindingFlags AllMethodFlags =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        private const BindingFlags AllConstructorFlags = AllMethodFlags;
        
        public ILLinker(PackageSymbol package, EmitContext emitContext)
        {
            _package = package;
            _emitContext = emitContext;
            _typeBuilders = new Dictionary<string, TypeBuilder>(emitContext.LinkedTypes);
            _typeGenericParams = new Dictionary<string, Dictionary<string, Type>>();
            _fieldBuilders = new Dictionary<string, FieldBuilder>(emitContext.LinkedFields);
            _methodBuilders = new Dictionary<string, MethodBuilder>(emitContext.LinkedMethods);
            _methodILIndices = new Dictionary<string, int>();
            _methodGenericParams = new Dictionary<string, Dictionary<string, Type>>();
            _constructorBuilders = new Dictionary<string, ConstructorBuilder>();
            _typeInfos = new List<DeserializedTypeInfo>();
            _currentArchiveTypes = new HashSet<string>();
            _deferredClassTypes = new List<string>();
            _sourceCompiledTypes = new HashSet<string>();
            _runtimeAssembly = typeof(Ngo.Runtime.Slice<>).Assembly;
            _currentMethodGenericParameters = Type.EmptyTypes;
            _currentTypeGenericParameters = Type.EmptyTypes;
        }

        public void Link(byte[] ilMetaBytes, byte[] ilCodeBytes)
        {
            using var metaStream = new MemoryStream(ilMetaBytes);
            using var codeStream = new MemoryStream(ilCodeBytes);
            var metaReader = new BinaryReader(metaStream);
            var codeReader = new BinaryReader(codeStream);

            DeserializeTypes(metaReader);
            DefineFields();
            DefineMethods();
            CreateValueTypesAndInterfaces();
            ProcessMethodOverrides();
            DefineDefaultConstructors();
            DeserializeAndReplayBodies(codeReader);
            CreateClassTypes();
            CreateStaticTypes();
        }

        private void DeserializeTypes(BinaryReader metaReader)
        {
            int typeCount = metaReader.ReadInt32();

            for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
            {
                var fullTypeName = metaReader.ReadString();
                var typeAttributes = (TypeAttributes)metaReader.ReadInt32();
                var baseTypeName = metaReader.ReadString();

                int interfaceCount = metaReader.ReadInt32();
                var interfaceNames = new string[interfaceCount];
                for (int interfaceIndex = 0; interfaceIndex < interfaceCount; interfaceIndex++)
                {
                    interfaceNames[interfaceIndex] = metaReader.ReadString();
                }

                int typeGenericParamCount = metaReader.ReadInt32();
                var typeGenericParamNames = new string[typeGenericParamCount];
                for (int genericIndex = 0; genericIndex < typeGenericParamCount; genericIndex++)
                {
                    typeGenericParamNames[genericIndex] = metaReader.ReadString();
                }

                if (string.IsNullOrEmpty(fullTypeName))
                {
                    SkipTypeData(metaReader);
                    continue;
                }

                // Check if this type was already compiled from source.
                // Skip DefineType to avoid creating a conflicting partial type.
                if (_emitContext.LinkedTypes.TryGetValue(fullTypeName, out var existingLinkedType))
                {
                    _typeBuilders[fullTypeName] = existingLinkedType;
                    _sourceCompiledTypes.Add(fullTypeName);
                    SkipTypeData(metaReader);
                    continue;
                }

                bool isStaticClass = (typeAttributes & TypeAttributes.Abstract) != 0
                    && (typeAttributes & TypeAttributes.Sealed) != 0;
                bool isInterface = (typeAttributes & TypeAttributes.Interface) != 0;

                TypeBuilder typeBuilder;
                try
                {
                    Type[]? resolvedInterfaces = null;
                    if (interfaceNames.Length > 0)
                    {
                        resolvedInterfaces = new Type[interfaceNames.Length];
                        for (int interfaceIndex = 0; interfaceIndex < interfaceNames.Length; interfaceIndex++)
                        {
                            resolvedInterfaces[interfaceIndex] = ILSerializer.ResolveType(interfaceNames[interfaceIndex], _typeBuilders);
                        }
                    }

                    if (isStaticClass || isInterface)
                    {
                        typeBuilder = ((LiveModuleBuilder)_emitContext.Module).Inner.DefineType(fullTypeName, typeAttributes);
                    }
                    else
                    {
                        Type parent;
                        if (!string.IsNullOrEmpty(baseTypeName))
                        {
                            parent = ILSerializer.ResolveType(baseTypeName);
                        }
                        else
                        {
                            bool hasSequentialLayout = (typeAttributes & TypeAttributes.SequentialLayout) != 0;
                            parent = hasSequentialLayout ? typeof(ValueType) : typeof(object);
                        }
                        // Preserve SequentialLayout so struct field ordering and interop ABI match the source.
                        var linkAttributes = typeAttributes;
                        if (resolvedInterfaces != null)
                        {
                            typeBuilder = ((LiveModuleBuilder)_emitContext.Module).Inner.DefineType(fullTypeName, linkAttributes, parent, resolvedInterfaces);
                        }
                        else
                        {
                            typeBuilder = ((LiveModuleBuilder)_emitContext.Module).Inner.DefineType(fullTypeName, linkAttributes, parent);
                        }
                    }
                }
                catch (ArgumentException exception)
                {
                    if (_emitContext.LinkedTypes.TryGetValue(fullTypeName, out var existingTypeBuilder))
                    {
                        _typeBuilders[fullTypeName] = existingTypeBuilder;
                        SkipTypeData(metaReader);
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"ILLinker: failed to define type '{fullTypeName}'", exception);
                }

                _typeBuilders[fullTypeName] = typeBuilder;
                _emitContext.LinkedTypes[fullTypeName] = typeBuilder;

                if (typeGenericParamCount > 0)
                {
                    var typeGenericParams = typeBuilder.DefineGenericParameters(typeGenericParamNames);
                    var genericParamMap = new Dictionary<string, Type>(typeGenericParamCount);
                    for (int genericIndex = 0; genericIndex < typeGenericParamCount; genericIndex++)
                    {
                        genericParamMap[typeGenericParamNames[genericIndex]] = typeGenericParams[genericIndex];
                    }

                    _typeGenericParams[fullTypeName] = genericParamMap;
                }
                else
                {
                    _typeGenericParams[fullTypeName] = new Dictionary<string, Type>();
                }

                int fieldCount = metaReader.ReadInt32();
                var fields = new List<SerializedFieldInfo>(fieldCount);
                for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                {
                    var fieldName = metaReader.ReadString();
                    var fieldAttributes = (FieldAttributes)metaReader.ReadInt32();
                    var fieldTypeName = metaReader.ReadString();
                    var goArrayLength = metaReader.ReadInt32();
                    var elementTypeName = metaReader.ReadString();
                    fields.Add(new SerializedFieldInfo(fieldName, fieldAttributes, fieldTypeName, goArrayLength, elementTypeName));
                }

                int methodCount = metaReader.ReadInt32();
                var methodInfos = new List<SerializedMethodInfo>(methodCount);
                for (int methodIndex = 0; methodIndex < methodCount; methodIndex++)
                {
                    var methodName = metaReader.ReadString();
                    var methodAttributes = (MethodAttributes)metaReader.ReadInt32();
                    int methodGenericParamCount = metaReader.ReadInt32();
                    var methodGenericParamNames = new string[methodGenericParamCount];
                    for (int genericIndex = 0; genericIndex < methodGenericParamCount; genericIndex++)
                    {
                        methodGenericParamNames[genericIndex] = metaReader.ReadString();
                    }
                    var returnTypeName = metaReader.ReadString();
                    int paramCount = metaReader.ReadInt32();
                    var paramTypeNames = new string[paramCount];
                    for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                    {
                        paramTypeNames[paramIndex] = metaReader.ReadString();
                    }
                    var bodyIndex = metaReader.ReadInt32();
                    methodInfos.Add(new SerializedMethodInfo(methodName, methodAttributes, returnTypeName, paramTypeNames, bodyIndex, methodGenericParamNames));
                }

                int interfaceImplCount = metaReader.ReadInt32();
                var interfaceMappings = new InterfaceMethodMapping[interfaceImplCount];
                for (int implIndex = 0; implIndex < interfaceImplCount; implIndex++)
                {
                    interfaceMappings[implIndex] = InterfaceMethodMapping.Read(metaReader);
                }

                _typeInfos.Add(new DeserializedTypeInfo(fullTypeName, typeBuilder, fields, methodInfos, interfaceMappings));
            }

            foreach (var typeInfo in _typeInfos)
            {
                _currentArchiveTypes.Add(typeInfo.FullTypeName);
            }
        }

        private void DefineFields()
        {
            foreach (var typeInfo in _typeInfos)
            {
                int blankFieldIndex = 0;
                foreach (var field in typeInfo.Fields)
                {
                    Type fieldType;

                    if (field.GoArrayLength > 0 && !string.IsNullOrEmpty(field.ElementTypeName))
                    {
                        var elementType = ILSerializer.ResolveType(field.ElementTypeName, _typeBuilders);
                        fieldType = _emitContext.Mapper.GetOrCreateInlineArrayType(elementType, field.GoArrayLength);
                    }
                    else if (field.GoArrayLength > 0 && field.TypeName.Contains("GoArray_"))
                    {
                        var arrayTypeName = field.TypeName.EndsWith("[]")
                            ? field.TypeName.Substring(0, field.TypeName.Length - 2)
                            : field.TypeName;
                        var parsedElementName = arrayTypeName.Replace("GoArray_", "").Replace($"_{field.GoArrayLength}", "").Replace('_', '.');
                        var elementType = ILSerializer.ResolveType(parsedElementName, _typeBuilders);
                        fieldType = _emitContext.Mapper.GetOrCreateInlineArrayType(elementType, field.GoArrayLength);
                    }
                    else
                    {
                        fieldType = ILSerializer.ResolveType(field.TypeName, _typeBuilders, GetTypeGenericParams(typeInfo.FullTypeName));

                        if (fieldType.IsArray && field.GoArrayLength > 0)
                        {
                            var elementType = fieldType.GetElementType()!;
                            var inlineType = _emitContext.Mapper.GetOrCreateInlineArrayType(elementType, field.GoArrayLength);
                            if (!inlineType.IsArray)
                            {
                                fieldType = inlineType;
                                if (inlineType is System.Reflection.Emit.TypeBuilder inlineTypeBuilder)
                                {
                                    _typeBuilders[inlineTypeBuilder.Name] = inlineTypeBuilder;
                                }
                            }
                        }
                    }

                    var actualFieldName = field.Name;
                    if (field.Name == "_")
                    {
                        actualFieldName = $"_pad{blankFieldIndex++}";
                    }

                    var fieldBuilder = typeInfo.TypeBuilder.DefineField(actualFieldName, fieldType, field.Attributes);
                    _fieldBuilders[typeInfo.FullTypeName + "." + actualFieldName] = fieldBuilder;
                    _emitContext.LinkedFields[typeInfo.FullTypeName + "." + actualFieldName] = fieldBuilder;
                    if (actualFieldName != field.Name)
                    {
                        _fieldBuilders[typeInfo.FullTypeName + "." + field.Name] = fieldBuilder;
                    }

                    foreach (var (_, symbol) in _package.Exports)
                    {
                        if (symbol is StructTypeSymbol structSymbol && structSymbol.Name == typeInfo.TypeBuilder.Name)
                        {
                            foreach (var fieldSymbol in structSymbol.Fields)
                            {
                                if (fieldSymbol.Name == field.Name)
                                {
                                    _emitContext.StructFields[fieldSymbol] = new LiveFieldBuilder(fieldBuilder);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void DefineMethods()
        {
            foreach (var typeInfo in _typeInfos)
            {
                var typeGenericParamMap = GetTypeGenericParams(typeInfo.FullTypeName);

                foreach (var methodInfo in typeInfo.Methods)
                {
                    MethodBuilder methodBuilder;
                    if (methodInfo.GenericParamNames.Length > 0)
                    {
                        methodBuilder = typeInfo.TypeBuilder.DefineMethod(methodInfo.MethodName, methodInfo.Attributes);
                        var genericParams = methodBuilder.DefineGenericParameters(methodInfo.GenericParamNames);
                        var methodGenericParamMap = new Dictionary<string, Type>(typeGenericParamMap);
                        for (int genericIndex = 0; genericIndex < genericParams.Length; genericIndex++)
                        {
                            methodGenericParamMap[methodInfo.GenericParamNames[genericIndex]] = genericParams[genericIndex];
                        }

                        var returnType = ILSerializer.ResolveTypeWithGenericParams(methodInfo.ReturnTypeName, _typeBuilders, methodGenericParamMap);
                        var paramTypes = new Type[methodInfo.ParamTypeNames.Length];
                        for (int paramIndex = 0; paramIndex < methodInfo.ParamTypeNames.Length; paramIndex++)
                        {
                            paramTypes[paramIndex] = ILSerializer.ResolveTypeWithGenericParams(methodInfo.ParamTypeNames[paramIndex], _typeBuilders, methodGenericParamMap);
                        }
                        methodBuilder.SetReturnType(returnType);
                        methodBuilder.SetParameters(paramTypes);

                        var methodKey = BuildMethodKey(typeInfo.FullTypeName, methodInfo.MethodName, methodInfo.ParamTypeNames, methodInfo.GenericParamNames.Length);
                        _methodGenericParams[methodKey] = methodGenericParamMap;
                    }
                    else
                    {
                        Type returnType;
                        try
                        {
                            returnType = ILSerializer.ResolveType(methodInfo.ReturnTypeName, _typeBuilders, typeGenericParamMap);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new InvalidOperationException(
                                $"LinkIL: failed to resolve return type '{methodInfo.ReturnTypeName}' for non-generic method " +
                                $"'{typeInfo.FullTypeName}.{methodInfo.MethodName}' (GenericParamNames={methodInfo.GenericParamNames.Length})", ex);
                        }
                        var paramTypes = new Type[methodInfo.ParamTypeNames.Length];
                        for (int paramIndex = 0; paramIndex < methodInfo.ParamTypeNames.Length; paramIndex++)
                        {
                            try
                            {
                                paramTypes[paramIndex] = ILSerializer.ResolveType(methodInfo.ParamTypeNames[paramIndex], _typeBuilders, typeGenericParamMap);
                            }
                            catch (InvalidOperationException ex)
                            {
                                throw new InvalidOperationException(
                                    $"LinkIL: failed to resolve param type '{methodInfo.ParamTypeNames[paramIndex]}' (param {paramIndex}) for non-generic method " +
                                    $"'{typeInfo.FullTypeName}.{methodInfo.MethodName}' (GenericParamNames={methodInfo.GenericParamNames.Length})", ex);
                            }
                        }
                        methodBuilder = typeInfo.TypeBuilder.DefineMethod(methodInfo.MethodName, methodInfo.Attributes, returnType, paramTypes);
                    }

                    RegisterLinkedMethod(typeInfo.FullTypeName, methodInfo, methodBuilder);

                    bool isPackageStaticClass = (typeInfo.TypeBuilder.Attributes & TypeAttributes.Abstract) != 0
                        && (typeInfo.TypeBuilder.Attributes & TypeAttributes.Sealed) != 0;
                    if (isPackageStaticClass)
                    {
                        _emitContext.LinkedMethods[methodInfo.MethodName] = methodBuilder;

                        foreach (var (_, symbol) in _package.Exports)
                        {
                            if (symbol is FunctionSymbol functionSymbol && functionSymbol.Name == methodInfo.MethodName)
                            {
                                _emitContext.CachedMethods[functionSymbol] = methodBuilder;
                            }
                        }
                    }
                }
            }
        }

        private Dictionary<string, Type> GetTypeGenericParams(string fullTypeName)
        {
            if (_typeGenericParams.TryGetValue(fullTypeName, out var genericParams))
            {
                return genericParams;
            }

            return new Dictionary<string, Type>();
        }

        private Dictionary<string, Type> GetTypeGenericParams(Type? type)
        {
            if (type == null)
            {
                return new Dictionary<string, Type>();
            }

            var fullTypeName = type.FullName ?? type.Name;
            return GetTypeGenericParams(fullTypeName);
        }

        private static string BuildMethodKey(string declaringTypeName, string methodName, string[] paramTypeNames, int genericArity)
        {
            return declaringTypeName + "." + methodName + "`" + genericArity + "(" + string.Join(",", paramTypeNames) + ")";
        }

        private string BuildMethodKey(string declaringTypeName, MethodToken token)
        {
            var paramTypeNames = new string[token.ParameterTypes.Length];
            for (int index = 0; index < token.ParameterTypes.Length; index++)
            {
                paramTypeNames[index] = GetTypeNameFromToken(token.ParameterTypes[index]);
            }

            var genericArity = 0;
            if (token.Kind == MethodTokenKind.MethodSpec && token.GenericDefinition != null)
            {
                genericArity = token.GenericDefinition.GenericTypeArguments.Length;
            }

            return BuildMethodKey(declaringTypeName, token.MethodName, paramTypeNames, genericArity);
        }

        private void RegisterLinkedMethod(string declaringTypeName, SerializedMethodInfo methodInfo, MethodBuilder methodBuilder)
        {
            var fullMethodKey = BuildMethodKey(declaringTypeName, methodInfo.MethodName,
                methodInfo.ParamTypeNames, methodInfo.GenericParamNames.Length);

            _methodBuilders[fullMethodKey] = methodBuilder;
            _emitContext.LinkedMethods[fullMethodKey] = methodBuilder;

            if (methodInfo.BodyIndex >= 0)
            {
                _methodILIndices[fullMethodKey] = methodInfo.BodyIndex;
            }
        }

        private static bool MatchesMethodName(string methodKey, string declaringTypeName, string methodName)
        {
            if (!methodKey.StartsWith(declaringTypeName + ".", StringComparison.Ordinal))
            {
                return false;
            }

            var methodPortion = methodKey.Substring(declaringTypeName.Length + 1);
            return methodPortion.StartsWith(methodName + "`", StringComparison.Ordinal);
        }


        private MethodBuilder? FindMethodBuilderBySignature(string declaringTypeName, string methodName, Type[] resolvedParamTypes)
        {
            var paramSignature = string.Join(",",
                Array.ConvertAll(resolvedParamTypes, NgoWriter.GetTypeNameStatic));

            MethodBuilder? match = null;
            foreach (var (key, builder) in _methodBuilders)
            {
                if (builder.Name != methodName)
                {
                    continue;
                }

                var declName = builder.DeclaringType?.FullName ?? builder.DeclaringType?.Name;
                if (declName != declaringTypeName
                    && !(declName != null && declaringTypeName.EndsWith("." + declName)))
                {
                    continue;
                }

                var paramsStart = key.IndexOf('(');
                if (paramsStart < 0)
                {
                    continue;
                }

                var keyParams = key.Substring(paramsStart + 1, key.Length - paramsStart - 2);
                if (!string.Equals(keyParams, paramSignature, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        $"ILLinker: ambiguous method lookup for '{declaringTypeName}.{methodName}'");
                }

                match = builder;
            }

            return match;
        }

        private MethodBuilder? FindUniqueMethodBuilder(string declaringTypeName, string methodName)
        {
            MethodBuilder? match = null;
            foreach (var (key, builder) in _methodBuilders)
            {
                if (!MatchesMethodName(key, declaringTypeName, methodName))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException($"ILLinker: method lookup for '{declaringTypeName}.{methodName}' is ambiguous");
                }

                match = builder;
            }

            return match;
        }

        private void CreateValueTypesAndInterfaces()
        {
            var valueTypesToCreate = new Dictionary<string, TypeBuilder>();
            foreach (var (fullName, typeBuilder) in _typeBuilders)
            {
                if (!_currentArchiveTypes.Contains(fullName))
                {
                    continue;
                }
                bool isStaticClass = (typeBuilder.Attributes & TypeAttributes.Abstract) != 0
                    && (typeBuilder.Attributes & TypeAttributes.Sealed) != 0;
                bool isInterface = (typeBuilder.Attributes & TypeAttributes.Interface) != 0;
                bool isValueType = typeBuilder.BaseType == typeof(ValueType);

                if (isStaticClass)
                {
                    continue;
                }

                if (isInterface || isValueType)
                {
                    valueTypesToCreate[fullName] = typeBuilder;
                }
                else
                {
                    _deferredClassTypes.Add(fullName);
                }
            }

            var fieldDependencies = new Dictionary<string, HashSet<string>>();
            foreach (var typeInfo in _typeInfos)
            {
                if (!valueTypesToCreate.ContainsKey(typeInfo.FullTypeName))
                {
                    continue;
                }
                var dependencies = new HashSet<string>();
                foreach (var field in typeInfo.Fields)
                {
                    if (valueTypesToCreate.ContainsKey(field.TypeName))
                    {
                        dependencies.Add(field.TypeName);
                    }
                    else
                    {
                        foreach (var (valueTypeName, _) in valueTypesToCreate)
                        {
                            if (field.TypeName.Contains(valueTypeName) && valueTypeName != typeInfo.FullTypeName)
                            {
                                dependencies.Add(valueTypeName);
                            }
                        }
                    }
                }
                fieldDependencies[typeInfo.FullTypeName] = dependencies;
            }

            var sortedValueTypes = TopologicalSortByDependencies(valueTypesToCreate.Keys, fieldDependencies);

            foreach (var fullName in sortedValueTypes)
            {
                var typeBuilder = valueTypesToCreate[fullName];
                var runtimeType = typeBuilder.CreateType()!;
                RegisterLinkedType(runtimeType, typeBuilder);
                FinalizeInlineArrayTypes();
            }
        }

        private void FinalizeInlineArrayTypes()
        {
            foreach (var kvp in _emitContext.InlineArrayTypes)
            {
                if (kvp.Value is TypeBuilder inlineTb && !inlineTb.IsCreated())
                {
                    inlineTb.CreateType();
                }
            }
        }

        private void ProcessMethodOverrides()
        {
            foreach (var typeInfo in _typeInfos)
            {
                foreach (var interfaceMapping in typeInfo.InterfaceMappings)
                {
                    Type interfaceType;
                    try
                    {
                        interfaceType = ILSerializer.ResolveType(interfaceMapping.InterfaceTypeName, _typeBuilders);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException(
                            $"ILLinker: failed to resolve interface type '{interfaceMapping.InterfaceTypeName}' " +
                            $"for method overrides on '{typeInfo.FullTypeName}'", exception);
                    }

                    if (!interfaceType.IsInterface)
                    {
                        continue;
                    }

                    foreach (var methodMapping in interfaceMapping.Methods)
                    {
                        var bodyMethod = FindUniqueMethodBuilder(typeInfo.FullTypeName, methodMapping.BodyMethodName);
                        if (bodyMethod == null)
                        {
                            continue;
                        }

                        var interfaceMethod = FindInterfaceMethod(interfaceType, methodMapping.InterfaceMethodName);
                        if (interfaceMethod == null)
                        {
                            throw new InvalidOperationException(
                                $"ILLinker: could not find interface method '{methodMapping.InterfaceMethodName}' " +
                                $"on '{interfaceType.FullName}' (or its parent interfaces) " +
                                $"for override from '{typeInfo.FullTypeName}.{methodMapping.BodyMethodName}'");
                        }

                        try
                        {
                            typeInfo.TypeBuilder.DefineMethodOverride(bodyMethod, interfaceMethod);
                        }
                        catch (Exception exception)
                        {
                            throw new InvalidOperationException(
                                $"ILLinker: failed to define method override " +
                                $"{typeInfo.FullTypeName}.{methodMapping.BodyMethodName} -> " +
                                $"{interfaceType.FullName}::{methodMapping.InterfaceMethodName}", exception);
                        }
                    }
                }
            }
        }

        private static MethodInfo? FindInterfaceMethod(Type interfaceType, string methodName)
        {
            var method = interfaceType.GetMethod(methodName);
            if (method != null)
            {
                return method;
            }

            foreach (var parentInterface in interfaceType.GetInterfaces())
            {
                method = parentInterface.GetMethod(methodName);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private void DefineDefaultConstructors()
        {
            foreach (var (fullName, typeBuilder) in _typeBuilders)
            {
                if (!_currentArchiveTypes.Contains(fullName))
                {
                    continue;
                }

                bool isStaticClass = (typeBuilder.Attributes & TypeAttributes.Abstract) != 0
                    && (typeBuilder.Attributes & TypeAttributes.Sealed) != 0;
                bool isInterface = (typeBuilder.Attributes & TypeAttributes.Interface) != 0;
                bool isValueType = typeBuilder.BaseType == typeof(ValueType);

                if (isStaticClass || isInterface || isValueType)
                {
                    continue;
                }

                bool hasConstructor = false;
                foreach (var typeInfo in _typeInfos)
                {
                    if (typeInfo.FullTypeName == fullName)
                    {
                        foreach (var method in typeInfo.Methods)
                        {
                            if (method.MethodName == ".ctor")
                            {
                                hasConstructor = true;
                                break;
                            }
                        }
                        break;
                    }
                }
                if (!hasConstructor)
                {
                    var constructorBuilder = typeBuilder.DefineDefaultConstructor(
                        MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                    _constructorBuilders[fullName] = constructorBuilder;
                }
            }
        }

        private void DeserializeAndReplayBodies(BinaryReader codeReader)
        {
            int bodyCount = codeReader.ReadInt32();
            var bodies = new List<MethodBodyData>(bodyCount);

            for (int bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
            {
                var bodyData = new MethodBodyData();
                bodyData.MaxStack = codeReader.ReadInt32();

                int localCount = codeReader.ReadInt32();
                bodyData.LocalTypes = new string[localCount];
                for (int localIndex = 0; localIndex < localCount; localIndex++)
                {
                    bodyData.LocalTypes[localIndex] = codeReader.ReadString();
                }

                int ilLength = codeReader.ReadInt32();
                bodyData.ILBytes = codeReader.ReadBytes(ilLength);

                int tokenCount = codeReader.ReadInt32();
                bodyData.TokenEntries = new List<ILTokenEntry>(tokenCount);
                for (int tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
                {
                    bodyData.TokenEntries.Add(ILTokenEntry.Read(codeReader));
                }

                int handlerCount = codeReader.ReadInt32();
                bodyData.ExceptionHandlers = new List<ExceptionHandlerData>(handlerCount);
                for (int handlerIndex = 0; handlerIndex < handlerCount; handlerIndex++)
                {
                    bodyData.ExceptionHandlers.Add(new ExceptionHandlerData
                    {
                        Kind = (ExceptionRegionKind)codeReader.ReadInt32(),
                        TryOffset = codeReader.ReadInt32(),
                        TryLength = codeReader.ReadInt32(),
                        HandlerOffset = codeReader.ReadInt32(),
                        HandlerLength = codeReader.ReadInt32(),
                        FilterOffset = codeReader.ReadInt32(),
                        CatchTypeName = codeReader.ReadString()
                    });
                }

                bodies.Add(bodyData);
            }

            foreach (var (methodKey, bodyIndex) in _methodILIndices)
            {
                if (bodyIndex >= bodies.Count)
                {
                    continue;
                }

                var bodyData = bodies[bodyIndex];

                if (_methodBuilders.TryGetValue(methodKey, out var methodBuilder))
                {
                    var combinedGenericParams = new Dictionary<string, Type>(GetTypeGenericParams(methodBuilder.DeclaringType));
                    if (_methodGenericParams.TryGetValue(methodKey, out var methodGenericParamMap))
                    {
                        foreach (var (name, genericParamType) in methodGenericParamMap)
                        {
                            combinedGenericParams[name] = genericParamType;
                        }
                    }

                    _currentReplayMethodKey = methodKey;
                    SetupGenericParameterContext(methodKey, methodBuilder);
                    ReplayIL(methodBuilder, bodyData.ILBytes, bodyData.LocalTypes, bodyData.TokenEntries,
                        bodyData.ExceptionHandlers, combinedGenericParams);
                }
            }
        }

        private void SetupGenericParameterContext(string methodKey, MethodBuilder methodBuilder)
        {
            if (methodBuilder.IsGenericMethodDefinition)
            {
                _currentMethodGenericParameters = methodBuilder.GetGenericArguments();
            }
            else
            {
                _currentMethodGenericParameters = Type.EmptyTypes;
            }

            var declaringType = methodBuilder.DeclaringType;
            if (declaringType != null && declaringType.IsGenericTypeDefinition)
            {
                _currentTypeGenericParameters = declaringType.GetGenericArguments();
            }
            else
            {
                _currentTypeGenericParameters = Type.EmptyTypes;
            }
        }

        private void CreateClassTypes()
        {
            var classTypeDependencies = new Dictionary<string, HashSet<string>>();
            foreach (var fullName in _deferredClassTypes)
            {
                var dependencies = new HashSet<string>();
                foreach (var typeInfo in _typeInfos)
                {
                    if (typeInfo.FullTypeName != fullName)
                    {
                        continue;
                    }
                    foreach (var field in typeInfo.Fields)
                    {
                        if (_deferredClassTypes.Contains(field.TypeName) && field.TypeName != fullName)
                        {
                            dependencies.Add(field.TypeName);
                        }
                    }
                }
                classTypeDependencies[fullName] = dependencies;
            }

            var sortedClassTypes = TopologicalSortByDependencies(_deferredClassTypes, classTypeDependencies);

            foreach (var fullName in sortedClassTypes)
            {
                if (_typeBuilders.TryGetValue(fullName, out var classTypeBuilder))
                {
                    try
                    {
                        var runtimeType = classTypeBuilder.CreateType()!;
                        RegisterLinkedType(runtimeType, classTypeBuilder);
                    }
                    catch (TypeLoadException exception)
                    {
                        throw new InvalidOperationException(
                            $"ILLinker: failed to create type '{fullName}'", exception);
                    }
                }
            }
        }

        private void CreateStaticTypes()
        {
            foreach (var (fullName, typeBuilder) in _typeBuilders)
            {
                if (!_currentArchiveTypes.Contains(fullName))
                {
                    continue;
                }
                bool isStaticClass = (typeBuilder.Attributes & TypeAttributes.Abstract) != 0
                    && (typeBuilder.Attributes & TypeAttributes.Sealed) != 0;
                if (isStaticClass)
                {
                    typeBuilder.CreateType();
                }
            }
        }

        private static void SkipTypeData(BinaryReader metaReader)
        {
            int fieldCount = metaReader.ReadInt32();
            for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
            {
                metaReader.ReadString();  // fieldName
                metaReader.ReadInt32();   // fieldAttributes
                metaReader.ReadString();  // fieldTypeName
                metaReader.ReadInt32();   // goArrayLength
                metaReader.ReadString();  // elemTypeName
            }
            int methodCount = metaReader.ReadInt32();
            for (int methodIndex = 0; methodIndex < methodCount; methodIndex++)
            {
                metaReader.ReadString();
                metaReader.ReadInt32();
                int genericParamCount = metaReader.ReadInt32();
                for (int genericIndex = 0; genericIndex < genericParamCount; genericIndex++)
                {
                    metaReader.ReadString();
                }
                metaReader.ReadString();
                int paramCount = metaReader.ReadInt32();
                for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                {
                    metaReader.ReadString();
                }
                metaReader.ReadInt32();
            }
            int interfaceImplCount = metaReader.ReadInt32();
            for (int implIndex = 0; implIndex < interfaceImplCount; implIndex++)
            {
                metaReader.ReadString();
                int mappingMethodCount = metaReader.ReadInt32();
                for (int mappingIndex = 0; mappingIndex < mappingMethodCount; mappingIndex++)
                {
                    metaReader.ReadString();
                    metaReader.ReadString();
                }
            }
        }

        private void RegisterLinkedType(Type runtimeType, TypeBuilder typeBuilder)
        {
            foreach (var (_, symbol) in _package.Exports)
            {
                if (symbol is StructTypeSymbol structSymbol && structSymbol.Name == typeBuilder.Name)
                {
                    _emitContext.Mapper.Register(structSymbol, runtimeType);
                    _emitContext.FinalizedTypes.Add(structSymbol);
                }
                else if (symbol is InterfaceTypeSymbol interfaceSymbol && interfaceSymbol.Name == typeBuilder.Name)
                {
                    _emitContext.Mapper.Register(interfaceSymbol, runtimeType);
                    _emitContext.FinalizedTypes.Add(interfaceSymbol);
                }
            }
        }

        // =====================================================================
        // IL replay
        // =====================================================================

        private const byte TwoByteOpcodePrefix = 0xFE;

        private void ReplayIL(MethodBuilder methodBuilder, byte[] il, string[] localTypeNames,
            List<ILTokenEntry> tokenEntries, List<ExceptionHandlerData> exceptionHandlers,
            Dictionary<string, Type>? genericParams = null)
        {
            var ilGenerator = methodBuilder.GetILGenerator();

            foreach (var localTypeName in localTypeNames)
            {
                var localType = ILSerializer.ResolveType(localTypeName, _typeBuilders, genericParams);
                ilGenerator.DeclareLocal(localType);
            }

            var tokenMap = new Dictionary<int, ILTokenEntry>();
            foreach (var entry in tokenEntries)
            {
                tokenMap[entry.Offset] = entry;
            }

            var labels = new Dictionary<int, Label>();
            PreScanBranchTargets(il, labels, ilGenerator);

            var exceptionEvents = BuildExceptionEventMap(exceptionHandlers, il, genericParams);

            int position = 0;
            while (position < il.Length)
            {
                if (labels.TryGetValue(position, out var targetLabel))
                {
                    ilGenerator.MarkLabel(targetLabel);
                }

                FireExceptionEvents(ilGenerator, exceptionEvents, position);

                byte firstByte = il[position++];
                OpCode opCode;
                if (firstByte == TwoByteOpcodePrefix && position < il.Length)
                {
                    byte secondByte = il[position++];
                    opCode = ILSerializer.GetTwoByteOpCode(secondByte);
                }
                else
                {
                    opCode = ILSerializer.GetOpCode(firstByte);
                }

                switch (opCode.OperandType)
                {
                    case OperandType.InlineNone:
                    {
                        ilGenerator.Emit(opCode);
                        break;
                    }
                    case OperandType.ShortInlineBrTarget:
                    {
                        int branchOffset = (sbyte)il[position++];
                        int branchTarget = position + branchOffset;
                        ilGenerator.Emit(opCode, GetOrCreateLabel(labels, branchTarget, ilGenerator));
                        break;
                    }
                    case OperandType.InlineBrTarget:
                    {
                        int branchOffset = BitConverter.ToInt32(il, position);
                        position += 4;
                        int branchTarget = position + branchOffset;
                        ilGenerator.Emit(opCode, GetOrCreateLabel(labels, branchTarget, ilGenerator));
                        break;
                    }
                    case OperandType.ShortInlineI:
                    {
                        if (opCode == OpCodes.Ldc_I4_S)
                        {
                            ilGenerator.Emit(OpCodes.Ldc_I4_S, (sbyte)il[position++]);
                        }
                        else
                        {
                            ilGenerator.Emit(opCode, il[position++]);
                        }
                        break;
                    }
                    case OperandType.InlineI:
                    {
                        ilGenerator.Emit(opCode, BitConverter.ToInt32(il, position));
                        position += 4;
                        break;
                    }
                    case OperandType.InlineI8:
                    {
                        ilGenerator.Emit(opCode, BitConverter.ToInt64(il, position));
                        position += 8;
                        break;
                    }
                    case OperandType.ShortInlineR:
                    {
                        ilGenerator.Emit(opCode, BitConverter.ToSingle(il, position));
                        position += 4;
                        break;
                    }
                    case OperandType.InlineR:
                    {
                        ilGenerator.Emit(opCode, BitConverter.ToDouble(il, position));
                        position += 8;
                        break;
                    }
                    case OperandType.ShortInlineVar:
                    {
                        ilGenerator.Emit(opCode, il[position++]);
                        break;
                    }
                    case OperandType.InlineVar:
                    {
                        ilGenerator.Emit(opCode, BitConverter.ToInt16(il, position));
                        position += 2;
                        break;
                    }
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                    case OperandType.InlineType:
                    case OperandType.InlineString:
                    case OperandType.InlineTok:
                    case OperandType.InlineSig:
                    {
                        int tokenOffset = position;
                        position += 4;
                        if (tokenMap.TryGetValue(tokenOffset, out var token))
                        {
                            EmitTokenOpcode(ilGenerator, opCode, token, genericParams);
                        }
                        break;
                    }
                    case OperandType.InlineSwitch:
                    {
                        int caseCount = BitConverter.ToInt32(il, position);
                        position += 4;
                        int switchBaseOffset = position + caseCount * 4;
                        var switchLabels = new Label[caseCount];
                        for (int caseIndex = 0; caseIndex < caseCount; caseIndex++)
                        {
                            int caseTarget = switchBaseOffset + BitConverter.ToInt32(il, position);
                            position += 4;
                            switchLabels[caseIndex] = GetOrCreateLabel(labels, caseTarget, ilGenerator);
                        }
                        ilGenerator.Emit(OpCodes.Switch, switchLabels);
                        break;
                    }
                    default:
                    {
                        throw new InvalidOperationException(
                            $"ILLinker: unsupported operand type {opCode.OperandType} for opcode {opCode.Name}");
                    }
                }
            }

            FireExceptionEvents(ilGenerator, exceptionEvents, position);
        }

        private void FireExceptionEvents(ILGenerator ilGenerator,
            Dictionary<int, List<ExceptionEventInfo>> exceptionEvents, int offset)
        {
            if (!exceptionEvents.TryGetValue(offset, out var events))
            {
                return;
            }

            // EndException must fire before BeginTry/BeginCatch at the same offset
            events.Sort((a, b) => GetEventPriority(a.EventKind).CompareTo(GetEventPriority(b.EventKind)));

            foreach (var exceptionEvent in events)
            {
                switch (exceptionEvent.EventKind)
                {
                    case ExceptionEventKind.EndException:
                    {
                        ilGenerator.EndExceptionBlock();
                        break;
                    }
                    case ExceptionEventKind.BeginTry:
                    {
                        ilGenerator.BeginExceptionBlock();
                        break;
                    }
                    case ExceptionEventKind.BeginCatch:
                    {
                        ilGenerator.BeginCatchBlock(exceptionEvent.CatchType!);
                        break;
                    }
                    case ExceptionEventKind.BeginFinally:
                    {
                        ilGenerator.BeginFinallyBlock();
                        break;
                    }
                    case ExceptionEventKind.BeginFilter:
                    {
                        ilGenerator.BeginExceptFilterBlock();
                        break;
                    }
                    case ExceptionEventKind.BeginFault:
                    {
                        ilGenerator.BeginFaultBlock();
                        break;
                    }
                }
            }
        }

        private static int GetEventPriority(ExceptionEventKind kind)
        {
            return kind switch
            {
                ExceptionEventKind.EndException => 0,
                ExceptionEventKind.BeginTry => 1,
                ExceptionEventKind.BeginCatch => 2,
                ExceptionEventKind.BeginFinally => 2,
                ExceptionEventKind.BeginFilter => 2,
                ExceptionEventKind.BeginFault => 2,
                _ => 3,
            };
        }


        private enum ExceptionEventKind
        {
            EndException,
            BeginTry,
            BeginCatch,
            BeginFinally,
            BeginFilter,
            BeginFault,
        }

        private struct ExceptionEventInfo
        {
            public ExceptionEventKind EventKind;
            public Type? CatchType;
        }

        private Dictionary<int, List<ExceptionEventInfo>> BuildExceptionEventMap(
            List<ExceptionHandlerData> handlers, byte[] il,
            Dictionary<string, Type>? genericParams)
        {
            var events = new Dictionary<int, List<ExceptionEventInfo>>();

            if (handlers.Count == 0)
            {
                return events;
            }

            var handlersByTryBlock = new Dictionary<(int offset, int length), List<ExceptionHandlerData>>();
            foreach (var handler in handlers)
            {
                var key = (handler.TryOffset, handler.TryLength);
                if (!handlersByTryBlock.TryGetValue(key, out var list))
                {
                    list = new List<ExceptionHandlerData>();
                    handlersByTryBlock[key] = list;
                }
                list.Add(handler);
            }

            foreach (var ((tryOffset, tryLength), groupedHandlers) in handlersByTryBlock)
            {
                AddExceptionEvent(events, tryOffset, new ExceptionEventInfo
                {
                    EventKind = ExceptionEventKind.BeginTry
                });

                foreach (var handler in groupedHandlers)
                {
                    ExceptionEventKind eventKind;
                    Type? catchType = null;

                    switch (handler.Kind)
                    {
                        case ExceptionRegionKind.Catch:
                            eventKind = ExceptionEventKind.BeginCatch;
                            if (!string.IsNullOrEmpty(handler.CatchTypeName))
                            {
                                catchType = ILSerializer.ResolveType(handler.CatchTypeName, _typeBuilders, genericParams);
                            }
                            catchType ??= typeof(Exception);
                            break;
                        case ExceptionRegionKind.Finally:
                            eventKind = ExceptionEventKind.BeginFinally;
                            break;
                        case ExceptionRegionKind.Filter:
                            eventKind = ExceptionEventKind.BeginFilter;
                            break;
                        case ExceptionRegionKind.Fault:
                            eventKind = ExceptionEventKind.BeginFault;
                            break;
                        default:
                            eventKind = ExceptionEventKind.BeginCatch;
                            catchType = typeof(Exception);
                            break;
                    }

                    AddExceptionEvent(events, handler.HandlerOffset, new ExceptionEventInfo
                    {
                        EventKind = eventKind,
                        CatchType = catchType,
                    });
                }

                // Find the handler with the highest end offset — groupedHandlers is not
                // guaranteed to be in source order, so taking the last element is incorrect.
                int endOffset = 0;
                foreach (var h in groupedHandlers)
                {
                    int hEnd = h.HandlerOffset + h.HandlerLength;
                    if (hEnd > endOffset) endOffset = hEnd;
                }
                AddExceptionEvent(events, endOffset, new ExceptionEventInfo
                {
                    EventKind = ExceptionEventKind.EndException
                });
            }

            return events;
        }

        private static void AddExceptionEvent(Dictionary<int, List<ExceptionEventInfo>> events,
            int offset, ExceptionEventInfo exceptionEvent)
        {
            if (!events.TryGetValue(offset, out var list))
            {
                list = new List<ExceptionEventInfo>();
                events[offset] = list;
            }

            int insertIndex = list.Count;
            for (int index = 0; index < list.Count; index++)
            {
                if (exceptionEvent.EventKind < list[index].EventKind)
                {
                    insertIndex = index;
                    break;
                }
            }
            list.Insert(insertIndex, exceptionEvent);
        }

        private void EmitTokenOpcode(ILGenerator ilGenerator, OpCode opCode, ILTokenEntry token,
            Dictionary<string, Type>? genericParams = null)
        {
            switch (token.Kind)
            {
                case ILTokenKind.Type:
                {
                    var type = ResolveTypeToken(token.TypeToken!);
                    ilGenerator.Emit(opCode, type);
                    break;
                }
                case ILTokenKind.Method:
                {
                    var method = ResolveMethodToken(token.MethodToken!);
                    if (method == null)
                    {
                        var methodDeclName = token.MethodToken!.DeclaringType != null
                            ? GetTypeNameFromToken(token.MethodToken.DeclaringType)
                            : "?";
                        throw new InvalidOperationException(
                            $"ILLinker: unresolved method token '{methodDeclName}::{token.MethodToken.MethodName}' (kind={token.MethodToken.Kind})");
                    }
                    if (method is ConstructorInfo constructor)
                    {
                        ilGenerator.Emit(opCode, constructor);
                    }
                    else
                    {
                        ilGenerator.Emit(opCode, (MethodInfo)method);
                    }
                    break;
                }
                case ILTokenKind.Field:
                {
                    var field = ResolveFieldToken(token.FieldToken!);
                    if (field == null)
                    {
                        var fieldDeclName = token.FieldToken!.DeclaringType != null
                            ? GetTypeNameFromToken(token.FieldToken.DeclaringType)
                            : "?";
                        throw new InvalidOperationException(
                            $"ILLinker: unresolved field token '{fieldDeclName}::{token.FieldToken.FieldName}' (kind={token.FieldToken.Kind})");
                    }
                    ilGenerator.Emit(opCode, field);
                    break;
                }
                case ILTokenKind.String:
                {
                    ilGenerator.Emit(opCode, token.StringValue!);
                    break;
                }
            }
        }

        // =====================================================================
        // Structured token resolution (Section 3)
        // =====================================================================

        private Type ResolveTypeToken(TypeToken token)
        {
            switch (token.Kind)
            {
                case TypeTokenKind.TypeDef:
                {
                    if (_typeBuilders.TryGetValue(token.TypeName, out var typeBuilder))
                    {
                        // Source-compiled types were skipped during archive linking.
                        // Use the runtime type from the module so the CLR can load them.
                        if (_sourceCompiledTypes.Contains(token.TypeName)
                            && _emitContext.Module is Emit.Builder.LiveModuleBuilder liveMod)
                        {
                            var runtimeType = liveMod.Inner.GetType(token.TypeName);
                            if (runtimeType != null)
                            {
                                return runtimeType;
                            }
                        }
                        return typeBuilder;
                    }
                    throw new InvalidOperationException(
                        $"ILLinker: TypeDef '{token.TypeName}' not found in type builders");
                }
                case TypeTokenKind.PackageTypeRef:
                {
                    return ResolvePackageTypeRef(token.PackageImportPath, token.TypeName);
                }
                case TypeTokenKind.Primitive:
                {
                    return ResolvePrimitiveType(token.PrimitiveKind);
                }
                case TypeTokenKind.GenericInst:
                {
                    var genericDefinition = ResolveTypeToken(token.GenericDefinition!);
                    var typeArguments = new Type[token.GenericArguments.Length];
                    for (int index = 0; index < token.GenericArguments.Length; index++)
                    {
                        typeArguments[index] = ResolveTypeToken(token.GenericArguments[index]);
                    }
                    return genericDefinition.MakeGenericType(typeArguments);
                }
                case TypeTokenKind.Array:
                {
                    var elementType = ResolveTypeToken(token.ElementType!);
                    return elementType.MakeArrayType();
                }
                case TypeTokenKind.Pointer:
                {
                    var elementType = ResolveTypeToken(token.ElementType!);
                    return elementType.MakePointerType();
                }
                case TypeTokenKind.ByRef:
                {
                    var elementType = ResolveTypeToken(token.ElementType!);
                    return elementType.MakeByRefType();
                }
                case TypeTokenKind.GenericMethodParam:
                {
                    if (token.GenericParamIndex < _currentMethodGenericParameters.Length)
                    {
                        return _currentMethodGenericParameters[token.GenericParamIndex];
                    }
                    // Fallback: closures inside generic functions emit method-level params
                    // that belong to the closure type at link time
                    if (token.GenericParamIndex < _currentTypeGenericParameters.Length)
                    {
                        return _currentTypeGenericParameters[token.GenericParamIndex];
                    }
                    throw new InvalidOperationException(
                        $"ILLinker: generic method parameter index {token.GenericParamIndex} " +
                        $"out of range (method has {_currentMethodGenericParameters.Length}, " +
                        $"type has {_currentTypeGenericParameters.Length} generic parameters). " +
                        $"Current method: {_currentReplayMethodKey}");
                }
                case TypeTokenKind.GenericTypeParam:
                {
                    if (token.GenericParamIndex < _currentTypeGenericParameters.Length)
                    {
                        return _currentTypeGenericParameters[token.GenericParamIndex];
                    }
                    // Fallback: try method generic params (some closures emit type params that
                    // should be method params due to proxy type limitations)
                    if (token.GenericParamIndex < _currentMethodGenericParameters.Length)
                    {
                        return _currentMethodGenericParameters[token.GenericParamIndex];
                    }
                    throw new InvalidOperationException(
                        $"ILLinker: generic type parameter index {token.GenericParamIndex} " +
                        $"out of range (type has {_currentTypeGenericParameters.Length}, method has {_currentMethodGenericParameters.Length} generic parameters). " +
                        $"Method: {_currentReplayMethodKey}");
                }
                default:
                {
                    throw new InvalidOperationException(
                        $"ILLinker: unknown TypeToken kind {token.Kind}");
                }
            }
        }

        private Type ResolvePackageTypeRef(string packageImportPath, string typeName)
        {
            // typeName may already be fully qualified (e.g., "Ngo.Runtime.GoString")
            // or just a short name (e.g., "GoString"). Try both forms.
            if (_typeBuilders.TryGetValue(typeName, out var directMatch))
            {
                return directMatch;
            }

            var runtimeDirect = _runtimeAssembly.GetType(typeName);
            if (runtimeDirect != null)
            {
                return runtimeDirect;
            }

            var fullName = packageImportPath.Replace("/", ".") + "." + typeName;

            if (_typeBuilders.TryGetValue(fullName, out var typeBuilder))
            {
                return typeBuilder;
            }

            if (_typeBuilders.TryGetValue(typeName, out typeBuilder))
            {
                return typeBuilder;
            }

            foreach (var (key, builder) in _typeBuilders)
            {
                if (key.EndsWith("." + typeName))
                {
                    return builder;
                }
            }

            var runtimeType = _runtimeAssembly.GetType(fullName);
            if (runtimeType != null)
            {
                return runtimeType;
            }

            // Cache GetTypes() result to avoid calling it twice (O(n) each time).
            var runtimeAssemblyTypes = _runtimeAssembly.GetTypes();
            foreach (var candidateType in runtimeAssemblyTypes)
            {
                if (candidateType.Name == typeName || candidateType.Name.StartsWith(typeName + "`"))
                {
                    var goPackageAttribute = candidateType.GetCustomAttribute(typeof(GoPackageAttribute)) as GoPackageAttribute;
                    if (goPackageAttribute != null && goPackageAttribute.ImportPath == packageImportPath)
                    {
                        return candidateType;
                    }
                }
            }

            foreach (var candidateType in runtimeAssemblyTypes)
            {
                if (candidateType.Name == typeName || candidateType.Name.StartsWith(typeName + "`"))
                {
                    return candidateType;
                }
            }

            var clrType = Type.GetType(fullName) ?? Type.GetType(typeName);
            if (clrType != null)
            {
                return clrType;
            }

            foreach (var referencedAssembly in new[] { typeof(System.Numerics.Complex).Assembly })
            {
                clrType = referencedAssembly.GetType(fullName) ?? referencedAssembly.GetType(typeName);
                if (clrType != null)
                {
                    return clrType;
                }
            }

            // Search the dynamic module for types created during compilation
            // (e.g., InlineArray types that are runtime types, not TypeBuilders)
            if (_emitContext.Module is Emit.Builder.LiveModuleBuilder searchMod)
            {
                var moduleType = searchMod.Inner.GetType(typeName);
                if (moduleType != null)
                {
                    return moduleType;
                }
            }

            throw new InvalidOperationException(
                $"ILLinker: PackageTypeRef '{packageImportPath}::{typeName}' could not be resolved");
        }

        private static Type ResolvePrimitiveType(PrimitiveTypeKind primitiveKind)
        {
            return primitiveKind switch
            {
                PrimitiveTypeKind.Void => typeof(void),
                PrimitiveTypeKind.Bool => typeof(bool),
                PrimitiveTypeKind.Byte => typeof(byte),
                PrimitiveTypeKind.SByte => typeof(sbyte),
                PrimitiveTypeKind.Int16 => typeof(short),
                PrimitiveTypeKind.UInt16 => typeof(ushort),
                PrimitiveTypeKind.Int32 => typeof(int),
                PrimitiveTypeKind.UInt32 => typeof(uint),
                PrimitiveTypeKind.Int64 => typeof(long),
                PrimitiveTypeKind.UInt64 => typeof(ulong),
                PrimitiveTypeKind.Float32 => typeof(float),
                PrimitiveTypeKind.Float64 => typeof(double),
                PrimitiveTypeKind.String => typeof(string),
                PrimitiveTypeKind.Object => typeof(object),
                PrimitiveTypeKind.IntPtr => typeof(IntPtr),
                PrimitiveTypeKind.UIntPtr => typeof(UIntPtr),
                PrimitiveTypeKind.Char => typeof(char),
                _ => throw new InvalidOperationException($"ILLinker: unknown primitive type kind {primitiveKind}"),
            };
        }

        private MethodBase? ResolveMethodToken(MethodToken token)
        {
            switch (token.Kind)
            {
                case MethodTokenKind.MethodDef:
                {
                    var declaringTypeName = GetTypeNameFromToken(token.DeclaringType!);
                    return ResolveMethodDef(declaringTypeName, token);
                }
                case MethodTokenKind.MemberRef:
                {
                    return ResolveMemberRefMethod(token);
                }
                case MethodTokenKind.MethodSpec:
                {
                    var genericDefinition = ResolveMethodToken(token.GenericDefinition!);
                    if (genericDefinition == null)
                    {
                        genericDefinition = FindGenericMethodDefinition(token.GenericDefinition!);
                    }
                    if (genericDefinition == null)
                    {
                        throw new InvalidOperationException(
                            $"ILLinker: MethodSpec generic definition could not be resolved");
                    }
                    if (genericDefinition is MethodInfo genericMethodInfo && genericMethodInfo.IsGenericMethodDefinition)
                    {
                        var typeArguments = new Type[token.GenericTypeArguments.Length];
                        for (int index = 0; index < token.GenericTypeArguments.Length; index++)
                        {
                            typeArguments[index] = ResolveTypeToken(token.GenericTypeArguments[index]);
                        }
                        return genericMethodInfo.MakeGenericMethod(typeArguments);
                    }
                    return genericDefinition;
                }
                default:
                {
                    throw new InvalidOperationException(
                        $"ILLinker: unknown MethodToken kind {token.Kind}");
                }
            }
        }

        private string GetTypeNameFromToken(TypeToken typeToken)
        {
            if (typeToken.Kind == TypeTokenKind.TypeDef || typeToken.Kind == TypeTokenKind.PackageTypeRef)
            {
                return typeToken.TypeName;
            }

            var resolvedType = ResolveTypeToken(typeToken);
            return resolvedType.FullName ?? resolvedType.Name;
        }

        private MethodBase? ResolveMethodDef(string declaringTypeName, MethodToken token)
        {
            var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);
            var methodBuilder = FindMethodBuilderBySignature(declaringTypeName, token.MethodName, resolvedParamTypes);
            if (methodBuilder != null)
            {
                return methodBuilder;
            }

            if (token.MethodName == ".ctor")
            {
                if (_constructorBuilders.TryGetValue(declaringTypeName, out var constructorBuilder))
                {
                    return constructorBuilder;
                }
            }

            return null;
        }

        private MethodBase? ResolveMemberRefMethod(MethodToken token)
        {
            var declaringType = ResolveTypeToken(token.DeclaringType!);

            if (token.MethodName == ".ctor")
            {
                return ResolveMemberRefConstructor(declaringType, token);
            }

            if (token.MethodName == ".cctor")
            {
                return declaringType.GetConstructor(
                    BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            }

            if (declaringType is TypeBuilder memberRefTypeBuilder)
            {
                return ResolveMethodOnTypeBuilderByName(memberRefTypeBuilder, token);
            }

            if (declaringType.IsGenericType && EmitContext.HasTypeBuilderArgs(declaringType))
            {
                return ResolveMethodOnGenericBuilderInstantiation(declaringType, token);
            }

            return FindMethodOnRuntimeType(declaringType, token);
        }

        private MethodBase? ResolveMemberRefConstructor(Type declaringType, MethodToken token)
        {
            if (declaringType is TypeBuilder constructorTypeBuilder)
            {
                if (constructorTypeBuilder.FullName != null &&
                    _constructorBuilders.TryGetValue(constructorTypeBuilder.FullName, out var defaultConstructor))
                {
                    return defaultConstructor;
                }
                return null;
            }

            if (declaringType.IsGenericType && EmitContext.HasTypeBuilderArgs(declaringType))
            {
                var genericDefinition = declaringType.GetGenericTypeDefinition();
                var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);
                foreach (var baseConstructor in genericDefinition.GetConstructors(
                    AllConstructorFlags))
                {
                    if (MatchesParameterTypes(baseConstructor, resolvedParamTypes))
                    {
                        return TypeBuilder.GetConstructor(declaringType, baseConstructor);
                    }
                }
                foreach (var baseConstructor in genericDefinition.GetConstructors(
                    AllConstructorFlags))
                {
                    if (baseConstructor.GetParameters().Length == token.ParameterTypes.Length)
                    {
                        return TypeBuilder.GetConstructor(declaringType, baseConstructor);
                    }
                }
                return null;
            }

            try
            {
                var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);
                foreach (var constructor in declaringType.GetConstructors(
                    AllConstructorFlags))
                {
                    if (MatchesParameterTypes(constructor, resolvedParamTypes))
                    {
                        return constructor;
                    }
                }
            }
            catch (NotSupportedException)
            {
                if (declaringType.IsGenericType)
                {
                    var genericDefinition = declaringType.GetGenericTypeDefinition();
                    foreach (var baseConstructor in genericDefinition.GetConstructors(
                        AllConstructorFlags))
                    {
                        if (baseConstructor.GetParameters().Length == token.ParameterTypes.Length)
                        {
                            return TypeBuilder.GetConstructor(declaringType, baseConstructor);
                        }
                    }
                }
            }

            return null;
        }

        private MethodBase? ResolveMethodOnTypeBuilderByName(TypeBuilder typeBuilder, MethodToken token)
        {
            var declaringTypeName = typeBuilder.FullName ?? GetTypeNameFromToken(token.DeclaringType!);
            var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);
            var methodBuilder = FindMethodBuilderBySignature(declaringTypeName, token.MethodName, resolvedParamTypes);
            if (methodBuilder != null)
            {
                return methodBuilder;
            }

            if (token.MethodName == ".ctor")
            {
                if (typeBuilder.FullName != null &&
                    _constructorBuilders.TryGetValue(typeBuilder.FullName, out var constructorBuilder))
                {
                    return constructorBuilder;
                }
            }

            return null;
        }

        private MethodBase? ResolveMethodOnGenericBuilderInstantiation(Type declaringType, MethodToken token)
        {
            var genericDefinition = declaringType.GetGenericTypeDefinition();
            var typeArguments = declaringType.GetGenericArguments();
            Type[] genericParams;
            try
            {
                genericParams = genericDefinition.GetGenericArguments();
            }
            catch
            {
                genericParams = Type.EmptyTypes;
            }

            var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);

            // First pass: exact match using MatchesParameterTypes (works when
            // the resolved params happen to match the generic definition params directly).
            foreach (var baseMethod in genericDefinition.GetMethods(AllMethodFlags))
            {
                if (baseMethod.Name == token.MethodName && MatchesParameterTypes(baseMethod, resolvedParamTypes))
                {
                    return TypeBuilder.GetMethod(declaringType, baseMethod);
                }
            }

            // Second pass: substitute the generic type parameters (T → concrete type)
            // in the method's parameter types and then compare. This is needed because
            // the generic definition's parameters use T, T[], Slice<T>, etc., but the
            // serialized token has concrete types like Ptr<Regexp>, Ptr<Regexp>[], etc.
            foreach (var baseMethod in genericDefinition.GetMethods(AllMethodFlags))
            {
                if (baseMethod.Name != token.MethodName)
                {
                    continue;
                }
                var methodParams = baseMethod.GetParameters();
                if (methodParams.Length != resolvedParamTypes.Length)
                {
                    continue;
                }
                bool allMatch = true;
                for (int index = 0; index < methodParams.Length; index++)
                {
                    var substitutedType = SubstituteGenericParameters(
                        methodParams[index].ParameterType, genericParams, typeArguments);
                    if (substitutedType != resolvedParamTypes[index]
                        && NgoWriter.GetTypeNameStatic(substitutedType) != NgoWriter.GetTypeNameStatic(resolvedParamTypes[index]))
                    {
                        allMatch = false;
                        break;
                    }
                }
                if (allMatch)
                {
                    return TypeBuilder.GetMethod(declaringType, baseMethod);
                }
            }

            // Third pass: match by name + parameter count only (last resort).
            foreach (var baseMethod in genericDefinition.GetMethods(AllMethodFlags))
            {
                if (baseMethod.Name == token.MethodName &&
                    baseMethod.GetParameters().Length == token.ParameterTypes.Length)
                {
                    return TypeBuilder.GetMethod(declaringType, baseMethod);
                }
            }

            if (genericDefinition is TypeBuilder genericTypeBuilder)
            {
                var prefix = genericTypeBuilder.FullName + ".";
                foreach (var (key, builder) in _methodBuilders)
                {
                    if (key.StartsWith(prefix) && key.Substring(prefix.Length) == token.MethodName)
                    {
                        return TypeBuilder.GetMethod(declaringType, builder);
                    }
                }
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

        private MethodBase? FindGenericMethodDefinition(MethodToken token)
        {
            if (token.DeclaringType == null)
            {
                return null;
            }
            var declaringType = ResolveTypeToken(token.DeclaringType);
            if (declaringType == null)
            {
                return null;
            }
            foreach (var method in declaringType.GetMethods(
                AllMethodFlags))
            {
                if (method.Name == token.MethodName && method.IsGenericMethodDefinition)
                {
                    return method;
                }
            }
            return null;
        }

        private MethodBase? FindMethodOnRuntimeType(Type declaringType, MethodToken token)
        {
            var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);

            foreach (var method in declaringType.GetMethods(
                AllMethodFlags))
            {
                if (method.Name != token.MethodName)
                {
                    continue;
                }
                if (method.IsGenericMethodDefinition)
                {
                    continue;
                }
                if (MatchesParameterTypes(method, resolvedParamTypes))
                {
                    return method;
                }
            }

            if (declaringType.IsInterface)
            {
                foreach (var parentInterface in declaringType.GetInterfaces())
                {
                    foreach (var method in parentInterface.GetMethods(
                        AllMethodFlags))
                    {
                        if (method.Name != token.MethodName)
                        {
                            continue;
                        }
                        if (method.IsGenericMethodDefinition)
                        {
                            continue;
                        }
                        if (MatchesParameterTypes(method, resolvedParamTypes))
                        {
                            return method;
                        }
                    }
                }
            }

            return null;
        }

        private Type[] ResolveMethodTokenParameterTypes(MethodToken token)
        {
            var resolvedTypes = new Type[token.ParameterTypes.Length];
            for (int index = 0; index < token.ParameterTypes.Length; index++)
            {
                resolvedTypes[index] = ResolveTypeToken(token.ParameterTypes[index]);
            }
            return resolvedTypes;
        }

        private static bool MatchesParameterTypes(MethodBase method, Type[] resolvedParamTypes)
        {
            var methodParameters = method.GetParameters();
            if (methodParameters.Length != resolvedParamTypes.Length)
            {
                return false;
            }
            for (int index = 0; index < methodParameters.Length; index++)
            {
                var candidateType = methodParameters[index].ParameterType;
                var expectedType = resolvedParamTypes[index];
                if (candidateType != expectedType
                    && NgoWriter.GetTypeNameStatic(candidateType) != NgoWriter.GetTypeNameStatic(expectedType))
                {
                    return false;
                }
            }
            return true;
        }

        private FieldInfo? ResolveFieldToken(FieldToken token)
        {
            switch (token.Kind)
            {
                case FieldTokenKind.FieldDef:
                {
                    var declaringTypeName = GetTypeNameFromToken(token.DeclaringType!);
                    return ResolveFieldDef(declaringTypeName, token.FieldName);
                }
                case FieldTokenKind.MemberRef:
                {
                    return ResolveMemberRefField(token);
                }
                default:
                {
                    throw new InvalidOperationException(
                        $"ILLinker: unknown FieldToken kind {token.Kind}");
                }
            }
        }

        private FieldInfo? ResolveFieldDef(string declaringTypeName, string fieldName)
        {
            var dotKey = declaringTypeName + "." + fieldName;
            if (_fieldBuilders.TryGetValue(dotKey, out var fieldBuilder))
            {
                return fieldBuilder;
            }

            var colonKey = declaringTypeName + "::" + fieldName;
            if (_fieldBuilders.TryGetValue(colonKey, out fieldBuilder))
            {
                return fieldBuilder;
            }

            foreach (var (key, builder) in _fieldBuilders)
            {
                if (key.EndsWith("." + fieldName))
                {
                    var keyTypeName = key.Substring(0, key.Length - fieldName.Length - 1);
                    if (keyTypeName == declaringTypeName || keyTypeName.EndsWith("." + declaringTypeName))
                    {
                        return builder;
                    }
                }
            }

            // Fallback: the type was compiled from source. Find the field
            // via the StructFields dictionary which tracks live-emitted fields.
            foreach (var kvp in _emitContext.StructFields)
            {
                if (kvp.Key.Name == fieldName)
                {
                    var fieldInfo = kvp.Value.AsFieldInfo();
                    var declaringName = fieldInfo.DeclaringType?.Name;
                    if (declaringName == declaringTypeName
                        || (declaringName != null && declaringTypeName.EndsWith("." + declaringName)))
                    {
                        return fieldInfo;
                    }
                }
            }

            return null;
        }

        private FieldInfo? ResolveMemberRefField(FieldToken token)
        {
            var declaringType = ResolveTypeToken(token.DeclaringType!);

            if (declaringType is TypeBuilder)
            {
                var prefix = declaringType.FullName + ".";
                foreach (var (key, builder) in _fieldBuilders)
                {
                    if (key.StartsWith(prefix) && key.Substring(prefix.Length) == token.FieldName)
                    {
                        return builder;
                    }
                }
                return null;
            }

            try
            {
                return declaringType.GetField(token.FieldName,
                    AllMethodFlags);
            }
            catch (NotSupportedException)
            {
                if (declaringType.IsGenericType)
                {
                    var genericDefinition = declaringType.GetGenericTypeDefinition();
                    var baseField = genericDefinition.GetField(token.FieldName,
                        AllMethodFlags);
                    if (baseField != null)
                    {
                        return TypeBuilder.GetField(declaringType, baseField);
                    }
                }
                return null;
            }
        }

        // =====================================================================
        // Branch scanning and label management
        // =====================================================================

        private static void PreScanBranchTargets(byte[] il, Dictionary<int, Label> labels, ILGenerator ilGenerator)
        {
            int position = 0;
            while (position < il.Length)
            {
                byte firstByte = il[position++];
                OpCode opCode;
                if (firstByte == TwoByteOpcodePrefix && position < il.Length)
                {
                    byte secondByte = il[position++];
                    opCode = ILSerializer.GetTwoByteOpCode(secondByte);
                }
                else
                {
                    opCode = ILSerializer.GetOpCode(firstByte);
                }

                switch (opCode.OperandType)
                {
                    case OperandType.ShortInlineBrTarget:
                    {
                        int branchOffset = (sbyte)il[position++];
                        int branchTarget = position + branchOffset;
                        if (!labels.ContainsKey(branchTarget))
                        {
                            labels[branchTarget] = ilGenerator.DefineLabel();
                        }
                        break;
                    }
                    case OperandType.InlineBrTarget:
                    {
                        int branchOffset = BitConverter.ToInt32(il, position);
                        position += 4;
                        int branchTarget = position + branchOffset;
                        if (!labels.ContainsKey(branchTarget))
                        {
                            labels[branchTarget] = ilGenerator.DefineLabel();
                        }
                        break;
                    }
                    case OperandType.InlineSwitch:
                    {
                        int caseCount = BitConverter.ToInt32(il, position);
                        position += 4;
                        int switchBaseOffset = position + caseCount * 4;
                        for (int caseIndex = 0; caseIndex < caseCount; caseIndex++)
                        {
                            int caseTarget = switchBaseOffset + BitConverter.ToInt32(il, position);
                            position += 4;
                            if (!labels.ContainsKey(caseTarget))
                            {
                                labels[caseTarget] = ilGenerator.DefineLabel();
                            }
                        }
                        break;
                    }
                    case OperandType.InlineNone:
                    {
                        break;
                    }
                    case OperandType.ShortInlineVar:
                    case OperandType.ShortInlineI:
                    {
                        position += 1;
                        break;
                    }
                    case OperandType.InlineVar:
                    {
                        position += 2;
                        break;
                    }
                    case OperandType.InlineI:
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                    case OperandType.InlineType:
                    case OperandType.InlineString:
                    case OperandType.InlineTok:
                    case OperandType.InlineSig:
                    case OperandType.ShortInlineR:
                    {
                        position += 4;
                        break;
                    }
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                    {
                        position += 8;
                        break;
                    }
                }
            }
        }

        private static Label GetOrCreateLabel(Dictionary<int, Label> labels, int target, ILGenerator ilGenerator)
        {
            if (!labels.TryGetValue(target, out var label))
            {
                label = ilGenerator.DefineLabel();
                labels[target] = label;
            }
            return label;
        }

        // =====================================================================
        // Topological sort
        // =====================================================================

        private static List<string> TopologicalSortByDependencies(
            IEnumerable<string> typeNames, Dictionary<string, HashSet<string>> dependencies)
        {
            var allNames = new List<string>(typeNames);
            var sorted = new List<string>();
            var completed = new HashSet<string>();
            int previousCount = -1;

            while (sorted.Count < allNames.Count && sorted.Count != previousCount)
            {
                previousCount = sorted.Count;
                foreach (var name in allNames)
                {
                    if (completed.Contains(name))
                    {
                        continue;
                    }
                    bool allDependenciesReady = true;
                    if (dependencies.TryGetValue(name, out var deps))
                    {
                        foreach (var dependency in deps)
                        {
                            if (!completed.Contains(dependency))
                            {
                                allDependenciesReady = false;
                                break;
                            }
                        }
                    }
                    if (allDependenciesReady)
                    {
                        sorted.Add(name);
                        completed.Add(name);
                    }
                }
            }

            // Any names still not completed are part of a dependency cycle.
            // Log a diagnostic and append them in an arbitrary order so linking can proceed
            // (it will likely fail at CreateType, but with a clearer exception site).
            var cycleMembers = new List<string>();
            foreach (var name in allNames)
            {
                if (!completed.Contains(name))
                {
                    cycleMembers.Add(name);
                }
            }

            if (cycleMembers.Count > 0)
            {
                // Build a cycle description to aid debugging.
                var cycleDesc = string.Join(" -> ", cycleMembers);
                System.Diagnostics.Debug.WriteLine(
                    $"ILLinker: circular type dependency detected among: {cycleDesc}. " +
                    "These types will be emitted in an arbitrary order and may fail at CreateType.");
                sorted.AddRange(cycleMembers);
            }

            return sorted;
        }

        // =====================================================================
        // Inner types
        // =====================================================================

        private sealed class MethodBodyData
        {
            public int MaxStack;
            public string[] LocalTypes = Array.Empty<string>();
            public byte[] ILBytes = Array.Empty<byte>();
            public List<ILTokenEntry> TokenEntries = new();
            public List<ExceptionHandlerData> ExceptionHandlers = new();
        }

        private struct ExceptionHandlerData
        {
            public ExceptionRegionKind Kind;
            public int TryOffset;
            public int TryLength;
            public int HandlerOffset;
            public int HandlerLength;
            public int FilterOffset;
            public string CatchTypeName;
        }
    }
}
