// -----------------------------------------------------------------------
// <copyright file="NgoWriter.cs" company="Ziad">
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
using Ngo.Compiler.Emit;
using Ngo.Compiler.Emit.Builder;
using Ngo.Runtime.Discovery;

namespace Ngo.Compiler.Archive
{
    internal sealed class NgoWriter : CilWriter
    {
        private readonly MemoryStream _code = new();
        private readonly List<ILTokenEntry> _tokens = new();
        private readonly List<string> _locals = new();
        private readonly List<ExceptionClause> _exceptionClauses = new();

        private int _nextLabelId;
        private readonly Dictionary<int, int> _labelOffsets = new();
        private readonly List<BranchFixup> _branchFixups = new();

        private readonly Stack<int> _tryStartOffsets = new();
        private int _currentTryStart = -1;
        private int _currentTryLength;
        private int _currentHandlerStart = -1;

        private ILGenerator _labelFactory;
        private ILGenerator _localFactory;

        private static readonly Dictionary<Type, PrimitiveTypeKind> PrimitiveTypeMap = new()
        {
            { typeof(void), PrimitiveTypeKind.Void },
            { typeof(bool), PrimitiveTypeKind.Bool },
            { typeof(byte), PrimitiveTypeKind.Byte },
            { typeof(sbyte), PrimitiveTypeKind.SByte },
            { typeof(short), PrimitiveTypeKind.Int16 },
            { typeof(ushort), PrimitiveTypeKind.UInt16 },
            { typeof(int), PrimitiveTypeKind.Int32 },
            { typeof(uint), PrimitiveTypeKind.UInt32 },
            { typeof(long), PrimitiveTypeKind.Int64 },
            { typeof(ulong), PrimitiveTypeKind.UInt64 },
            { typeof(float), PrimitiveTypeKind.Float32 },
            { typeof(double), PrimitiveTypeKind.Float64 },
            { typeof(string), PrimitiveTypeKind.String },
            { typeof(object), PrimitiveTypeKind.Object },
            { typeof(nint), PrimitiveTypeKind.IntPtr },
            { typeof(nuint), PrimitiveTypeKind.UIntPtr },
            { typeof(char), PrimitiveTypeKind.Char },
        };

        private readonly SerializationContext _serializationContext;

        public NgoWriter(SerializationContext? serializationContext = null)
        {
            _serializationContext = serializationContext ?? SerializationContext.Empty;
            _labelFactory = CreateFactory();
            _localFactory = CreateFactory();
        }

        public byte[] GetILBytes()
        {
            PatchBranches();
            return _code.ToArray();
        }

        public string[] GetLocalTypes() => _locals.ToArray();
        public List<ILTokenEntry> GetTokenEntries() => _tokens;
        public List<ExceptionClause> GetExceptionClauses() => _exceptionClauses;

        // ----- Emit overloads -----

        public override void Emit(OpCode op)
        {
            WriteOpCode(op);
        }

        public override void Emit(OpCode op, int arg)
        {
            WriteOpCode(op);
            WriteInt32(arg);
        }

        public override void Emit(OpCode op, long arg)
        {
            WriteOpCode(op);
            WriteInt64(arg);
        }

        public override void Emit(OpCode op, float arg)
        {
            WriteOpCode(op);
            WriteSingle(arg);
        }

        public override void Emit(OpCode op, double arg)
        {
            WriteOpCode(op);
            WriteDouble(arg);
        }

        public override void Emit(OpCode op, string arg)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateString(offset, arg));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, byte arg)
        {
            WriteOpCode(op);
            _code.WriteByte(arg);
        }

        public override void Emit(OpCode op, Type type)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateType(offset, BuildTypeToken(type)));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, MethodInfo method)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateMethod(offset, BuildMethodToken(method)));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, ConstructorInfo constructor)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateMethod(offset, BuildConstructorToken(constructor)));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, FieldInfo field)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateField(offset, BuildFieldToken(field)));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, Label label)
        {
            WriteOpCode(op);
            var fixupOffset = (int)_code.Position;
            var labelId = GetLabelId(label);

            if (IsShortBranch(op))
            {
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = true });
                _code.WriteByte(0);
            }
            else
            {
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = false });
                WriteInt32(0);
            }
        }

        public override void Emit(OpCode op, Label[] labels)
        {
            WriteOpCode(op);
            WriteInt32(labels.Length);
            int baseOffset = (int)_code.Position + labels.Length * 4;
            for (int index = 0; index < labels.Length; index++)
            {
                var fixupOffset = (int)_code.Position;
                var labelId = GetLabelId(labels[index]);
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = false, BaseOffset = baseOffset });
                WriteInt32(0);
            }
        }

        public override void Emit(OpCode op, LocalBuilder local)
        {
            WriteOpCode(op);
            var operandType = op.OperandType;
            if (operandType == OperandType.ShortInlineVar)
            {
                _code.WriteByte((byte)local.LocalIndex);
            }
            else if (operandType == OperandType.InlineVar)
            {
                WriteInt16((short)local.LocalIndex);
            }
        }

        // ----- Label/Local support -----

        public override LocalBuilder DeclareLocal(Type type)
        {
            _locals.Add(GetTypeNameStatic(type));
            var safeType = type is TypeDelegator ? typeof(object) : type;
            try
            {
                return _localFactory.DeclareLocal(safeType);
            }
            catch (ArgumentException)
            {
                return _localFactory.DeclareLocal(typeof(object));
            }
        }

        public override Label DefineLabel()
        {
            var label = _labelFactory.DefineLabel();
            _nextLabelId++;
            return label;
        }

        public override void MarkLabel(Label label)
        {
            var labelId = GetLabelId(label);
            _labelOffsets[labelId] = (int)_code.Position;
        }

        // ----- Exception handling -----

        public override void BeginExceptionBlock()
        {
            _tryStartOffsets.Push((int)_code.Position);
        }

        public override void BeginCatchBlock(Type type)
        {
            if (_tryStartOffsets.Count > 0)
            {
                int tryStart = _tryStartOffsets.Peek();
                bool isFirstHandler = _currentTryStart != tryStart
                    || !_exceptionClauses.Any(c => c.TryOffset == tryStart);
                if (isFirstHandler)
                {
                    _currentTryStart = tryStart;
                    _currentTryLength = (int)_code.Position - _currentTryStart;
                }
            }
            _currentHandlerStart = (int)_code.Position;

            _exceptionClauses.Add(new ExceptionClause
            {
                Kind = 0,
                TryOffset = _currentTryStart,
                TryLength = _currentTryLength,
                HandlerOffset = _currentHandlerStart,
                CatchTypeName = GetTypeNameStatic(type),
            });
        }

        public override void EndExceptionBlock()
        {
            if (_tryStartOffsets.Count > 0)
            {
                _tryStartOffsets.Pop();
            }

            if (_exceptionClauses.Count > 0)
            {
                var last = _exceptionClauses[_exceptionClauses.Count - 1];
                last.HandlerLength = (int)_code.Position - last.HandlerOffset;
                _exceptionClauses[_exceptionClauses.Count - 1] = last;
            }
        }

        // ----- Serialization to Section 3 format -----

        public void WriteMethodBody(BinaryWriter writer)
        {
            var ilBytes = GetILBytes();

            writer.Write(8);

            writer.Write(_locals.Count);
            foreach (var local in _locals)
            {
                writer.Write(local);
            }

            writer.Write(ilBytes.Length);
            writer.Write(ilBytes);

            writer.Write(_tokens.Count);
            foreach (var entry in _tokens)
            {
                entry.Write(writer);
            }

            writer.Write(_exceptionClauses.Count);
            foreach (var clause in _exceptionClauses)
            {
                writer.Write(clause.Kind);
                writer.Write(clause.TryOffset);
                writer.Write(clause.TryLength);
                writer.Write(clause.HandlerOffset);
                writer.Write(clause.HandlerLength);
                writer.Write(clause.FilterOffset);
                writer.Write(clause.CatchTypeName ?? "");
            }
        }

        public void Reset()
        {
            _code.SetLength(0);
            _code.Position = 0;
            _tokens.Clear();
            _locals.Clear();
            _exceptionClauses.Clear();
            _labelOffsets.Clear();
            _branchFixups.Clear();
            _nextLabelId = 0;
            _tryStartOffsets.Clear();
            _currentTryStart = -1;
            _currentHandlerStart = -1;
            _labelFactory = CreateFactory();
            _localFactory = CreateFactory();
        }

        // ----- Structured token builders -----

        [ThreadStatic] private static HashSet<Type>? _typeTokenInProgress;

        private TypeToken BuildTypeToken(Type type)
        {
            _typeTokenInProgress ??= new HashSet<Type>(ReferenceEqualityComparer.Instance);
            if (!_typeTokenInProgress.Add(type))
            {
                return TypeToken.CreateTypeDef(type.FullName ?? type.Name);
            }
            try
            {
                return BuildTypeTokenCore(type);
            }
            finally
            {
                _typeTokenInProgress.Remove(type);
            }
        }

        private TypeToken BuildTypeTokenCore(Type type)
        {
            // Check if this type is a generic parameter that belongs to the current context.
            // This handles both NgoProxyType params and runtime GenericTypeParameterBuilder.
            int methodIndex = _serializationContext.FindMethodGenericParamIndex(type);
            if (methodIndex >= 0)
            {
                return TypeToken.CreateGenericMethodParam(methodIndex);
            }

            int typeIndex = _serializationContext.FindTypeGenericParamIndex(type);
            if (typeIndex >= 0)
            {
                return TypeToken.CreateGenericTypeParam(typeIndex);
            }

            if (type is NgoProxyType proxyType)
            {
                if (proxyType.IsGenericParam)
                {
                    return TypeToken.CreatePrimitive(PrimitiveTypeKind.Object);
                }

                if (type.IsGenericType && !type.IsGenericTypeDefinition)
                {
                    var genericDefinition = type.GetGenericTypeDefinition();
                    var genericArguments = type.GetGenericArguments();
                    var argumentTokens = genericArguments.Select(BuildTypeToken).ToArray();
                    return TypeToken.CreateGenericInst(BuildTypeToken(genericDefinition), argumentTokens);
                }

                return TypeToken.CreateTypeDef(type.FullName ?? type.Name);
            }

            if (PrimitiveTypeMap.TryGetValue(type, out var primitiveKind))
            {
                return TypeToken.CreatePrimitive(primitiveKind);
            }

            if (type.IsGenericParameter)
            {
                return TypeToken.CreatePrimitive(PrimitiveTypeKind.Object);
            }

            if (type.IsArray)
            {
                return TypeToken.CreateArray(BuildTypeToken(type.GetElementType()!));
            }

            if (type.IsPointer)
            {
                return TypeToken.CreatePointer(BuildTypeToken(type.GetElementType()!));
            }

            if (type.IsByRef)
            {
                return TypeToken.CreateByRef(BuildTypeToken(type.GetElementType()!));
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type genericDefinition;
                Type[] genericArguments;
                try
                {
                    genericDefinition = type.GetGenericTypeDefinition();
                    genericArguments = type.GetGenericArguments();
                }
                catch (NotSupportedException)
                {
                    // TypeBuilderInstantiation doesn't support GetGenericTypeDefinition.
                    // Parse the FullName to extract the generic definition and arguments.
                    var instName = type.FullName ?? type.Name;
                    var instBacktick = instName.IndexOf('`');
                    if (instBacktick > 0)
                    {
                        var bracketStart = instName.IndexOf('[', instBacktick);
                        if (bracketStart > 0)
                        {
                            var defName = instName.Substring(0, bracketStart);
                            var defToken = TypeToken.CreatePackageTypeRef(GetPackageImportPath(type), defName);
                            var argsStr = instName.Substring(bracketStart);
                            var argTokens = ParseGenericArgumentsFromName(argsStr);
                            return TypeToken.CreateGenericInst(defToken, argTokens);
                        }
                    }
                    return TypeToken.CreatePackageTypeRef(type.Namespace ?? "", type.Name);
                }
                var argumentTokens = genericArguments.Select(BuildTypeToken).ToArray();
                return TypeToken.CreateGenericInst(BuildTypeToken(genericDefinition), argumentTokens);
            }

            // Last-resort check for constructed generic types that bypassed the IsGenericType check
            // (e.g., TypeBuilderInstantiation or types with NgoProxyType arguments where
            // reflection doesn't report IsGenericType correctly)
            var typeName = type.FullName ?? type.Name;
            var backtickIndex = typeName.IndexOf('`');
            if (backtickIndex > 0 && typeName.IndexOf('[', backtickIndex) > 0
                && !typeName.EndsWith("[]") && !type.IsGenericTypeDefinition)
            {
                try
                {
                    var genericDefinition = type.GetGenericTypeDefinition();
                    var genericArguments = type.GetGenericArguments();
                    var argumentTokens = new TypeToken[genericArguments.Length];
                    for (int i = 0; i < genericArguments.Length; i++)
                    {
                        argumentTokens[i] = BuildTypeToken(genericArguments[i]);
                    }
                    return TypeToken.CreateGenericInst(BuildTypeToken(genericDefinition), argumentTokens);
                }
                catch (NotSupportedException)
                {
                    // Constructed generic with proxy type args — parse the name to extract parts.
                    // Format: "Namespace.Type`N[[ArgFullName, Assembly], ...]"
                    var defName = typeName.Substring(0, typeName.IndexOf('[', backtickIndex));
                    var defToken = TypeToken.CreatePackageTypeRef(GetPackageImportPath(type), defName);

                    // Parse type arguments from the FullName string
                    var argsStr = typeName.Substring(typeName.IndexOf('[', backtickIndex));
                    var argTokens = ParseGenericArgumentsFromName(argsStr);
                    return TypeToken.CreateGenericInst(defToken, argTokens);
                }
            }

            // For generic definitions, strip the type arg brackets: "Slice`1[[T]]" → "Slice`1"
            if (backtickIndex > 0 && type.IsGenericTypeDefinition)
            {
                var bracketStart = typeName.IndexOf('[', backtickIndex);
                if (bracketStart > 0)
                {
                    typeName = typeName.Substring(0, bracketStart);
                }
            }

            var packagePath = GetPackageImportPath(type);
            return TypeToken.CreatePackageTypeRef(packagePath, typeName);
        }

        private TypeToken[] ParseGenericArgumentsFromName(string argsStr)
        {
            // argsStr format: "[[FullName1, Assembly1],[FullName2, Assembly2]]"
            // or just "[FullName1,FullName2]" for simple names
            var result = new List<TypeToken>();
            int depth = 0;
            int start = -1;
            for (int i = 0; i < argsStr.Length; i++)
            {
                if (argsStr[i] == '[')
                {
                    depth++;
                    if (depth == 2)
                    {
                        start = i + 1;
                    }
                }
                else if (argsStr[i] == ']')
                {
                    depth--;
                    if (depth == 1 && start >= 0)
                    {
                        var argFullName = argsStr.Substring(start, i - start);
                        // Strip assembly qualification: "TypeName, Assembly, Version=..." → "TypeName"
                        var commaIndex = argFullName.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            argFullName = argFullName.Substring(0, commaIndex).Trim();
                        }
                        // Try to resolve as a known type
                        var argType = Type.GetType(argFullName) ?? RuntimeAssembly.GetType(argFullName);
                        if (argType != null)
                        {
                            result.Add(BuildTypeToken(argType));
                        }
                        else
                        {
                            // Might be a TypeDef in this archive
                            result.Add(TypeToken.CreateTypeDef(argFullName));
                        }
                        start = -1;
                    }
                }
            }
            return result.ToArray();
        }

        private static readonly Assembly RuntimeAssembly = typeof(Ngo.Runtime.Slice<>).Assembly;

        private MethodToken BuildMethodToken(MethodInfo method)
        {
            if (method == null)
            {
                throw new InvalidOperationException("NgoWriter: attempted to serialize a null method reference");
            }

            if (method.IsGenericMethod && !method.IsGenericMethodDefinition)
            {
                var genericDefinition = method.GetGenericMethodDefinition();
                var typeArguments = method.GetGenericArguments();
                var typeArgumentTokens = typeArguments.Select(BuildTypeToken).ToArray();
                var baseMethodToken = BuildMethodToken(genericDefinition);
                return MethodToken.CreateMethodSpec(baseMethodToken, typeArgumentTokens);
            }

            var declaringType = method.DeclaringType;
            var parameterTokens = BuildParameterTypeTokens(method.GetParameters(), declaringType);
            var returnTypeToken = BuildTypeToken(method.ReturnType);

            if (declaringType is NgoProxyType)
            {
                return MethodToken.CreateMethodDef(BuildTypeToken(declaringType), method.Name, parameterTokens, returnTypeToken);
            }

            return MethodToken.CreateMemberRef(BuildTypeToken(declaringType!), method.Name, parameterTokens, returnTypeToken);
        }

        private MethodToken BuildConstructorToken(ConstructorInfo constructor)
        {
            if (constructor == null)
            {
                throw new InvalidOperationException("NgoWriter: attempted to serialize a null constructor reference");
            }

            var declaringType = constructor.DeclaringType!;
            var parameterTokens = BuildParameterTypeTokens(constructor.GetParameters(), declaringType);
            var returnTypeToken = TypeToken.CreatePrimitive(PrimitiveTypeKind.Void);

            if (declaringType is NgoProxyType)
            {
                return MethodToken.CreateMethodDef(BuildTypeToken(declaringType), ".ctor", parameterTokens, returnTypeToken);
            }

            return MethodToken.CreateMemberRef(BuildTypeToken(declaringType), ".ctor", parameterTokens, returnTypeToken);
        }

        private FieldToken BuildFieldToken(FieldInfo field)
        {
            var declaringType = field.DeclaringType!;

            if (declaringType is NgoProxyType)
            {
                return FieldToken.CreateFieldDef(BuildTypeToken(declaringType), field.Name);
            }

            return FieldToken.CreateMemberRef(BuildTypeToken(declaringType), field.Name);
        }

        private TypeToken[] BuildParameterTypeTokens(ParameterInfo[] parameters, Type? declaringType)
        {
            Type[]? declaringTypeArguments = null;
            Type[]? declaringTypeDefParameters = null;
            if (declaringType != null && declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition)
            {
                declaringTypeArguments = declaringType.GetGenericArguments();
                declaringTypeDefParameters = declaringType.GetGenericTypeDefinition().GetGenericArguments();
            }

            var parameterTokens = new TypeToken[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                var parameterType = parameters[index].ParameterType;
                if (parameterType.IsGenericParameter && declaringTypeArguments != null && declaringTypeDefParameters != null)
                {
                    for (int genericIndex = 0; genericIndex < declaringTypeDefParameters.Length; genericIndex++)
                    {
                        if (declaringTypeDefParameters[genericIndex] == parameterType
                            || declaringTypeDefParameters[genericIndex].Name == parameterType.Name)
                        {
                            parameterType = declaringTypeArguments[genericIndex];
                            break;
                        }
                    }
                }
                parameterTokens[index] = BuildTypeToken(parameterType);
            }
            return parameterTokens;
        }

        private static MethodInfo GetGenericMethodDefinition(MethodInfo method)
        {
            if (method is NgoProxyMethodInfo)
            {
                return method;
            }
            return method.GetGenericMethodDefinition();
        }

        private static string GetPackageImportPath(Type type)
        {
            // TypeBuilder-based types don't support GetCustomAttribute
            if (type is TypeBuilder || type is TypeDelegator
                || type.GetType().Name == "TypeBuilderInstantiation")
            {
                return type.Namespace ?? "";
            }

            try
            {
                var goTypeAttribute = type.GetCustomAttribute<GoTypeAttribute>();
                if (goTypeAttribute?.Package != null)
                {
                    return goTypeAttribute.Package;
                }

                var declaringType = type.DeclaringType ?? type;
                var goPackageAttribute = declaringType.GetCustomAttribute<GoPackageAttribute>();
                if (goPackageAttribute != null)
                {
                    return goPackageAttribute.ImportPath;
                }
            }
            catch (NotSupportedException)
            {
                // Some reflection types don't support custom attributes
            }

            return type.Namespace ?? "";
        }

        // ----- Static type name helpers (used by other serializers for Section 2 metadata) -----

        internal static string GetTypeNameStatic(Type type) => GetTypeName(type);

        [ThreadStatic] private static HashSet<Type>? _typeNameInProgress;

        private static string GetTypeName(Type type)
        {
            _typeNameInProgress ??= new HashSet<Type>(ReferenceEqualityComparer.Instance);
            if (!_typeNameInProgress.Add(type))
            {
                return type.Name ?? "$$circular";
            }
            try
            {
                return GetTypeNameCore(type);
            }
            finally
            {
                _typeNameInProgress.Remove(type);
            }
        }

        private static string GetTypeNameCore(Type type)
        {
            if (type == typeof(void)) { return "System.Void"; }
            if (type == typeof(object)) { return "System.Object"; }
            if (type == typeof(string)) { return "System.String"; }
            if (type == typeof(int)) { return "System.Int32"; }
            if (type == typeof(long)) { return "System.Int64"; }
            if (type == typeof(bool)) { return "System.Boolean"; }
            if (type == typeof(byte)) { return "System.Byte"; }
            if (type == typeof(short)) { return "System.Int16"; }
            if (type == typeof(float)) { return "System.Single"; }
            if (type == typeof(double)) { return "System.Double"; }
            if (type == typeof(char)) { return "System.Char"; }
            if (type == typeof(uint)) { return "System.UInt32"; }
            if (type == typeof(ulong)) { return "System.UInt64"; }
            if (type == typeof(ushort)) { return "System.UInt16"; }
            if (type == typeof(sbyte)) { return "System.SByte"; }
            if (type == typeof(nint)) { return "System.IntPtr"; }
            if (type == typeof(nuint)) { return "System.UIntPtr"; }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (type.IsArray)
            {
                return GetTypeName(type.GetElementType()!) + "[]";
            }

            if (type.IsByRef)
            {
                return GetTypeName(type.GetElementType()!) + "&";
            }

            if (type.IsPointer)
            {
                return GetTypeName(type.GetElementType()!) + "*";
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var genericDefinition = type.GetGenericTypeDefinition();
                var arguments = type.GetGenericArguments();
                var argumentNames = string.Join(",", Array.ConvertAll(arguments, GetTypeName));
                return GetTypeName(genericDefinition) + "[" + argumentNames + "]";
            }

            var result = type.FullName ?? type.Name;
            // .NET TypeBuilder.FullName escapes special characters with backslash
            // (e.g., backtick: \`, brackets: \[\], plus: \+). Strip all escapes
            // since the archive format doesn't use .NET type name escaping.
            if (result.IndexOf('\\') >= 0)
            {
                var cleaned = new System.Text.StringBuilder(result.Length);
                for (int charIndex = 0; charIndex < result.Length; charIndex++)
                {
                    if (result[charIndex] == '\\' && charIndex + 1 < result.Length)
                    {
                        charIndex++;
                        cleaned.Append(result[charIndex]);
                    }
                    else
                    {
                        cleaned.Append(result[charIndex]);
                    }
                }
                result = cleaned.ToString();
            }
            return result;
        }

        // ----- Private helpers -----

        private static ILGenerator CreateFactory()
        {
            var dynamicMethod = new DynamicMethod("ngo_factory_" + Guid.NewGuid().ToString("N"), typeof(void), Type.EmptyTypes);
            return dynamicMethod.GetILGenerator();
        }

        private static int GetLabelId(Label label)
        {
            return label.GetHashCode();
        }

        private void WriteOpCode(OpCode op)
        {
            if (op.Size == 2)
            {
                _code.WriteByte((byte)(op.Value >> 8));
                _code.WriteByte((byte)(op.Value & 0xFF));
            }
            else
            {
                _code.WriteByte((byte)op.Value);
            }
        }

        private void WriteInt16(short value)
        {
            _code.WriteByte((byte)(value & 0xFF));
            _code.WriteByte((byte)((value >> 8) & 0xFF));
        }

        private void WriteInt32(int value)
        {
            _code.WriteByte((byte)(value & 0xFF));
            _code.WriteByte((byte)((value >> 8) & 0xFF));
            _code.WriteByte((byte)((value >> 16) & 0xFF));
            _code.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private void WriteInt64(long value)
        {
            for (int byteIndex = 0; byteIndex < 8; byteIndex++)
            {
                _code.WriteByte((byte)((value >> (byteIndex * 8)) & 0xFF));
            }
        }

        private void WriteSingle(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            _code.Write(bytes, 0, 4);
        }

        private void WriteDouble(double value)
        {
            var bytes = BitConverter.GetBytes(value);
            _code.Write(bytes, 0, 8);
        }

        private void PatchBranches()
        {
            var buffer = _code.GetBuffer();
            foreach (var fixup in _branchFixups)
            {
                if (!_labelOffsets.TryGetValue(fixup.LabelId, out var targetOffset))
                {
                    continue;
                }

                if (fixup.IsShort)
                {
                    var relativeOffset = targetOffset - (fixup.Offset + 1);
                    buffer[fixup.Offset] = (byte)(sbyte)relativeOffset;
                }
                else
                {
                    var baseOff = fixup.BaseOffset > 0 ? fixup.BaseOffset : fixup.Offset + 4;
                    var relativeOffset = targetOffset - baseOff;
                    buffer[fixup.Offset] = (byte)(relativeOffset & 0xFF);
                    buffer[fixup.Offset + 1] = (byte)((relativeOffset >> 8) & 0xFF);
                    buffer[fixup.Offset + 2] = (byte)((relativeOffset >> 16) & 0xFF);
                    buffer[fixup.Offset + 3] = (byte)((relativeOffset >> 24) & 0xFF);
                }
            }
        }

        private static bool IsShortBranch(OpCode op)
        {
            return op.OperandType == OperandType.ShortInlineBrTarget;
        }

        // ----- Data types -----

        internal struct ExceptionClause
        {
            public int Kind;
            public int TryOffset;
            public int TryLength;
            public int HandlerOffset;
            public int HandlerLength;
            public int FilterOffset;
            public string? CatchTypeName;
        }

        private struct BranchFixup
        {
            public int Offset;
            public int LabelId;
            public bool IsShort;
            public int BaseOffset;
        }
    }
}
