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
using Ngo.Compiler.Emit;
using Ngo.Runtime;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// Serializes and deserializes IL metadata and IL bytecode
    /// for .ngo archives. Handles token remapping when linking into a target module.
    /// </summary>
    internal static class ILSerializer
    {
        private static readonly Dictionary<short, OpCode> OpCodeMap = BuildOpCodeMap();

        private static readonly Assembly[] ReferencedAssemblies = new[]
        {
            typeof(Ngo.Runtime.Slice<>).Assembly,
            typeof(System.Numerics.Complex).Assembly,
        };

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
            ctx.CurrentPackagePath = importPath;
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
        public static bool LinkFromArchive(string archivePath, PackageSymbol pkg, EmitContext ctx)
        {
            var (ilMetaBytes, ilCodeBytes) = NgoArchive.ReadIL(archivePath);
            if (ilMetaBytes == null || ilCodeBytes == null)
            {
                return false;
            }

            new ILLinker(pkg, ctx).Link(ilMetaBytes, ilCodeBytes);
            return true;
        }

        // =====================================================================
        // Type resolution
        // =====================================================================

        internal static Type ResolveType(string typeName, Dictionary<string, TypeBuilder>? typeBuilders = null,
            Dictionary<string, Type>? genericParams = null)
        {
            if (typeName == "$$null" || typeName == "$$error" || typeName == "?")
            {
                throw new InvalidOperationException(
                    $"ILSerializer.ResolveType: invalid sentinel type name '{typeName}' leaked into archive. " +
                    "This indicates a bug in the semantic analyzer or emitter — the type was never resolved.");
            }

            if (genericParams != null && genericParams.TryGetValue(typeName, out var gp))
            {
                return gp;
            }

            if (typeBuilders != null && typeBuilders.TryGetValue(typeName, out var tb))
            {
                return tb;
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

            if (typeName.EndsWith("*"))
            {
                var elemType = ResolveType(typeName.Substring(0, typeName.Length - 1), typeBuilders, genericParams);
                return elemType.MakePointerType();
            }

            int bracketIndex = typeName.IndexOf('[');
            if (bracketIndex > 0 && typeName.EndsWith("]") && !typeName.EndsWith("[]"))
            {
                var genericDefName = typeName.Substring(0, bracketIndex);
                var argsStr = typeName.Substring(bracketIndex + 1, typeName.Length - bracketIndex - 2);

                Type genericDef;
                if (typeBuilders != null && typeBuilders.TryGetValue(genericDefName, out var genTb))
                {
                    genericDef = genTb;
                }
                else
                {
                    genericDef = ResolveType(genericDefName, typeBuilders, genericParams);
                }

                var argNames = SplitGenericArgs(argsStr);

                var typeArgs = new Type[argNames.Length];
                for (int i = 0; i < argNames.Length; i++)
                {
                    typeArgs[i] = ResolveType(argNames[i].Trim(), typeBuilders, genericParams);
                }

                return genericDef.MakeGenericType(typeArgs);
            }

            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

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
                "System.String" => typeof(GoString),
                "Ngo.Runtime.GoString" => typeof(GoString),
                "System.Object" => typeof(object),
                "System.IntPtr" => typeof(IntPtr),
                "System.UIntPtr" => typeof(UIntPtr),
                "System.ValueType" => typeof(ValueType),
                _ => null
            };
            if (type != null)
            {
                return type;
            }

            // Search assemblies referenced by the compiler for types it emits
            // (Ngo.Runtime, System.Numerics, etc.)
            foreach (var referencedAssembly in ReferencedAssemblies)
            {
                type = referencedAssembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException($"LinkIL: failed to resolve type '{typeName}'");
        }

        internal static Type ResolveTypeWithGenericParams(string typeName,
            Dictionary<string, TypeBuilder> typeBuilders,
            Dictionary<string, Type> genericParamMap)
        {
            if (genericParamMap.TryGetValue(typeName, out var genericParam))
            {
                return genericParam;
            }
            return ResolveType(typeName, typeBuilders, genericParamMap);
        }

        /// <summary>
        /// Splits a comma-separated generic argument string while respecting nested brackets.
        /// </summary>
        private static string[] SplitGenericArgs(string argsStr)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < argsStr.Length; i++)
            {
                if (argsStr[i] == '[')
                {
                    depth++;
                }
                else if (argsStr[i] == ']')
                {
                    depth--;
                }
                else if (argsStr[i] == ',' && depth == 0)
                {
                    result.Add(argsStr.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(argsStr.Substring(start));
            return result.ToArray();
        }

        // =====================================================================
        // EmitPackageForSerialization
        // =====================================================================

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

        // =====================================================================
        // OpCode utilities
        // =====================================================================

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

        internal static bool IsShortBranch(byte op)
        {
            return OpCodeMap.TryGetValue(op, out var opCode) && opCode.OperandType == OperandType.ShortInlineBrTarget;
        }

        internal static bool IsLongBranch(byte op)
        {
            return OpCodeMap.TryGetValue(op, out var opCode) && opCode.OperandType == OperandType.InlineBrTarget;
        }

        internal static OpCode GetOpCode(byte op)
        {
            if (OpCodeMap.TryGetValue(op, out var opCode))
            {
                return opCode;
            }
            throw new InvalidOperationException($"LinkIL: unknown single-byte opcode 0x{op:X2}");
        }

        internal static OpCode GetTwoByteOpCode(byte op2)
        {
            short value = (short)(0xFE00 | op2);
            if (OpCodeMap.TryGetValue(value, out var opCode))
            {
                return opCode;
            }
            throw new InvalidOperationException($"LinkIL: unknown two-byte opcode 0xFE 0x{op2:X2}");
        }

        internal static bool HasInlineToken(byte op)
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

        internal static int GetOperandSize(byte op)
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

        internal static int GetTwoByteOperandSize(byte op2)
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

    }
}
