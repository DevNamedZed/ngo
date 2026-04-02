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
        private readonly Dictionary<string, Type> _genericParamTypes;
        private readonly Dictionary<string, FieldBuilder> _fieldBuilders;
        private readonly Dictionary<string, MethodBuilder> _methodBuilders;
        private readonly Dictionary<string, int> _methodILIndices;
        private readonly Dictionary<string, Dictionary<string, Type>> _methodGenericParams;
        private readonly Dictionary<string, (string[] genericParamNames, string[] paramTypeNames)> _methodGenericInfo;
        private readonly Dictionary<string, ConstructorBuilder> _constructorBuilders;
        private readonly List<DeserializedTypeInfo> _typeInfos;
        private readonly HashSet<string> _currentArchiveTypes;

        private readonly List<(string fullTypeName, TypeBuilder typeBuilder,
            List<(string name, FieldAttributes attributes, string typeName)> fields,
            List<SerializedMethodInfo> methods, InterfaceMethodMapping[] interfaceMappings)> _typeRawData;

        private readonly List<string> _deferredClassTypes;

        private readonly Assembly _runtimeAssembly;

        private Type[] _currentMethodGenericParameters;
        private Type[] _currentTypeGenericParameters;
        private string _currentReplayMethodKey = "";

        public ILLinker(PackageSymbol package, EmitContext emitContext)
        {
            _package = package;
            _emitContext = emitContext;
            _typeBuilders = new Dictionary<string, TypeBuilder>(emitContext.LinkedTypes);
            _genericParamTypes = new Dictionary<string, Type>();
            _fieldBuilders = new Dictionary<string, FieldBuilder>(emitContext.LinkedFields);
            _methodBuilders = new Dictionary<string, MethodBuilder>(emitContext.LinkedMethods);
            _methodILIndices = new Dictionary<string, int>();
            _methodGenericParams = new Dictionary<string, Dictionary<string, Type>>();
            _methodGenericInfo = new Dictionary<string, (string[] genericParamNames, string[] paramTypeNames)>();
            _constructorBuilders = new Dictionary<string, ConstructorBuilder>();
            _typeInfos = new List<DeserializedTypeInfo>();
            _currentArchiveTypes = new HashSet<string>();
            _typeRawData = new List<(string, TypeBuilder,
                List<(string, FieldAttributes, string)>,
                List<SerializedMethodInfo>, InterfaceMethodMapping[])>();
            _deferredClassTypes = new List<string>();
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
                        var linkAttributes = typeAttributes & ~TypeAttributes.SequentialLayout;
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
                    for (int genericIndex = 0; genericIndex < typeGenericParamCount; genericIndex++)
                    {
                        _genericParamTypes[typeGenericParamNames[genericIndex]] = typeGenericParams[genericIndex];
                    }
                }

                int fieldCount = metaReader.ReadInt32();
                var fields = new List<(string name, FieldAttributes attributes, string typeName)>(fieldCount);
                for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                {
                    fields.Add((metaReader.ReadString(), (FieldAttributes)metaReader.ReadInt32(), metaReader.ReadString()));
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

                _typeRawData.Add((fullTypeName, typeBuilder, fields, methodInfos, interfaceMappings));
                _typeInfos.Add(new DeserializedTypeInfo(fullTypeName, typeBuilder, methodCount, methodInfos, interfaceMappings));
            }

            foreach (var typeInfo in _typeInfos)
            {
                _currentArchiveTypes.Add(typeInfo.FullTypeName);
            }
        }

        private void DefineFields()
        {
            foreach (var (fullTypeName, typeBuilder, fields, methods, interfaceMappings) in _typeRawData)
            {
                int blankFieldIndex = 0;
                foreach (var (fieldName, fieldAttributes, fieldTypeName) in fields)
                {
                    Type fieldType = ILSerializer.ResolveType(fieldTypeName, _typeBuilders, _genericParamTypes);

                    var actualFieldName = fieldName;
                    if (fieldName == "_")
                    {
                        actualFieldName = $"_pad{blankFieldIndex++}";
                    }

                    var fieldBuilder = typeBuilder.DefineField(actualFieldName, fieldType, fieldAttributes);
                    _fieldBuilders[fullTypeName + "." + actualFieldName] = fieldBuilder;
                    _emitContext.LinkedFields[fullTypeName + "." + actualFieldName] = fieldBuilder;
                    if (actualFieldName != fieldName)
                    {
                        _fieldBuilders[fullTypeName + "." + fieldName] = fieldBuilder;
                    }

                    foreach (var (_, symbol) in _package.Exports)
                    {
                        if (symbol is StructTypeSymbol structSymbol && structSymbol.Name == typeBuilder.Name)
                        {
                            foreach (var fieldSymbol in structSymbol.Fields)
                            {
                                if (fieldSymbol.Name == fieldName)
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
                foreach (var methodInfo in typeInfo.Methods)
                {
                    MethodBuilder methodBuilder;
                    if (methodInfo.GenericParamNames.Length > 0)
                    {
                        methodBuilder = typeInfo.TypeBuilder.DefineMethod(methodInfo.MethodName, methodInfo.Attributes);
                        var genericParams = methodBuilder.DefineGenericParameters(methodInfo.GenericParamNames);
                        var genericParamMap = new Dictionary<string, Type>();
                        for (int genericIndex = 0; genericIndex < genericParams.Length; genericIndex++)
                        {
                            genericParamMap[methodInfo.GenericParamNames[genericIndex]] = genericParams[genericIndex];
                        }

                        var returnType = ILSerializer.ResolveTypeWithGenericParams(methodInfo.ReturnTypeName, _typeBuilders, genericParamMap);
                        var paramTypes = new Type[methodInfo.ParamTypeNames.Length];
                        for (int paramIndex = 0; paramIndex < methodInfo.ParamTypeNames.Length; paramIndex++)
                        {
                            paramTypes[paramIndex] = ILSerializer.ResolveTypeWithGenericParams(methodInfo.ParamTypeNames[paramIndex], _typeBuilders, genericParamMap);
                        }
                        methodBuilder.SetReturnType(returnType);
                        methodBuilder.SetParameters(paramTypes);

                        var methodKey = typeInfo.FullTypeName + "." + methodInfo.MethodName;
                        _methodGenericParams[methodKey] = genericParamMap;
                        _methodGenericInfo[methodKey] = (methodInfo.GenericParamNames, methodInfo.ParamTypeNames);
                        _emitContext.MethodGenericInfo[methodKey] = (methodInfo.GenericParamNames, methodInfo.ParamTypeNames);
                    }
                    else
                    {
                        Type returnType;
                        try
                        {
                            returnType = ILSerializer.ResolveType(methodInfo.ReturnTypeName, _typeBuilders, _genericParamTypes);
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
                                paramTypes[paramIndex] = ILSerializer.ResolveType(methodInfo.ParamTypeNames[paramIndex], _typeBuilders, _genericParamTypes);
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

                    var fullMethodKey = typeInfo.FullTypeName + "." + methodInfo.MethodName;
                    _methodBuilders[fullMethodKey] = methodBuilder;

                    if (methodInfo.BodyIndex >= 0)
                    {
                        _methodILIndices[fullMethodKey] = methodInfo.BodyIndex;
                    }

                    _emitContext.LinkedMethods[fullMethodKey] = methodBuilder;

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
            foreach (var (fullTypeName, _, fields, _, _) in _typeRawData)
            {
                if (!valueTypesToCreate.ContainsKey(fullTypeName))
                {
                    continue;
                }
                var dependencies = new HashSet<string>();
                foreach (var (_, _, fieldTypeName) in fields)
                {
                    if (valueTypesToCreate.ContainsKey(fieldTypeName))
                    {
                        dependencies.Add(fieldTypeName);
                    }
                    else
                    {
                        foreach (var (valueTypeName, _) in valueTypesToCreate)
                        {
                            if (fieldTypeName.Contains(valueTypeName) && valueTypeName != fullTypeName)
                            {
                                dependencies.Add(valueTypeName);
                            }
                        }
                    }
                }
                fieldDependencies[fullTypeName] = dependencies;
            }

            var created = new HashSet<string>();
            var sortedValueTypes = new List<string>();
            int previousCount = -1;
            while (sortedValueTypes.Count < valueTypesToCreate.Count && sortedValueTypes.Count != previousCount)
            {
                previousCount = sortedValueTypes.Count;
                foreach (var (name, _) in valueTypesToCreate)
                {
                    if (created.Contains(name))
                    {
                        continue;
                    }
                    bool allDependenciesReady = true;
                    if (fieldDependencies.TryGetValue(name, out var dependencies))
                    {
                        foreach (var dependency in dependencies)
                        {
                            if (!created.Contains(dependency))
                            {
                                allDependenciesReady = false;
                                break;
                            }
                        }
                    }
                    if (allDependenciesReady)
                    {
                        sortedValueTypes.Add(name);
                        created.Add(name);
                    }
                }
            }
            foreach (var (name, _) in valueTypesToCreate)
            {
                if (!created.Contains(name))
                {
                    sortedValueTypes.Add(name);
                }
            }

            foreach (var fullName in sortedValueTypes)
            {
                var typeBuilder = valueTypesToCreate[fullName];
                try
                {
                    var runtimeType = typeBuilder.CreateType()!;
                    RegisterLinkedType(runtimeType, typeBuilder);
                }
                catch (TypeLoadException)
                {
                    _deferredClassTypes.Add(fullName);
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
                        var bodyKey = typeInfo.FullTypeName + "." + methodMapping.BodyMethodName;
                        if (!_methodBuilders.TryGetValue(bodyKey, out var bodyMethod))
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

                var combinedGenericParams = new Dictionary<string, Type>(_genericParamTypes);
                if (_methodGenericParams.TryGetValue(methodKey, out var methodGenericParamMap))
                {
                    foreach (var (name, genericParamType) in methodGenericParamMap)
                    {
                        combinedGenericParams[name] = genericParamType;
                    }
                }

                if (_methodBuilders.TryGetValue(methodKey, out var methodBuilder))
                {
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
                foreach (var (typeName, _, fields, _, _) in _typeRawData)
                {
                    if (typeName != fullName)
                    {
                        continue;
                    }
                    foreach (var (_, _, fieldTypeName) in fields)
                    {
                        if (_deferredClassTypes.Contains(fieldTypeName) && fieldTypeName != fullName)
                        {
                            dependencies.Add(fieldTypeName);
                        }
                    }
                }
                classTypeDependencies[fullName] = dependencies;
            }

            var classCreated = new HashSet<string>();
            var sortedClassTypes = new List<string>();
            int previousCount = -1;
            while (sortedClassTypes.Count < _deferredClassTypes.Count && sortedClassTypes.Count != previousCount)
            {
                previousCount = sortedClassTypes.Count;
                foreach (var fullName in _deferredClassTypes)
                {
                    if (classCreated.Contains(fullName))
                    {
                        continue;
                    }
                    bool allDependenciesReady = true;
                    if (classTypeDependencies.TryGetValue(fullName, out var dependencies))
                    {
                        foreach (var dependency in dependencies)
                        {
                            if (!classCreated.Contains(dependency))
                            {
                                allDependenciesReady = false;
                                break;
                            }
                        }
                    }
                    if (allDependenciesReady)
                    {
                        sortedClassTypes.Add(fullName);
                        classCreated.Add(fullName);
                    }
                }
            }
            foreach (var fullName in _deferredClassTypes)
            {
                if (!classCreated.Contains(fullName))
                {
                    sortedClassTypes.Add(fullName);
                }
            }

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
                metaReader.ReadString();
                metaReader.ReadInt32();
                metaReader.ReadString();
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

            var exceptionEvents = new Dictionary<int, List<ExceptionEventInfo>>();
            var skipOffsets = new HashSet<int>();

            int position = 0;
            while (position < il.Length)
            {
                if (exceptionEvents.TryGetValue(position, out var events))
                {
                    foreach (var exceptionEvent in events)
                    {
                        switch (exceptionEvent.EventKind)
                        {
                            case ExceptionEventKind.EndException:
                                ilGenerator.EndExceptionBlock();
                                break;
                            case ExceptionEventKind.BeginTry:
                                ilGenerator.BeginExceptionBlock();
                                break;
                            case ExceptionEventKind.BeginCatch:
                                ilGenerator.BeginCatchBlock(exceptionEvent.CatchType!);
                                break;
                        }
                    }
                }

                if (labels.TryGetValue(position, out var targetLabel))
                {
                    ilGenerator.MarkLabel(targetLabel);
                }

                if (skipOffsets.Contains(position))
                {
                    byte skipOpcode = il[position];
                    position += 1 + ILSerializer.GetOperandSize(skipOpcode);
                    continue;
                }

                byte opcode = il[position++];

                if (opcode == 0xFE && position < il.Length)
                {
                    byte secondByte = il[position++];
                    ReplayTwoByteOpcode(secondByte, il, ref position, ilGenerator, tokenMap, labels, genericParams);
                    continue;
                }

                if (ILSerializer.HasInlineToken(opcode) && position + 4 <= il.Length)
                {
                    int tokenOffset = position;
                    position += 4;

                    if (opcode == OpCodes.Ldstr.Value)
                    {
                        if (tokenMap.TryGetValue(tokenOffset, out var stringToken) && stringToken.Kind == ILTokenKind.String)
                        {
                            ilGenerator.Emit(OpCodes.Ldstr, stringToken.StringValue!);
                        }
                        else
                        {
                            ilGenerator.Emit(OpCodes.Ldnull);
                        }
                        continue;
                    }

                    var resolvedOpCode = ILSerializer.GetOpCode(opcode);
                    if (tokenMap.TryGetValue(tokenOffset, out var token))
                    {
                        EmitTokenOpcode(ilGenerator, resolvedOpCode, token, genericParams);
                    }
                    continue;
                }

                if (ILSerializer.IsShortBranch(opcode))
                {
                    var offset = (sbyte)il[position++];
                    var target = position + offset;
                    var branchLabel = GetOrCreateLabel(labels, target, ilGenerator);
                    ilGenerator.Emit(ILSerializer.GetOpCode(opcode), branchLabel);
                    continue;
                }

                if (ILSerializer.IsLongBranch(opcode))
                {
                    var offset = BitConverter.ToInt32(il, position);
                    position += 4;
                    var target = position + offset;
                    var branchLabel = GetOrCreateLabel(labels, target, ilGenerator);
                    ilGenerator.Emit(ILSerializer.GetOpCode(opcode), branchLabel);
                    continue;
                }

                if (opcode == OpCodes.Switch.Value)
                {
                    int count = BitConverter.ToInt32(il, position);
                    position += 4;
                    int baseOffset = position + count * 4;
                    var switchLabels = new Label[count];
                    for (int switchIndex = 0; switchIndex < count; switchIndex++)
                    {
                        int target = baseOffset + BitConverter.ToInt32(il, position);
                        position += 4;
                        switchLabels[switchIndex] = GetOrCreateLabel(labels, target, ilGenerator);
                    }
                    ilGenerator.Emit(OpCodes.Switch, switchLabels);
                    continue;
                }

                if (opcode == OpCodes.Ldarg_S.Value || opcode == OpCodes.Ldarga_S.Value || opcode == OpCodes.Starg_S.Value
                    || opcode == OpCodes.Ldloc_S.Value || opcode == OpCodes.Ldloca_S.Value || opcode == OpCodes.Stloc_S.Value)
                {
                    ilGenerator.Emit(ILSerializer.GetOpCode(opcode), il[position++]);
                    continue;
                }

                if (opcode == OpCodes.Ldc_I4_S.Value)
                {
                    ilGenerator.Emit(OpCodes.Ldc_I4_S, (sbyte)il[position++]);
                    continue;
                }

                if (opcode == OpCodes.Ldc_I4.Value)
                {
                    ilGenerator.Emit(OpCodes.Ldc_I4, BitConverter.ToInt32(il, position));
                    position += 4;
                    continue;
                }

                if (opcode == OpCodes.Ldc_I8.Value)
                {
                    ilGenerator.Emit(OpCodes.Ldc_I8, BitConverter.ToInt64(il, position));
                    position += 8;
                    continue;
                }

                if (opcode == OpCodes.Ldc_R4.Value)
                {
                    ilGenerator.Emit(OpCodes.Ldc_R4, BitConverter.ToSingle(il, position));
                    position += 4;
                    continue;
                }

                if (opcode == OpCodes.Ldc_R8.Value)
                {
                    ilGenerator.Emit(OpCodes.Ldc_R8, BitConverter.ToDouble(il, position));
                    position += 8;
                    continue;
                }

                if (opcode == OpCodes.Leave_S.Value)
                {
                    var offset = (sbyte)il[position++];
                    var target = position + offset;
                    var branchLabel = GetOrCreateLabel(labels, target, ilGenerator);
                    ilGenerator.Emit(OpCodes.Leave_S, branchLabel);
                    continue;
                }

                if (opcode == OpCodes.Leave.Value)
                {
                    var offset = BitConverter.ToInt32(il, position);
                    position += 4;
                    var target = position + offset;
                    var branchLabel = GetOrCreateLabel(labels, target, ilGenerator);
                    ilGenerator.Emit(OpCodes.Leave, branchLabel);
                    continue;
                }

                ilGenerator.Emit(ILSerializer.GetOpCode(opcode));
            }

            if (exceptionEvents.TryGetValue(position, out var endEvents))
            {
                foreach (var exceptionEvent in endEvents)
                {
                    switch (exceptionEvent.EventKind)
                    {
                        case ExceptionEventKind.EndException:
                            ilGenerator.EndExceptionBlock();
                            break;
                        case ExceptionEventKind.BeginTry:
                            ilGenerator.BeginExceptionBlock();
                            break;
                        case ExceptionEventKind.BeginCatch:
                            ilGenerator.BeginCatchBlock(exceptionEvent.CatchType!);
                            break;
                    }
                }
            }
        }

        private enum ExceptionEventKind
        {
            EndException,
            BeginTry,
            BeginCatch,
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
                    Type? catchType = null;
                    if (handler.Kind == ExceptionRegionKind.Catch && !string.IsNullOrEmpty(handler.CatchTypeName))
                    {
                        catchType = ILSerializer.ResolveType(handler.CatchTypeName, _typeBuilders, genericParams);
                    }

                    AddExceptionEvent(events, handler.HandlerOffset, new ExceptionEventInfo
                    {
                        EventKind = ExceptionEventKind.BeginCatch,
                        CatchType = catchType ?? typeof(Exception),
                    });
                }

                var lastHandler = groupedHandlers[groupedHandlers.Count - 1];
                int endOffset = lastHandler.HandlerOffset + lastHandler.HandlerLength;
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

        private static HashSet<int> BuildExceptionSkipOffsets(List<ExceptionHandlerData> handlers, byte[] il)
        {
            var skipOffsets = new HashSet<int>();

            foreach (var handler in handlers)
            {
                int tryEnd = handler.TryOffset + handler.TryLength;
                int leaveOffset = FindLeaveInstructionBefore(il, tryEnd);
                if (leaveOffset >= 0)
                {
                    skipOffsets.Add(leaveOffset);
                }

                int handlerEnd = handler.HandlerOffset + handler.HandlerLength;
                if (handler.Kind == ExceptionRegionKind.Finally || handler.Kind == ExceptionRegionKind.Fault)
                {
                    if (handlerEnd >= 1 && il[handlerEnd - 1] == OpCodes.Endfinally.Value)
                    {
                        skipOffsets.Add(handlerEnd - 1);
                    }
                }
                else
                {
                    int handlerLeave = FindLeaveInstructionBefore(il, handlerEnd);
                    if (handlerLeave >= 0)
                    {
                        skipOffsets.Add(handlerLeave);
                    }
                }
            }

            return skipOffsets;
        }

        private static int FindLeaveInstructionBefore(byte[] il, int boundaryOffset)
        {
            if (boundaryOffset >= 2 && il[boundaryOffset - 2] == OpCodes.Leave_S.Value)
            {
                return boundaryOffset - 2;
            }
            if (boundaryOffset >= 5 && il[boundaryOffset - 5] == OpCodes.Leave.Value)
            {
                return boundaryOffset - 5;
            }
            return -1;
        }

        private void ReplayTwoByteOpcode(byte secondByte, byte[] il, ref int position, ILGenerator ilGenerator,
            Dictionary<int, ILTokenEntry> tokenMap,
            Dictionary<int, Label> labels,
            Dictionary<string, Type>? genericParams = null)
        {
            switch (secondByte)
            {
                case 0x15:
                case 0x16:
                case 0x1C:
                {
                    var resolvedOpCode = secondByte == 0x15 ? OpCodes.Initobj : secondByte == 0x16 ? OpCodes.Constrained : OpCodes.Sizeof;
                    if (position + 4 <= il.Length && tokenMap.TryGetValue(position, out var token))
                    {
                        var type = ResolveTokenAsType(token, genericParams);
                        ilGenerator.Emit(resolvedOpCode, type);
                    }
                    position += 4;
                    break;
                }
                case 0x06:
                case 0x07:
                {
                    var resolvedOpCode = secondByte == 0x06 ? OpCodes.Ldftn : OpCodes.Ldvirtftn;
                    if (position + 4 <= il.Length && tokenMap.TryGetValue(position, out var token))
                    {
                        var method = ResolveTokenAsMethod(token, genericParams);
                        if (method is MethodInfo methodInfo)
                        {
                            ilGenerator.Emit(resolvedOpCode, methodInfo);
                        }
                    }
                    position += 4;
                    break;
                }
                case 0x09: case 0x0A: case 0x0B:
                case 0x0C: case 0x0D: case 0x0E:
                {
                    ilGenerator.Emit(ILSerializer.GetTwoByteOpCode(secondByte), BitConverter.ToInt16(il, position));
                    position += 2;
                    break;
                }
                case 0x12:
                {
                    ilGenerator.Emit(OpCodes.Unaligned, il[position++]);
                    break;
                }
                default:
                {
                    ilGenerator.Emit(ILSerializer.GetTwoByteOpCode(secondByte));
                    break;
                }
            }
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
                        throw new InvalidOperationException(
                            $"ILLinker: unresolved method token '{token.MethodToken!.DeclaringType}::{token.MethodToken.MethodName}'");
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
                        throw new InvalidOperationException(
                            $"ILLinker: unresolved field token '{token.FieldToken!.DeclaringType}::{token.FieldToken.FieldName}'");
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

        private Type ResolveTokenAsType(ILTokenEntry token, Dictionary<string, Type>? genericParams)
        {
            if (token.Kind == ILTokenKind.Type)
            {
                return ResolveTypeToken(token.TypeToken!);
            }
            throw new InvalidOperationException(
                $"ILLinker: expected type token but got {token.Kind}");
        }

        private MethodBase? ResolveTokenAsMethod(ILTokenEntry token, Dictionary<string, Type>? genericParams)
        {
            if (token.Kind == ILTokenKind.Method)
            {
                return ResolveMethodToken(token.MethodToken!);
            }
            throw new InvalidOperationException(
                $"ILLinker: expected method token but got {token.Kind}");
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

            foreach (var candidateType in _runtimeAssembly.GetTypes())
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

            foreach (var candidateType in _runtimeAssembly.GetTypes())
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
                    return ResolveMethodDef(declaringTypeName, token.MethodName);
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

        private MethodBase? ResolveMethodDef(string declaringTypeName, string methodName)
        {
            var dotKey = declaringTypeName + "." + methodName;
            if (_methodBuilders.TryGetValue(dotKey, out var methodBuilder))
            {
                return methodBuilder;
            }

            if (methodName == ".ctor")
            {
                if (_constructorBuilders.TryGetValue(declaringTypeName, out var constructorBuilder))
                {
                    return constructorBuilder;
                }
                var constructorMethodKey = declaringTypeName + "..ctor";
                if (_methodBuilders.TryGetValue(constructorMethodKey, out var constructorMethodBuilder))
                {
                    return constructorMethodBuilder;
                }
            }

            foreach (var (key, builder) in _methodBuilders)
            {
                if (key.EndsWith("." + methodName))
                {
                    var keyTypeName = key.Substring(0, key.Length - methodName.Length - 1);
                    if (keyTypeName == declaringTypeName || keyTypeName.EndsWith("." + declaringTypeName))
                    {
                        return builder;
                    }
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
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (MatchesParameterTypes(baseConstructor, resolvedParamTypes))
                    {
                        return TypeBuilder.GetConstructor(declaringType, baseConstructor);
                    }
                }
                foreach (var baseConstructor in genericDefinition.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
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
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
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
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
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
            var dotKey = declaringTypeName + "." + token.MethodName;

            if (_methodBuilders.TryGetValue(dotKey, out var methodBuilder))
            {
                return methodBuilder;
            }

            foreach (var (key, builder) in _methodBuilders)
            {
                if (key.EndsWith("." + token.MethodName))
                {
                    var keyTypeName = key.Substring(0, key.Length - token.MethodName.Length - 1);
                    if (keyTypeName == declaringTypeName)
                    {
                        return builder;
                    }
                }
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

            var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);
            foreach (var baseMethod in genericDefinition.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (baseMethod.Name == token.MethodName && MatchesParameterTypes(baseMethod, resolvedParamTypes))
                {
                    return TypeBuilder.GetMethod(declaringType, baseMethod);
                }
            }

            foreach (var baseMethod in genericDefinition.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
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

        private MethodBase? FindMethodOnRuntimeType(Type declaringType, MethodToken token)
        {
            var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);

            foreach (var method in declaringType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
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
                if (methodParameters[index].ParameterType != resolvedParamTypes[index])
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
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            }
            catch (NotSupportedException)
            {
                if (declaringType.IsGenericType)
                {
                    var genericDefinition = declaringType.GetGenericTypeDefinition();
                    var baseField = genericDefinition.GetField(token.FieldName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
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
                byte opcode = il[position++];

                if (opcode == 0xFE && position < il.Length)
                {
                    byte secondByte = il[position++];
                    position += ILSerializer.GetTwoByteOperandSize(secondByte);
                    continue;
                }

                if (ILSerializer.HasInlineToken(opcode))
                {
                    position += 4;
                    continue;
                }

                if (ILSerializer.IsShortBranch(opcode))
                {
                    var offset = (sbyte)il[position++];
                    var target = position + offset;
                    if (!labels.ContainsKey(target))
                    {
                        labels[target] = ilGenerator.DefineLabel();
                    }
                    continue;
                }

                if (ILSerializer.IsLongBranch(opcode))
                {
                    var offset = BitConverter.ToInt32(il, position);
                    position += 4;
                    var target = position + offset;
                    if (!labels.ContainsKey(target))
                    {
                        labels[target] = ilGenerator.DefineLabel();
                    }
                    continue;
                }

                if (opcode == OpCodes.Switch.Value)
                {
                    int count = BitConverter.ToInt32(il, position);
                    position += 4;
                    int baseOffset = position + count * 4;
                    for (int switchIndex = 0; switchIndex < count; switchIndex++)
                    {
                        int target = baseOffset + BitConverter.ToInt32(il, position);
                        position += 4;
                        if (!labels.ContainsKey(target))
                        {
                            labels[target] = ilGenerator.DefineLabel();
                        }
                    }
                    continue;
                }

                position += ILSerializer.GetOperandSize(opcode);
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
