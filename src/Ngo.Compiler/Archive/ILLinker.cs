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
using Ngo.Runtime;
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

        // The linker's own type table (only types defined during archive linking), kept
        // separate from the emitter's EmitContext.Definitions to avoid source/linked
        // cross-contamination. (spec/PHASE-1-ILLINKER.md P1.1)
        private readonly Ngo.Compiler.Emit.DefinitionTable _definitions = new();
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
                TypeToken? baseTypeToken = string.IsNullOrEmpty(baseTypeName) ? null : TypeToken.Read(metaReader);

                int interfaceCount = metaReader.ReadInt32();
                var interfaceNames = new string[interfaceCount];
                for (int interfaceIndex = 0; interfaceIndex < interfaceCount; interfaceIndex++)
                {
                    interfaceNames[interfaceIndex] = metaReader.ReadString();
                }
                var interfaceTokens = new TypeToken[interfaceCount];
                for (int interfaceIndex = 0; interfaceIndex < interfaceCount; interfaceIndex++)
                {
                    interfaceTokens[interfaceIndex] = TypeToken.Read(metaReader);
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

                // Check if this type was already compiled from source or a prior archive.
                // Skip DefineType to avoid creating a conflicting partial type.
                string? originalShortName = null;

                // If a DIFFERENT package already linked this short name, qualify with the import path
                // to avoid a CLR name collision.
                if (_emitContext.LinkedTypes.ContainsKey(fullTypeName)
                    && _emitContext.LinkedTypePackages.TryGetValue(fullTypeName, out var existingPkg)
                    && existingPkg != _package.ImportPath)
                {
                    originalShortName = fullTypeName;
                    fullTypeName = _package.ImportPath.Replace('/', '.') + "." + fullTypeName;
                }

                // Reuse an already-linked builder for the FINAL name — covers a same-package re-link of
                // the short name AND a re-link of the qualified name (a dependency pulled in via more
                // than one path). Defining it again leaves an uncreated duplicate type. (spec/A4 §A4.3)
                if (_emitContext.LinkedTypes.TryGetValue(fullTypeName, out var existingLinkedType))
                {
                    _typeBuilders[fullTypeName] = existingLinkedType;
                    if (originalShortName != null)
                    {
                        _typeBuilders[originalShortName] = existingLinkedType;
                    }
                    _sourceCompiledTypes.Add(fullTypeName);

                    if (typeGenericParamCount > 0 && existingLinkedType.IsGenericTypeDefinition)
                    {
                        var existingGenericArgs = existingLinkedType.GetGenericArguments();
                        var genericParamMap = new Dictionary<string, Type>(typeGenericParamCount);
                        for (int genericIndex = 0; genericIndex < Math.Min(typeGenericParamCount, existingGenericArgs.Length); genericIndex++)
                        {
                            genericParamMap[typeGenericParamNames[genericIndex]] = existingGenericArgs[genericIndex];
                        }
                        _typeGenericParams[fullTypeName] = genericParamMap;
                    }
                    else
                    {
                        _typeGenericParams[fullTypeName] = new Dictionary<string, Type>();
                    }

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
                            resolvedInterfaces[interfaceIndex] = ResolveTypeToken(interfaceTokens[interfaceIndex]);
                        }
                    }

                    var liveModule = (LiveModuleBuilder)_emitContext.Module;
                    if (isStaticClass || isInterface)
                    {
                        typeBuilder = liveModule.DefineTypeTracked(fullTypeName, typeAttributes);
                    }
                    else
                    {
                        Type parent;
                        if (!string.IsNullOrEmpty(baseTypeName))
                        {
                            parent = ResolveTypeToken(baseTypeToken!);
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
                            typeBuilder = liveModule.DefineTypeTracked(fullTypeName, linkAttributes, parent, resolvedInterfaces);
                        }
                        else
                        {
                            typeBuilder = liveModule.DefineTypeTracked(fullTypeName, linkAttributes, parent);
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
                _emitContext.LinkedTypePackages[fullTypeName] = _package.ImportPath;
                _definitions.RegisterType(fullTypeName, new LiveTypeBuilder(typeBuilder));

                // For cross-package name collisions, also register under the
                // original short name so this archive's IL can resolve it
                if (originalShortName != null)
                {
                    _typeBuilders[originalShortName] = typeBuilder;
                }

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
                    var fieldTypeToken = TypeToken.Read(metaReader);
                    TypeToken? elementTypeToken = goArrayLength > 0 ? TypeToken.Read(metaReader) : null;
                    fields.Add(new SerializedFieldInfo(fieldName, fieldAttributes, fieldTypeName, goArrayLength, elementTypeName, fieldTypeToken, elementTypeToken));
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

                    var returnTypeToken = TypeToken.Read(metaReader);
                    var paramTypeTokens = new TypeToken[paramCount];
                    for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                    {
                        paramTypeTokens[paramIndex] = TypeToken.Read(metaReader);
                    }

                    var bodyIndex = metaReader.ReadInt32();
                    methodInfos.Add(new SerializedMethodInfo(methodName, methodAttributes, returnTypeName, paramTypeNames, bodyIndex, methodGenericParamNames, returnTypeToken, paramTypeTokens));
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

                // The declaring type's generic parameters in definition order, so index-based
                // GenericTypeParam tokens in each field type resolve through ResolveTypeToken.
                // A field has no method-level generic parameters.
                _currentMethodGenericParameters = Type.EmptyTypes;
                _currentTypeGenericParameters = typeInfo.TypeBuilder.IsGenericTypeDefinition
                    ? typeInfo.TypeBuilder.GetGenericArguments()
                    : Type.EmptyTypes;

                foreach (var field in typeInfo.Fields)
                {
                    Type fieldType;

                    if (field.GoArrayLength > 0)
                    {
                        // Go inline array [N]T: resolve the element type token (index-based, so a
                        // generic element like 'K' resolves correctly) and synthesize the inline array.
                        Type elementType;
                        try
                        {
                            elementType = ResolveTypeToken(field.ElementTypeToken!);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new InvalidOperationException(
                                $"LinkIL: failed to resolve inline-array element type '{field.ElementTypeName}' for " +
                                $"'{typeInfo.FullTypeName}.{field.Name}'", ex);
                        }
                        fieldType = _emitContext.Mapper.GetOrCreateInlineArrayType(elementType, field.GoArrayLength);
                        if (fieldType is System.Reflection.Emit.TypeBuilder inlineTypeBuilder)
                        {
                            _typeBuilders[inlineTypeBuilder.Name] = inlineTypeBuilder;
                        }
                    }
                    else
                    {
                        try
                        {
                            fieldType = ResolveTypeToken(field.FieldTypeToken);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new InvalidOperationException(
                                $"LinkIL: failed to resolve field type '{field.TypeName}' for " +
                                $"'{typeInfo.FullTypeName}.{field.Name}'", ex);
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

                // The declaring type's generic parameters in definition order, so the index-based
                // GenericTypeParam tokens in each signature resolve through ResolveTypeToken.
                _currentTypeGenericParameters = typeInfo.TypeBuilder.IsGenericTypeDefinition
                    ? typeInfo.TypeBuilder.GetGenericArguments()
                    : Type.EmptyTypes;

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

                        _currentMethodGenericParameters = genericParams;
                        var returnType = ResolveSignatureToken(methodInfo.ReturnType, typeInfo.FullTypeName, methodInfo, methodInfo.ReturnTypeName, "return type");
                        var paramTypes = ResolveParameterTokens(methodInfo, typeInfo.FullTypeName);
                        methodBuilder.SetReturnType(returnType);
                        methodBuilder.SetParameters(paramTypes);

                        var methodKey = BuildMethodKey(typeInfo.FullTypeName, methodInfo.MethodName, methodInfo.ParamTypeNames, methodInfo.GenericParamNames.Length);
                        _methodGenericParams[methodKey] = methodGenericParamMap;
                    }
                    else
                    {
                        _currentMethodGenericParameters = Type.EmptyTypes;
                        var returnType = ResolveSignatureToken(methodInfo.ReturnType, typeInfo.FullTypeName, methodInfo, methodInfo.ReturnTypeName, "return type");
                        var paramTypes = ResolveParameterTokens(methodInfo, typeInfo.FullTypeName);
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

        private Type[] ResolveParameterTokens(SerializedMethodInfo methodInfo, string declaringTypeName)
        {
            var paramTypes = new Type[methodInfo.ParamTypes.Length];
            for (int paramIndex = 0; paramIndex < paramTypes.Length; paramIndex++)
            {
                paramTypes[paramIndex] = ResolveSignatureToken(methodInfo.ParamTypes[paramIndex],
                    declaringTypeName, methodInfo, methodInfo.ParamTypeNames[paramIndex],
                    $"parameter {paramIndex} type");
            }
            return paramTypes;
        }

        private Type ResolveSignatureToken(TypeToken token, string declaringTypeName,
            SerializedMethodInfo methodInfo, string nameForDiagnostics, string role)
        {
            try
            {
                return ResolveTypeToken(token);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"LinkIL: failed to resolve {role} '{nameForDiagnostics}' for method " +
                    $"'{declaringTypeName}.{methodInfo.MethodName}' " +
                    $"(generic arity {methodInfo.GenericParamNames.Length})", ex);
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
                    && !(declName != null && declName.EndsWith("." + declaringTypeName))
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

                if (match != null && !ReferenceEquals(match, builder))
                {
                    return null;
                }

                match = builder;
            }

            return match;
        }

        private MethodBuilder? FindMethodBuilderByNameAndParamCount(string declaringTypeName, string methodName, int paramCount)
        {
            MethodBuilder? match = null;
            foreach (var (key, builder) in _methodBuilders)
            {
                if (builder.Name != methodName)
                {
                    continue;
                }
                var declName = builder.DeclaringType?.FullName ?? builder.DeclaringType?.Name;
                if (declName != declaringTypeName
                    && !(declName != null && declName.EndsWith("." + declaringTypeName))
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
                var keyParamCount = CountTopLevelParams(keyParams);
                if (keyParamCount != paramCount)
                {
                    continue;
                }
                if (match != null)
                {
                    return null;
                }
                match = builder;
            }

            if (match != null)
            {
                return match;
            }

            foreach (var (key, builder) in _emitContext.LinkedMethods)
            {
                if (builder.Name != methodName)
                {
                    continue;
                }
                var declName = builder.DeclaringType?.FullName ?? builder.DeclaringType?.Name;
                if (declName != declaringTypeName
                    && !(declName != null && declName.EndsWith("." + declaringTypeName))
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
                var keyParamCount = CountTopLevelParams(keyParams);
                if (keyParamCount != paramCount)
                {
                    continue;
                }
                if (match != null)
                {
                    return null;
                }
                match = builder;
            }

            return match;
        }

        private static int CountTopLevelParams(string paramString)
        {
            if (string.IsNullOrEmpty(paramString))
            {
                return 0;
            }
            int count = 1;
            int bracketDepth = 0;
            for (int index = 0; index < paramString.Length; index++)
            {
                char character = paramString[index];
                if (character == '[')
                {
                    bracketDepth++;
                }
                else if (character == ']')
                {
                    bracketDepth--;
                }
                else if (character == ',' && bracketDepth == 0)
                {
                    count++;
                }
            }
            return count;
        }

        private MethodBuilder? FindMethodBuilderByNameOnly(string declaringTypeName, string methodName)
        {
            MethodBuilder? match = null;
            foreach (var (key, builder) in _methodBuilders)
            {
                if (builder.Name != methodName)
                {
                    continue;
                }
                var declName = builder.DeclaringType?.FullName ?? builder.DeclaringType?.Name;
                if (declName != declaringTypeName
                    && !(declName != null && declName.EndsWith("." + declaringTypeName))
                    && !(declName != null && declaringTypeName.EndsWith("." + declName)))
                {
                    continue;
                }
                if (match != null && !ReferenceEquals(match, builder))
                {
                    return null;
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
                try
                {
                    var runtimeType = typeBuilder.CreateType()!;
                    RegisterLinkedType(runtimeType, typeBuilder);
                    FinalizeInlineArrayTypes();
                }
                catch (InvalidOperationException)
                {
                }
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
                        interfaceType = ResolveTypeToken(interfaceMapping.InterfaceTypeToken);
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
                bodyData.LocalTypes = new TypeToken[localCount];
                for (int localIndex = 0; localIndex < localCount; localIndex++)
                {
                    bodyData.LocalTypes[localIndex] = TypeToken.Read(codeReader);
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
                    var handlerData = new ExceptionHandlerData
                    {
                        Kind = (ExceptionRegionKind)codeReader.ReadInt32(),
                        TryOffset = codeReader.ReadInt32(),
                        TryLength = codeReader.ReadInt32(),
                        HandlerOffset = codeReader.ReadInt32(),
                        HandlerLength = codeReader.ReadInt32(),
                        FilterOffset = codeReader.ReadInt32(),
                        CatchTypeName = codeReader.ReadString(),
                    };
                    if (!string.IsNullOrEmpty(handlerData.CatchTypeName))
                    {
                        handlerData.CatchTypeToken = TypeToken.Read(codeReader);
                    }
                    bodyData.ExceptionHandlers.Add(handlerData);
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
                    catch (InvalidOperationException)
                    {
                        // Method body exceeds .NET's 64KB IL limit — skip this type
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
                    try
                    {
                        typeBuilder.CreateType();
                    }
                    catch (InvalidOperationException)
                    {
                    }
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
                int skipGoArrayLength = metaReader.ReadInt32();   // goArrayLength
                metaReader.ReadString();  // elemTypeName
                TypeToken.Read(metaReader);  // fieldTypeToken
                if (skipGoArrayLength > 0)
                {
                    TypeToken.Read(metaReader);  // elementTypeToken
                }
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

                // Structured signature tokens — must be consumed to keep the stream aligned
                // with the layout written by NgoModuleBuilder (one return token + one per param).
                TypeToken.Read(metaReader);
                for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                {
                    TypeToken.Read(metaReader);
                }

                metaReader.ReadInt32();
            }
            int interfaceImplCount = metaReader.ReadInt32();
            for (int implIndex = 0; implIndex < interfaceImplCount; implIndex++)
            {
                metaReader.ReadString();     // interface type name
                TypeToken.Read(metaReader);  // interface type token
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

        private void ReplayIL(MethodBuilder methodBuilder, byte[] il, TypeToken[] localTypes,
            List<ILTokenEntry> tokenEntries, List<ExceptionHandlerData> exceptionHandlers,
            Dictionary<string, Type>? genericParams = null)
        {
            var ilGenerator = methodBuilder.GetILGenerator();

            foreach (var localType in localTypes)
            {
                ilGenerator.DeclareLocal(ResolveTypeToken(localType));
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
                                catchType = ResolveTypeToken(handler.CatchTypeToken!);
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
                        if (_sourceCompiledTypes.Contains(token.TypeName)
                            && _emitContext.Module is Emit.Builder.LiveModuleBuilder liveMod)
                        {
                            try
                            {
                                var runtimeType = liveMod.Inner.GetType(token.TypeName);
                                if (runtimeType != null)
                                {
                                    return runtimeType;
                                }
                            }
                            catch (NotImplementedException)
                            {
                            }
                        }
                        return typeBuilder;
                    }

                    if (_emitContext.LinkedTypes.TryGetValue(token.TypeName, out var linkedType))
                    {
                        return linkedType;
                    }

                    var runtimeGoType = _emitContext.RuntimeCatalog.ResolveByGoTypeName(token.TypeName);
                    if (runtimeGoType != null)
                    {
                        return runtimeGoType;
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
                    // Fallback: closures sometimes serialize a generic parameter under TypeParam
                    // when the resolved parameter actually belongs to the enclosing method's
                    // generic signature. Retry against method generic parameters.
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
            // First: the DefinitionTable's two-lookup (package-qualified, then short) over the
            // types we defined — the prescribed resolution path. (spec/PHASE-1-ILLINKER.md P1.1)
            var fromDefinitions = _definitions.FindType(packageImportPath, typeName);
            if (fromDefinitions != null)
            {
                return fromDefinitions;
            }

            // typeName may already be fully qualified (e.g., "Ngo.Runtime.GoString")
            // or just a short name (e.g., "GoString"). Try both forms.
            if (_typeBuilders.TryGetValue(typeName, out var directMatch))
            {
                return directMatch;
            }

            var runtimeDirect = _emitContext.RuntimeCatalog.ResolveByClrFullName(typeName);
            if (runtimeDirect != null)
            {
                return runtimeDirect;
            }

            var fullName = packageImportPath.Replace("/", ".") + "." + typeName;

            if (_typeBuilders.TryGetValue(fullName, out var typeBuilder))
            {
                return typeBuilder;
            }

            if (_emitContext.LinkedTypes.TryGetValue(typeName, out var linkedType))
            {
                return linkedType;
            }
            if (_emitContext.LinkedTypes.TryGetValue(fullName, out linkedType))
            {
                return linkedType;
            }

            var runtimeType = _emitContext.RuntimeCatalog.ResolveByClrFullName(fullName);
            if (runtimeType != null)
            {
                return runtimeType;
            }

            var packageShortName = packageImportPath;
            var lastSlash = packageImportPath.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                packageShortName = packageImportPath.Substring(lastSlash + 1);
            }

            var byGoPackage = _emitContext.RuntimeCatalog.ResolveByGoPackageAndName(packageImportPath, typeName);
            if (byGoPackage != null)
            {
                return byGoPackage;
            }

            if (typeName == packageShortName)
            {
                var packageClass = _emitContext.RuntimeCatalog.ResolvePackageClass(packageImportPath);
                if (packageClass != null)
                {
                    return packageClass;
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

            if (_emitContext.Module is Emit.Builder.LiveModuleBuilder searchMod)
            {
                try
                {
                    var moduleType = searchMod.Inner.GetType(typeName);
                    if (moduleType != null)
                    {
                        return moduleType;
                    }
                }
                catch (NotImplementedException)
                {
                }
            }

            // Check InlineArray types (GoArray_ElementType_Length pattern)
            foreach (var (_, inlineArrayType) in _emitContext.InlineArrayTypes)
            {
                var inlineName = inlineArrayType is TypeBuilder inlineTb
                    ? inlineTb.Name
                    : inlineArrayType.Name;
                if (inlineName == typeName || inlineName == fullName)
                {
                    return inlineArrayType;
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
                PrimitiveTypeKind.String => typeof(GoString),
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

            var signatureMatch = FindMethodBuilderBySignature(declaringTypeName, token.MethodName, resolvedParamTypes);
            if (signatureMatch != null)
            {
                return signatureMatch;
            }

            if (token.MethodName == ".ctor")
            {
                if (_constructorBuilders.TryGetValue(declaringTypeName, out var constructorBuilder))
                {
                    return constructorBuilder;
                }
            }

            if (token.DeclaringType != null)
            {
                var declaringType = ResolveTypeToken(token.DeclaringType);
                if (declaringType is TypeBuilder typeBuilder)
                {
                    if (typeBuilder.IsCreated())
                    {
                        var runtimeResult = FindMethodOnRuntimeType(typeBuilder, token);
                        if (runtimeResult != null)
                        {
                            return runtimeResult;
                        }
                    }
                }
                else
                {
                    var runtimeResult = FindMethodOnRuntimeType(declaringType, token);
                    if (runtimeResult != null)
                    {
                        return runtimeResult;
                    }
                }

                // The resolved type didn't have the method — the TypeDef name may be ambiguous
                // (e.g. multiple "I__interface" wrapper types). Search methods on the
                // declaring type's implemented interfaces and also across all TypeBuilders
                // sharing the same short name.
                var typeName = token.DeclaringType.TypeName;

                if (declaringType is TypeBuilder declTypeBuilder && declTypeBuilder.IsCreated())
                {
                    try
                    {
                        foreach (var iface in declTypeBuilder.GetInterfaces())
                        {
                            if (iface is TypeBuilder ifaceTb && ifaceTb.IsCreated())
                            {
                                var ifaceResult = FindMethodOnRuntimeType(ifaceTb, token);
                                if (ifaceResult != null)
                                {
                                    return ifaceResult;
                                }
                            }
                            else if (!(iface is TypeBuilder))
                            {
                                var ifaceResult = FindMethodOnRuntimeType(iface, token);
                                if (ifaceResult != null)
                                {
                                    return ifaceResult;
                                }
                            }
                        }
                    }
                    catch (NotSupportedException)
                    {
                    }
                }

                foreach (var (key, candidateMethod) in _methodBuilders)
                {
                    if (candidateMethod.Name != token.MethodName)
                    {
                        continue;
                    }
                    var candidateDeclName = candidateMethod.DeclaringType?.FullName ?? candidateMethod.DeclaringType?.Name;
                    if (candidateDeclName == typeName && candidateMethod.DeclaringType != declaringType)
                    {
                        return candidateMethod;
                    }
                }

                var runtimeGoType = _emitContext.RuntimeCatalog.ResolveByGoTypeName(typeName);
                if (runtimeGoType != null)
                {
                    var goResult = FindMethodOnRuntimeType(runtimeGoType, token);
                    if (goResult != null)
                    {
                        return goResult;
                    }
                }
            }

            // Cross-package methods registered by a prior archive live in the shared
            // EmitContext.LinkedMethods (not this linker's _methodBuilders). FindMethodBuilderByNameAndParamCount
            // consults it by name + arity; FindMethodBuilderByNameOnly is the final unique-or-null fallback.
            var byNameAndParamCount = FindMethodBuilderByNameAndParamCount(declaringTypeName, token.MethodName, token.ParameterTypes.Length);
            if (byNameAndParamCount != null)
            {
                return byNameAndParamCount;
            }

            var byNameOnly = FindMethodBuilderByNameOnly(declaringTypeName, token.MethodName);
            if (byNameOnly != null)
            {
                return byNameOnly;
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
                var result = ResolveMethodOnTypeBuilderByName(memberRefTypeBuilder, token);
                if (result != null)
                {
                    return result;
                }
                return ResolveReceiverMethodOnRuntimeType(declaringType, token);
            }

            if (declaringType.IsGenericType && EmitContext.HasTypeBuilderArgs(declaringType))
            {
                return ResolveMethodOnGenericBuilderInstantiation(declaringType, token);
            }

            var directResult = FindMethodOnRuntimeType(declaringType, token);
            if (directResult != null)
            {
                return directResult;
            }
            return ResolveReceiverMethodOnRuntimeType(declaringType, token);
        }

        private MethodBase? ResolveReceiverMethodOnRuntimeType(Type moduleType, MethodToken token)
        {
            var underscoreIndex = token.MethodName.IndexOf('_');
            if (underscoreIndex <= 0 || underscoreIndex >= token.MethodName.Length - 1)
            {
                return null;
            }

            var receiverTypeName = token.MethodName.Substring(0, underscoreIndex);
            var methodName = token.MethodName.Substring(underscoreIndex + 1);

            var packageImportPath = token.DeclaringType?.PackageImportPath;

            var receiverType = FindReceiverType(moduleType, receiverTypeName, packageImportPath);
            if (receiverType == null)
            {
                return null;
            }

            foreach (var method in receiverType.GetMethods(AllMethodFlags))
            {
                if (method.Name == methodName)
                {
                    return method;
                }
            }

            return null;
        }

        private Type? FindReceiverType(Type moduleType, string receiverTypeName, string? packageImportPath)
        {
            var moduleNamespace = moduleType.Namespace;
            if (moduleNamespace != null)
            {
                var receiverType = _emitContext.RuntimeCatalog.ResolveByClrFullName(moduleNamespace + "." + receiverTypeName);
                if (receiverType != null)
                {
                    return receiverType;
                }

                var byShortName = _emitContext.RuntimeCatalog.ResolveByShortNameInNamespace(receiverTypeName, moduleNamespace);
                if (byShortName != null)
                {
                    return byShortName;
                }
            }

            var byGoType = _emitContext.RuntimeCatalog.ResolveByGoTypeNameInPackageOrNamespace(
                receiverTypeName, packageImportPath, moduleNamespace);
            if (byGoType != null)
            {
                return byGoType;
            }

            if (_typeBuilders.TryGetValue(receiverTypeName, out var typeBuilder))
            {
                if (typeBuilder.IsCreated())
                {
                    return typeBuilder.CreateTypeInfo()!;
                }
            }

            if (_emitContext.LinkedTypes.TryGetValue(receiverTypeName, out var linkedType))
            {
                if (linkedType.IsCreated())
                {
                    return linkedType.CreateTypeInfo()!;
                }
            }

            return null;
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

                if (genericDefinition is TypeBuilder uncreatedGenericCtor && !uncreatedGenericCtor.IsCreated())
                {
                    return ResolveConstructorOnUncreatedGenericBuilder(declaringType, uncreatedGenericCtor, token);
                }

                if (genericDefinition is TypeBuilder createdGenericCtor && createdGenericCtor.IsCreated())
                {
                    genericDefinition = createdGenericCtor.CreateTypeInfo()!;
                }

                var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);
                foreach (var baseConstructor in genericDefinition.GetConstructors(AllConstructorFlags))
                {
                    if (MatchesParameterTypes(baseConstructor, resolvedParamTypes))
                    {
                        return TypeBuilder.GetConstructor(declaringType, baseConstructor);
                    }
                }
                foreach (var baseConstructor in genericDefinition.GetConstructors(AllConstructorFlags))
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
                foreach (var constructor in declaringType.GetConstructors(AllConstructorFlags))
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
                    if (genericDefinition is TypeBuilder uncreatedFallback && !uncreatedFallback.IsCreated())
                    {
                        return ResolveConstructorOnUncreatedGenericBuilder(declaringType, uncreatedFallback, token);
                    }
                    if (genericDefinition is TypeBuilder createdFallback && createdFallback.IsCreated())
                    {
                        genericDefinition = createdFallback.CreateTypeInfo()!;
                    }
                    foreach (var baseConstructor in genericDefinition.GetConstructors(AllConstructorFlags))
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

            methodBuilder = FindMethodBuilderByNameAndParamCount(declaringTypeName, token.MethodName, token.ParameterTypes.Length);

            if (methodBuilder != null)
            {
                return methodBuilder;
            }

            methodBuilder = FindMethodBuilderByNameOnly(declaringTypeName, token.MethodName);
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

            // The TypeBuilder may belong to a previously linked archive whose methods
            // are not in this linker's _methodBuilders. If CreateType has been called,
            // use runtime reflection to find the method.
            if (typeBuilder.IsCreated())
            {
                var runtimeType = typeBuilder.CreateTypeInfo()!;
                return FindMethodOnRuntimeType(runtimeType, token);
            }

            // TypeBuilder not created yet — search global LinkedMethods by name match.
            foreach (var (linkedKey, linkedMethod) in _emitContext.LinkedMethods)
            {
                if (MatchesMethodName(linkedKey, declaringTypeName, token.MethodName))
                {
                    return linkedMethod;
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

            if (genericDefinition is TypeBuilder genericTypeBuilder && !genericTypeBuilder.IsCreated())
            {
                return ResolveMethodOnUncreatedGenericBuilder(declaringType, genericTypeBuilder, token);
            }

            if (genericDefinition is TypeBuilder createdGenericBuilder && createdGenericBuilder.IsCreated())
            {
                genericDefinition = createdGenericBuilder.CreateTypeInfo()!;
            }

            var resolvedParamTypes = ResolveMethodTokenParameterTypes(token);

            foreach (var baseMethod in genericDefinition.GetMethods(AllMethodFlags))
            {
                if (baseMethod.Name == token.MethodName && MatchesParameterTypes(baseMethod, resolvedParamTypes))
                {
                    return TypeBuilder.GetMethod(declaringType, baseMethod);
                }
            }

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

            foreach (var baseMethod in genericDefinition.GetMethods(AllMethodFlags))
            {
                if (baseMethod.Name == token.MethodName &&
                    baseMethod.GetParameters().Length == token.ParameterTypes.Length)
                {
                    return TypeBuilder.GetMethod(declaringType, baseMethod);
                }
            }

            return null;
        }

        private MethodBase? ResolveMethodOnUncreatedGenericBuilder(
            Type instantiatedType, TypeBuilder genericDefinition, MethodToken token)
        {
            var declaringTypeName = genericDefinition.FullName ?? genericDefinition.Name;

            foreach (var (key, builder) in _methodBuilders)
            {
                if (builder.Name != token.MethodName)
                {
                    continue;
                }
                var builderDeclName = builder.DeclaringType?.FullName ?? builder.DeclaringType?.Name;
                if (builderDeclName == declaringTypeName)
                {
                    return TypeBuilder.GetMethod(instantiatedType, builder);
                }
            }

            foreach (var (key, builder) in _emitContext.LinkedMethods)
            {
                if (builder.Name != token.MethodName)
                {
                    continue;
                }
                var builderDeclName = builder.DeclaringType?.FullName ?? builder.DeclaringType?.Name;
                if (builderDeclName == declaringTypeName)
                {
                    return TypeBuilder.GetMethod(instantiatedType, builder);
                }
            }

            return null;
        }

        private MethodBase? ResolveConstructorOnUncreatedGenericBuilder(
            Type instantiatedType, TypeBuilder genericDefinition, MethodToken token)
        {
            var declaringTypeName = genericDefinition.FullName ?? genericDefinition.Name;

            if (_constructorBuilders.TryGetValue(declaringTypeName, out var constructorBuilder))
            {
                return TypeBuilder.GetConstructor(instantiatedType, constructorBuilder);
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

            if (declaringType is TypeBuilder typeBuilder)
            {
                if (typeBuilder.IsCreated())
                {
                    declaringType = typeBuilder.CreateTypeInfo()!;
                }
                else
                {
                    return FindGenericMethodBuilderOnType(typeBuilder, token.MethodName);
                }
            }

            foreach (var method in declaringType.GetMethods(AllMethodFlags))
            {
                if (method.Name == token.MethodName && method.IsGenericMethodDefinition)
                {
                    return method;
                }
            }
            return null;
        }

        private MethodBuilder? FindGenericMethodBuilderOnType(TypeBuilder typeBuilder, string methodName)
        {
            var declaringTypeName = typeBuilder.FullName ?? typeBuilder.Name;

            foreach (var (key, builder) in _methodBuilders)
            {
                if (builder.Name != methodName)
                {
                    continue;
                }
                if (!builder.IsGenericMethodDefinition)
                {
                    continue;
                }
                var builderDeclName = builder.DeclaringType?.FullName ?? builder.DeclaringType?.Name;
                if (builderDeclName == declaringTypeName)
                {
                    return builder;
                }
            }

            foreach (var (key, builder) in _emitContext.LinkedMethods)
            {
                if (builder.Name != methodName)
                {
                    continue;
                }
                if (!builder.IsGenericMethodDefinition)
                {
                    continue;
                }
                var builderDeclName = builder.DeclaringType?.FullName ?? builder.DeclaringType?.Name;
                if (builderDeclName == declaringTypeName)
                {
                    return builder;
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

            // Source-compiled receiver methods include the receiver as the first parameter,
            // but runtime instance methods do not. Retry with the receiver parameter stripped.
            if (resolvedParamTypes.Length > 0)
            {
                var paramsWithoutReceiver = resolvedParamTypes.Skip(1).ToArray();
                foreach (var method in declaringType.GetMethods(AllMethodFlags))
                {
                    if (method.Name != token.MethodName)
                    {
                        continue;
                    }
                    if (method.IsGenericMethodDefinition)
                    {
                        continue;
                    }
                    if (MatchesParameterTypes(method, paramsWithoutReceiver))
                    {
                        return method;
                    }
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

            foreach (var method in declaringType.GetMethods(AllMethodFlags))
            {
                if (method.Name == token.MethodName && method.IsGenericMethodDefinition)
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
                var candidateType = methodParameters[index].ParameterType;
                var expectedType = resolvedParamTypes[index];
                if (candidateType == expectedType)
                {
                    continue;
                }
                if (NgoWriter.GetTypeNameStatic(candidateType) == NgoWriter.GetTypeNameStatic(expectedType))
                {
                    continue;
                }
                if (AreSignedUnsignedEquivalent(candidateType, expectedType))
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        private static bool AreSignedUnsignedEquivalent(Type typeA, Type typeB)
        {
            if (typeA == typeof(long) && typeB == typeof(ulong))
            {
                return true;
            }
            if (typeA == typeof(ulong) && typeB == typeof(long))
            {
                return true;
            }
            if (typeA == typeof(int) && typeB == typeof(uint))
            {
                return true;
            }
            if (typeA == typeof(uint) && typeB == typeof(int))
            {
                return true;
            }
            if (typeA == typeof(short) && typeB == typeof(ushort))
            {
                return true;
            }
            if (typeA == typeof(ushort) && typeB == typeof(short))
            {
                return true;
            }
            if (typeA == typeof(byte) && typeB == typeof(sbyte))
            {
                return true;
            }
            if (typeA == typeof(sbyte) && typeB == typeof(byte))
            {
                return true;
            }
            if (typeA == typeof(nint) && typeB == typeof(nuint))
            {
                return true;
            }
            if (typeA == typeof(nuint) && typeB == typeof(nint))
            {
                return true;
            }
            return false;
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
                if (kvp.Key.Name != fieldName)
                {
                    continue;
                }
                if (kvp.Value is not LiveFieldBuilder liveField)
                {
                    continue;
                }
                var declaringName = liveField.DeclaringType?.Name;
                if (declaringName == declaringTypeName
                    || (declaringName != null && declaringTypeName.EndsWith("." + declaringName)))
                {
                    return liveField.Inner;
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
            public TypeToken[] LocalTypes = Array.Empty<TypeToken>();
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
            public TypeToken? CatchTypeToken;
        }
    }
}
