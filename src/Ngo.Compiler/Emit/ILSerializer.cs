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
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Serializes and deserializes IL metadata (Section 2) and IL bytecode (Section 3)
    /// for .ngo archives. Handles token remapping when linking into a target module.
    /// </summary>
    internal static class ILSerializer
    {
        // Token reference kinds
        private const byte TokenKindType = 0;
        private const byte TokenKindMethod = 1;
        private const byte TokenKindField = 2;
        private const byte TokenKindString = 3;

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

        // ================================================================
        // IL Extraction: PE → Sections 2+3
        // ================================================================

        private static void ExtractIL(byte[] peBytes, BinaryWriter metaWriter, BinaryWriter codeWriter)
        {
            using var peStream = new MemoryStream(peBytes);
            using var peReader = new PEReader(peStream);
            var mdReader = peReader.GetMetadataReader();

            // Collect type definitions (skip <Module> and builtin error interface)
            var typeDefs = new List<TypeDefinitionHandle>();
            foreach (var typeHandle in mdReader.TypeDefinitions)
            {
                var typeDef = mdReader.GetTypeDefinition(typeHandle);
                var name = mdReader.GetString(typeDef.Name);
                if (name == "<Module>" || name == "error") continue;
                typeDefs.Add(typeHandle);
            }

            // Section 2: type definitions
            metaWriter.Write(typeDefs.Count);

            // Track method body indices
            var methodBodyIndex = 0;
            var methodBodies = new List<MethodBodyReference>();

            foreach (var typeHandle in typeDefs)
            {
                var typeDef = mdReader.GetTypeDefinition(typeHandle);
                var typeName = mdReader.GetString(typeDef.Name);
                var typeNs = mdReader.GetString(typeDef.Namespace);
                var fullTypeName = string.IsNullOrEmpty(typeNs) ? typeName : typeNs + "." + typeName;

                metaWriter.Write(fullTypeName);
                metaWriter.Write((int)typeDef.Attributes);

                // Base type
                if (!typeDef.BaseType.IsNil)
                    metaWriter.Write(ResolveTypeRef(mdReader, typeDef.BaseType));
                else
                    metaWriter.Write("");

                // Fields
                var fields = typeDef.GetFields();
                int fieldCount = 0;
                foreach (var _ in fields) fieldCount++;
                metaWriter.Write(fieldCount);

                foreach (var fieldHandle in fields)
                {
                    var field = mdReader.GetFieldDefinition(fieldHandle);
                    metaWriter.Write(mdReader.GetString(field.Name));
                    metaWriter.Write((int)field.Attributes);
                    metaWriter.Write(DecodeFieldType(mdReader, field));
                }

                // Methods
                var methods = typeDef.GetMethods();
                int methodCount = 0;
                foreach (var _ in methods) methodCount++;
                metaWriter.Write(methodCount);

                foreach (var methodHandle in methods)
                {
                    var method = mdReader.GetMethodDefinition(methodHandle);
                    var methodName = mdReader.GetString(method.Name);
                    metaWriter.Write(methodName);
                    metaWriter.Write((int)method.Attributes);

                    // Decode method signature
                    var sig = method.DecodeSignature(new TypeNameProvider(mdReader), null);
                    metaWriter.Write(sig.ReturnType);
                    metaWriter.Write(sig.ParameterTypes.Length);
                    foreach (var paramType in sig.ParameterTypes)
                        metaWriter.Write(paramType);

                    // IL body index (-1 if no body)
                    if (method.RelativeVirtualAddress > 0)
                    {
                        metaWriter.Write(methodBodyIndex);
                        methodBodies.Add(new MethodBodyReference(methodHandle, methodBodyIndex));
                        methodBodyIndex++;
                    }
                    else
                    {
                        metaWriter.Write(-1);
                    }
                }
            }

            // Section 3: method bodies
            codeWriter.Write(methodBodies.Count);

            foreach (var bodyRef in methodBodies)
            {
                var method = mdReader.GetMethodDefinition(bodyRef.Handle);
                var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
                var ilBytes = body.GetILBytes()!;

                // Max stack
                codeWriter.Write(body.MaxStack);

                // Local variables
                if (!body.LocalSignature.IsNil)
                {
                    var localSig = mdReader.GetStandaloneSignature(body.LocalSignature);
                    var locals = localSig.DecodeLocalSignature(new TypeNameProvider(mdReader), null);
                    codeWriter.Write(locals.Length);
                    foreach (var local in locals)
                        codeWriter.Write(local);
                }
                else
                {
                    codeWriter.Write(0);
                }

                // IL bytes
                codeWriter.Write(ilBytes.Length);
                codeWriter.Write(ilBytes);

                // Token table: scan IL for embedded metadata tokens
                var tokenEntries = ScanTokens(ilBytes, mdReader);
                codeWriter.Write(tokenEntries.Count);
                foreach (var entry in tokenEntries)
                {
                    codeWriter.Write(entry.Offset);
                    codeWriter.Write(entry.Kind);
                    codeWriter.Write(entry.Reference);
                }

                // Exception handlers
                var handlers = body.ExceptionRegions;
                codeWriter.Write(handlers.Length);
                foreach (var handler in handlers)
                {
                    codeWriter.Write((int)handler.Kind);
                    codeWriter.Write(handler.TryOffset);
                    codeWriter.Write(handler.TryLength);
                    codeWriter.Write(handler.HandlerOffset);
                    codeWriter.Write(handler.HandlerLength);
                    codeWriter.Write(handler.FilterOffset);
                    if (handler.Kind == ExceptionRegionKind.Catch && !handler.CatchType.IsNil)
                        codeWriter.Write(ResolveTypeRef(mdReader, handler.CatchType));
                    else
                        codeWriter.Write("");
                }
            }
        }

        // ================================================================
        // IL Linking: Sections 2+3 → target ModuleBuilder
        // ================================================================

        private static void LinkIL(byte[] ilMetaBytes, byte[] ilCodeBytes, PackageSymbol pkg, EmitContext ctx)
        {
            using var metaStream = new MemoryStream(ilMetaBytes);
            using var codeStream = new MemoryStream(ilCodeBytes);
            var metaReader = new BinaryReader(metaStream);
            var codeReader = new BinaryReader(codeStream);

            int typeCount = metaReader.ReadInt32();

            // Maps from serialized type/method names to builders
            var typeBuilders = new Dictionary<string, TypeBuilder>();
            var fieldBuilders = new Dictionary<string, FieldBuilder>();
            var methodBuilders = new Dictionary<string, MethodBuilder>();
            var methodILIndices = new Dictionary<string, int>(); // method fullname → body index

            // Pass 1a: Create all TypeBuilders and FieldBuilders first
            // We need all types defined before resolving method signatures,
            // since methods may reference types defined later in the metadata.
            var typeInfos = new List<DeserializedTypeInfo>();

            for (int t = 0; t < typeCount; t++)
            {
                var fullTypeName = metaReader.ReadString();
                var typeAttrs = (TypeAttributes)metaReader.ReadInt32();
                var baseTypeName = metaReader.ReadString();

                TypeBuilder tb;
                bool isStaticClass = (typeAttrs & TypeAttributes.Abstract) != 0
                    && (typeAttrs & TypeAttributes.Sealed) != 0;

                if (isStaticClass)
                {
                    tb = ((LiveModuleBuilder)ctx.Module).Inner.DefineType(fullTypeName, typeAttrs);
                }
                else
                {
                    var parent = !string.IsNullOrEmpty(baseTypeName)
                        ? ResolveType(baseTypeName)
                        : typeof(ValueType);
                    tb = ((LiveModuleBuilder)ctx.Module).Inner.DefineType(fullTypeName, typeAttrs, parent);
                }

                typeBuilders[fullTypeName] = tb;

                // Fields
                int fieldCount = metaReader.ReadInt32();
                for (int f = 0; f < fieldCount; f++)
                {
                    var fieldName = metaReader.ReadString();
                    var fieldAttrs = (FieldAttributes)metaReader.ReadInt32();
                    var fieldTypeName = metaReader.ReadString();
                    var fieldType = ResolveType(fieldTypeName, typeBuilders);
                    var fb = tb.DefineField(fieldName, fieldType, fieldAttrs);
                    fieldBuilders[fullTypeName + "." + fieldName] = fb;

                    // Register in EmitContext for struct field access
                    foreach (var (_, sym) in pkg.Exports)
                    {
                        if (sym is StructTypeSymbol structSym && structSym.Name == tb.Name)
                        {
                            foreach (var fSym in structSym.Fields)
                            {
                                if (fSym.Name == fieldName)
                                    ctx.StructFields[fSym] = new LiveFieldBuilder(fb);
                            }
                        }
                    }
                }

                // Read method metadata but defer defining them
                int methodCount = metaReader.ReadInt32();
                var methodInfos = new List<SerializedMethodInfo>(methodCount);
                for (int m = 0; m < methodCount; m++)
                {
                    var methodName = metaReader.ReadString();
                    var methodAttrs = (MethodAttributes)metaReader.ReadInt32();
                    var returnTypeName = metaReader.ReadString();
                    int paramCount = metaReader.ReadInt32();
                    var paramTypeNames = new string[paramCount];
                    for (int p = 0; p < paramCount; p++)
                        paramTypeNames[p] = metaReader.ReadString();
                    var bodyIndex = metaReader.ReadInt32();
                    methodInfos.Add(new SerializedMethodInfo(methodName, methodAttrs, returnTypeName, paramTypeNames, bodyIndex, Array.Empty<string>()));
                }

                // Read method overrides
                var overrides = new List<SerializedMethodOverride>();
                int overrideCount = metaReader.ReadInt32();
                for (int o = 0; o < overrideCount; o++)
                {
                    var bodyMethodName = metaReader.ReadString();
                    var declTypeName = metaReader.ReadString();
                    var declMethodName = metaReader.ReadString();
                    overrides.Add(new SerializedMethodOverride(bodyMethodName, declTypeName, declMethodName));
                }

                typeInfos.Add(new DeserializedTypeInfo(fullTypeName, tb, methodCount, methodInfos, overrides));
            }

            // Pass 1b: Now define all methods (all type builders exist)
            foreach (var typeInfo in typeInfos)
            {
                foreach (var methodInfo in typeInfo.Methods)
                {
                    var returnType = ResolveType(methodInfo.ReturnTypeName, typeBuilders);
                    var paramTypes = new Type[methodInfo.ParamTypeNames.Length];
                    for (int p = 0; p < methodInfo.ParamTypeNames.Length; p++)
                    {
                        paramTypes[p] = ResolveType(methodInfo.ParamTypeNames[p], typeBuilders);
                    }

                    var mb = typeInfo.TypeBuilder.DefineMethod(methodInfo.MethodName, methodInfo.Attributes, returnType, paramTypes);
                    var methodKey = typeInfo.FullTypeName + "." + methodInfo.MethodName;
                    methodBuilders[methodKey] = mb;

                    if (methodInfo.BodyIndex >= 0)
                    {
                        methodILIndices[methodKey] = methodInfo.BodyIndex;
                    }

                    // Register exported functions in CachedMethods for the emitter
                    foreach (var (_, sym) in pkg.Exports)
                    {
                        if (sym is FunctionSymbol funcSym && funcSym.Name == methodInfo.MethodName)
                            ctx.CachedMethods[funcSym] = mb;
                    }
                }
            }

            // Pass 1c: Apply method overrides (all methods now exist)
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

            // Finalize struct types and register in mapper
            foreach (var (fullName, tb) in typeBuilders)
            {
                bool isStaticClass = (tb.Attributes & TypeAttributes.Abstract) != 0
                    && (tb.Attributes & TypeAttributes.Sealed) != 0;
                if (!isStaticClass)
                {
                    var runtimeType = tb.CreateType()!;
                    // Register in mapper using the PackageSymbol's type symbols
                    foreach (var (_, sym) in pkg.Exports)
                    {
                        if (sym is StructTypeSymbol structSym && structSym.Name == tb.Name)
                        {
                            ctx.Mapper.Register(structSym, runtimeType);
                            ctx.FinalizedTypes.Add(structSym);
                        }
                    }
                }
            }

            // Pass 2: Set method IL bodies with remapped tokens
            int bodyCount = codeReader.ReadInt32();
            var bodies = new List<MethodBodyData>(bodyCount);

            for (int b = 0; b < bodyCount; b++)
            {
                var bodyData = new MethodBodyData();
                bodyData.MaxStack = codeReader.ReadInt32();

                int localCount = codeReader.ReadInt32();
                bodyData.LocalTypes = new string[localCount];
                for (int l = 0; l < localCount; l++)
                    bodyData.LocalTypes[l] = codeReader.ReadString();

                int ilLen = codeReader.ReadInt32();
                bodyData.ILBytes = codeReader.ReadBytes(ilLen);

                int tokenCount = codeReader.ReadInt32();
                bodyData.TokenEntries = new List<TokenEntry>(tokenCount);
                for (int te = 0; te < tokenCount; te++)
                {
                    bodyData.TokenEntries.Add(new TokenEntry(
                        codeReader.ReadInt32(),
                        codeReader.ReadByte(),
                        codeReader.ReadString()));
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

            // Apply IL bodies to MethodBuilders
            foreach (var (methodKey, bodyIndex) in methodILIndices)
            {
                if (bodyIndex >= bodies.Count || !methodBuilders.TryGetValue(methodKey, out var mb))
                    continue;

                var bodyData = bodies[bodyIndex];

                // Replay IL through ILGenerator with token resolution
                ReplayIL(mb, bodyData.ILBytes, bodyData.LocalTypes, bodyData.TokenEntries,
                    bodyData.ExceptionHandlers, typeBuilders, methodBuilders, fieldBuilders, ctx);
            }

            // Finalize the static package class
            foreach (var (_, tb) in typeBuilders)
            {
                bool isStaticClass = (tb.Attributes & TypeAttributes.Abstract) != 0
                    && (tb.Attributes & TypeAttributes.Sealed) != 0;
                if (isStaticClass)
                    tb.CreateType();
            }
        }

        // ================================================================
        // IL Replayer: re-emits IL bytes through ILGenerator
        // ================================================================

        private static void ReplayIL(MethodBuilder mb, byte[] il, string[] localTypeNames,
            List<TokenEntry> tokenEntries, List<ExceptionHandlerData> exceptionHandlers,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, MethodBuilder> methodBuilders,
            Dictionary<string, FieldBuilder> fieldBuilders,
            EmitContext ctx)
        {
            var ilGen = mb.GetILGenerator();

            // Declare locals
            foreach (var localTypeName in localTypeNames)
            {
                var localType = ResolveType(localTypeName, typeBuilders);
                ilGen.DeclareLocal(localType);
            }

            // Build token lookup: offset → (kind, reference)
            var tokenMap = new Dictionary<int, TokenReference>();
            foreach (var entry in tokenEntries)
            {
                tokenMap[entry.Offset] = new TokenReference(entry.Kind, entry.Reference);
            }

            // Pre-scan for branch targets and create labels
            var labels = new Dictionary<int, Label>();
            PreScanBranchTargets(il, labels, ilGen);

            // Replay each instruction
            int i = 0;
            while (i < il.Length)
            {
                // Mark label if this offset is a branch target
                if (labels.TryGetValue(i, out var targetLabel))
                    ilGen.MarkLabel(targetLabel);

                byte op = il[i++];

                // Two-byte prefix
                if (op == 0xFE && i < il.Length)
                {
                    byte op2 = il[i++];
                    ReplayFEOpcode(op2, il, ref i, ilGen, tokenMap, typeBuilders, methodBuilders, fieldBuilders, labels, ctx);
                    continue;
                }

                // Token-bearing opcodes
                if (HasInlineToken(op) && i + 4 <= il.Length)
                {
                    int tokenOffset = i;
                    i += 4; // skip token bytes

                    if (op == 0x72) // ldstr
                    {
                        if (tokenMap.TryGetValue(tokenOffset, out var strRef) && strRef.Kind == TokenKindString)
                            ilGen.Emit(OpCodes.Ldstr, strRef.Reference);
                        else
                            ilGen.Emit(OpCodes.Ldnull); // fallback
                        continue;
                    }

                    var opCode = GetOpCode(op);
                    if (tokenMap.TryGetValue(tokenOffset, out var tok))
                    {
                        EmitTokenOpcode(ilGen, opCode, tok.Kind, tok.Reference, typeBuilders, methodBuilders, fieldBuilders, ctx);
                    }
                    continue;
                }

                // Branch opcodes (short form — 1 byte offset)
                if (IsShortBranch(op))
                {
                    var offset = (sbyte)il[i++];
                    var target = i + offset;
                    var brLabel = GetOrCreateLabel(labels, target, ilGen);
                    ilGen.Emit(GetOpCode(op), brLabel);
                    continue;
                }

                // Branch opcodes (long form — 4 byte offset)
                if (IsLongBranch(op))
                {
                    var offset = BitConverter.ToInt32(il, i);
                    i += 4;
                    var target = i + offset;
                    var brLabel = GetOrCreateLabel(labels, target, ilGen);
                    ilGen.Emit(GetOpCode(op), brLabel);
                    continue;
                }

                // Switch
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

                // Inline int8
                if (op == 0x0E || op == 0x0F || op == 0x10 || op == 0x11 || op == 0x12 || op == 0x13)
                {
                    ilGen.Emit(GetOpCode(op), il[i++]);
                    continue;
                }
                if (op == 0x1F) // ldc.i4.s
                {
                    ilGen.Emit(OpCodes.Ldc_I4_S, (sbyte)il[i++]);
                    continue;
                }

                // Inline int32 (non-token)
                if (op == 0x20) // ldc.i4
                {
                    ilGen.Emit(OpCodes.Ldc_I4, BitConverter.ToInt32(il, i));
                    i += 4;
                    continue;
                }

                // Inline int64
                if (op == 0x21)
                {
                    ilGen.Emit(OpCodes.Ldc_I8, BitConverter.ToInt64(il, i));
                    i += 8;
                    continue;
                }

                // Inline float32
                if (op == 0x22)
                {
                    ilGen.Emit(OpCodes.Ldc_R4, BitConverter.ToSingle(il, i));
                    i += 4;
                    continue;
                }

                // Inline float64
                if (op == 0x23)
                {
                    ilGen.Emit(OpCodes.Ldc_R8, BitConverter.ToDouble(il, i));
                    i += 8;
                    continue;
                }

                // leave.s (1 byte offset)
                if (op == 0xDE)
                {
                    var offset = (sbyte)il[i++];
                    var target = i + offset;
                    var brLabel = GetOrCreateLabel(labels, target, ilGen);
                    ilGen.Emit(OpCodes.Leave_S, brLabel);
                    continue;
                }

                // leave (4 byte offset)
                if (op == 0xDD)
                {
                    var offset = BitConverter.ToInt32(il, i);
                    i += 4;
                    var target = i + offset;
                    var brLabel = GetOrCreateLabel(labels, target, ilGen);
                    ilGen.Emit(OpCodes.Leave, brLabel);
                    continue;
                }

                // Simple no-operand opcodes
                ilGen.Emit(GetOpCode(op));
            }
        }

        private static void ReplayFEOpcode(byte op2, byte[] il, ref int i, ILGenerator ilGen,
            Dictionary<int, TokenReference> tokenMap,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, MethodBuilder> methodBuilders,
            Dictionary<string, FieldBuilder> fieldBuilders,
            Dictionary<int, Label> labels, EmitContext ctx)
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
                        var type = ResolveTypeReference(tok.Reference, typeBuilders, ctx);
                        if (type != null)
                            ilGen.Emit(opCode, type);
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
                        var method = ResolveMethodReference(tok.Reference, typeBuilders, methodBuilders, ctx);
                        if (method is MethodInfo mi)
                            ilGen.Emit(opCode, mi);
                    }
                    i += 4;
                    break;
                }
                case 0x09: case 0x0A: case 0x0B: // ldarg, ldarga, starg
                case 0x0C: case 0x0D: case 0x0E: // ldloc, ldloca, stloc
                    ilGen.Emit(GetFEOpCode(op2), BitConverter.ToInt16(il, i));
                    i += 2;
                    break;
                case 0x12: // unaligned.
                    ilGen.Emit(OpCodes.Unaligned, il[i++]);
                    break;
                default:
                    ilGen.Emit(GetFEOpCode(op2));
                    break;
            }
        }

        private static void EmitTokenOpcode(ILGenerator ilGen, OpCode opCode, byte kind, string reference,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, MethodBuilder> methodBuilders,
            Dictionary<string, FieldBuilder> fieldBuilders,
            EmitContext ctx)
        {
            switch (kind)
            {
                case TokenKindType:
                {
                    var type = ResolveTypeReference(reference, typeBuilders, ctx);
                    if (type != null) ilGen.Emit(opCode, type);
                    break;
                }
                case TokenKindMethod:
                {
                    var method = ResolveMethodReference(reference, typeBuilders, methodBuilders, ctx);
                    if (method != null)
                    {
                        if (method is ConstructorInfo ctor)
                            ilGen.Emit(opCode, ctor);
                        else
                            ilGen.Emit(opCode, (MethodInfo)method);
                    }
                    break;
                }
                case TokenKindField:
                {
                    var field = ResolveFieldReference(reference, typeBuilders, fieldBuilders, ctx);
                    if (field != null) ilGen.Emit(opCode, field);
                    break;
                }
            }
        }

        private static Type? ResolveTypeReference(string reference,
            Dictionary<string, TypeBuilder> typeBuilders, EmitContext ctx)
        {
            if (typeBuilders.TryGetValue(reference, out var tb))
                return tb;
            return ResolveType(reference, typeBuilders);
        }

        private static MethodBase? ResolveMethodReference(string reference,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, MethodBuilder> methodBuilders,
            EmitContext ctx)
        {
            if (methodBuilders.TryGetValue(reference, out var mb))
                return mb;

            var parts = reference.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (parts.Length != 2) return null;

            // methodBuilders keys use "." separator, references use "::"
            var dotKey = parts[0] + "." + parts[1];
            if (methodBuilders.TryGetValue(dotKey, out mb))
                return mb;

            var type = ResolveType(parts[0], typeBuilders);
            if (type == null) return null;

            // TypeBuilder types can't use GetMethod/GetConstructor before finalization
            if (type is TypeBuilder)
            {
                // Search methodBuilders for any method on this type
                var prefix = parts[0] + ".";
                foreach (var (key, method) in methodBuilders)
                {
                    if (key.StartsWith(prefix) && key.Substring(prefix.Length) == parts[1])
                        return method;
                }
                return null;
            }

            if (parts[1] == ".ctor")
            {
                var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return ctors.Length > 0 ? ctors[0] : null;
            }
            if (parts[1] == ".cctor")
            {
                return type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            }

            return type.GetMethod(parts[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }

        private static FieldInfo? ResolveFieldReference(string reference,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, FieldBuilder> fieldBuilders,
            EmitContext ctx)
        {
            if (fieldBuilders.TryGetValue(reference, out var fb))
                return fb;

            var parts = reference.Split(new[] { "::" }, 2, StringSplitOptions.None);
            if (parts.Length != 2) return null;

            // fieldBuilders keys use "." separator, references use "::"
            var dotKey = parts[0] + "." + parts[1];
            if (fieldBuilders.TryGetValue(dotKey, out fb))
                return fb;

            var type = ResolveType(parts[0], typeBuilders);
            if (type == null) return null;

            // TypeBuilder types can't use GetField before finalization
            if (type is TypeBuilder)
            {
                var prefix = parts[0] + ".";
                foreach (var (key, field) in fieldBuilders)
                {
                    if (key.StartsWith(prefix) && key.Substring(prefix.Length) == parts[1])
                        return field;
                }
                return null;
            }

            return type.GetField(parts[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
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
                        labels[target] = ilGen.DefineLabel();
                    continue;
                }

                if (IsLongBranch(op))
                {
                    var offset = BitConverter.ToInt32(il, i);
                    i += 4;
                    var target = i + offset;
                    if (!labels.ContainsKey(target))
                        labels[target] = ilGen.DefineLabel();
                    continue;
                }

                if (op == 0x45) // switch
                {
                    int count = BitConverter.ToInt32(il, i);
                    i += 4;
                    int baseOffset = i + count * 4;
                    for (int s = 0; s < count; s++)
                    {
                        int target = baseOffset + BitConverter.ToInt32(il, i);
                        i += 4;
                        if (!labels.ContainsKey(target))
                            labels[target] = ilGen.DefineLabel();
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

        private static readonly Dictionary<short, OpCode> OpCodeMap = BuildOpCodeMap();

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

        private static OpCode DEAD_GetOpCode(byte op)
        {
            return op switch
            {
                0x00 => OpCodes.Nop, 0x01 => OpCodes.Break,
                0x02 => OpCodes.Ldarg_0, 0x03 => OpCodes.Ldarg_1, 0x04 => OpCodes.Ldarg_2, 0x05 => OpCodes.Ldarg_3,
                0x06 => OpCodes.Ldloc_0, 0x07 => OpCodes.Ldloc_1, 0x08 => OpCodes.Ldloc_2, 0x09 => OpCodes.Ldloc_3,
                0x0A => OpCodes.Stloc_0, 0x0B => OpCodes.Stloc_1, 0x0C => OpCodes.Stloc_2, 0x0D => OpCodes.Stloc_3,
                0x0E => OpCodes.Ldarg_S, 0x0F => OpCodes.Ldarga_S, 0x10 => OpCodes.Starg_S,
                0x11 => OpCodes.Ldloc_S, 0x12 => OpCodes.Ldloca_S, 0x13 => OpCodes.Stloc_S,
                0x14 => OpCodes.Ldnull,
                0x15 => OpCodes.Ldc_I4_M1,
                0x16 => OpCodes.Ldc_I4_0, 0x17 => OpCodes.Ldc_I4_1, 0x18 => OpCodes.Ldc_I4_2, 0x19 => OpCodes.Ldc_I4_3,
                0x1A => OpCodes.Ldc_I4_4, 0x1B => OpCodes.Ldc_I4_5, 0x1C => OpCodes.Ldc_I4_6, 0x1D => OpCodes.Ldc_I4_7, 0x1E => OpCodes.Ldc_I4_8,
                0x1F => OpCodes.Ldc_I4_S, 0x20 => OpCodes.Ldc_I4,
                0x21 => OpCodes.Ldc_I8, 0x22 => OpCodes.Ldc_R4, 0x23 => OpCodes.Ldc_R8,
                0x25 => OpCodes.Dup, 0x26 => OpCodes.Pop,
                0x27 => OpCodes.Jmp,
                0x28 => OpCodes.Call, 0x29 => OpCodes.Calli, 0x2A => OpCodes.Ret,
                0x2B => OpCodes.Br_S, 0x2C => OpCodes.Brfalse_S, 0x2D => OpCodes.Brtrue_S,
                0x2E => OpCodes.Beq_S, 0x2F => OpCodes.Bge_S,
                0x30 => OpCodes.Bgt_S, 0x31 => OpCodes.Ble_S, 0x32 => OpCodes.Blt_S,
                0x33 => OpCodes.Bne_Un_S, 0x34 => OpCodes.Bge_Un_S, 0x35 => OpCodes.Bgt_Un_S,
                0x36 => OpCodes.Ble_Un_S, 0x37 => OpCodes.Blt_Un_S,
                0x38 => OpCodes.Br, 0x39 => OpCodes.Brfalse, 0x3A => OpCodes.Brtrue,
                0x3B => OpCodes.Beq, 0x3C => OpCodes.Bge, 0x3D => OpCodes.Bgt,
                0x3E => OpCodes.Ble, 0x3F => OpCodes.Blt,
                0x40 => OpCodes.Bne_Un, 0x41 => OpCodes.Bge_Un, 0x42 => OpCodes.Bgt_Un,
                0x43 => OpCodes.Ble_Un, 0x44 => OpCodes.Blt_Un,
                0x45 => OpCodes.Switch,
                0x46 => OpCodes.Ldind_I1, 0x47 => OpCodes.Ldind_U1, 0x48 => OpCodes.Ldind_I2,
                0x49 => OpCodes.Ldind_U2, 0x4A => OpCodes.Ldind_I4, 0x4B => OpCodes.Ldind_U4,
                0x4C => OpCodes.Ldind_I8, 0x4D => OpCodes.Ldind_I, 0x4E => OpCodes.Ldind_R4,
                0x4F => OpCodes.Ldind_R8, 0x50 => OpCodes.Ldind_Ref,
                0x51 => OpCodes.Stind_Ref, 0x52 => OpCodes.Stind_I1, 0x53 => OpCodes.Stind_I2,
                0x54 => OpCodes.Stind_I4, 0x55 => OpCodes.Stind_I8, 0x56 => OpCodes.Stind_R4,
                0x57 => OpCodes.Stind_R8,
                0x58 => OpCodes.Add, 0x59 => OpCodes.Sub, 0x5A => OpCodes.Mul, 0x5B => OpCodes.Div,
                0x5C => OpCodes.Div_Un, 0x5D => OpCodes.Rem, 0x5E => OpCodes.Rem_Un,
                0x5F => OpCodes.And, 0x60 => OpCodes.Or, 0x61 => OpCodes.Xor,
                0x62 => OpCodes.Shl, 0x63 => OpCodes.Shr, 0x64 => OpCodes.Shr_Un,
                0x65 => OpCodes.Neg, 0x66 => OpCodes.Not,
                0x67 => OpCodes.Conv_I1, 0x68 => OpCodes.Conv_I2, 0x69 => OpCodes.Conv_I4, 0x6A => OpCodes.Conv_I8,
                0x6B => OpCodes.Conv_R4, 0x6C => OpCodes.Conv_R8,
                0x6D => OpCodes.Conv_U4, 0x6E => OpCodes.Conv_U8,
                0x6F => OpCodes.Callvirt, 0x70 => OpCodes.Cpobj, 0x71 => OpCodes.Ldobj,
                0x72 => OpCodes.Ldstr, 0x73 => OpCodes.Newobj,
                0x74 => OpCodes.Castclass, 0x75 => OpCodes.Isinst,
                0x76 => OpCodes.Conv_R_Un,
                0x79 => OpCodes.Unbox,
                0x7A => OpCodes.Throw,
                0x7B => OpCodes.Ldfld, 0x7C => OpCodes.Ldflda, 0x7D => OpCodes.Stfld,
                0x7E => OpCodes.Ldsfld, 0x7F => OpCodes.Ldsflda, 0x80 => OpCodes.Stsfld,
                0x81 => OpCodes.Stobj,
                0x8C => OpCodes.Box, 0x8D => OpCodes.Newarr,
                0x8E => OpCodes.Ldlen, 0x8F => OpCodes.Ldelema,
                0x90 => OpCodes.Ldelem_I1, 0x91 => OpCodes.Ldelem_U1, 0x92 => OpCodes.Ldelem_I2,
                0x93 => OpCodes.Ldelem_U2, 0x94 => OpCodes.Ldelem_I4, 0x95 => OpCodes.Ldelem_U4,
                0x96 => OpCodes.Ldelem_I8, 0x97 => OpCodes.Ldelem_I, 0x98 => OpCodes.Ldelem_R4,
                0x99 => OpCodes.Ldelem_R8, 0x9A => OpCodes.Ldelem_Ref,
                0x9B => OpCodes.Stelem_I, 0x9C => OpCodes.Stelem_I1, 0x9D => OpCodes.Stelem_I2,
                0x9E => OpCodes.Stelem_I4, 0x9F => OpCodes.Stelem_I8, 0xA0 => OpCodes.Stelem_R4,
                0xA1 => OpCodes.Stelem_R8, 0xA2 => OpCodes.Stelem_Ref,
                0xA3 => OpCodes.Ldelem, 0xA4 => OpCodes.Stelem, 0xA5 => OpCodes.Unbox_Any,
                0xC6 => OpCodes.Mkrefany,
                0xD0 => OpCodes.Ldtoken,
                0xD3 => OpCodes.Conv_U2, 0xD4 => OpCodes.Conv_U1, 0xD5 => OpCodes.Conv_I,
                0xD6 => OpCodes.Conv_Ovf_I, 0xD7 => OpCodes.Conv_Ovf_U,
                0xD8 => OpCodes.Add_Ovf, 0xD9 => OpCodes.Add_Ovf_Un,
                0xDA => OpCodes.Mul_Ovf, 0xDB => OpCodes.Mul_Ovf_Un,
                0xDC => OpCodes.Sub_Ovf, 0xDD => OpCodes.Leave,
                0xDE => OpCodes.Leave_S,
                0xE0 => OpCodes.Conv_U,
                _ => OpCodes.Nop
            };
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

        // ================================================================
        // Emit helper — same as AssemblyEmitter.EmitPackage but accessible
        // ================================================================

        /// <summary>
        /// Runs the standard 3-pass emit using NgoModuleBuilder.
        /// NgoMethodBuilder.GetILWriter() returns NgoWriter, so IL is captured automatically.
        /// </summary>
        private static void EmitPackageForSerialization(Ast.SourceFile root, EmitContext ctx)
        {
            var packageName = root.Package.Symbol.Name;

            ctx.PackageType = ctx.Module.DefineType(
                ctx.QualifyName(packageName),
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            var declEmitter = new DeclarationEmitter(ctx);
            ctx.DeclEmitter = declEmitter;
            var bodyEmitter = new MethodBodyEmitter(ctx);

            // Pass 1a: Forward-declare types
            foreach (var typeDecl in root.Types)
            {
                try
                {
                    if (typeDecl.Symbol is StructTypeSymbol structType)
                        declEmitter.DefineStructType(structType);
                    else if (typeDecl.Symbol is InterfaceTypeSymbol)
                        declEmitter.EmitTypeDeclaration(typeDecl);
                }
                catch (Exception) { }
            }

            // Pass 1b: Populate struct fields
            foreach (var typeDecl in root.Types)
            {
                try
                {
                    if (typeDecl.Symbol is StructTypeSymbol structType)
                        declEmitter.PopulateStructFields(structType);
                }
                catch (Exception) { }
            }

            // Finalize struct/interface types
            foreach (var kvp in ctx.StructTypes)
            {
                if (!ctx.FinalizedTypes.Contains(kvp.Key))
                {
                    try
                    {
                        var runtimeType = kvp.Value.CreateType()!;
                        ctx.Mapper.Register(kvp.Key, runtimeType);
                        ctx.FinalizedTypes.Add(kvp.Key);
                    }
                    catch (Exception) { }
                }
            }

            foreach (var kvp in ctx.InterfaceTypes)
            {
                if (!ctx.FinalizedTypes.Contains(kvp.Key))
                {
                    try
                    {
                        var runtimeType = kvp.Value.CreateType()!;
                        ctx.Mapper.Register(kvp.Key, runtimeType);
                        ctx.FinalizedTypes.Add(kvp.Key);
                    }
                    catch (Exception) { }
                }
            }

            // Pass 2: Define function/method signatures
            foreach (var func in root.Functions)
            {
                try { declEmitter.EmitFunction(func); }
                catch (Exception) { }
            }

            foreach (var method in root.Methods)
            {
                try { declEmitter.EmitMethod(method); }
                catch (Exception) { }
            }

            foreach (var varDecl in root.Variables)
            {
                try { declEmitter.EmitPackageVar(varDecl); }
                catch (Exception) { }
            }

            // Pass 3: Emit bodies — NgoMethodBuilder.GetILWriter() returns NgoWriter
            // Each body is emitted independently; failures produce a minimal stub body.
            foreach (var func in root.Functions)
            {
                try
                {
                    bodyEmitter.EmitFunctionBody(func);
                }
                catch (Exception)
                {
                    EmitStubBody(ctx, func.Symbol);
                }
            }

            foreach (var method in root.Methods)
            {
                try
                {
                    bodyEmitter.EmitMethodBody(method);
                }
                catch (Exception)
                {
                    EmitStubBody(ctx, method.Symbol);
                }
            }

            // init() + package var init
            var initFuncs = new List<Ast.FunctionDeclaration>();
            foreach (var func in root.Functions)
            {
                if (func.Symbol.Name == "init")
                    initFuncs.Add(func);
            }

            if (root.Variables.Count > 0 || initFuncs.Count > 0)
                bodyEmitter.EmitPackageInit(root.Variables, initFuncs);

            ctx.PackageType.CreateType();
        }

        private static void EmitStubBody(EmitContext ctx, Symbols.FunctionSymbol func)
        {
            if (!ctx.Methods.TryGetValue(func, out var method))
            {
                return;
            }
            var il = method.GetILWriter();
            ctx.ResetMethodState();
            if (func.ReturnType != Symbols.BuiltinTypes.Void && func.ReturnTypes.Count > 0)
            {
                var clrRetType = ctx.Mapper.MapReturnType(func.ReturnTypes);
                if (clrRetType.IsValueType)
                {
                    var local = il.DeclareLocal(clrRetType);
                    il.Emit(System.Reflection.Emit.OpCodes.Ldloca, local);
                    il.Emit(System.Reflection.Emit.OpCodes.Initobj, clrRetType);
                    il.Emit(System.Reflection.Emit.OpCodes.Ldloc, local);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Ldnull);
                }
            }
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
        }

        private static void EmitStubBody(EmitContext ctx, Symbols.MethodSymbol method)
        {
            var funcSym = new Symbols.FunctionSymbol(method.Name,
                method.Parameters, method.ReturnTypes, method.IsVariadic);
            if (ctx.Methods.TryGetValue(method, out var mb))
            {
                ctx.Methods[funcSym] = mb;
            }
            else
            {
                foreach (var kvp in ctx.Methods)
                {
                    if (kvp.Key.Name == method.Name)
                    {
                        mb = kvp.Value;
                        break;
                    }
                }
                if (mb == null)
                {
                    return;
                }
            }
            var il = mb.GetILWriter();
            ctx.ResetMethodState();
            if (method.ReturnType != Symbols.BuiltinTypes.Void && method.ReturnTypes.Count > 0)
            {
                var clrRetType = ctx.Mapper.MapReturnType(method.ReturnTypes);
                if (clrRetType.IsValueType)
                {
                    var local = il.DeclareLocal(clrRetType);
                    il.Emit(System.Reflection.Emit.OpCodes.Ldloca, local);
                    il.Emit(System.Reflection.Emit.OpCodes.Initobj, clrRetType);
                    il.Emit(System.Reflection.Emit.OpCodes.Ldloc, local);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Ldnull);
                }
            }
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
        }

        // ================================================================
        // Token scanning
        // ================================================================

        /// <summary>
        /// Scans IL bytes for opcodes that embed metadata tokens and resolves
        /// each token to a symbolic reference string.
        /// </summary>
        private static List<TokenEntry> ScanTokens(
            byte[] il, MetadataReader mdReader)
        {
            var entries = new List<TokenEntry>();
            int i = 0;

            while (i < il.Length)
            {
                int opcodeStart = i;
                byte op = il[i++];

                if (op == 0xFE && i < il.Length)
                {
                    // Two-byte opcode prefix
                    byte op2 = il[i++];
                    switch (op2)
                    {
                        case 0x15: // initobj
                        case 0x16: // constrained
                        case 0x1C: // sizeof
                            if (i + 4 <= il.Length)
                            {
                                var token = BitConverter.ToInt32(il, i);
                                var resolved = ResolveMetadataToken(mdReader, token);
                                if (resolved != null)
                                    entries.Add(new TokenEntry(i, resolved.Kind, resolved.Reference));
                                i += 4;
                            }
                            break;
                        default:
                            // Other FE-prefixed opcodes: mostly no inline operand or short operands
                            i += GetFEOperandSize(op2);
                            break;
                    }
                    continue;
                }

                // Single-byte opcodes with inline metadata tokens (4 bytes)
                if (HasInlineToken(op))
                {
                    if (i + 4 <= il.Length)
                    {
                        var token = BitConverter.ToInt32(il, i);
                        var resolved = ResolveMetadataToken(mdReader, token);
                        if (resolved != null)
                            entries.Add(new TokenEntry(i, resolved.Kind, resolved.Reference));
                        i += 4;
                    }
                    continue;
                }

                // Skip operand bytes for non-token opcodes
                i += GetOperandSize(op);
            }

            return entries;
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

        // dead code below — old hand-maintained switch, kept until verified safe to delete
        #pragma warning disable CS0162
        private static bool DEAD_HasInlineToken(byte op) => op switch
        {
            0x28 => true, // call
            0x29 => true, // calli
            0x6F => true, // callvirt
            0x70 => true, // cpobj
            0x71 => true, // ldobj
            0x72 => true, // ldstr
            0x73 => true, // newobj
            0x74 => true, // castclass
            0x75 => true, // isinst
            0x79 => true, // unbox
            0x7B => true, // ldfld
            0x7C => true, // ldflda
            0x7D => true, // stfld
            0x7E => true, // ldsfld
            0x7F => true, // ldsflda
            0x80 => true, // stsfld
            0x81 => true, // stobj
            0x8C => true, // box
            0x8D => true, // newarr
            0x8F => true, // ldelema
            0xA3 => true, // ldelem
            0xA4 => true, // stelem
            0xA5 => true, // unbox.any
            0xC6 => true, // mkrefany
            0xD0 => true, // ldtoken
            _ => false
        };

        private static int DEAD_GetOperandSize(byte op) => op switch
        {
            // No operand (0 bytes)
            0x00 => 0, 0x01 => 0, // nop, break
            0x02 => 0, 0x03 => 0, 0x04 => 0, 0x05 => 0, // ldarg.0-3
            0x06 => 0, 0x07 => 0, 0x08 => 0, 0x09 => 0, // ldloc.0-3
            0x0A => 0, 0x0B => 0, 0x0C => 0, 0x0D => 0, // stloc.0-3
            0x14 => 0, // ldnull
            0x15 => 0, 0x16 => 0, 0x17 => 0, 0x18 => 0, // ldc.i4.m1 through ldc.i4.2
            0x19 => 0, 0x1A => 0, 0x1B => 0, 0x1C => 0, 0x1D => 0, 0x1E => 0, // ldc.i4.3-8
            0x25 => 0, // dup
            0x26 => 0, // pop
            0x2A => 0, // ret
            0x46 => 0, 0x47 => 0, 0x48 => 0, 0x49 => 0, 0x4A => 0, // ldind.*
            0x4B => 0, 0x4C => 0, 0x4D => 0, 0x4E => 0, 0x4F => 0, // ldind.* + stind.*
            0x50 => 0, 0x51 => 0, 0x52 => 0, // stind.*
            0x57 => 0, // conv.ovf.*
            0x58 => 0, 0x59 => 0, 0x5A => 0, 0x5B => 0, // add, sub, mul, div
            0x5C => 0, 0x5D => 0, 0x5E => 0, 0x5F => 0, // div.un, rem, rem.un, and
            0x60 => 0, 0x61 => 0, 0x62 => 0, 0x63 => 0, // or, xor, shl, shr
            0x64 => 0, 0x65 => 0, 0x66 => 0, 0x67 => 0, // shr.un, neg, not, conv.*
            0x68 => 0, 0x69 => 0, 0x6A => 0, 0x6B => 0,
            0x6C => 0, 0x6D => 0,
            0x76 => 0, 0x77 => 0, 0x78 => 0, // conv.r.un, ...
            0x82 => 0, // conv.ovf.i1.un...
            0x83 => 0, 0x84 => 0, 0x85 => 0, 0x86 => 0,
            0x87 => 0, 0x88 => 0, 0x89 => 0, 0x8A => 0, 0x8B => 0,
            0x90 => 0, 0x91 => 0, 0x92 => 0, 0x93 => 0, // ldelem.*
            0x94 => 0, 0x95 => 0, 0x96 => 0, 0x97 => 0, 0x98 => 0,
            0x99 => 0, 0x9A => 0, // ldelem/stelem variants
            0x9B => 0, 0x9C => 0, 0x9D => 0, 0x9E => 0, 0x9F => 0,
            0xA0 => 0, 0xA1 => 0, 0xA2 => 0,
            0xB3 => 0, 0xB4 => 0, 0xB5 => 0, 0xB6 => 0, 0xB7 => 0, 0xB8 => 0,
            0xC3 => 0, 0xD1 => 0, 0xD2 => 0,
            0xD3 => 0, 0xD4 => 0, 0xD5 => 0, 0xD6 => 0, 0xD7 => 0, 0xD8 => 0,
            0xD9 => 0, 0xDA => 0, 0xDC => 0,
            0xE0 => 0,

            // Inline int8 (1 byte)
            0x0E => 1, 0x0F => 1, 0x10 => 1, 0x11 => 1, 0x12 => 1, 0x13 => 1, // ldarg.s, ldarga.s, starg.s, ldloc.s, ldloca.s, stloc.s
            0x1F => 1, // ldc.i4.s
            0x2B => 1, // br.s
            0x2C => 1, 0x2D => 1, 0x2E => 1, 0x2F => 1, // brfalse.s, brtrue.s, beq.s, bge.s
            0x30 => 1, 0x31 => 1, 0x32 => 1, 0x33 => 1, // bgt.s, ble.s, blt.s, bne.un.s
            0x34 => 1, 0x35 => 1, 0x36 => 1, 0x37 => 1, // bge.un.s, bgt.un.s, ble.un.s, blt.un.s
            0xDE => 1, // leave.s — already listed above as 0, fix:

            // Inline int32 (4 bytes) — non-token
            0x20 => 4, // ldc.i4
            0x38 => 4, // br
            0x39 => 4, 0x3A => 4, 0x3B => 4, 0x3C => 4, // brfalse, brtrue, beq, bge
            0x3D => 4, 0x3E => 4, 0x3F => 4, 0x40 => 4, // bgt, ble, blt, bne.un
            0x41 => 4, 0x42 => 4, 0x43 => 4, 0x44 => 4, // bge.un, bgt.un, ble.un, blt.un
            0xDD => 4, // leave

            // Inline int64 (8 bytes)
            0x21 => 8, // ldc.i8

            // Inline float32 (4 bytes)
            0x22 => 4, // ldc.r4

            // Inline float64 (8 bytes)
            0x23 => 8, // ldc.r8

            // Switch (variable length)
            0x45 => -1, // switch — special handling needed

            // Token opcodes return 4 but are handled separately
            _ => 0
        };

        private static int DEAD_GetFEOperandSize(byte op2) => op2 switch
        {
            0x00 => 0, 0x01 => 0, // arglist, ceq
            0x02 => 0, 0x03 => 0, // cgt, cgt.un
            0x04 => 0, 0x05 => 0, // clt, clt.un
            0x06 => 4, // ldftn (token)
            0x07 => 4, // ldvirtftn (token)
            0x09 => 2, // ldarg
            0x0A => 2, // ldarga
            0x0B => 2, // starg
            0x0C => 2, // ldloc
            0x0D => 2, // ldloca
            0x0E => 2, // stloc
            0x0F => 0, // localloc
            0x11 => 0, // endfilter
            0x12 => 1, // unaligned.
            0x13 => 0, // volatile.
            0x14 => 0, // tail.
            0x15 => 4, // initobj (handled separately)
            0x16 => 4, // constrained. (handled separately)
            0x17 => 0, // cpblk
            0x18 => 0, // initblk
            0x1A => 0, // rethrow
            0x1C => 4, // sizeof (handled separately)
            0x1D => 0, // refanytype
            0x1E => 0, // readonly.
            _ => 0
        };

        // ================================================================
        // Metadata token resolution (PE tokens → symbolic references)
        // ================================================================

        private static TokenReference? ResolveMetadataToken(MetadataReader mdReader, int token)
        {
            // User string tokens have table byte 0x70
            if ((token >> 24) == 0x70)
            {
                try
                {
                    var userStringHandle = MetadataTokens.UserStringHandle(token & 0x00FFFFFF);
                    var str = mdReader.GetUserString(userStringHandle);
                    return new TokenReference(TokenKindString, str);
                }
                catch { return null; }
            }

            var handle = MetadataTokens.EntityHandle(token);
            if (handle.IsNil) return null;

            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                {
                    var td = mdReader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    var name = mdReader.GetString(td.Name);
                    var ns = mdReader.GetString(td.Namespace);
                    var fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                    return new TokenReference(TokenKindType, fullName);
                }
                case HandleKind.TypeReference:
                {
                    var tr = mdReader.GetTypeReference((TypeReferenceHandle)handle);
                    var name = mdReader.GetString(tr.Name);
                    var ns = mdReader.GetString(tr.Namespace);
                    var fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                    return new TokenReference(TokenKindType, fullName);
                }
                case HandleKind.TypeSpecification:
                {
                    var ts = mdReader.GetTypeSpecification((TypeSpecificationHandle)handle);
                    var typeName = ts.DecodeSignature(new TypeNameProvider(mdReader), null);
                    return new TokenReference(TokenKindType, typeName);
                }
                case HandleKind.MethodDefinition:
                {
                    var method = mdReader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    var methodName = mdReader.GetString(method.Name);
                    var declType = method.GetDeclaringType();
                    var td = mdReader.GetTypeDefinition(declType);
                    var typeName = mdReader.GetString(td.Name);
                    var typeNs = mdReader.GetString(td.Namespace);
                    var fullTypeName = string.IsNullOrEmpty(typeNs) ? typeName : typeNs + "." + typeName;
                    return new TokenReference(TokenKindMethod, fullTypeName + "::" + methodName);
                }
                case HandleKind.MemberReference:
                {
                    var mr = mdReader.GetMemberReference((MemberReferenceHandle)handle);
                    var memberName = mdReader.GetString(mr.Name);
                    var parentName = ResolveTypeRef(mdReader, mr.Parent);

                    if (mr.GetKind() == MemberReferenceKind.Method)
                        return new TokenReference(TokenKindMethod, parentName + "::" + memberName);
                    else
                        return new TokenReference(TokenKindField, parentName + "::" + memberName);
                }
                case HandleKind.FieldDefinition:
                {
                    var field = mdReader.GetFieldDefinition((FieldDefinitionHandle)handle);
                    var fieldName = mdReader.GetString(field.Name);
                    var declType = field.GetDeclaringType();
                    var td = mdReader.GetTypeDefinition(declType);
                    var typeName = mdReader.GetString(td.Name);
                    var typeNs = mdReader.GetString(td.Namespace);
                    var fullTypeName = string.IsNullOrEmpty(typeNs) ? typeName : typeNs + "." + typeName;
                    return new TokenReference(TokenKindField, fullTypeName + "::" + fieldName);
                }
                default:
                    return null;
            }
        }

        private static string ResolveTypeRef(MetadataReader mdReader, EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                {
                    var td = mdReader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    var name = mdReader.GetString(td.Name);
                    var ns = mdReader.GetString(td.Namespace);
                    return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                }
                case HandleKind.TypeReference:
                {
                    var tr = mdReader.GetTypeReference((TypeReferenceHandle)handle);
                    var name = mdReader.GetString(tr.Name);
                    var ns = mdReader.GetString(tr.Namespace);
                    return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                }
                case HandleKind.TypeSpecification:
                {
                    var ts = mdReader.GetTypeSpecification((TypeSpecificationHandle)handle);
                    return ts.DecodeSignature(new TypeNameProvider(mdReader), null);
                }
                default:
                    return "";
            }
        }

        private static string DecodeFieldType(MetadataReader mdReader, FieldDefinition field)
        {
            return field.DecodeSignature(new TypeNameProvider(mdReader), null);
        }

        // ================================================================
        // Type resolution helpers
        // ================================================================

        private static Type ResolveType(string typeName, Dictionary<string, TypeBuilder>? typeBuilders = null)
        {
            if (typeBuilders != null && typeBuilders.TryGetValue(typeName, out var tb))
                return tb;

            // Try well-known types
            var type = Type.GetType(typeName);
            if (type != null) return type;

            // Handle common CLR type names
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
            if (type != null) return type;

            // Search loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) return type;
            }

            return typeof(object); // fallback
        }

        // ================================================================
        // SignatureTypeProvider for MetadataReader
        // ================================================================

        private sealed class TypeNameProvider : ISignatureTypeProvider<string, object?>
        {
            private readonly MetadataReader _reader;
            public TypeNameProvider(MetadataReader reader) { _reader = reader; }

            public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
            {
                PrimitiveTypeCode.Void => "System.Void",
                PrimitiveTypeCode.Boolean => "System.Boolean",
                PrimitiveTypeCode.Char => "System.Char",
                PrimitiveTypeCode.SByte => "System.SByte",
                PrimitiveTypeCode.Byte => "System.Byte",
                PrimitiveTypeCode.Int16 => "System.Int16",
                PrimitiveTypeCode.UInt16 => "System.UInt16",
                PrimitiveTypeCode.Int32 => "System.Int32",
                PrimitiveTypeCode.UInt32 => "System.UInt32",
                PrimitiveTypeCode.Int64 => "System.Int64",
                PrimitiveTypeCode.UInt64 => "System.UInt64",
                PrimitiveTypeCode.Single => "System.Single",
                PrimitiveTypeCode.Double => "System.Double",
                PrimitiveTypeCode.String => "System.String",
                PrimitiveTypeCode.IntPtr => "System.IntPtr",
                PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
                PrimitiveTypeCode.Object => "System.Object",
                PrimitiveTypeCode.TypedReference => "System.TypedReference",
                _ => "System.Object"
            };

            public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                var td = reader.GetTypeDefinition(handle);
                var name = reader.GetString(td.Name);
                var ns = reader.GetString(td.Namespace);
                return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            }

            public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                var tr = reader.GetTypeReference(handle);
                var name = reader.GetString(tr.Name);
                var ns = reader.GetString(tr.Namespace);
                return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            }

            public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                var ts = reader.GetTypeSpecification(handle);
                return ts.DecodeSignature(this, genericContext);
            }

            public string GetSZArrayType(string elementType) => elementType + "[]";
            public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
            public string GetByReferenceType(string elementType) => elementType + "&";
            public string GetPointerType(string elementType) => elementType + "*";
            public string GetGenericInstantiation(string genericType, System.Collections.Immutable.ImmutableArray<string> typeArguments)
                => genericType + "<" + string.Join(",", typeArguments) + ">";
            public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;
            public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;
            public string GetPinnedType(string elementType) => elementType;
            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
            public string GetFunctionPointerType(MethodSignature<string> signature) => "System.IntPtr";
        }

        // ================================================================
        // Data structures
        // ================================================================

        private sealed class MethodBodyData
        {
            public int MaxStack;
            public string[] LocalTypes = Array.Empty<string>();
            public byte[] ILBytes = Array.Empty<byte>();
            public List<TokenEntry> TokenEntries = new();
            public List<ExceptionHandlerData> ExceptionHandlers = new();
        }

        // TokenEntry is now defined in ILSerializerTypes.cs

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
