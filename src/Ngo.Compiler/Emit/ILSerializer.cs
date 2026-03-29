// -----------------------------------------------------------------------
// <copyright file="ILSerializer.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Serializes and deserializes IL metadata and IL bytecode
    /// for .ngo archives. Handles token remapping when linking into a target module.
    /// </summary>
    internal static class ILSerializer
    {
        private const byte TokenKindType = 0;
        private const byte TokenKindMethod = 1;
        private const byte TokenKindField = 2;
        private const byte TokenKindString = 3;

        private static readonly Dictionary<short, OpCode> OpCodeMap = BuildOpCodeMap();

        /// <summary>
        /// Emits a package into a .ngo archive using NgoModuleBuilder (zero DynamicAssembly).
        /// NgoMethodBuilder.GetILWriter() returns NgoWriter, so IL is captured automatically
        /// during the standard 3-pass emit flow.
        /// </summary>
        public static void WriteArchive(string path, PackageSymbol pkg, string importPath,
            AnalysisResult result, CompilationContext compilationContext)
        {
            var ngoModule = new NgoModuleBuilder();
            var mapper = new TypeMapper(compilationContext);
            var ctx = new EmitContext(ngoModule, mapper, null, compilationContext.Log);
            ctx.IsDependencyEmit = true;
            mapper.SetEmitContext(ctx);
            EmitPackageForSerialization(result.Root, ctx);

            using var ilMetaStream = new MemoryStream();
            using var ilCodeStream = new MemoryStream();
            using (var metaWriter = new BinaryWriter(ilMetaStream, System.Text.Encoding.UTF8, leaveOpen: true))
            using (var codeWriter = new BinaryWriter(ilCodeStream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                ngoModule.WriteILSections(metaWriter, codeWriter);
            }

            NgoArchive.WriteComplete(path, pkg, importPath, ilMetaStream.ToArray(), ilCodeStream.ToArray());
        }

        /// <summary>
        /// Reads IL from a .ngo archive and links it into the target module.
        /// Creates TypeBuilders, FieldBuilders, MethodBuilders; remaps tokens; sets IL bodies.
        /// </summary>
        /// <returns>true if IL was found and linked; false if archive missing or has no IL sections.</returns>
        public static bool LinkFromArchive(string archivePath, PackageSymbol pkg, EmitContext ctx)
        {
            var (ilMetaBytes, ilCodeBytes) = NgoArchive.ReadIL(archivePath);
            if (ilMetaBytes == null || ilCodeBytes == null)
            {
                return false;
            }

            LinkIL(ilMetaBytes, ilCodeBytes, pkg, ctx);
            return true;
        }

        private static void LinkIL(byte[] ilMetaBytes, byte[] ilCodeBytes, PackageSymbol pkg, EmitContext ctx)
        {
            using var metaStream = new MemoryStream(ilMetaBytes);
            using var codeStream = new MemoryStream(ilCodeBytes);
            var metaReader = new BinaryReader(metaStream);
            var codeReader = new BinaryReader(codeStream);

            int typeCount = metaReader.ReadInt32();

            var typeBuilders = new Dictionary<string, TypeBuilder>();
            var genericParamTypes = new Dictionary<string, Type>();
            var fieldBuilders = new Dictionary<string, FieldBuilder>();
            var methodBuilders = new Dictionary<string, MethodBuilder>();
            var methodILIndices = new Dictionary<string, int>();
            var methodGenericParams = new Dictionary<string, Dictionary<string, Type>>();

            var typeInfos = new List<DeserializedTypeInfo>();
            var typeRawData = new List<(string fullTypeName, TypeBuilder tb,
                List<(string name, FieldAttributes attrs, string typeName)> fields,
                List<SerializedMethodInfo> methods, List<SerializedMethodOverride> overrides)>();

            for (int t = 0; t < typeCount; t++)
            {
                var fullTypeName = metaReader.ReadString();
                var typeAttrs = (TypeAttributes)metaReader.ReadInt32();
                var baseTypeName = metaReader.ReadString();

                int typeGenericParamCount = metaReader.ReadInt32();
                var typeGpNames = new string[typeGenericParamCount];
                for (int g = 0; g < typeGenericParamCount; g++)
                {
                    typeGpNames[g] = metaReader.ReadString();
                }

                if (string.IsNullOrEmpty(fullTypeName))
                {
                    int skipFieldCount = metaReader.ReadInt32();
                    for (int f = 0; f < skipFieldCount; f++)
                    {
                        metaReader.ReadString(); metaReader.ReadInt32(); metaReader.ReadString();
                    }
                    int skipMethodCount = metaReader.ReadInt32();
                    for (int m = 0; m < skipMethodCount; m++)
                    {
                        metaReader.ReadString();
                        metaReader.ReadInt32();
                        int mgpc = metaReader.ReadInt32();
                        for (int g = 0; g < mgpc; g++) { metaReader.ReadString(); }
                        metaReader.ReadString();
                        int skipParamCount = metaReader.ReadInt32();
                        for (int p = 0; p < skipParamCount; p++) { metaReader.ReadString(); }
                        metaReader.ReadInt32();
                    }
                    int skipOverrideCount = metaReader.ReadInt32();
                    for (int o = 0; o < skipOverrideCount; o++)
                    {
                        metaReader.ReadString(); metaReader.ReadString(); metaReader.ReadString();
                    }
                    continue;
                }

                TypeBuilder tb;
                bool isStaticClass = (typeAttrs & TypeAttributes.Abstract) != 0
                    && (typeAttrs & TypeAttributes.Sealed) != 0;
                bool isInterface = (typeAttrs & TypeAttributes.Interface) != 0;

                if (isStaticClass || isInterface)
                {
                    tb = ((LiveModuleBuilder)ctx.Module).Inner.DefineType(fullTypeName, typeAttrs);
                }
                else
                {
                    Type parent;
                    if (!string.IsNullOrEmpty(baseTypeName))
                    {
                        parent = ResolveType(baseTypeName);
                    }
                    else
                    {
                        bool hasSequentialLayout = (typeAttrs & TypeAttributes.SequentialLayout) != 0;
                        parent = hasSequentialLayout ? typeof(ValueType) : typeof(object);
                    }
                    tb = ((LiveModuleBuilder)ctx.Module).Inner.DefineType(fullTypeName, typeAttrs, parent);
                }

                typeBuilders[fullTypeName] = tb;

                if (typeGenericParamCount > 0)
                {
                    var typeGenericParams = tb.DefineGenericParameters(typeGpNames);
                    for (int g = 0; g < typeGenericParamCount; g++)
                    {
                        genericParamTypes[typeGpNames[g]] = typeGenericParams[g];
                    }
                }

                int fieldCount = metaReader.ReadInt32();
                var fields = new List<(string name, FieldAttributes attrs, string typeName)>(fieldCount);
                for (int f = 0; f < fieldCount; f++)
                {
                    fields.Add((metaReader.ReadString(), (FieldAttributes)metaReader.ReadInt32(), metaReader.ReadString()));
                }

                int methodCount = metaReader.ReadInt32();
                var methodInfos = new List<SerializedMethodInfo>(methodCount);
                for (int m = 0; m < methodCount; m++)
                {
                    var methodName = metaReader.ReadString();
                    var methodAttrs = (MethodAttributes)metaReader.ReadInt32();
                    int methodGenericParamCount = metaReader.ReadInt32();
                    var methodGpNames = new string[methodGenericParamCount];
                    for (int g = 0; g < methodGenericParamCount; g++)
                    {
                        methodGpNames[g] = metaReader.ReadString();
                    }
                    var returnTypeName = metaReader.ReadString();
                    int paramCount = metaReader.ReadInt32();
                    var paramTypeNames = new string[paramCount];
                    for (int p = 0; p < paramCount; p++)
                    {
                        paramTypeNames[p] = metaReader.ReadString();
                    }
                    var bodyIndex = metaReader.ReadInt32();
                    methodInfos.Add(new SerializedMethodInfo(methodName, methodAttrs, returnTypeName, paramTypeNames, bodyIndex, methodGpNames));
                }

                var overrides = new List<SerializedMethodOverride>();
                int overrideCount = metaReader.ReadInt32();
                for (int o = 0; o < overrideCount; o++)
                {
                    overrides.Add(new SerializedMethodOverride(metaReader.ReadString(), metaReader.ReadString(), metaReader.ReadString()));
                }

                typeRawData.Add((fullTypeName, tb, fields, methodInfos, overrides));
                typeInfos.Add(new DeserializedTypeInfo(fullTypeName, tb, methodCount, methodInfos, overrides));
            }

            foreach (var (fullTypeName, tb, fields, methods, overrides) in typeRawData)
            {
                int blankFieldIndex = 0;
                foreach (var (fieldName, fieldAttrs, fieldTypeName) in fields)
                {
                    Type fieldType;
                    try
                    {
                        fieldType = ResolveType(fieldTypeName, typeBuilders, genericParamTypes);
                    }
                    catch (InvalidOperationException)
                    {
                        fieldType = typeof(object);
                    }

                    var actualFieldName = fieldName;
                    if (fieldName == "_")
                    {
                        actualFieldName = $"_pad{blankFieldIndex++}";
                    }

                    var fb = tb.DefineField(actualFieldName, fieldType, fieldAttrs);
                    fieldBuilders[fullTypeName + "." + actualFieldName] = fb;
                    if (actualFieldName != fieldName)
                    {
                        fieldBuilders[fullTypeName + "." + fieldName] = fb;
                    }

                    foreach (var (_, sym) in pkg.Exports)
                    {
                        if (sym is StructTypeSymbol structSym && structSym.Name == tb.Name)
                        {
                            foreach (var fSym in structSym.Fields)
                            {
                                if (fSym.Name == fieldName)
                                {
                                    ctx.StructFields[fSym] = new LiveFieldBuilder(fb);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var typeInfo in typeInfos)
            {
                foreach (var methodInfo in typeInfo.Methods)
                {
                    MethodBuilder mb;
                    if (methodInfo.GenericParamNames.Length > 0)
                    {
                        mb = typeInfo.TypeBuilder.DefineMethod(methodInfo.MethodName, methodInfo.Attributes);
                        var genericParams = mb.DefineGenericParameters(methodInfo.GenericParamNames);
                        var genericParamMap = new Dictionary<string, Type>();
                        for (int g = 0; g < genericParams.Length; g++)
                        {
                            genericParamMap[methodInfo.GenericParamNames[g]] = genericParams[g];
                        }

                        var returnType = ResolveTypeWithGenericParams(methodInfo.ReturnTypeName, typeBuilders, genericParamMap);
                        var paramTypes = new Type[methodInfo.ParamTypeNames.Length];
                        for (int p = 0; p < methodInfo.ParamTypeNames.Length; p++)
                        {
                            paramTypes[p] = ResolveTypeWithGenericParams(methodInfo.ParamTypeNames[p], typeBuilders, genericParamMap);
                        }
                        mb.SetReturnType(returnType);
                        mb.SetParameters(paramTypes);

                        var methodKey2 = typeInfo.FullTypeName + "." + methodInfo.MethodName;
                        methodGenericParams[methodKey2] = genericParamMap;
                    }
                    else
                    {
                        var returnType = ResolveType(methodInfo.ReturnTypeName, typeBuilders, genericParamTypes);
                        var paramTypes = new Type[methodInfo.ParamTypeNames.Length];
                        for (int p = 0; p < methodInfo.ParamTypeNames.Length; p++)
                        {
                            paramTypes[p] = ResolveType(methodInfo.ParamTypeNames[p], typeBuilders, genericParamTypes);
                        }
                        mb = typeInfo.TypeBuilder.DefineMethod(methodInfo.MethodName, methodInfo.Attributes, returnType, paramTypes);
                    }

                    var methodKey = typeInfo.FullTypeName + "." + methodInfo.MethodName;
                    methodBuilders[methodKey] = mb;

                    if (methodInfo.BodyIndex >= 0)
                    {
                        methodILIndices[methodKey] = methodInfo.BodyIndex;
                    }

                    foreach (var (_, sym) in pkg.Exports)
                    {
                        if (sym is FunctionSymbol funcSym && funcSym.Name == methodInfo.MethodName)
                        {
                            ctx.CachedMethods[funcSym] = mb;
                        }
                    }
                }
            }

            foreach (var typeInfo in typeInfos)
            {
                foreach (var ov in typeInfo.Overrides)
                {
                    var bodyKey = typeInfo.FullTypeName + "." + ov.BodyMethodName;
                    if (methodBuilders.TryGetValue(bodyKey, out var bodyMb))
                    {
                        var declType = ResolveType(ov.DeclarationTypeName, typeBuilders);
                        var declMethod = declType.GetMethod(ov.DeclarationMethodName);
                        if (declMethod != null)
                        {
                            typeInfo.TypeBuilder.DefineMethodOverride(bodyMb, declMethod);
                        }
                    }
                }
            }

            var constructorBuilders = new Dictionary<string, ConstructorBuilder>();
            var deferredClassTypes = new List<string>();
            foreach (var (fullName, tb) in typeBuilders)
            {
                bool isStaticClass = (tb.Attributes & TypeAttributes.Abstract) != 0
                    && (tb.Attributes & TypeAttributes.Sealed) != 0;
                bool isInterface = (tb.Attributes & TypeAttributes.Interface) != 0;
                bool isValueType = tb.BaseType == typeof(ValueType);

                if (isStaticClass)
                {
                    continue;
                }

                if (isInterface || isValueType)
                {
                    try
                    {
                        var runtimeType = tb.CreateType()!;
                        RegisterLinkedType(runtimeType, tb, pkg, ctx);
                    }
                    catch (TypeLoadException)
                    {
                        deferredClassTypes.Add(fullName);
                    }
                }
                else
                {
                    deferredClassTypes.Add(fullName);
                    bool hasCtor = false;
                    foreach (var ti in typeInfos)
                    {
                        if (ti.FullTypeName == fullName)
                        {
                            foreach (var m in ti.Methods)
                            {
                                if (m.MethodName == ".ctor")
                                {
                                    hasCtor = true;
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    if (!hasCtor)
                    {
                        var ctorBuilder = tb.DefineDefaultConstructor(
                            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                        constructorBuilders[fullName] = ctorBuilder;
                    }
                }
            }

            int bodyCount = codeReader.ReadInt32();
            var bodies = new List<MethodBodyData>(bodyCount);

            for (int b = 0; b < bodyCount; b++)
            {
                var bodyData = new MethodBodyData();
                bodyData.MaxStack = codeReader.ReadInt32();

                int localCount = codeReader.ReadInt32();
                bodyData.LocalTypes = new string[localCount];
                for (int l = 0; l < localCount; l++)
                {
                    bodyData.LocalTypes[l] = codeReader.ReadString();
                }

                int ilLen = codeReader.ReadInt32();
                bodyData.ILBytes = codeReader.ReadBytes(ilLen);

                int tokenCount = codeReader.ReadInt32();
                bodyData.TokenEntries = new List<TokenEntry>(tokenCount);
                for (int te = 0; te < tokenCount; te++)
                {
                    bodyData.TokenEntries.Add(new TokenEntry(
                        codeReader.ReadInt32(), codeReader.ReadByte(), codeReader.ReadString()));
                }

                int handlerCount = codeReader.ReadInt32();
                bodyData.ExceptionHandlers = new List<ExceptionHandlerData>(handlerCount);
                for (int h = 0; h < handlerCount; h++)
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

            foreach (var (methodKey, bodyIndex) in methodILIndices)
            {
                if (bodyIndex >= bodies.Count || !methodBuilders.TryGetValue(methodKey, out var mb))
                {
                    continue;
                }

                var bodyData = bodies[bodyIndex];

                var combinedGenericParams = new Dictionary<string, Type>(genericParamTypes);
                if (methodGenericParams.TryGetValue(methodKey, out var methodGps))
                {
                    foreach (var (name, gpType) in methodGps)
                    {
                        combinedGenericParams[name] = gpType;
                    }
                }

                ReplayIL(mb, bodyData.ILBytes, bodyData.LocalTypes, bodyData.TokenEntries,
                    bodyData.ExceptionHandlers, typeBuilders, methodBuilders, fieldBuilders,
                    constructorBuilders, ctx, combinedGenericParams);
            }

            foreach (var fullName in deferredClassTypes)
            {
                if (typeBuilders.TryGetValue(fullName, out var classTb))
                {
                    try
                    {
                        var runtimeType = classTb.CreateType()!;
                        RegisterLinkedType(runtimeType, classTb, pkg, ctx);
                    }
                    catch (TypeLoadException) { }
                }
            }

            foreach (var (_, tb) in typeBuilders)
            {
                bool isStaticClass = (tb.Attributes & TypeAttributes.Abstract) != 0
                    && (tb.Attributes & TypeAttributes.Sealed) != 0;
                if (isStaticClass)
                {
                    tb.CreateType();
                }
            }
        }

        private static void RegisterLinkedType(Type runtimeType, TypeBuilder tb, PackageSymbol pkg, EmitContext ctx)
        {
            foreach (var (_, sym) in pkg.Exports)
            {
                if (sym is StructTypeSymbol structSym && structSym.Name == tb.Name)
                {
                    ctx.Mapper.Register(structSym, runtimeType);
                    ctx.FinalizedTypes.Add(structSym);
                }
                else if (sym is InterfaceTypeSymbol ifaceSym && ifaceSym.Name == tb.Name)
                {
                    ctx.Mapper.Register(ifaceSym, runtimeType);
                    ctx.FinalizedTypes.Add(ifaceSym);
                }
            }
        }

        private static void ReplayIL(MethodBuilder mb, byte[] il, string[] localTypeNames,
            List<TokenEntry> tokenEntries, List<ExceptionHandlerData> exceptionHandlers,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, MethodBuilder> methodBuilders,
            Dictionary<string, FieldBuilder> fieldBuilders,
            Dictionary<string, ConstructorBuilder> constructorBuilders,
            EmitContext ctx,
            Dictionary<string, Type>? genericParams = null)
        {
            var ilGen = mb.GetILGenerator();

            foreach (var localTypeName in localTypeNames)
            {
                var localType = ResolveType(localTypeName, typeBuilders, genericParams);
                ilGen.DeclareLocal(localType);
            }

            var tokenMap = new Dictionary<int, TokenReference>();
            foreach (var entry in tokenEntries)
            {
                tokenMap[entry.Offset] = new TokenReference(entry.Kind, entry.Reference);
            }

            var labels = new Dictionary<int, Label>();
            PreScanBranchTargets(il, labels, ilGen);

            int i = 0;
            while (i < il.Length)
            {
                if (labels.TryGetValue(i, out var targetLabel))
                {
                    ilGen.MarkLabel(targetLabel);
                }

                byte op = il[i++];

                if (op == 0xFE && i < il.Length)
                {
                    byte op2 = il[i++];
                    ReplayFEOpcode(op2, il, ref i, ilGen, tokenMap, typeBuilders, methodBuilders, fieldBuilders, constructorBuilders, labels, ctx, genericParams);
                    continue;
                }

                if (HasInlineToken(op) && i + 4 <= il.Length)
                {
                    int tokenOffset = i;
                    i += 4;

                    if (op == 0x72)
                    {
                        if (tokenMap.TryGetValue(tokenOffset, out var strRef) && strRef.Kind == TokenKindString)
                        {
                            ilGen.Emit(OpCodes.Ldstr, strRef.Reference);
                        }
                        else
                        {
                            ilGen.Emit(OpCodes.Ldnull);
                        }
                        continue;
                    }

                    var opCode = GetOpCode(op);
                    if (tokenMap.TryGetValue(tokenOffset, out var tok))
                    {
                        EmitTokenOpcode(ilGen, opCode, tok.Kind, tok.Reference, typeBuilders, methodBuilders, fieldBuilders, constructorBuilders, ctx, genericParams);
                    }
                    continue;
                }

                if (IsShortBranch(op))
                {
                    var offset = (sbyte)il[i++];
                    var target = i + offset;
                    var brLabel = GetOrCreateLabel(labels, target, ilGen);
                    ilGen.Emit(GetOpCode(op), brLabel);
                    continue;
                }

                if (IsLongBranch(op))
                {
                    var offset = BitConverter.ToInt32(il, i);
                    i += 4;
                    var target = i + offset;
                    var brLabel = GetOrCreateLabel(labels, target, ilGen);
                    ilGen.Emit(GetOpCode(op), brLabel);
                    continue;
                }

                if (op == 0x45)
                {
                    int count = BitConverter.ToInt32(il, i);
                    i += 4;
                    int baseOffset = i + count * 4;
                    var switchLabels = new Label[count];
                    for (int s = 0; s < count; s++)
                    {
                        int target = baseOffset + BitConverter.ToInt32(il, i);
                        i += 4;
                        switchLabels[s] = GetOrCreateLabel(labels, target, ilGen);
                    }
                    ilGen.Emit(OpCodes.Switch, switchLabels);
                    continue;
                }

                if (op == 0x0E || op == 0x0F || op == 0x10 || op == 0x11 || op == 0x12 || op == 0x13)
                {
                    ilGen.Emit(GetOpCode(op), il[i++]);
                    continue;
                }
                if (op == 0x1F)
                {
                    ilGen.Emit(OpCodes.Ldc_I4_S, (sbyte)il[i++]);
                    continue;
                }

                if (op == 0x20)
                {
                    ilGen.Emit(OpCodes.Ldc_I4, BitConverter.ToInt32(il, i));
                    i += 4;
                    continue;
                }

                if (op == 0x21)
                {
                    ilGen.Emit(OpCodes.Ldc_I8, BitConverter.ToInt64(il, i));
                    i += 8;
                    continue;
                }

                if (op == 0x22)
                {
                    ilGen.Emit(OpCodes.Ldc_R4, BitConverter.ToSingle(il, i));
                    i += 4;
                    continue;
                }

                if (op == 0x23)
                {
                    ilGen.Emit(OpCodes.Ldc_R8, BitConverter.ToDouble(il, i));
                    i += 8;
                    continue;
                }

                if (op == 0xDE)
                {
                    var offset = (sbyte)il[i++];
                    var target = i + offset;
                    var brLabel = GetOrCreateLabel(labels, target, ilGen);
                    ilGen.Emit(OpCodes.Leave_S, brLabel);
                    continue;
                }

                if (op == 0xDD)
                {
                    var offset = BitConverter.ToInt32(il, i);
                    i += 4;
                    var target = i + offset;
                    var brLabel = GetOrCreateLabel(labels, target, ilGen);
                    ilGen.Emit(OpCodes.Leave, brLabel);
                    continue;
                }

                ilGen.Emit(GetOpCode(op));
            }
        }

        private static void ReplayFEOpcode(byte op2, byte[] il, ref int i, ILGenerator ilGen,
            Dictionary<int, TokenReference> tokenMap,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, MethodBuilder> methodBuilders,
            Dictionary<string, FieldBuilder> fieldBuilders,
            Dictionary<string, ConstructorBuilder> constructorBuilders,
            Dictionary<int, Label> labels, EmitContext ctx,
            Dictionary<string, Type>? genericParams = null)
        {
            switch (op2)
            {
                case 0x15: // initobj
                case 0x16: // constrained
                case 0x1C: // sizeof
                {
                    var opCode = op2 == 0x15 ? OpCodes.Initobj : op2 == 0x16 ? OpCodes.Constrained : OpCodes.Sizeof;
                    if (i + 4 <= il.Length && tokenMap.TryGetValue(i, out var tok))
                    {
                        var type = ResolveTypeReference(tok.Reference, typeBuilders, ctx, genericParams);
                        if (type != null)
                        {
                            ilGen.Emit(opCode, type);
                        }
                    }
                    i += 4;
                    break;
                }
                case 0x06: // ldftn
                case 0x07: // ldvirtftn
                {
                    var opCode = op2 == 0x06 ? OpCodes.Ldftn : OpCodes.Ldvirtftn;
                    if (i + 4 <= il.Length && tokenMap.TryGetValue(i, out var tok))
                    {
                        var method = ResolveMethodReference(tok.Reference, typeBuilders, methodBuilders, constructorBuilders, ctx, genericParams);
                        if (method is MethodInfo mi)
                        {
                            ilGen.Emit(opCode, mi);
                        }
                    }
                    i += 4;
                    break;
                }
                case 0x09: case 0x0A: case 0x0B:
                case 0x0C: case 0x0D: case 0x0E:
                {
                    ilGen.Emit(GetFEOpCode(op2), BitConverter.ToInt16(il, i));
                    i += 2;
                    break;
                }
                case 0x12:
                {
                    ilGen.Emit(OpCodes.Unaligned, il[i++]);
                    break;
                }
                default:
                {
                    ilGen.Emit(GetFEOpCode(op2));
                    break;
                }
            }
        }

        private static void EmitTokenOpcode(ILGenerator ilGen, OpCode opCode, byte kind, string reference,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, MethodBuilder> methodBuilders,
            Dictionary<string, FieldBuilder> fieldBuilders,
            Dictionary<string, ConstructorBuilder> constructorBuilders,
            EmitContext ctx,
            Dictionary<string, Type>? genericParams = null)
        {
            switch (kind)
            {
                case TokenKindType:
                {
                    var type = ResolveTypeReference(reference, typeBuilders, ctx, genericParams);
                    if (type == null)
                    {
                        throw new InvalidOperationException(
                            $"LinkIL: failed to resolve type '{reference}' for opcode {opCode}");
                    }
                    ilGen.Emit(opCode, type);
                    break;
                }
                case TokenKindMethod:
                {
                    var method = ResolveMethodReference(reference, typeBuilders, methodBuilders, constructorBuilders, ctx, genericParams);
                    if (method == null)
                    {
                        throw new InvalidOperationException(
                            $"LinkIL: failed to resolve method '{reference}' for opcode {opCode}");
                    }
                    if (method is ConstructorInfo ctor)
                    {
                        ilGen.Emit(opCode, ctor);
                    }
                    else
                    {
                        ilGen.Emit(opCode, (MethodInfo)method);
                    }
                    break;
                }
                case TokenKindField:
                {
                    var field = ResolveFieldReference(reference, typeBuilders, fieldBuilders, ctx, genericParams);
                    if (field == null)
                    {
                        throw new InvalidOperationException(
                            $"LinkIL: failed to resolve field '{reference}' for opcode {opCode}");
                    }
                    ilGen.Emit(opCode, field);
                    break;
                }
            }
        }

        private static Type? ResolveTypeReference(string reference,
            Dictionary<string, TypeBuilder> typeBuilders, EmitContext ctx,
            Dictionary<string, Type>? genericParams = null)
        {
            if (genericParams != null && genericParams.TryGetValue(reference, out var gp))
            {
                return gp;
            }
            if (typeBuilders.TryGetValue(reference, out var tb))
            {
                return tb;
            }
            return ResolveType(reference, typeBuilders, genericParams);
        }

        private static MethodBase? ResolveMethodReference(string reference,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, MethodBuilder> methodBuilders,
            Dictionary<string, ConstructorBuilder> constructorBuilders,
            EmitContext ctx,
            Dictionary<string, Type>? genericParams = null)
        {
            if (methodBuilders.TryGetValue(reference, out var mb))
            {
                return mb;
            }

            var parts = reference.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                return null;
            }

            var dotKey = parts[0] + "." + parts[1];
            if (methodBuilders.TryGetValue(dotKey, out mb))
            {
                return mb;
            }

            var type = ResolveType(parts[0], typeBuilders, genericParams);

            if (type is TypeBuilder typeBuilder)
            {
                var methodNameForLookup = parts[1];
                int parenPos = methodNameForLookup.IndexOf('(');
                if (parenPos >= 0)
                {
                    methodNameForLookup = methodNameForLookup.Substring(0, parenPos);
                }

                var prefix = parts[0] + ".";
                foreach (var (key, method) in methodBuilders)
                {
                    if (key.StartsWith(prefix) && key.Substring(prefix.Length) == methodNameForLookup)
                    {
                        return method;
                    }
                }

                if (typeBuilder.FullName != null && typeBuilder.FullName != parts[0])
                {
                    var qualifiedPrefix = typeBuilder.FullName + ".";
                    foreach (var (key, method) in methodBuilders)
                    {
                        if (key.StartsWith(qualifiedPrefix) && key.Substring(qualifiedPrefix.Length) == methodNameForLookup)
                        {
                            return method;
                        }
                    }
                }

                if (methodNameForLookup == ".ctor")
                {
                    if (typeBuilder.FullName != null && constructorBuilders.TryGetValue(typeBuilder.FullName, out var ctorBuilder))
                    {
                        return ctorBuilder;
                    }
                    try
                    {
                        var ctors = typeBuilder.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (ctors.Length > 0)
                        {
                            return ctors[0];
                        }
                    }
                    catch (NotSupportedException) { }
                }

                return null;
            }

            var methodRef = parts[1];
            int parenIndex = methodRef.IndexOf('(');
            string methodName;
            string[] paramTypeNames;
            if (parenIndex >= 0)
            {
                methodName = methodRef.Substring(0, parenIndex);
                var paramStr = methodRef.Substring(parenIndex + 1, methodRef.Length - parenIndex - 2);
                paramTypeNames = string.IsNullOrEmpty(paramStr)
                    ? Array.Empty<string>()
                    : paramStr.Split(',');
            }
            else
            {
                methodName = methodRef;
                paramTypeNames = Array.Empty<string>();
            }

            if (methodName == ".ctor")
            {
                if (type.IsGenericType && type is not TypeBuilder)
                {
                    var genericDef = type.GetGenericTypeDefinition();
                    try
                    {
                        foreach (var baseCtor in genericDef.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (baseCtor.GetParameters().Length == paramTypeNames.Length)
                            {
                                return TypeBuilder.GetConstructor(type, baseCtor);
                            }
                        }
                    }
                    catch (NotSupportedException) { }
                    catch (ArgumentException) { }
                }

                try
                {
                    foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (ctor.GetParameters().Length == paramTypeNames.Length)
                        {
                            return ctor;
                        }
                    }
                }
                catch (NotSupportedException)
                {
                    if (type.IsGenericType)
                    {
                        var genericDef = type.GetGenericTypeDefinition();
                        foreach (var baseCtor in genericDef.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (baseCtor.GetParameters().Length == paramTypeNames.Length)
                            {
                                return TypeBuilder.GetConstructor(type, baseCtor);
                            }
                        }
                    }
                }
                return null;
            }

            if (methodName == ".cctor")
            {
                return type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            }

            if (type.IsGenericType && type is not TypeBuilder)
            {
                var genericDef = type.GetGenericTypeDefinition();
                bool hasTypeBuilderArg = false;
                try
                {
                    foreach (var arg in type.GetGenericArguments())
                    {
                        if (arg is TypeBuilder || (arg.IsGenericType && arg.GetGenericTypeDefinition() is TypeBuilder))
                        {
                            hasTypeBuilderArg = true;
                            break;
                        }
                    }
                }
                catch (NotSupportedException)
                {
                    hasTypeBuilderArg = true;
                }

                try
                {
                    foreach (var baseMethod in genericDef.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        if (baseMethod.Name == methodName && baseMethod.GetParameters().Length == paramTypeNames.Length)
                        {
                            if (genericDef is TypeBuilder || hasTypeBuilderArg)
                            {
                                return TypeBuilder.GetMethod(type, baseMethod);
                            }
                            MethodInfo? bestMatch = null;
                            foreach (var instMethod in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                            {
                                if (instMethod.Name != methodName || instMethod.GetParameters().Length != paramTypeNames.Length)
                                {
                                    continue;
                                }
                                var instParams = instMethod.GetParameters();
                                bool exactMatch = true;
                                for (int p = 0; p < instParams.Length; p++)
                                {
                                    if (NgoWriter.GetTypeNameStatic(instParams[p].ParameterType) != paramTypeNames[p])
                                    {
                                        exactMatch = false;
                                        break;
                                    }
                                }
                                if (exactMatch)
                                {
                                    return instMethod;
                                }
                                if (bestMatch == null)
                                {
                                    bestMatch = instMethod;
                                }
                            }
                            if (bestMatch != null)
                            {
                                return bestMatch;
                            }
                        }
                    }
                }
                catch (NotSupportedException)
                {
                    var prefix2 = genericDef.FullName + ".";
                    foreach (var (key, method) in methodBuilders)
                    {
                        if (key.StartsWith(prefix2) && key.Substring(prefix2.Length) == methodName)
                        {
                            return TypeBuilder.GetMethod(type, method);
                        }
                    }
                }
                return null;
            }

            try
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (method.Name != methodName || method.GetParameters().Length != paramTypeNames.Length)
                    {
                        continue;
                    }
                    var methodParams = method.GetParameters();
                    bool match = true;
                    for (int paramIdx = 0; paramIdx < methodParams.Length; paramIdx++)
                    {
                        if (methodParams[paramIdx].ParameterType.FullName != paramTypeNames[paramIdx]
                            && methodParams[paramIdx].ParameterType.Name != paramTypeNames[paramIdx])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        return method;
                    }
                }
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (method.Name == methodName && method.GetParameters().Length == paramTypeNames.Length)
                    {
                        return method;
                    }
                }
            }
            catch (NotSupportedException) { }
            return null;
        }

        private static FieldInfo? ResolveFieldReference(string reference,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, FieldBuilder> fieldBuilders,
            EmitContext ctx,
            Dictionary<string, Type>? genericParams = null)
        {
            if (fieldBuilders.TryGetValue(reference, out var fb))
            {
                return fb;
            }

            var parts = reference.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                return null;
            }

            var dotKey = parts[0] + "." + parts[1];
            if (fieldBuilders.TryGetValue(dotKey, out fb))
            {
                return fb;
            }

            var type = ResolveType(parts[0], typeBuilders, genericParams);

            if (type is TypeBuilder)
            {
                var prefix = parts[0] + ".";
                foreach (var (key, field) in fieldBuilders)
                {
                    if (key.StartsWith(prefix) && key.Substring(prefix.Length) == parts[1])
                    {
                        return field;
                    }
                }
                return null;
            }

            try
            {
                return type.GetField(parts[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            }
            catch (NotSupportedException)
            {
                if (type.IsGenericType)
                {
                    var genericDef = type.GetGenericTypeDefinition();
                    var baseField = genericDef.GetField(parts[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    if (baseField != null)
                    {
                        return TypeBuilder.GetField(type, baseField);
                    }
                }
                return null;
            }
        }

        private static void PreScanBranchTargets(byte[] il, Dictionary<int, Label> labels, ILGenerator ilGen)
        {
            int i = 0;
            while (i < il.Length)
            {
                byte op = il[i++];

                if (op == 0xFE && i < il.Length)
                {
                    byte op2 = il[i++];
                    i += GetFEOperandSize(op2);
                    continue;
                }

                if (HasInlineToken(op)) { i += 4; continue; }

                if (IsShortBranch(op))
                {
                    var offset = (sbyte)il[i++];
                    var target = i + offset;
                    if (!labels.ContainsKey(target))
                    {
                        labels[target] = ilGen.DefineLabel();
                    }
                    continue;
                }

                if (IsLongBranch(op))
                {
                    var offset = BitConverter.ToInt32(il, i);
                    i += 4;
                    var target = i + offset;
                    if (!labels.ContainsKey(target))
                    {
                        labels[target] = ilGen.DefineLabel();
                    }
                    continue;
                }

                if (op == 0x45)
                {
                    int count = BitConverter.ToInt32(il, i);
                    i += 4;
                    int baseOffset = i + count * 4;
                    for (int s = 0; s < count; s++)
                    {
                        int target = baseOffset + BitConverter.ToInt32(il, i);
                        i += 4;
                        if (!labels.ContainsKey(target))
                        {
                            labels[target] = ilGen.DefineLabel();
                        }
                    }
                    continue;
                }

                i += GetOperandSize(op);
            }
        }

        private static Label GetOrCreateLabel(Dictionary<int, Label> labels, int target, ILGenerator ilGen)
        {
            if (!labels.TryGetValue(target, out var label))
            {
                label = ilGen.DefineLabel();
                labels[target] = label;
            }
            return label;
        }

        private static void EmitPackageForSerialization(Ast.SourceFile root, EmitContext ctx)
        {
            var packageName = root.Package.Symbol.Name;

            ctx.PackageType = ctx.Module.DefineType(
                ctx.QualifyName(packageName),
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            var declEmitter = new DeclarationEmitter(ctx);
            ctx.DeclEmitter = declEmitter;
            var bodyEmitter = new MethodBodyEmitter(ctx);

            foreach (var typeDecl in root.Types)
            {
                if (typeDecl.Symbol is StructTypeSymbol structType)
                {
                    declEmitter.DefineStructType(structType);
                }
                else if (typeDecl.Symbol is InterfaceTypeSymbol)
                {
                    declEmitter.EmitTypeDeclaration(typeDecl);
                }
            }

            foreach (var typeDecl in root.Types)
            {
                if (typeDecl.Symbol is StructTypeSymbol structType)
                {
                    declEmitter.PopulateStructFields(structType);
                }
            }

            foreach (var kvp in ctx.StructTypes)
            {
                if (!ctx.FinalizedTypes.Contains(kvp.Key))
                {
                    var runtimeType = kvp.Value.CreateType()!;
                    ctx.Mapper.Register(kvp.Key, runtimeType);
                    ctx.FinalizedTypes.Add(kvp.Key);
                }
            }

            foreach (var kvp in ctx.InterfaceTypes)
            {
                if (!ctx.FinalizedTypes.Contains(kvp.Key))
                {
                    var runtimeType = kvp.Value.CreateType()!;
                    ctx.Mapper.Register(kvp.Key, runtimeType);
                    ctx.FinalizedTypes.Add(kvp.Key);
                }
            }

            foreach (var func in root.Functions)
            {
                declEmitter.EmitFunction(func);
            }

            foreach (var method in root.Methods)
            {
                declEmitter.EmitMethod(method);
            }

            foreach (var varDecl in root.Variables)
            {
                declEmitter.EmitPackageVar(varDecl);
            }

            foreach (var func in root.Functions)
            {
                bodyEmitter.EmitFunctionBody(func);
            }

            foreach (var method in root.Methods)
            {
                bodyEmitter.EmitMethodBody(method);
            }

            var initFuncs = new List<Ast.FunctionDeclaration>();
            foreach (var func in root.Functions)
            {
                if (func.Symbol.Name == "init")
                {
                    initFuncs.Add(func);
                }
            }

            if (root.Variables.Count > 0 || initFuncs.Count > 0)
            {
                bodyEmitter.EmitPackageInit(root.Variables, initFuncs);
            }

            ctx.PackageType.CreateType();
        }

        private static Dictionary<short, OpCode> BuildOpCodeMap()
        {
            var map = new Dictionary<short, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(OpCode))
                {
                    var opCode = (OpCode)field.GetValue(null)!;
                    map[opCode.Value] = opCode;
                }
            }
            return map;
        }

        private static bool IsShortBranch(byte op)
        {
            return OpCodeMap.TryGetValue(op, out var opCode) && opCode.OperandType == OperandType.ShortInlineBrTarget;
        }

        private static bool IsLongBranch(byte op)
        {
            return OpCodeMap.TryGetValue(op, out var opCode) && opCode.OperandType == OperandType.InlineBrTarget;
        }

        private static OpCode GetOpCode(byte op)
        {
            if (OpCodeMap.TryGetValue(op, out var opCode))
            {
                return opCode;
            }
            throw new InvalidOperationException($"LinkIL: unknown single-byte opcode 0x{op:X2}");
        }

        private static OpCode GetFEOpCode(byte op2)
        {
            short value = (short)(0xFE00 | op2);
            if (OpCodeMap.TryGetValue(value, out var opCode))
            {
                return opCode;
            }
            throw new InvalidOperationException($"LinkIL: unknown two-byte opcode 0xFE 0x{op2:X2}");
        }

        private static bool HasInlineToken(byte op)
        {
            if (!OpCodeMap.TryGetValue(op, out var opCode))
            {
                return false;
            }
            return opCode.OperandType == OperandType.InlineMethod
                || opCode.OperandType == OperandType.InlineField
                || opCode.OperandType == OperandType.InlineType
                || opCode.OperandType == OperandType.InlineString
                || opCode.OperandType == OperandType.InlineTok
                || opCode.OperandType == OperandType.InlineSig;
        }

        private static int GetOperandSize(byte op)
        {
            if (!OpCodeMap.TryGetValue(op, out var opCode))
            {
                return 0;
            }
            return opCode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineVar => 1,
                OperandType.ShortInlineI => 1,
                OperandType.ShortInlineBrTarget => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI => 4,
                OperandType.InlineBrTarget => 4,
                OperandType.InlineMethod => 4,
                OperandType.InlineField => 4,
                OperandType.InlineType => 4,
                OperandType.InlineString => 4,
                OperandType.InlineTok => 4,
                OperandType.InlineSig => 4,
                OperandType.ShortInlineR => 4,
                OperandType.InlineI8 => 8,
                OperandType.InlineR => 8,
                OperandType.InlineSwitch => -1,
                _ => 0
            };
        }

        private static int GetFEOperandSize(byte op2)
        {
            short value = (short)(0xFE00 | op2);
            if (!OpCodeMap.TryGetValue(value, out var opCode))
            {
                return 0;
            }
            return opCode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineVar => 1,
                OperandType.ShortInlineI => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI => 4,
                OperandType.InlineMethod => 4,
                OperandType.InlineField => 4,
                OperandType.InlineType => 4,
                OperandType.InlineTok => 4,
                _ => 0
            };
        }

        private static Type ResolveType(string typeName, Dictionary<string, TypeBuilder>? typeBuilders = null,
            Dictionary<string, Type>? genericParams = null)
        {
            if (genericParams != null && genericParams.TryGetValue(typeName, out var gp))
            {
                return gp;
            }

            if (typeBuilders != null && typeBuilders.TryGetValue(typeName, out var tb))
            {
                return tb;
            }

            if (typeBuilders != null && !typeName.Contains('.'))
            {
                foreach (var (key, builder) in typeBuilders)
                {
                    if (key.EndsWith("." + typeName))
                    {
                        return builder;
                    }
                }
            }

            if (typeName.EndsWith("[]"))
            {
                var elemType = ResolveType(typeName.Substring(0, typeName.Length - 2), typeBuilders, genericParams);
                return elemType.MakeArrayType();
            }

            if (typeName.EndsWith("&"))
            {
                var elemType = ResolveType(typeName.Substring(0, typeName.Length - 1), typeBuilders, genericParams);
                return elemType.MakeByRefType();
            }

            int bracketIndex = typeName.IndexOf('[');
            if (bracketIndex > 0 && typeName.EndsWith("]") && typeName.Contains('`'))
            {
                var genericDefName = typeName.Substring(0, bracketIndex);
                var argsStr = typeName.Substring(bracketIndex + 1, typeName.Length - bracketIndex - 2);
                var genericDef = ResolveType(genericDefName, typeBuilders, genericParams);

                var argNames = new List<string>();
                int depth = 0;
                int start = 0;
                for (int i = 0; i < argsStr.Length; i++)
                {
                    if (argsStr[i] == '[') { depth++; }
                    else if (argsStr[i] == ']') { depth--; }
                    else if (argsStr[i] == ',' && depth == 0)
                    {
                        argNames.Add(argsStr.Substring(start, i - start));
                        start = i + 1;
                    }
                }
                argNames.Add(argsStr.Substring(start));

                var typeArgs = new Type[argNames.Count];
                for (int i = 0; i < argNames.Count; i++)
                {
                    typeArgs[i] = ResolveType(argNames[i].Trim(), typeBuilders, genericParams);
                }

                return genericDef.MakeGenericType(typeArgs);
            }

            var type = Type.GetType(typeName);
            if (type != null) { return type; }

            type = typeName switch
            {
                "System.Void" => typeof(void),
                "System.Boolean" => typeof(bool),
                "System.Byte" => typeof(byte),
                "System.SByte" => typeof(sbyte),
                "System.Int16" => typeof(short),
                "System.UInt16" => typeof(ushort),
                "System.Int32" => typeof(int),
                "System.UInt32" => typeof(uint),
                "System.Int64" => typeof(long),
                "System.UInt64" => typeof(ulong),
                "System.Single" => typeof(float),
                "System.Double" => typeof(double),
                "System.String" => typeof(string),
                "System.Object" => typeof(object),
                "System.IntPtr" => typeof(IntPtr),
                "System.UIntPtr" => typeof(UIntPtr),
                "System.ValueType" => typeof(ValueType),
                _ => null
            };
            if (type != null) { return type; }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) { return type; }
            }

            if (typeName.Length <= 2 && char.IsUpper(typeName[0]))
            {
                return typeof(object);
            }

            throw new InvalidOperationException($"LinkIL: failed to resolve type '{typeName}'");
        }

        private static Type ResolveTypeWithGenericParams(string typeName,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, Type> genericParamMap)
        {
            if (genericParamMap.TryGetValue(typeName, out var genericParam))
            {
                return genericParam;
            }
            return ResolveType(typeName, typeBuilders, genericParamMap);
        }

        private sealed class MethodBodyData
        {
            public int MaxStack;
            public string[] LocalTypes = Array.Empty<string>();
            public byte[] ILBytes = Array.Empty<byte>();
            public List<TokenEntry> TokenEntries = new();
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
